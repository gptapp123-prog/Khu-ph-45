"""Configured headings control both extracted values and Excel columns."""
import io
from unittest.mock import patch
from openpyxl import load_workbook
from PIL import Image
from server.ocr_extraction import extract_configured
from server.parsers import parse_file
from test_foundation_accounts import api  # noqa: F401 - shared vault fixture


def fields():
    return [
        {'label':'Họ và tên','aliases':['Họ tên'],'column':'Họ và tên','type':'text'},
        {'label':'Số hiệu bằng','aliases':[],'column':'Số hiệu bằng','type':'text'},
        {'label':'Học sinh trường','aliases':['Trường'],'column':'Trường','type':'text'},
        {'label':'Tại hội đồng thi','aliases':['Hội đồng thi'],'column':'Hội đồng thi','type':'text'},
    ]


def test_full_values_suffix_and_multiple_document_headings():
    text='Họ và tên: Nguyễn Văn A\nSố hiệu bằng: 12345/ptxn\nHọc sinh trường: THPT Thủ Đức\nTại hội đồng thi: THPT Tam Phú\nNghề nghiệp: Kế toán'
    result,warnings=extract_configured(text,fields())
    assert result=={'Họ và tên':'Nguyễn Văn A','Số hiệu bằng':'12345/ptxn','Trường':'THPT Thủ Đức','Hội đồng thi':'THPT Tam Phú'}
    assert warnings==[] and 'Nghề nghiệp' not in result


def test_dotted_leaders_multiline_and_adjacent_heading():
    text='Số hiệu bằng:..... 2244863 /PT.\nHọ và tên:\nNguyễn Văn A\nHọc sinh trường: THPT Thủ Đức\nTại hội đồng thi: THPT Tam Phú'
    result,_=extract_configured(text,fields())
    assert result['Số hiệu bằng']=='2244863 /PT.'
    assert result['Họ và tên']=='Nguyễn Văn A'
    only=[{'label':'Ngày sinh','aliases':[],'column':'Ngày sinh','type':'text'}]
    assert extract_configured('Ngày sinh: 18/07/1988 Giới tính: Nam',only)[0]['Ngày sinh']=='18/07/1988'
    both=only+[{'label':'Giới tính','aliases':[],'column':'Giới tính','type':'text'}]
    assert extract_configured('Ngày sinh: 18/07/1988 Giới tính: Nam',both)[0]=={'Ngày sinh':'18/07/1988','Giới tính':'Nam'}


def test_conflict_and_missing_stay_blank():
    result,warnings=extract_configured('Số hiệu bằng: 12345/PT\nSố hiệu bằng: 12346/PT\nHọ và tên:\nNgày sinh: 01/01/2000',fields())
    assert result['Số hiệu bằng']=='' and result['Họ và tên']==''
    assert any('nhiều giá trị' in w for w in warnings)


def test_api_template_image_extract_and_excel_export(api):
    client,headers,_=api
    saved=client.post('/api/ocr/templates',headers=headers,json={'name':'Thông tin văn bằng','fields':fields()})
    assert saved.status_code==200,saved.text
    tid=saved.json()['id']
    image=Image.new('RGB',(60,30),'white');stream=io.BytesIO();image.save(stream,format='PNG')
    text='Họ và tên: Nguyễn Văn A\nSố hiệu bằng: 12345/ptxn\nHọc sinh trường: THPT Thủ Đức\nTại hội đồng thi: THPT Tam Phú\nNghề nghiệp: Kế toán'
    with patch('pytesseract.image_to_string',return_value=text):
        preview=client.post('/api/import/preview',headers=headers,files={'file':('anh.png',stream.getvalue(),'image/png')})
    assert preview.status_code==200,preview.text
    extracted=client.post('/api/ocr/extract',headers=headers,json={'template_id':tid,'text':preview.json()['raw_text']})
    assert extracted.status_code==200,extracted.text
    row=extracted.json()['values']
    assert row['Số hiệu bằng']=='12345/ptxn' and row['Hội đồng thi']=='THPT Tam Phú'
    assert 'Nghề nghiệp' not in row
    output=client.post('/api/ocr/export',headers=headers,json={'template_id':tid,'filename':'van_bang.xlsx','rows':[row]})
    assert output.status_code==200,output.text
    wb=load_workbook(io.BytesIO(output.content));ws=wb.active
    assert [c.value for c in ws[1]]==list(row)
    assert [c.value for c in ws[2]]==list(row.values())
    wb.close()
    invalid=client.post('/api/ocr/export',headers=headers,json={'template_id':tid,'filename':'van_bang','rows':[{**row,'Nghề nghiệp':'Kế toán'}]})
    assert invalid.status_code==422
