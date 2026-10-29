from fastapi.testclient import TestClient
from server.app import app,configure_vault

def test_named_routes_serve_client_without_exposing_api(tmp_path):
    configure_vault(str(tmp_path/'route.vault'),'Route_Test_Passphrase_2026!')
    routes=['cong-viec','to-chuc','reports','bao-cao-thang','import','tro-ly',
            'lich-su','conflicts','sao-luu','tai-khoan','cai-dat','huong-dan',
            'login','setup','owner-setup']
    with TestClient(app) as client:
        for route in routes:
            response=client.get('/'+route)
            assert response.status_code==200
            assert 'Quản lý Khu phố' in response.text
        assert client.get('/khong-ton-tai').status_code==404
        assert client.get('/api/accounts').status_code==401
