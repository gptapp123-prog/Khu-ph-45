"""Nhập/xuất phải giữ đúng dữ liệu, quyền và an toàn bảng tính."""
import csv
import io
import json
from openpyxl import load_workbook
import pytest
from fastapi.testclient import TestClient
from server.app import app,configure_vault,SESSIONS

PW='KhoNhapXuat_2026!'
ADMIN='QuanTriNhapXuat_2026!'

@pytest.fixture
def api(tmp_path):
    SESSIONS.sessions.clear()
    configure_vault(str(tmp_path/'import_export.vault'),PW)
    with TestClient(app) as client:
        assert client.post('/api/setup',json={'username':'quantri','full_name':'Quản trị nhập xuất','password':ADMIN}).status_code==200
        login=client.post('/api/login',json={'username':'quantri','password':ADMIN})
        yield client,{'Authorization':'Bearer '+login.json()['token']}

def test_csv_semicolon_preview_commit_and_json_export(api):
    client,headers=api
    raw='Họ tên;Số CCCD;Tổ dân phố;Ghi chú\nNguyễn Thị Ánh;079123456789;3;Hồ sơ thử\n'.encode('utf-8-sig')
    preview=client.post('/api/import/preview',headers=headers,files={'file':('dan-cu.csv',raw,'text/csv')})
    assert preview.status_code==200,preview.text
    data=preview.json()
    assert data['count']==1
    assert data['mapping']['Họ tên']=='full_name'
    assert data['mapping']['Tổ dân phố']=='team_id'
    commit=client.post('/api/import/commit',headers=headers,json={
        'rows':data['rows'],'mapping':data['mapping'],
        'selected':['full_name','cccd','team_id','note']})
    assert commit.status_code==200,commit.text
    assert commit.json()=={'inserted':1,'duplicates':0,'invalid':0,'errors':[]}
    exported=client.get('/api/export',headers=headers,params={'fields':'full_name,cccd,team_id,note','format':'json'})
    assert exported.status_code==200
    assert exported.json()[0]['full_name']=='Nguyễn Thị Ánh'

@pytest.mark.parametrize('header',[
    'Họ tên,Họ tên,Tổ\nNguyễn A,Nguyễn B,1\n',
    'Họ tên,,Tổ\nNguyễn A,x,1\n'])
def test_csv_rejects_duplicate_or_empty_headers(api,header):
    client,headers=api
    response=client.post('/api/import/preview',headers=headers,files={'file':('loi.csv',header.encode('utf-8'),'text/csv')})
    assert response.status_code==422

def test_csv_and_xlsx_exports_neutralize_formula_cells_but_json_preserves_text(api):
    client,headers=api
    payload={'full_name':'=HYPERLINK("https://invalid.example","Bấm")','team_id':1,'note':'@SUM(1+1)'}
    created=client.post('/api/residents',headers=headers,json=payload)
    assert created.status_code==200,created.text
    csv_response=client.get('/api/export',headers=headers,params={'fields':'full_name,note','format':'csv'})
    rows=list(csv.reader(io.StringIO(csv_response.content.decode('utf-8-sig'))))
    assert rows[1][0].startswith("'=") and rows[1][1].startswith("'@")
    xlsx_response=client.get('/api/export',headers=headers,params={'fields':'full_name,note','format':'xlsx'})
    wb=load_workbook(io.BytesIO(xlsx_response.content),data_only=False)
    assert wb.active['A2'].value.startswith("'=") and wb.active['A2'].data_type=='s'
    assert wb.active['B2'].value.startswith("'@")
    wb.close()
    json_response=client.get('/api/export',headers=headers,params={'fields':'full_name,note','format':'json'})
    assert json_response.json()[0]['full_name'].startswith('=')
