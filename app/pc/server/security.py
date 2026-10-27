from __future__ import annotations
from argon2 import PasswordHasher
from argon2.exceptions import VerifyMismatchError,VerificationError
from datetime import datetime,timezone,timedelta
import secrets,hashlib,hmac,json
PH=PasswordHasher(time_cost=3,memory_cost=32768,parallelism=2)
ROLES=['viewer','reviewer','admin_kp','admin_root','super_admin']

def now():return datetime.now(timezone.utc).isoformat()
def hash_password(password:str)->str:
    if len(password)<12:raise ValueError('Mật khẩu phải dài tối thiểu 12 ký tự.')
    return PH.hash(password)
def verify_password(hashval:str,password:str)->bool:
    try:return bool(PH.verify(hashval,password))
    except (VerifyMismatchError,VerificationError):return False

def masking(data:dict, role:str, permission_lookup=None)->dict:
    """Enforce per-field access at the server boundary; never rely on hiding UI columns."""
    result=dict(data)
    modes=permission_lookup or {}
    for k in list(result):
        mode=modes.get(k,'optional')
        if mode=='hidden':
            del result[k]
        elif mode=='masked' or (role=='viewer' and k in ('cccd','phone')):
            value=result[k]
            if not value:continue
            if k=='cccd':result[k]='*'*max(0,len(str(value))-5)+str(value)[-5:]
            elif k=='phone':result[k]=str(value)[:3]+'****'+str(value)[-3:]
            else:result[k]='***'
    return result

class SessionStore:
    def __init__(self):self.sessions={}
    def issue(self,user_id:str,device_id:str)->str:
        token=secrets.token_urlsafe(36)
        self.sessions[hashlib.sha256(token.encode()).digest()]={'user_id':user_id,'device_id':device_id,'expiry':datetime.now(timezone.utc)+timedelta(minutes=15)}
        return token
    def resolve(self,token:str):
        if not token:return None
        item=self.sessions.get(hashlib.sha256(token.encode()).digest())
        if item and item['expiry']>datetime.now(timezone.utc):
            item['expiry']=datetime.now(timezone.utc)+timedelta(minutes=15)
            return item
        return None
    def revoke(self,token:str):self.sessions.pop(hashlib.sha256(token.encode()).digest(),None)
    def revoke_user(self,user_id:str):
        for k,v in list(self.sessions.items()):
            if v['user_id']==user_id:del self.sessions[k]
