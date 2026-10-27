"""Executable integration checks for PC-scoped permissions and approved remote sessions."""
import pytest
from fastapi.testclient import TestClient
from server.app import app,configure_vault,SESSIONS,MOBILE_APPROVALS

VAULT_PW='KhoDuLieuChuPC#2026'
ADMIN_PW='DangNhapQuanTri#2026'
USER_PW='DangNhapCanBo#2026'

@pytest.fixture
def env(tmp_path):
    configure_vault(str(tmp_path/'co_so_du_lieu.vault'),VAULT_PW)
    with TestClient(app) as c:
        r=c.post('/api/setup',json={'username':'quantri','full_name':'Phạm Thị Ánh','password':ADMIN_PW})
        assert r.status_code==200,r.text
        t=c.post('/api/login',json={'username':'quantri','password':ADMIN_PW}).json()['token']
        yield c,{'Authorization':'Bearer '+t},tmp_path

def add(c,adm,name,role,team):
    r=c.post('/api/accounts',headers=adm,json={'username':name,'full_name':'Nguyễn Thị Hòa','role':role,'team_id':team,'password':USER_PW})
    assert r.status_code==200,r.text
    return r.json()['id']

def signin(c,name):
    resp=c.post('/api/login',json={'username':name,'password':USER_PW})
    assert resp.status_code==200,resp.text
    h={'Authorization':'Bearer '+resp.json()['token']}
    r=c.post('/api/account/change-password',headers=h,json={'old_password':USER_PW,'new_password':'DaDoiMatKhau#2026'})
    assert r.status_code==200,r.text
    r=c.post('/api/login',json={'username':name,'password':'DaDoiMatKhau#2026'})
    return {'Authorization':'Bearer '+r.json()['token']}

def test_role_capability_is_server_enforced_immediately(env):
    c,adm,_=env
    add(c,adm,'canbo','reviewer',1)
    h=signin(c,'canbo')
    assert c.get('/api/residents',headers=h).status_code==200
    matrix=c.get('/api/permissions',headers=adm)
    assert matrix.status_code==200 and matrix.json()['role_grants']['reviewer']['read_residents']
    deny=c.put('/api/permissions/capability',headers=adm,json={'role':'reviewer','capability':'read_residents','allowed':False})
    assert deny.status_code==200
    assert c.get('/api/residents',headers=h).status_code==403
    grant=c.put('/api/permissions/capability',headers=adm,json={'role':'reviewer','capability':'read_residents','allowed':True})
    assert grant.status_code==200
    assert c.get('/api/residents',headers=h).status_code==200
    assert c.put('/api/permissions/capability',headers=adm,json={'role':'viewer','capability':'export','allowed':True}).status_code==403
    assert c.put('/api/permissions/capability',headers=adm,json={'role':'super_admin','capability':'backup','allowed':False}).status_code==403
    assert c.get('/api/audit/verify',headers=adm).json()['valid'] is True

def test_field_privacy_is_enforced_in_api_not_just_ui(env):
    c,adm,_=env
    add(c,adm,'viewer01','viewer',1)
    viewer=signin(c,'viewer01')
    create=c.post('/api/residents',headers=adm,json={'full_name':'Đặng Thị Ngọc Anh','cccd':'079123456789','phone':'0901234567','team_id':1})
    assert create.status_code==200,create.text
    rows=c.get('/api/residents',headers=viewer).json()['items']
    assert rows[0]['full_name']=='Đặng Thị Ngọc Anh' and rows[0]['cccd'].endswith('56789')
    assert '079123456789' not in str(rows)
    change=c.put('/api/permissions/field',headers=adm,json={'role':'viewer','field':'phone','mode':'hidden'})
    assert change.status_code==200
    rows=c.get('/api/residents',headers=viewer).json()['items']
    assert 'phone' not in rows[0]
    assert c.put('/api/permissions/field',headers=adm,json={'role':'viewer','field':'cccd','mode':'optional'}).status_code==403
    assert c.get('/api/permissions',headers=viewer).status_code==403


def test_mobile_approval_proof_and_one_time_use(env):
    c,adm,_=env
    add(c,adm,'mobile01','reviewer',1)
    pending=c.post('/api/login',json={'username':'mobile01','password':USER_PW,'device_id':'android-01','device_label':'Máy cá nhân'})
    assert pending.status_code==202
    data=pending.json()
    assert 'token' not in data
    rid=data['request_id'];proof=data['proof']
    assert c.post('/api/mobile/login-status',json={'request_id':rid,'proof':'bad-proof-'+'x'*22}).status_code==404
    assert c.get('/api/mobile/login-requests',headers=adm).json()['items'][0]['request_id']==rid
    did=c.get('/api/devices',headers=adm).json()['items'][0]['id']
    assert c.patch('/api/devices/'+did,headers=adm,json={'status':'approved'}).status_code==200
    assert c.post(f'/api/mobile/login-requests/{rid}/decision',headers=adm,json={'approved':True}).status_code==200
    good=c.post('/api/mobile/login-status',json={'request_id':rid,'proof':proof})
    assert good.status_code==200 and good.json()['token']
    assert c.post('/api/mobile/login-status',json={'request_id':rid,'proof':proof}).status_code==404
    assert c.post(f'/api/mobile/login-requests/{rid}/decision',headers=adm,json={'approved':True}).status_code==404


def test_mobile_denial_and_admin_PC_only(env):
    c,adm,_=env
    add(c,adm,'deny01','reviewer',1)
    req=c.post('/api/login',json={'username':'deny01','password':USER_PW,'device_id':'android-02','device_label':'Điện thoại chưa rõ'}).json()
    rid=req['request_id']
    remote=TestClient(app,client=('192.168.1.85',34444))
    assert remote.get('/api/permissions',headers=adm).status_code==403
    assert remote.get('/api/mobile/login-requests',headers=adm).status_code==403
    assert remote.post(f'/api/mobile/login-requests/{rid}/decision',headers=adm,json={'approved':False}).status_code==403
    assert c.post(f'/api/mobile/login-requests/{rid}/decision',headers=adm,json={'approved':False}).status_code==200
    assert c.post('/api/mobile/login-status',json={'request_id':rid,'proof':req['proof']}).status_code==403
    assert c.post('/api/mobile/login-status',json={'request_id':rid,'proof':req['proof']}).status_code==404


def test_upgrade_existing_encrypted_vault_keeps_accounts(env):
    c,adm,path=env
    uid=add(c,adm,'keepuser','viewer',4)
    from server.crypto import Vault
    v=Vault(path/'co_so_du_lieu.vault',VAULT_PW)
    assert v.conn.execute('SELECT role FROM accounts WHERE id=?',(uid,)).fetchone()[0]=='viewer'
    assert v.conn.execute('SELECT allowed FROM role_permissions WHERE role=? AND capability=?',('viewer','export')).fetchone()[0]==0
    assert v.conn.execute('SELECT value FROM meta WHERE key=?',('schema_version',)).fetchone()[0]=='7.3.0'
    v.conn.close()

def test_admin_kp_is_restricted_to_assigned_team_on_writes(env):
    c,adm,_=env
    add(c,adm,'kp1','admin_kp',1)
    kp=signin(c,'kp1')
    assert c.post('/api/households',headers=kp,json={'code':'H2','address':'Tổ 2, Khu phố','team_id':2}).status_code==403
    a=c.post('/api/residents',headers=adm,json={'full_name':'Người thuộc tổ hai','team_id':2})
    assert a.status_code==200
    rid=a.json()['id']
    assert c.put('/api/residents/'+rid,headers=kp,json={'full_name':'Chuyển khác tổ','team_id':1}).status_code==403
    assert c.delete('/api/residents/'+rid,headers=kp).status_code==403
    assert c.get('/api/residents',headers=kp).json()['total']==0


def test_masked_fields_cannot_be_overwritten_by_reviewer(env):
    c,adm,_=env
    add(c,adm,'review01','reviewer',1)
    reviewer=signin(c,'review01')
    r=c.post('/api/residents',headers=adm,json={'full_name':'Bùi Thị Hà','team_id':1,'cccd':'079000003456'})
    assert r.status_code==200
    rid=r.json()['id']
    assert c.put('/api/permissions/field',headers=adm,json={'role':'reviewer','field':'cccd','mode':'masked'}).status_code==200
    q=c.get('/api/residents',headers=reviewer).json()['items'][0]
    assert '079000003456' not in str(q)
    attempt=c.put('/api/residents/'+rid,headers=reviewer,json={'full_name':'Bùi Thị Hà mới','team_id':1,'cccd':'079000009999'})
    assert attempt.status_code==403
    okay=c.put('/api/residents/'+rid,headers=reviewer,json={'full_name':'Bùi Thị Hà mới','team_id':1})
    assert okay.status_code==200
    actual=c.get('/api/residents',headers=adm).json()['items'][0]
    assert actual['cccd']=='079000003456' and actual['full_name']=='Bùi Thị Hà mới'
    # A masked ID cannot be searched via a blind equality probe.
    assert c.get('/api/residents',headers=reviewer,params={'q':'079000003456'}).json()['total']==0


def test_restore_old_v71_schema_into_stage2_without_data_loss(tmp_path):
    from pathlib import Path
    from server.crypto import Vault
    old_schema=Path(__file__).resolve().parent/'fixtures'/'legacy_7_1_schema.sql'
    assert old_schema.exists()
    vault_path=tmp_path/'legacy.vault'
    v=Vault(vault_path,VAULT_PW)
    v.conn.executescript(old_schema.read_text(encoding='utf8'))
    v.conn.execute("INSERT INTO accounts(id,username,display_name,password_hash,role,team_id,created_at) VALUES(?,?,?,?,?,?,?)",
                   ('legacy-001','cu01','Lê Thị Hiền','legacy-password-hash','viewer',1,'2026-10-01T00:00:00Z'))
    v.conn.commit();v.persist();v.conn.close()
    configure_vault(str(vault_path),VAULT_PW)
    from server.app import db
    assert db().execute("SELECT display_name FROM accounts WHERE id='legacy-001'").fetchone()[0]=='Lê Thị Hiền'
    assert db().execute('SELECT count(*) FROM role_permissions').fetchone()[0]==80
    assert db().execute("SELECT value FROM meta WHERE key='schema_version'").fetchone()[0]=='7.3.0'

def test_cannot_clear_masked_field_by_sending_empty_value(env):
    c,adm,_=env
    add(c,adm,'review02','reviewer',1)
    h=signin(c,'review02')
    create=c.post('/api/residents',headers=adm,json={'full_name':'Trần Thị Quỳnh','cccd':'079000001234','team_id':1})
    rid=create.json()['id']
    assert c.put('/api/permissions/field',headers=adm,json={'role':'reviewer','field':'cccd','mode':'hidden'}).status_code==200
    assert c.put('/api/residents/'+rid,headers=h,json={'full_name':'Trần Thị Quỳnh','team_id':1,'cccd':''}).status_code==403
    assert c.put('/api/residents/'+rid,headers=h,json={'full_name':'Trần Thị Quỳnh mới','team_id':1}).status_code==200
    assert c.get('/api/residents',headers=adm).json()['items'][0]['cccd']=='079000001234'
