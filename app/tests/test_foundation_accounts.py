"""Security regression checks against real FastAPI + encrypted local vault."""
from pathlib import Path
import pytest
from fastapi.testclient import TestClient
from server.app import app,configure_vault,SESSIONS,VAULT
from server.crypto import Vault

PW='QuyenQuanTriKho_2026!'
PASSWORD='MoiTao_Quyen2026!'

@pytest.fixture
def api(tmp_path):
    import server.app as srv
    SESSIONS.sessions.clear()
    path=tmp_path/'kho.vault'
    configure_vault(str(path),PW)
    with TestClient(app) as client:
        bootstrap=client.post('/api/setup',json={'username':'quantri','full_name':'Nguyễn Quản Trị','password':PASSWORD})
        assert bootstrap.status_code==200, bootstrap.text
        login=client.post('/api/login',json={'username':'quantri','password':PASSWORD})
        assert login.status_code==200,login.text
        token=login.json()['token']
        yield client,{'Authorization':'Bearer '+token},path

def make_account(client,headers,username,role='viewer',team=1,**extras):
    payload={'username':username,'full_name':'Trần Thị Hạnh','password':'DauTien_2026XX!','role':role,'team_id':team,**extras}
    res=client.post('/api/accounts',headers=headers,json=payload)
    assert res.status_code==200,res.text
    return res.json()['id']

def login(client,username,password='DauTien_2026XX!',device_id='pc'):
    return client.post('/api/login',json={'username':username,'password':password,'device_id':device_id,'device_label':'Điện thoại thử nghiệm'})

def auth(response):return {'Authorization':'Bearer '+response.json()['token']}

def test_bootstrap_only_once_and_unauthenticated_closed(api):
    client,admin,path=api
    assert client.get('/api/accounts').status_code==401
    assert client.get('/api/health').json()['setup_required'] is False
    assert client.post('/api/setup',json={'username':'other','full_name':'Nguyễn Văn A','password':PASSWORD}).status_code==409
    assert path.exists()
    raw=path.read_bytes()
    assert raw.startswith(b'KPHO2') and b'Nguy\xe1\xbb\x85n' not in raw and b'CREATE TABLE' not in raw

def test_account_vietnamese_personal_data_and_password_rotation(api):
    client,admin,_=api
    uid=make_account(client,admin,'canbo01',phone='0912345678',email='n.v@internal.local',zalo_id='ma_noi_bo')
    rows=client.get('/api/accounts',headers=admin).json()['items']
    a=next(r for r in rows if r['id']==uid)
    assert a['name']=='Trần Thị Hạnh' and a['must_change_password'] is True
    assert a['phone']=='0912345678'
    r=login(client,'canbo01');assert r.status_code==200
    limited=auth(r)
    assert client.get('/api/stats',headers=limited).status_code==403
    assert client.get('/api/me',headers=limited).json()['must_change_password']
    assert client.post('/api/account/change-password',headers=limited,json={'old_password':'wrong','new_password':'MatKhauCuoiCung_2026!'}).status_code==401
    assert client.post('/api/account/change-password',headers=limited,json={'old_password':'DauTien_2026XX!','new_password':'MatKhauCuoiCung_2026!'}).status_code==200
    assert client.get('/api/me',headers=limited).status_code==401
    assert login(client,'canbo01').status_code==401
    new_login=login(client,'canbo01','MatKhauCuoiCung_2026!')
    assert new_login.status_code==200 and new_login.json()['must_change_password'] is False

def test_role_hierarchy_and_team_scope(api):
    client,admin,_=api
    make_account(client,admin,'kp1','admin_kp',team=1)
    make_account(client,admin,'kp2','admin_kp',team=2)
    for user in ('kp1','kp2'):
        r=login(client,user);h=auth(r)
        assert client.post('/api/account/change-password',headers=h,json={'old_password':'DauTien_2026XX!','new_password':'MatKhauCuoiCung_2026!'}).status_code==200
    kp1=auth(login(client,'kp1','MatKhauCuoiCung_2026!'))
    kp2=auth(login(client,'kp2','MatKhauCuoiCung_2026!'))
    assert client.post('/api/accounts',headers=kp1,json={'username':'outside','full_name':'Bên ngoài','password':'Password12345!','role':'reviewer','team_id':2}).status_code==403
    assert client.post('/api/accounts',headers=kp1,json={'username':'sameadmin','full_name':'Quản trị','password':'Password12345!','role':'admin_kp','team_id':1}).status_code==403
    new=make_account(client,kp1,'to1','viewer',team=1)
    names=[r['username'] for r in client.get('/api/accounts',headers=kp2).json()['items']]
    assert 'to1' not in names
    assert client.patch('/api/accounts/'+new,headers=kp2,json={'active':False}).status_code==403
    assert client.patch('/api/accounts/'+new,headers=kp1,json={'active':False}).status_code==200
    assert client.patch('/api/accounts/'+new,headers=kp1,json={'active':True}).status_code==200

def test_mobile_pending_approval_then_block_revoke(api):
    client,admin,_=api
    uid=make_account(client,admin,'thucdia','reviewer',team=1)
    first=login(client,'thucdia',device_id='android-test-01')
    assert first.status_code==202 and first.json()['status']=='pending_approval'
    req=first.json()
    assert 'token' not in req
    check={'request_id':req['request_id'],'proof':req['proof']}
    assert client.post('/api/mobile/login-status',json=check).json()['status']=='pending_approval'
    devices=client.get('/api/devices',headers=admin).json()['items']
    assert len(devices)==1 and devices[0]['status']=='pending'
    did=devices[0]['id']
    # Until a device is approved, even a PC administrator cannot approve its login.
    reject=client.post('/api/mobile/login-requests/'+req['request_id']+'/decision',headers=admin,json={'approved':True})
    assert reject.status_code==409
    assert client.patch('/api/devices/'+did,headers=admin,json={'status':'approved'}).status_code==200
    assert client.post('/api/mobile/login-requests/'+req['request_id']+'/decision',headers=admin,json={'approved':True}).status_code==200
    approved=client.post('/api/mobile/login-status',json=check)
    assert approved.status_code==200 and approved.json()['status']=='approved'
    mobile=auth(approved)
    assert client.get('/api/me',headers=mobile).status_code==200
    assert client.post('/api/mobile/login-status',json=check).status_code==404  # replay blocked
    assert client.patch('/api/devices/'+did,headers=admin,json={'status':'blocked'}).status_code==200
    assert client.get('/api/me',headers=mobile).status_code==401
    assert login(client,'thucdia',device_id='android-test-01').status_code==403

def test_login_lockout_and_reset(api):
    client,admin,_=api
    uid=make_account(client,admin,'lockout','viewer',team=1)
    for i in range(5):
        assert login(client,'lockout','badpassword').status_code==401
    assert login(client,'lockout').status_code==429
    reset=client.patch('/api/accounts/'+uid,headers=admin,json={'password':'Reset2026_SauKhoa!'})
    assert reset.status_code==200
    assert login(client,'lockout','Reset2026_SauKhoa!').status_code==200

def test_audit_chain_and_encrypted_reopen(api):
    client,admin,path=api
    make_account(client,admin,'nguoiviet','reviewer',team=3)
    assert client.get('/api/audit/verify',headers=admin).json()['valid'] is True
    data=path.read_bytes()
    from server.crypto import Vault
    wrong=pytest.raises(ValueError)
    with wrong:Vault(path,'KhoaKhacKhongDung_2026!')
    reopened=Vault(path,PW)
    assert reopened.conn.execute("SELECT display_name FROM accounts WHERE username='nguoiviet'").fetchone()[0]=='Trần Thị Hạnh'
    reopened.conn.close()

def test_account_validation_duplicate_and_no_permission(api):
    client,admin,_=api
    uid=make_account(client,admin,'kiemtra','viewer',team=1)
    assert client.post('/api/accounts',headers=admin,json={'username':'kiemtra','full_name':'Lặp','password':'AAAAAA2026_!Bb','role':'viewer','team_id':1}).status_code==409
    assert client.post('/api/accounts',headers=admin,json={'username':'đăngnhập','full_name':'Kiểm tra','password':'AAAAAA2026_!Bb','role':'viewer','team_id':1}).status_code==422
    assert client.post('/api/accounts',headers=admin,json={'username':'abcnew','full_name':'Kiểm tra','password':'AAAAAA2026_!Bb','role':'viewer','team_id':1,'phone':'123'}).status_code==422
    r=login(client,'kiemtra');h=auth(r)
    assert client.get('/api/accounts',headers=h).status_code==403


def test_pc_only_account_mutations_rejected_for_remote_source(api):
    client,admin,_=api
    uid=make_account(client,admin,'testremote','viewer',team=1)
    with TestClient(app,client=('192.168.1.87',50000)) as remote:
        assert remote.post('/api/accounts',headers=admin,json={'username':'remote','full_name':'Remote','password':'Password2026_1234!','role':'viewer','team_id':1}).status_code==403
        assert remote.patch('/api/accounts/'+uid,headers=admin,json={'active':False}).status_code==403
        assert remote.get('/api/export',headers=admin).status_code==403
