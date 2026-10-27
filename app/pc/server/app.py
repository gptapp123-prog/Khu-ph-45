"""Quản lý Khu phố — server is the only location holding data.
Mobile is a stateless, no-storage viewer/editor. No cloud APIs, telemetry or remote backups.
"""
from __future__ import annotations
import os,sys,io,re,json,uuid,hashlib,hmac,threading,secrets
from datetime import datetime,timezone,timedelta
from pathlib import Path
from typing import Any, Optional, List, Dict
from contextlib import asynccontextmanager
from fastapi import FastAPI,Request,Depends,HTTPException,UploadFile,File
from fastapi.responses import HTMLResponse,FileResponse,StreamingResponse,JSONResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel,Field
from openpyxl import Workbook
from .crypto import Vault
from .schema import initialize,BASE_FIELDS,CAPABILITIES,DEFAULT_CAPABILITIES
from .security import SessionStore,ROLES,hash_password,verify_password,masking,now
from .parsers import parse_file
from .model_compat import fields_set, copy_with, declared_fields, validate_model

ROOT=Path(__file__).resolve().parents[1]
WEB=ROOT/'web'
if not WEB.is_dir() and hasattr(sys,'_MEIPASS'):WEB=Path(sys._MEIPASS)/'web'
LOCK=threading.RLock()
SESSIONS=SessionStore()
# Pending remote requests are RAM-only and expire. Neither phone nor cloud has a persistent vault.
MOBILE_APPROVALS:Dict[str,dict]={}
MOBILE_APPROVAL_TTL=180
VAULT:Optional[Vault]=None

def configure_vault(path:str,passphrase:str):
    global VAULT
    with LOCK:
        SESSIONS.sessions.clear()
        MOBILE_APPROVALS.clear()
        if VAULT is not None:
            VAULT.conn.close()
        VAULT=Vault(Path(path),passphrase)
        initialize(VAULT.conn)
        VAULT.persist()

@asynccontextmanager
async def lifespan(app:FastAPI):
    if VAULT is None:raise RuntimeError('Kho dữ liệu PC chưa được mở. Hãy chạy launcher.py.')
    yield
    SESSIONS.sessions.clear()
    MOBILE_APPROVALS.clear()
    if VAULT:VAULT.conn.close()

app=FastAPI(title='Quản lý Khu phố — PC Server',version='7.3.0-windows-compatibility',lifespan=lifespan,
            docs_url=None,redoc_url=None,openapi_url=None)
app.mount('/static',StaticFiles(directory=str(WEB)),name='static')

@app.middleware('http')
async def security_headers(request:Request,call_next):
    response=await call_next(request)
    for k,v in {'Cache-Control':'no-store, no-cache, max-age=0, must-revalidate',
                'Pragma':'no-cache','Expires':'0','X-Content-Type-Options':'nosniff',
                'X-Frame-Options':'DENY','Referrer-Policy':'no-referrer',
                'Permissions-Policy':'camera=(self),microphone=(),geolocation=()',
                'Content-Security-Policy':"default-src 'self'; img-src 'self' data: blob:; style-src 'self'; script-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"}.items(): response.headers[k]=v
    return response

def db():
    if VAULT is None:raise HTTPException(503,'Kho dữ liệu chưa mở trên PC')
    return VAULT.conn

def commit():
    db().commit()
    VAULT.persist()

def record(actor:str,action:str,entity_type:str='',entity_id:str='',details:Optional[dict]=None):
    c=db()
    old=c.execute('SELECT hash FROM audit_log ORDER BY seq DESC LIMIT 1').fetchone()
    previous=old['hash'] if old else '0'*64
    timestamp=now();details=details or {}
    # Never record passwords, session tokens, raw ID numbers, full imported data
    safe_details={k:v for k,v in details.items() if k not in ('password','token','cccd','phone','raw')}
    details_json=json.dumps(safe_details,sort_keys=True,ensure_ascii=False,separators=(',',':'))
    blob=json.dumps([timestamp,actor,action,entity_type,entity_id,details_json,previous],ensure_ascii=False,separators=(',',':'))
    digest=hashlib.sha256(blob.encode()).hexdigest()
    c.execute('INSERT INTO audit_log(at,actor,action,entity_type,entity_id,details_json,prev_hash,hash) VALUES(?,?,?,?,?,?,?,?)',
              (timestamp,actor,action,entity_type,entity_id,details_json,previous,digest))

def pc_only(request:Request):
    if request.client is None or request.client.host not in ('127.0.0.1','::1','testclient'):
        raise HTTPException(403,'Hành động này chỉ được thực hiện tại PC chính.')

def normalize_username(value:str)->str:
    value=value.strip().lower()
    if not re.fullmatch(r'[a-z][a-z0-9._-]{2,49}',value):
        raise HTTPException(422,'Tên đăng nhập: 3–50 ký tự a–z, 0–9, dấu chấm, gạch dưới hoặc gạch nối; bắt đầu bằng chữ.')
    return value

def validate_identity(name:str,phone:Optional[str]=None,email:Optional[str]=None):
    if not name.strip():raise HTTPException(422,'Họ tên không được để trống.')
    if phone and not re.fullmatch(r'0[0-9]{9}',phone):raise HTTPException(422,'Số điện thoại phải gồm 10 số, bắt đầu bằng số 0.')
    if email and not re.fullmatch(r'[^@\s]+@[^@\s]+\.[^@\s]+',email):raise HTTPException(422,'Email không hợp lệ.')

def bearer_token(request:Request):
    auth=request.headers.get('Authorization','')
    return auth[7:].strip() if auth.startswith('Bearer ') else ''

def user(request:Request):
    token=bearer_token(request)
    sess=SESSIONS.resolve(token)
    if not sess:raise HTTPException(401,'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.')
    with LOCK:
        row=db().execute('SELECT * FROM accounts WHERE id=? AND active=1',(sess['user_id'],)).fetchone()
        if not row:raise HTTPException(401,'Tài khoản không còn hoạt động.')
        if row['locked_until'] and row['locked_until']>now():
            raise HTTPException(403,'Tài khoản đang tạm khóa.')
        if row['must_change_password'] and request.url.path not in ('/api/me','/api/logout','/api/account/change-password'):
            raise HTTPException(403,'Phải đổi mật khẩu trước khi tiếp tục sử dụng.')
        if sess['device_id']!='pc':
            d=db().execute('SELECT status FROM devices WHERE id=? AND user_id=?',(sess['device_id'],row['id'])).fetchone()
            if not d or d['status']!='approved':raise HTTPException(403,'Thiết bị chưa được duyệt.')
        return dict(row)

def permit(account:dict,*allowed):
    if account['role'] not in allowed:raise HTTPException(403,'Không có quyền sử dụng chức năng này.')

def capability(account:dict, code:str):
    if code not in CAPABILITIES:raise RuntimeError('Unknown capability')
    row=db().execute('SELECT allowed FROM role_permissions WHERE role=? AND capability=?',
                     (account['role'],code)).fetchone()
    if not row or not row['allowed']:raise HTTPException(403,'Vai trò không được cấp quyền cho thao tác này.')

def approvals_gc():
    from time import monotonic
    expired=[k for k,v in MOBILE_APPROVALS.items() if v['expires'] <= monotonic()]
    for k in expired: MOBILE_APPROVALS.pop(k,None)

def team_scope(account:dict):
    if account['role'] in ('viewer','reviewer','admin_kp'):
        if not account['team_id']:return ' AND 1=0',[]  # fail closed for unassigned teams
        return ' AND team_id=?',[account['team_id']]
    return '',[]

def field_modes(role:str):
    return {r['field']:r['mode'] for r in db().execute('SELECT field,mode FROM field_permissions WHERE role=?',(role,))}

def protect(r:dict,role:str):
    return masking(r,role,field_modes(role))

class Setup(BaseModel):
    username:str=Field(min_length=3,max_length=50)
    full_name:str=Field(min_length=2,max_length=120)
    password:str=Field(min_length=12,max_length=200)

@app.get('/',response_class=HTMLResponse)
def home():return (WEB/'index.html').read_text(encoding='utf-8')

@app.get('/api/health')
def health():
    return {'application':'Quản lý Khu phố','version':'7.3.0-windows-compatibility','data_location':'PC_ONLY',
            'setup_required':not bool(db().execute('SELECT 1 FROM accounts LIMIT 1').fetchone())}

@app.post('/api/setup')
def setup(p:Setup,request:Request):
    pc_only(request)
    with LOCK:
        if db().execute('SELECT 1 FROM accounts LIMIT 1').fetchone():raise HTTPException(409,'Đã tạo quản trị viên. Không cho phép thiết lập lại.')
        username=normalize_username(p.username)
        validate_identity(p.full_name)
        try:pwd=hash_password(p.password)
        except ValueError as e:raise HTTPException(422,str(e))
        uid=uuid.uuid4().hex
        db().execute('INSERT INTO accounts(id,username,display_name,password_hash,role,team_id,created_at) VALUES(?,?,?,?,?,?,?)',
                     (uid,username,p.full_name.strip(),pwd,'super_admin',None,now()))
        record(uid,'INITIAL_SETUP','account',uid)
        commit()
    return {'status':'created'}

class Login(BaseModel):
    username:str;password:str
    device_id:str='pc';device_label:str='PC chính'

@app.post('/api/login')
def login(p:Login,request:Request):
    if len(p.username)>100 or len(p.password)>300:raise HTTPException(422,'Đầu vào không hợp lệ.')
    device=p.device_id.strip()[:128]
    if device=='pc' and request.client and request.client.host not in ('127.0.0.1','::1','testclient'):
        raise HTTPException(403,'Chỉ PC chính được dùng định danh pc.')
    if not device:raise HTTPException(422,'Thiếu định danh thiết bị.')
    with LOCK:
        acc=db().execute('SELECT * FROM accounts WHERE username=? AND active=1',(p.username.lower().strip(),)).fetchone()
        if acc and acc['locked_until'] and acc['locked_until']>now():raise HTTPException(429,'Tài khoản đang tạm khóa.')
        if not acc or not verify_password(acc['password_hash'],p.password):
            if acc:
                attempts=acc['failed_attempts']+1
                until=(datetime.now(timezone.utc)+timedelta(minutes=30)).isoformat() if attempts>=5 else None
                db().execute('UPDATE accounts SET failed_attempts=?,locked_until=? WHERE id=?',(attempts,until,acc['id']))
                if attempts>=5:SESSIONS.revoke_user(acc['id'])
                record(acc['id'],'LOGIN_FAILED','account',acc['id'],{'attempts':attempts})
                commit()
            raise HTTPException(401,'Thông tin đăng nhập không chính xác.')
        db().execute('UPDATE accounts SET failed_attempts=0,locked_until=NULL WHERE id=?',(acc['id'],))
        if device!='pc':
            from time import monotonic
            approvals_gc()
            existing=db().execute('SELECT status FROM devices WHERE id=? AND user_id=?',(device,acc['id'])).fetchone()
            if existing and existing['status']=='blocked':raise HTTPException(403,'Thiết bị đã bị chặn.')
            if not existing:
                count=db().execute("SELECT count(*) FROM devices WHERE user_id=? AND status IN ('approved','pending')",(acc['id'],)).fetchone()[0]
                if count>=2:raise HTTPException(403,'Tài khoản đã đăng ký tối đa hai thiết bị.')
                db().execute('INSERT INTO devices(id,user_id,device_label,status,seen_at) VALUES(?,?,?,?,?)',
                             (device,acc['id'],p.device_label[:80],'pending',now()))
                record(acc['id'],'DEVICE_REQUEST','device',device)
            pending=sum(1 for v in MOBILE_APPROVALS.values() if v['user_id']==acc['id'])
            if pending>=3:raise HTTPException(429,'Có quá nhiều yêu cầu đăng nhập đang chờ. Thử lại sau.')
            request_id=secrets.token_urlsafe(24)
            proof=secrets.token_urlsafe(32)
            MOBILE_APPROVALS[request_id]={
                'proof_hash':hashlib.sha256(proof.encode()).digest(),'user_id':acc['id'],
                'device_id':device,'device_label':p.device_label[:80],
                'expires':monotonic()+MOBILE_APPROVAL_TTL,'approved_by':None,'denied':False,
                'created_at':now()}
            record(acc['id'],'MOBILE_LOGIN_REQUEST','device',device)
            commit()
            return JSONResponse(status_code=202,content={
                'status':'pending_approval','request_id':request_id,'proof':proof,
                'expires_in_seconds':MOBILE_APPROVAL_TTL,
                'message':'Yêu cầu đăng nhập phải được quản trị viên xác nhận trực tiếp trên PC. Không lưu mã truy cập trên điện thoại.'})
        db().execute('UPDATE accounts SET last_login_at=? WHERE id=?',(now(),acc['id']))
        token=SESSIONS.issue(acc['id'],device)
        record(acc['id'],'LOGIN_SUCCESS','account',acc['id'],{'device':device})
        commit()
    return {'token':token,'id':acc['id'],'role':acc['role'],'name':acc['display_name'],'team_id':acc['team_id'],
            'must_change_password':bool(acc['must_change_password'])}

class MobilePoll(BaseModel):
    request_id:str=Field(min_length=10,max_length=100)
    proof:str=Field(min_length=20,max_length=150)

@app.post('/api/mobile/login-status')
def mobile_login_status(p:MobilePoll):
    with LOCK:
        approvals_gc()
        r=MOBILE_APPROVALS.get(p.request_id)
        if not r or not hmac.compare_digest(r['proof_hash'],hashlib.sha256(p.proof.encode()).digest()):
            raise HTTPException(404,'Yêu cầu không tồn tại hoặc đã hết hạn.')
        if r['denied']:
            MOBILE_APPROVALS.pop(p.request_id,None)
            raise HTTPException(403,'Quản trị viên đã từ chối đăng nhập.')
        if not r['approved_by']:return {'status':'pending_approval','message':'Đang chờ quản trị viên PC phê duyệt.'}
        acc=db().execute('SELECT * FROM accounts WHERE id=? AND active=1',(r['user_id'],)).fetchone()
        device=db().execute('SELECT status FROM devices WHERE id=? AND user_id=?',(r['device_id'],r['user_id'])).fetchone()
        if not acc or not device or device['status']!='approved':
            raise HTTPException(403,'Thiết bị hoặc tài khoản không được phép đăng nhập.')
        if acc['locked_until'] and acc['locked_until']>now():raise HTTPException(403,'Tài khoản đang bị khóa.')
        # One approval is consumed once. No second call can mint another session.
        MOBILE_APPROVALS.pop(p.request_id,None)
        token=SESSIONS.issue(acc['id'],r['device_id'])
        db().execute('UPDATE accounts SET last_login_at=? WHERE id=?',(now(),acc['id']))
        record(acc['id'],'MOBILE_LOGIN_APPROVED','account',acc['id'],{'approved_by':r['approved_by']})
        commit()
        return {'status':'approved','token':token,'id':acc['id'],'role':acc['role'],
                'name':acc['display_name'],'team_id':acc['team_id'],
                'must_change_password':bool(acc['must_change_password'])}

@app.get('/api/mobile/login-requests')
def mobile_login_requests(request:Request,account:dict=Depends(user)):
    pc_only(request);capability(account,'manage_devices')
    with LOCK:
        approvals_gc()
        items=[]
        for rid,r in MOBILE_APPROVALS.items():
            a=db().execute('SELECT display_name,role,team_id FROM accounts WHERE id=?',(r['user_id'],)).fetchone()
            if not a or ROLES.index(a['role'])>=ROLES.index(account['role']):continue
            if account['role']=='admin_kp' and a['team_id']!=account['team_id']:continue
            device=db().execute('SELECT status FROM devices WHERE id=? AND user_id=?',(r['device_id'],r['user_id'])).fetchone()
            items.append({'request_id':rid,'user_name':a['display_name'],'role':a['role'],'team_id':a['team_id'],
                          'device_label':r['device_label'],'device_id':r['device_id'],'device_status':device['status'] if device else 'missing',
                          'created_at':r['created_at'],'approved':bool(r['approved_by'])})
        return {'items':items}

class MobileDecision(BaseModel):
    approved:bool

@app.post('/api/mobile/login-requests/{request_id}/decision')
def mobile_login_decision(request_id:str,p:MobileDecision,request:Request,account:dict=Depends(user)):
    pc_only(request);capability(account,'manage_devices')
    with LOCK:
        approvals_gc()
        r=MOBILE_APPROVALS.get(request_id)
        if not r:raise HTTPException(404,'Yêu cầu đã hết hạn hoặc không tồn tại.')
        target=db().execute('SELECT role,team_id FROM accounts WHERE id=?',(r['user_id'],)).fetchone()
        if not target or ROLES.index(target['role'])>=ROLES.index(account['role']):
            raise HTTPException(403,'Không được duyệt người ngang/cao quyền.')
        if account['role']=='admin_kp' and target['team_id']!=account['team_id']:
            raise HTTPException(403,'Ngoài Tổ quản lý.')
        if r['approved_by'] or r['denied']:raise HTTPException(409,'Yêu cầu đã được xử lý.')
        if p.approved:
            device=db().execute('SELECT status FROM devices WHERE id=? AND user_id=?',(r['device_id'],r['user_id'])).fetchone()
            if not device or device['status']!='approved':
                raise HTTPException(409,'Cần duyệt thiết bị trước khi cho phép phiên đăng nhập.')
            r['approved_by']=account['id']
        else:r['denied']=True
        record(account['id'],'MOBILE_LOGIN_ALLOW' if p.approved else 'MOBILE_LOGIN_DENY','device',r['device_id'])
        commit()
        return {'ok':True}

@app.post('/api/logout')
def logout(request:Request,account:dict=Depends(user)):
    SESSIONS.revoke(bearer_token(request))
    with LOCK:record(account['id'],'LOGOUT');commit()
    return {'ok':True}

@app.get('/api/me')
def me(account:dict=Depends(user)):
    return {'id':account['id'],'username':account['username'],'name':account['display_name'],
            'role':account['role'],'team_id':account['team_id'],
            'must_change_password':bool(account['must_change_password'])}

class PasswordChange(BaseModel):
    old_password:str=Field(min_length=1,max_length=200)
    new_password:str=Field(min_length=12,max_length=200)

@app.post('/api/account/change-password')
def change_own_password(p:PasswordChange,request:Request,account:dict=Depends(user)):
    if not verify_password(account['password_hash'],p.old_password):
        with LOCK:record(account['id'],'CHANGE_PASSWORD_FAILED','account',account['id']);commit()
        raise HTTPException(401,'Mật khẩu hiện tại không chính xác.')
    if p.old_password==p.new_password:raise HTTPException(422,'Mật khẩu mới phải khác mật khẩu cũ.')
    try:new_hash=hash_password(p.new_password)
    except ValueError as e:raise HTTPException(422,str(e))
    with LOCK:
        db().execute('UPDATE accounts SET password_hash=?,must_change_password=0,failed_attempts=0,locked_until=NULL WHERE id=?',
                     (new_hash,account['id']))
        record(account['id'],'CHANGE_PASSWORD','account',account['id']);commit()
        SESSIONS.revoke_user(account['id'])
        for k,v in list(MOBILE_APPROVALS.items()):
            if v['user_id']==account['id']: MOBILE_APPROVALS.pop(k,None)
    return {'ok':True,'message':'Đã đổi mật khẩu. Vui lòng đăng nhập lại trên tất cả thiết bị.'}

@app.get('/api/stats')
def stats(account:dict=Depends(user)):
    with LOCK:
        scope,args=team_scope(account)
        n=db().execute('SELECT count(*) FROM residents WHERE deleted_at IS NULL'+scope,args).fetchone()[0]
        households=db().execute('SELECT count(*) FROM households'+scope.replace(' AND ',' WHERE ',1),args).fetchone()[0]
        return {'residents':n,'households':households,'accounts':db().execute('SELECT count(*) FROM accounts WHERE active=1').fetchone()[0]
                if account['role'] in ('admin_root','super_admin','admin_kp') else None,
                'audit_events':db().execute('SELECT count(*) FROM audit_log').fetchone()[0] if account['role'] in ('admin_root','super_admin') else None}

class ResidentIn(BaseModel):
    full_name:str=Field(min_length=2,max_length=180)
    cccd:Optional[str]=None;dob:Optional[str]=None;gender:Optional[str]=None;hometown:Optional[str]=None
    permanent_address:Optional[str]=None;team_id:int=Field(ge=1,le=15)
    phone:Optional[str]=None;health_insurance:Optional[str]=None;occupation:Optional[str]=None
    note:str='';household_id:Optional[str]=None

def validate_resident(p:ResidentIn,account:dict):
    if account['role'] in ('viewer',):raise HTTPException(403,'Không có quyền sửa dân cư.')
    modes=field_modes(account['role'])
    for field,mode in modes.items():
        if not hasattr(p,field):continue
        value=getattr(p,field)
        if mode in ('hidden','masked') and field in fields_set(p):
            raise HTTPException(403,'Vai trò không được ghi trường dữ liệu: '+field)
        if mode=='required' and value in (None,''):
            raise HTTPException(422,'Thiếu trường bắt buộc: '+field)
    if account['role'] in ('reviewer','admin_kp') and p.team_id!=account['team_id']:
        raise HTTPException(403,'Không được sửa dữ liệu khác tổ phụ trách.')
    if p.cccd and not re.fullmatch(r'\d{12}',p.cccd):raise HTTPException(422,'CCCD phải gồm đúng 12 chữ số.')
    if p.phone and not re.fullmatch(r'0\d{9}',p.phone):raise HTTPException(422,'Số điện thoại phải gồm 10 số, bắt đầu bằng 0.')
    if p.dob:
        try:datetime.strptime(p.dob,'%Y-%m-%d')
        except ValueError:raise HTTPException(422,'Ngày sinh theo định dạng YYYY-MM-DD.')
    if len(p.note)>3000:raise HTTPException(422,'Ghi chú quá dài.')
    if p.household_id:
        h=db().execute('SELECT team_id FROM households WHERE id=?',(p.household_id,)).fetchone()
        if not h or h['team_id']!=p.team_id:raise HTTPException(422,'Hộ không tồn tại hoặc khác tổ.')

def person_params(p:ResidentIn):
    return [p.full_name.strip(),p.cccd or None,p.dob,p.gender,p.hometown,p.permanent_address,p.team_id,
            p.phone,p.health_insurance,p.occupation,p.note,p.household_id]

@app.get('/api/residents')
def list_residents(q:str='',team:Optional[int]=None,limit:int=100,offset:int=0,account:dict=Depends(user)):
    capability(account,'read_residents')
    if limit<1 or limit>500 or offset<0:raise HTTPException(422,'Giới hạn truy vấn không hợp lệ.')
    with LOCK:
        where=['deleted_at IS NULL'];args=[]
        scope,scope_args=team_scope(account)
        if scope:where.append('team_id=?');args+=scope_args
        if team:
            where.append('team_id=?');args.append(team)
        if q:
            if len(q)>180:raise HTTPException(422,'Từ khóa quá dài.')
            modes=field_modes(account['role'])
            search_fields=[f for f in ('full_name','permanent_address','cccd')
                           if modes.get(f,'optional') in ('optional','required') and not (account['role']=='viewer' and f=='cccd')]
            if not search_fields:raise HTTPException(403,'Không có trường tìm kiếm được cấp quyền.')
            escaped=q.replace('\\','\\\\').replace('%','\\%').replace('_','\\_')
            term='%'+escaped+'%'
            where.append('('+" OR ".join(f"{f} LIKE ? ESCAPE '\\'" for f in search_fields)+')')
            args += [term]*len(search_fields)
        w=' AND '.join(where)
        count=db().execute('SELECT count(*) FROM residents WHERE '+w,args).fetchone()[0]
        rows=db().execute('SELECT * FROM residents WHERE '+w+' ORDER BY full_name LIMIT ? OFFSET ?',args+[limit,offset]).fetchall()
        return {'total':count,'items':[protect(dict(r),account['role']) for r in rows]}

@app.post('/api/residents')
def create_resident(p:ResidentIn,account:dict=Depends(user)):
    capability(account,'edit_residents')
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    with LOCK:
        validate_resident(p,account);rid=uuid.uuid4().hex;t=now()
        try:db().execute('''INSERT INTO residents(id,full_name,cccd,dob,gender,hometown,permanent_address,team_id,phone,health_insurance,occupation,note,household_id,created_at,updated_at)
        VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''',[rid]+person_params(p)+[t,t])
        except Exception as e:
            if 'UNIQUE' in str(e):raise HTTPException(409,'CCCD đã tồn tại trong hệ thống.')
            raise
        record(account['id'],'CREATE_RESIDENT','resident',rid)
        commit()
    return {'id':rid}

@app.put('/api/residents/{rid}')
def update_resident(rid:str,p:ResidentIn,account:dict=Depends(user)):
    capability(account,'edit_residents')
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    with LOCK:
        old=db().execute('SELECT * FROM residents WHERE id=? AND deleted_at IS NULL',(rid,)).fetchone()
        if not old:raise HTTPException(404,'Không tìm thấy nhân khẩu.')
        if account['role'] in ('reviewer','admin_kp') and old['team_id']!=account['team_id']:
            raise HTTPException(403,'Không có quyền sửa nhân khẩu khác tổ.')
        validate_resident(p,account)
        protected=field_modes(account['role'])
        preserved={k:old[k] for k,mode in protected.items()
                   if mode in ('hidden','masked') and k not in fields_set(p) and k in old.keys()}
        effective=copy_with(p,update=preserved)
        try:db().execute('''UPDATE residents SET full_name=?,cccd=?,dob=?,gender=?,hometown=?,permanent_address=?,team_id=?,phone=?,health_insurance=?,occupation=?,note=?,household_id=?,updated_at=? WHERE id=?''',person_params(effective)+[now(),rid])
        except Exception as e:
            if 'UNIQUE' in str(e):raise HTTPException(409,'CCCD đã tồn tại.')
            raise
        record(account['id'],'UPDATE_RESIDENT','resident',rid);commit()
    return {'ok':True}

@app.delete('/api/residents/{rid}')
def delete_resident(rid:str,account:dict=Depends(user)):
    capability(account,'delete_residents')
    permit(account,'admin_kp','admin_root','super_admin')
    with LOCK:
        row=db().execute('SELECT team_id FROM residents WHERE id=? AND deleted_at IS NULL',(rid,)).fetchone()
        if not row:raise HTTPException(404,'Không tìm thấy dữ liệu.')
        if account['role']=='admin_kp' and row['team_id']!=account['team_id']:
            raise HTTPException(403,'Không được xóa hồ sơ ngoài tổ phụ trách.')
        db().execute('UPDATE residents SET deleted_at=?,updated_at=? WHERE id=?',(now(),now(),rid))
        record(account['id'],'DELETE_RESIDENT','resident',rid);commit()
    return {'ok':True}

class HouseholdIn(BaseModel):
    code:str=Field(min_length=1,max_length=50);address:str=Field(min_length=3,max_length=300)
    team_id:int=Field(ge=1,le=15);note:str=''

@app.get('/api/households')
def households(account:dict=Depends(user)):
    capability(account,'read_households')
    with LOCK:
        scope,args=team_scope(account)
        return {'items':[dict(r) for r in db().execute('SELECT * FROM households WHERE 1=1'+scope+' ORDER BY team_id,code',args)]}

@app.post('/api/households')
def add_household(p:HouseholdIn,account:dict=Depends(user)):
    capability(account,'edit_households')
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    if account['role'] in ('reviewer','admin_kp') and p.team_id!=account['team_id']:raise HTTPException(403,'Khác tổ phụ trách.')
    with LOCK:
        rid=uuid.uuid4().hex
        try:db().execute('INSERT INTO households(id,code,address,team_id,note,created_at) VALUES(?,?,?,?,?,?)',(rid,p.code.strip(),p.address.strip(),p.team_id,p.note,now()))
        except Exception as e:
            if 'UNIQUE' in str(e):raise HTTPException(409,'Mã hộ đã tồn tại.')
            raise
        record(account['id'],'CREATE_HOUSEHOLD','household',rid);commit()
    return {'id':rid}

@app.get('/api/fields')
def fields(account:dict=Depends(user)):
    capability(account,'read_residents')
    with LOCK:
        modes=field_modes(account['role'])
        return {'standard':[{'id':k,'label':v,'mode':modes.get(k,'optional')} for k,v in BASE_FIELDS.items() if modes.get(k)!='hidden'],
                'custom':[dict(r) for r in db().execute('SELECT * FROM field_definitions ORDER BY label')]}

class FieldIn(BaseModel):
    name:str=Field(min_length=2,max_length=41)
    label:str=Field(min_length=2,max_length=100)
    type:str='text';options:List[str]=[]

@app.post('/api/fields')
def add_field(p:FieldIn,account:dict=Depends(user)):
    capability(account,'manage_fields')
    permit(account,'admin_kp','admin_root','super_admin')
    if not re.fullmatch(r'[a-z][a-z0-9_]{1,40}',p.name):raise HTTPException(422,'Mã trường chỉ gồm a-z, 0-9, gạch dưới và bắt đầu bằng chữ.')
    if p.type not in ('text','number','date','dropdown','checkbox'):raise HTTPException(422,'Kiểu cột không hỗ trợ.')
    with LOCK:
        id_=uuid.uuid4().hex
        try:db().execute('INSERT INTO field_definitions VALUES(?,?,?,?,?,?)',(id_,p.name,p.label,p.type,json.dumps(p.options,ensure_ascii=False),now()))
        except Exception as e:
            if 'UNIQUE' in str(e):raise HTTPException(409,'Trường đã tồn tại.')
            raise
        record(account['id'],'CREATE_FIELD','field',id_);commit()
    return {'id':id_}

@app.get('/api/accounts')
def accounts(account:dict=Depends(user)):
    capability(account,'read_accounts')
    permit(account,'admin_kp','admin_root','super_admin')
    with LOCK:
        query='SELECT id,username,display_name,phone,zalo_id,email,role,team_id,active,must_change_password,locked_until,created_at,last_login_at FROM accounts'
        args=[]
        if account['role']=='admin_kp':
            if not account['team_id']:raise HTTPException(403,'Quản trị khu phố phải được gán tổ phụ trách.')
            query+=' WHERE team_id=?';args=[account['team_id']]
        query+=' ORDER BY display_name'
        return {'items':[dict(id=r['id'],username=r['username'],name=r['display_name'],phone=r['phone'],
                              zalo_id=r['zalo_id'],email=r['email'],role=r['role'],team_id=r['team_id'],
                              active=bool(r['active']),must_change_password=bool(r['must_change_password']),
                              locked_until=r['locked_until'],last_login_at=r['last_login_at'],created_at=r['created_at'])
                         for r in db().execute(query,args)]}

class AccountIn(BaseModel):
    username:str=Field(min_length=3,max_length=50)
    full_name:str=Field(min_length=2,max_length=120)
    password:str=Field(min_length=12,max_length=200)
    role:str='viewer'
    team_id:Optional[int]=Field(default=None,ge=1,le=15)
    phone:Optional[str]=None
    zalo_id:Optional[str]=Field(default=None,max_length=120)
    email:Optional[str]=Field(default=None,max_length=120)

@app.post('/api/accounts')
def create_account(p:AccountIn,request:Request,account:dict=Depends(user)):
    capability(account,'manage_accounts')
    pc_only(request);permit(account,'admin_kp','admin_root','super_admin')
    if p.role not in ROLES:raise HTTPException(422,'Vai trò không hợp lệ.')
    if ROLES.index(p.role)>=ROLES.index(account['role']):raise HTTPException(403,'Không thể tạo tài khoản ngang/cao quyền hơn.')
    if account['role']=='admin_kp' and p.role not in ('viewer','reviewer'):
        raise HTTPException(403,'Admin KP chỉ được tạo Viewer/Rà soát.')
    if p.role in ('viewer','reviewer','admin_kp') and p.team_id is None:
        raise HTTPException(422,'Vai trò phụ trách địa bàn phải được gán Tổ.')
    if account['role']=='admin_kp' and p.team_id!=account['team_id']:
        raise HTTPException(403,'Chỉ được tạo tài khoản cùng Tổ phụ trách.')
    username=normalize_username(p.username);validate_identity(p.full_name,p.phone,p.email)
    try:hashed=hash_password(p.password)
    except ValueError as e:raise HTTPException(422,str(e))
    with LOCK:
        rid=uuid.uuid4().hex
        try:db().execute("""INSERT INTO accounts
            (id,username,display_name,password_hash,role,team_id,created_at,must_change_password,phone,zalo_id,email)
            VALUES(?,?,?,?,?,?,?,?,?,?,?)""",
            (rid,username,p.full_name.strip(),hashed,p.role,p.team_id,now(),1,p.phone,p.zalo_id,p.email))
        except Exception as e:
            if 'UNIQUE' in str(e):raise HTTPException(409,'Tên đăng nhập đã tồn tại.')
            raise
        record(account['id'],'CREATE_ACCOUNT','account',rid,{'role':p.role,'team_id':p.team_id});commit()
    return {'id':rid,'must_change_password':True}

class AccountPatch(BaseModel):
    active:Optional[bool]=None
    password:Optional[str]=None
    team_id:Optional[int]=Field(default=None,ge=1,le=15)
    full_name:Optional[str]=Field(default=None,min_length=2,max_length=120)
    phone:Optional[str]=None
    zalo_id:Optional[str]=Field(default=None,max_length=120)
    email:Optional[str]=Field(default=None,max_length=120)

@app.patch('/api/accounts/{aid}')
def patch_account(aid:str,p:AccountPatch,request:Request,account:dict=Depends(user)):
    capability(account,'manage_accounts')
    pc_only(request);permit(account,'admin_kp','admin_root','super_admin')
    with LOCK:
        row=db().execute('SELECT * FROM accounts WHERE id=?',(aid,)).fetchone()
        if not row:raise HTTPException(404,'Không tìm thấy tài khoản.')
        if ROLES.index(row['role'])>=ROLES.index(account['role']) or aid==account['id']:
            raise HTTPException(403,'Không được chỉnh sửa tài khoản này.')
        if account['role']=='admin_kp' and (row['team_id']!=account['team_id'] or
            (p.team_id is not None and p.team_id!=account['team_id'])):
            raise HTTPException(403,'Không được sửa tài khoản ngoài Tổ phụ trách.')
        if p.active is not None:
            db().execute('UPDATE accounts SET active=? WHERE id=?',(int(p.active),aid));SESSIONS.revoke_user(aid)
        if p.password is not None:
            try:hashed=hash_password(p.password)
            except ValueError as e:raise HTTPException(422,str(e))
            db().execute('UPDATE accounts SET password_hash=?,must_change_password=1,failed_attempts=0,locked_until=NULL WHERE id=?',
                         (hashed,aid));SESSIONS.revoke_user(aid)
        if p.team_id is not None:
            db().execute('UPDATE accounts SET team_id=? WHERE id=?',(p.team_id,aid));SESSIONS.revoke_user(aid)
        if any(k in fields_set(p) for k in ('full_name','phone','zalo_id','email')):
            name=p.full_name if p.full_name is not None else row['display_name']
            phone=p.phone if 'phone' in fields_set(p) else row['phone']
            email=p.email if 'email' in fields_set(p) else row['email']
            validate_identity(name,phone,email)
            db().execute('UPDATE accounts SET display_name=?,phone=?,zalo_id=?,email=? WHERE id=?',
                         (name.strip(),phone,p.zalo_id if 'zalo_id' in fields_set(p) else row['zalo_id'],email,aid))
        if any(f in fields_set(p) for f in ('password','active','team_id')):
            for k,v in list(MOBILE_APPROVALS.items()):
                if v['user_id']==aid: MOBILE_APPROVALS.pop(k,None)
        record(account['id'],'UPDATE_ACCOUNT','account',aid,{'updated_fields':sorted(fields_set(p))});commit()
    return {'ok':True}

@app.get('/api/devices')
def list_devices(account:dict=Depends(user)):
    capability(account,'read_devices')
    permit(account,'admin_kp','admin_root','super_admin')
    with LOCK:
        query='SELECT d.*,a.display_name as user_name,a.role,a.team_id FROM devices d JOIN accounts a ON d.user_id=a.id'
        params=[]
        if account['role']=='admin_kp':query+=' WHERE a.team_id=?';params=[account['team_id']]
        query+=' ORDER BY d.seen_at DESC'
        return {'items':[dict(r) for r in db().execute(query,params)]}

class DeviceAction(BaseModel):status:str
@app.patch('/api/devices/{device_id}')
def change_device(device_id:str,p:DeviceAction,request:Request,account:dict=Depends(user)):
    capability(account,'manage_devices')
    pc_only(request);permit(account,'admin_kp','admin_root','super_admin')
    if p.status not in ('approved','blocked'):raise HTTPException(422,'Trạng thái không hợp lệ.')
    with LOCK:
        row=db().execute('SELECT d.*,a.role,a.team_id FROM devices d JOIN accounts a ON d.user_id=a.id WHERE d.id=?',(device_id,)).fetchone()
        if not row:raise HTTPException(404,'Không tìm thấy thiết bị.')
        if ROLES.index(row['role'])>=ROLES.index(account['role']):raise HTTPException(403,'Không thể duyệt thiết bị của quản trị viên cấp trên.')
        if account['role']=='admin_kp' and row['team_id']!=account['team_id']:
            raise HTTPException(403,'Không được xử lý thiết bị ngoài Tổ phụ trách.')
        if p.status=='approved':
            count=db().execute("SELECT count(*) FROM devices WHERE user_id=? AND id<>? AND status='approved'",(row['user_id'],device_id)).fetchone()[0]
            if count>=2:raise HTTPException(409,'Đã đạt giới hạn 2 thiết bị được duyệt.')
        db().execute('UPDATE devices SET status=? WHERE id=?',(p.status,device_id))
        if p.status=='blocked':
            SESSIONS.revoke_user(row['user_id'])
            for k,v in list(MOBILE_APPROVALS.items()):
                if v['device_id']==device_id: MOBILE_APPROVALS.pop(k,None)
        record(account['id'],'DEVICE_'+p.status.upper(),'device',device_id);commit()
    return {'ok':True}

@app.get('/api/audit')
def audit(limit:int=100,account:dict=Depends(user)):
    capability(account,'read_audit')
    permit(account,'admin_root','super_admin')
    if not 1<=limit<=500:raise HTTPException(422,'Giới hạn không hợp lệ.')
    with LOCK:
        return {'items':[dict(r) for r in db().execute('SELECT seq,at,actor,action,entity_type,entity_id,details_json,prev_hash,hash FROM audit_log ORDER BY seq DESC LIMIT ?',(limit,))]}

@app.get('/api/audit/verify')
def audit_verify(account:dict=Depends(user)):
    capability(account,'verify_audit')
    permit(account,'super_admin')
    with LOCK:
        previous='0'*64;count=0
        for r in db().execute('SELECT * FROM audit_log ORDER BY seq'):
            blob=json.dumps([r['at'],r['actor'],r['action'],r['entity_type'],r['entity_id'],r['details_json'],previous],ensure_ascii=False,separators=(',',':'))
            expected=hashlib.sha256(blob.encode()).hexdigest()
            if not hmac.compare_digest(previous,r['prev_hash']) or not hmac.compare_digest(expected,r['hash']):
                return {'valid':False,'first_invalid_seq':r['seq']}
            previous=expected;count+=1
        return {'valid':True,'count':count}

# Governance: allow revocation of default rights, never add privileges above role hierarchy.
@app.get('/api/permissions')
def permission_matrix(request:Request,account:dict=Depends(user)):
    pc_only(request);permit(account,'super_admin')
    with LOCK:
        grants={(r['role'],r['capability']):bool(r['allowed']) for r in db().execute('SELECT * FROM role_permissions')}
        field_grants={(r['role'],r['field']):r['mode'] for r in db().execute('SELECT * FROM field_permissions')}
        return {'roles':ROLES,'capabilities':CAPABILITIES,
                'default_capabilities':{r:sorted(v) for r,v in DEFAULT_CAPABILITIES.items()},
                'role_grants':{r:{c:grants.get((r,c),False) for c in CAPABILITIES} for r in ROLES},
                'field_labels':BASE_FIELDS,
                'field_grants':{r:{f:field_grants.get((r,f),'hidden') for f in BASE_FIELDS} for r in ROLES}}

class PermissionUpdate(BaseModel):
    role:str;capability:str;allowed:bool

@app.put('/api/permissions/capability')
def permission_update(p:PermissionUpdate,request:Request,account:dict=Depends(user)):
    pc_only(request);permit(account,'super_admin')
    if p.role not in ROLES or p.capability not in CAPABILITIES:raise HTTPException(422,'Mã quyền hoặc vai trò không hợp lệ.')
    if p.role=='super_admin':raise HTTPException(403,'Không sửa được quyền cơ bản của quản trị cao nhất.')
    if p.allowed and p.capability not in DEFAULT_CAPABILITIES[p.role]:
        raise HTTPException(403,'Cấm nâng quyền vượt vai trò chuẩn. Chỉ cho thu hồi/khôi phục quyền mặc định.')
    with LOCK:
        db().execute('UPDATE role_permissions SET allowed=? WHERE role=? AND capability=?',
                     (int(p.allowed),p.role,p.capability))
        record(account['id'],'UPDATE_ROLE_PERMISSION','role',p.role,{'capability':p.capability,'allowed':p.allowed})
        commit()
    return {'ok':True}

class FieldPermissionUpdate(BaseModel):
    role:str;field:str;mode:str

@app.put('/api/permissions/field')
def field_permission_update(p:FieldPermissionUpdate,request:Request,account:dict=Depends(user)):
    pc_only(request);permit(account,'super_admin')
    if p.role not in ROLES or p.field not in BASE_FIELDS or p.mode not in ('required','optional','hidden','masked'):
        raise HTTPException(422,'Trường, vai trò hoặc kiểu quyền không hợp lệ.')
    if p.role=='super_admin' and p.mode in ('hidden','masked'):
        raise HTTPException(403,'Không được che trường dữ liệu với quản trị cao nhất.')
    if p.role=='viewer' and p.field in ('cccd','phone') and p.mode not in ('hidden','masked'):
        raise HTTPException(403,'Viewer không được phép xem đầy đủ CCCD/SĐT.')
    with LOCK:
        db().execute('INSERT INTO field_permissions(role,field,mode) VALUES(?,?,?) ON CONFLICT(role,field) DO UPDATE SET mode=excluded.mode',
                     (p.role,p.field,p.mode))
        record(account['id'],'UPDATE_FIELD_PERMISSION','field',p.field,{'role':p.role,'mode':p.mode})
        commit()
    return {'ok':True}

class TemplateIn(BaseModel):
    name:str=Field(min_length=2,max_length=100)
    fields:List[str];mapping:Dict[str,str]={}

@app.get('/api/templates')
def templates(account:dict=Depends(user)):
    with LOCK:return {'items':[dict(r) for r in db().execute('SELECT * FROM templates ORDER BY name')]}

@app.post('/api/templates')
def add_template(p:TemplateIn,account:dict=Depends(user)):
    capability(account,'manage_templates')
    permit(account,'admin_kp','admin_root','super_admin')
    with LOCK:
        rid=uuid.uuid4().hex
        db().execute('INSERT INTO templates VALUES(?,?,?,?,?)',(rid,p.name,json.dumps(p.fields,ensure_ascii=False),json.dumps(p.mapping,ensure_ascii=False),now()))
        record(account['id'],'CREATE_TEMPLATE','template',rid);commit()
        return {'id':rid}

@app.post('/api/import/preview')
async def import_preview(file:UploadFile=File(...),account:dict=Depends(user)):
    capability(account,'import')
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    raw=await file.read(10*1024*1024+1)
    if len(raw)>10*1024*1024:raise HTTPException(413,'Tệp vượt 10 MB.')
    try:return parse_file(raw,file.filename or '')
    except (ValueError,RuntimeError,ImportError) as e:raise HTTPException(422,str(e))
    except Exception:raise HTTPException(422,'Không đọc được nội dung tệp. Kiểm tra định dạng hoặc phần mềm OCR trên PC.')

class ImportCommit(BaseModel):
    rows:List[Dict[str,Any]]
    mapping:Dict[str,str]
    selected:List[str]

@app.post('/api/import/commit')
def import_commit(p:ImportCommit,account:dict=Depends(user)):
    capability(account,'import')
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    if not p.selected:raise HTTPException(422,'Chưa chọn trường nhập.')
    allowed=declared_fields(ResidentIn)
    if any(k not in allowed for k in p.selected):raise HTTPException(422,'Trường đích không hợp lệ.')
    if any(v not in allowed for v in p.mapping.values()):raise HTTPException(422,'Ánh xạ chứa trường không hợp lệ.')
    if len(p.rows)>5000:raise HTTPException(422,'Quá 5.000 dòng.')
    if 'full_name' not in p.selected or 'team_id' not in p.selected:raise HTTPException(422,'Bắt buộc chọn Họ tên và Tổ.')
    summary={'inserted':0,'duplicates':0,'invalid':0,'errors':[]}
    with LOCK:
        for idx,row in enumerate(p.rows):
            data={v:row.get(k) for k,v in p.mapping.items() if v in p.selected}
            if not data.get('team_id') and account['team_id']:data['team_id']=account['team_id']
            try:
                data['team_id']=int(str(data.get('team_id','')).replace('Tổ','').strip())
                if data.get('dob'):
                    dt=str(data['dob']).strip()
                    if re.fullmatch(r'\d\d/\d\d/\d{4}',dt):data['dob']=datetime.strptime(dt,'%d/%m/%Y').strftime('%Y-%m-%d')
                clean={k:(None if v=='' else v) for k,v in data.items()}
                ri=validate_model(ResidentIn,clean)
                validate_resident(ri,account)
                rid=uuid.uuid4().hex;t=now()
                db().execute('''INSERT INTO residents(id,full_name,cccd,dob,gender,hometown,permanent_address,team_id,phone,health_insurance,occupation,note,household_id,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''',
                             [rid]+person_params(ri)+[t,t])
                summary['inserted']+=1
            except Exception as e:
                if 'UNIQUE' in str(e):summary['duplicates']+=1
                else:
                    summary['invalid']+=1
                    if len(summary['errors'])<30:summary['errors'].append({'line':idx+2,'reason':str(e)[:200]})
        record(account['id'],'IMPORT_RESIDENTS','resident','',{'inserted':summary['inserted'],'duplicates':summary['duplicates'],'invalid':summary['invalid']})
        commit()
    return summary

def spreadsheet_safe(value):
    """Prevent CSV/XLSX cells from being interpreted as formulas."""
    if isinstance(value,str) and value.lstrip().startswith(('=','+','-','@')):
        return "'"+value
    return value

@app.get('/api/export')
def export_data(request:Request,fields:str='full_name,cccd,team_id',format:str='xlsx',team:Optional[int]=None,account:dict=Depends(user)):
    capability(account,'export')
    pc_only(request)
    permit(account,'reviewer','admin_kp','admin_root','super_admin')
    selected=[x.strip() for x in fields.split(',') if x.strip()]
    modes=field_modes(account['role'])
    if not selected or any(k not in BASE_FIELDS or modes.get(k)=='hidden' for k in selected):
        raise HTTPException(422,'Có trường xuất không được phép.')
    if format not in ('xlsx','csv','json'):raise HTTPException(422,'Định dạng không hợp lệ.')
    with LOCK:
        where='deleted_at IS NULL';params=[]
        scope,args=team_scope(account)
        where+=scope;params+=args
        if team:where+=' AND team_id=?';params.append(team)
        rows=[{k:r[k] for k in selected} for r in db().execute('SELECT * FROM residents WHERE '+where+' ORDER BY full_name',params)]
        if account['role']=='reviewer' and len(rows)>=100:
            raise HTTPException(403,'Vai trò Rà soát chỉ được xuất dưới 100 dòng.')
        for r in rows:
            masked=masking(r,account['role'],modes)
            r.clear();r.update(masked)
        record(account['id'],'EXPORT','resident','',{'format':format,'count':len(rows),'fields':selected})
        commit()
    headers=[BASE_FIELDS[x] for x in selected]
    stream=io.BytesIO()
    if format=='xlsx':
        wb=Workbook();ws=wb.active;ws.title='Dữ liệu dân cư';ws.append(headers)
        for r in rows:ws.append([spreadsheet_safe(r.get(x,'')) for x in selected])
        wb.save(stream)
        mimetype='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    elif format=='csv':
        import csv
        out=io.StringIO();writer=csv.writer(out);writer.writerow(headers)
        for r in rows:writer.writerow([spreadsheet_safe(r.get(x,'')) for x in selected])
        stream.write(b'\xef\xbb\xbf'+out.getvalue().encode('utf-8'))
        mimetype='text/csv; charset=utf-8'
    else:
        stream.write(json.dumps(rows,ensure_ascii=False,indent=2).encode('utf-8'))
        mimetype='application/json; charset=utf-8'
    stream.seek(0)
    name='QuanLyKhuPho_'+datetime.now().strftime('%Y%m%d')+'.'+format
    return StreamingResponse(stream,media_type=mimetype,headers={'Content-Disposition':f'attachment; filename="{name}"','Cache-Control':'no-store'})

@app.post('/api/backup')
def backup(request:Request,account:dict=Depends(user)):
    capability(account,'backup')
    pc_only(request);permit(account,'super_admin')
    with LOCK:
        record(account['id'],'BACKUP_LOCAL','vault');commit()
        path=VAULT.backup(VAULT.path.parent/'backups')
    return {'ok':True,'location':str(path),'note':'Sao lưu mã hóa trên cùng PC, không chuyển ra nơi khác.'}
