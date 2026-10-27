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


def workbook_bytes(sheets):
    from openpyxl import Workbook
    wb=Workbook()
    wb.remove(wb.active)
    for title,rows in sheets:
        ws=wb.create_sheet(title)
        for row in rows:ws.append(row)
    output=io.BytesIO();wb.save(output);wb.close()
    return output.getvalue()

def test_excel_multiple_sheets_same_structure_are_merged(api):
    client,headers=api
    raw=workbook_bytes([
        ('Tổ 1',[['Họ tên','Số CCCD','Tổ'],['Nguyễn Văn An','079000000001',1]]),
        ('Tổ 2',[['Họ tên','Số CCCD','Tổ'],['Trần Thị Bình','079000000002',2]])])
    preview=client.post('/api/import/preview',headers=headers,files={'file':('nhieu-sheet.xlsx',raw,'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')})
    assert preview.status_code==200,preview.text
    assert preview.json()['count']==2

def test_excel_rejects_mismatched_sheets_and_formulas(api):
    client,headers=api
    mismatch=workbook_bytes([
        ('A',[['Họ tên','Tổ'],['Nguyễn A',1]]),
        ('B',[['Tên khác','Tổ'],['Nguyễn B',2]])])
    response=client.post('/api/import/preview',headers=headers,files={'file':('lech.xlsx',mismatch,'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')})
    assert response.status_code==422 and 'cùng tiêu đề' in response.json()['detail']
    formula=workbook_bytes([('A',[['Họ tên','Tổ'],['=CONCAT("Nguyễn"," A")',1]])])
    response=client.post('/api/import/preview',headers=headers,files={'file':('cong-thuc.xlsx',formula,'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')})
    assert response.status_code==422 and 'công thức' in response.json()['detail']

def test_import_commit_rejects_duplicate_destination_mapping(api):
    client,headers=api
    response=client.post('/api/import/commit',headers=headers,json={
        'rows':[{'Tên 1':'Nguyễn A','Tên 2':'Nguyễn B','Tổ':'1'}],
        'mapping':{'Tên 1':'full_name','Tên 2':'full_name','Tổ':'team_id'},
        'selected':['full_name','team_id']})
    assert response.status_code==422 and 'cùng một trường đích' in response.json()['detail']
