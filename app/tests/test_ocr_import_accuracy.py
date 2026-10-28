"""Reject ambiguous OCR identities and malformed tabular rows."""
import io
import sys
from unittest.mock import patch
from PIL import Image
import pytest
from openpyxl import Workbook
from server.parsers import parse_file
from unittest.mock import MagicMock


def test_ocr_never_selects_one_of_multiple_distinct_id_numbers():
    image=Image.new('RGB',(20,20),'white');buffer=io.BytesIO();image.save(buffer,format='PNG')
    with patch('pytesseract.image_to_string',return_value='CCCD 079123456789\nKhác 079123456788'):
        result=parse_file(buffer.getvalue(),'can-cuoc.png')
    assert result['rows'][0]['Số CCCD nhận dạng']==''
    assert any('nhiều số' in warning for warning in result['warnings'])
    assert result['mapping']=={}


def test_ocr_splits_labelled_fields_without_guessing_unlabelled_values():
    image=Image.new('RGB',(20,20),'white');buffer=io.BytesIO();image.save(buffer,format='PNG')
    text='Họ và tên: Nguyễn Thị Ánh\nNgày sinh: 03-02-1990\nGiới tính: Nữ\nQuê quán: Long An\nNơi thường trú: Bình Tân\nCCCD: 079123456789\nDòng lạ: Bỏ qua'
    with patch('pytesseract.image_to_string',return_value=text):
        result=parse_file(buffer.getvalue(),'can-cuoc.png')
    assert result['ocr_review'] is True
    assert result['rows'][0]['Họ tên OCR']=='Nguyễn Thị Ánh'
    assert result['rows'][0]['Ngày sinh OCR']=='03-02-1990'
    assert result['rows'][0]['Số CCCD nhận dạng']=='079123456789'
    assert result['mapping']['Họ tên OCR']=='full_name'
    assert 'Dòng lạ' in result['raw_text']


def test_csv_extra_cells_and_excel_unheaded_values_are_rejected():
    with pytest.raises(ValueError,match='nhiều cột'):
        parse_file('Họ tên,Tổ\nNguyễn A,1,dữ liệu lạc\n'.encode(),'nguon.csv')
    wb=Workbook();ws=wb.active;ws.append(['Họ tên','Tổ']);ws.append(['Nguyễn A',1,'dữ liệu lạc'])
    buffer=io.BytesIO();wb.save(buffer);wb.close()
    with pytest.raises(ValueError,match='không có tiêu đề'):
        parse_file(buffer.getvalue(),'nguon.xlsx')


def test_scanned_pdf_uses_embedded_image_ocr_and_requires_review():
    image=Image.new('RGB',(20,20),'white');buffer=io.BytesIO();image.save(buffer,format='PNG')
    page=MagicMock();page.extract_text.return_value='';page.images=[MagicMock(data=buffer.getvalue())]
    document=MagicMock();document.pages=[page]
    with patch('server.parsers.PdfReader',return_value=document),patch('pytesseract.image_to_string',return_value='Họ tên: Nguyễn A\nNgày sinh: 01-01-1990'):
        result=parse_file(b'%PDF synthetic','scan.pdf')
    assert result['ocr_review'] is True
    assert result['rows'][0]['Họ tên OCR']=='Nguyễn A'


def test_multiline_label_is_extracted_and_next_label_is_not_used_as_value():
    image=Image.new('RGB',(20,20),'white');buffer=io.BytesIO();image.save(buffer,format='PNG')
    with patch('pytesseract.image_to_string',return_value='Họ tên:\nNguyễn Văn A\nNgày sinh:\n02-03-1991\nQuê quán:\nGiới tính: Nam'):
        result=parse_file(buffer.getvalue(),'scan.png')
    row=result['rows'][0]
    assert row['Họ tên OCR']=='Nguyễn Văn A'
    assert row['Ngày sinh OCR']=='02-03-1991'
    assert row['Quê quán OCR']==''


def test_pdf_scan_without_embedded_image_renders_page_in_memory():
    page=MagicMock();page.extract_text.return_value='';page.images=[]
    document=MagicMock();document.pages=[page]
    rendered=MagicMock();rendered.get_size.return_value=(600,800)
    bitmap=MagicMock();rendered.render.return_value=bitmap
    bitmap.to_pil.return_value=Image.new('RGB',(20,20),'white')
    pdfium=MagicMock();pdfium.PdfDocument.return_value.__getitem__.return_value=rendered
    with patch('server.parsers.PdfReader',return_value=document),patch.dict(sys.modules,{'pypdfium2':pdfium}),patch('pytesseract.image_to_string',return_value='Họ tên: Trần B'):
        result=parse_file(b'%PDF synthetic','scan.pdf')
    assert result['rows'][0]['Họ tên OCR']=='Trần B'
    pdfium.PdfDocument.assert_called_once_with(b'%PDF synthetic')
    rendered.render.assert_called_once_with(scale=2.0)


def test_general_document_keeps_unrecognized_label_for_manual_mapping():
    image=Image.new('RGB',(20,20),'white');buffer=io.BytesIO();image.save(buffer,format='PNG')
    with patch('pytesseract.image_to_string',return_value='Họ tên: Nguyễn A\nNghề nghiệp: Thợ may\nMã hồ sơ: KH-42'):
        result=parse_file(buffer.getvalue(),'mau-khac.png')
    assert result['rows'][0]['Nghề nghiệp']=='Thợ may'
    assert result['rows'][0]['Mã hồ sơ']=='KH-42'
    assert next(x for x in result['ocr_items'] if x['source']=='Nghề nghiệp')['suggested']=='occupation'
    assert next(x for x in result['ocr_items'] if x['source']=='Mã hồ sơ')['suggested']==''
