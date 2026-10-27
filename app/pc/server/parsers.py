from __future__ import annotations
import io,csv,re,unicodedata
from openpyxl import load_workbook
from pypdf import PdfReader
from PIL import Image

KEYWORDS={'cccd':['cccd','can cuoc','so dinh danh','so cccd'],
'full_name':['ho ten','ten day du','ten'], 'dob':['ngay sinh','sinh ngay'],
'gender':['gioi tinh'], 'hometown':['que quan'],
'permanent_address':['thuong tru','dia chi','noi thuong tru'],
'team_id':['to dan pho','to','to so'], 'phone':['so dien thoai','sdt','dien thoai'],
'occupation':['nghe nghiep'], 'health_insurance':['bhyt','bao hiem y te'],
'note':['ghi chu']}

def strip_accents(v):
    return ''.join(c for c in unicodedata.normalize('NFD',str(v).lower()) if unicodedata.category(c)!='Mn').strip()
def validate_headers(headers):
    cleaned=[str(h).strip() for h in headers]
    if not cleaned or not any(cleaned):raise ValueError('Tệp không có tiêu đề cột.')
    normalized=[strip_accents(h) for h in cleaned]
    if any(not h for h in normalized):raise ValueError('Tiêu đề cột không được để trống.')
    if len(normalized)!=len(set(normalized)):raise ValueError('Tiêu đề cột bị trùng.')
    return cleaned

def automap(headers):
    mapped={}
    for h in headers:
        s=strip_accents(h)
        for k,variants in KEYWORDS.items():
            if s in variants: mapped[h]=k;break
    return mapped

def parse_file(raw:bytes,filename:str):
    if len(raw)>10*1024*1024:raise ValueError('Giới hạn tệp nhập: 10 MB.')
    ext=filename.lower().rsplit('.',1)[-1] if '.' in filename else ''
    rows=[]
    if ext in ('xlsx','xlsm'):
        wb=load_workbook(io.BytesIO(raw),read_only=True,data_only=False)
        headers=[]
        try:
            for sh in wb.worksheets:
                it=sh.iter_rows()
                first=next(it,None)
                if not first or not any(c.value is not None and str(c.value).strip() for c in first):continue
                if any(c.data_type=='f' for c in first):raise ValueError('Tiêu đề Excel không được chứa công thức.')
                sheet_headers=validate_headers([str(c.value).strip() if c.value is not None else f'Cột {i+1}' for i,c in enumerate(first)])
                if headers and [strip_accents(x) for x in sheet_headers]!=[strip_accents(x) for x in headers]:
                    raise ValueError('Các sheet Excel phải có cùng tiêu đề và thứ tự cột.')
                if not headers:headers=sheet_headers
                for cells in it:
                    if any(c.data_type=='f' for c in cells):raise ValueError('Excel chứa công thức. Hãy chuyển công thức thành giá trị trước khi nhập.')
                    values=[c.value for c in cells]
                    if any(v is not None and str(v).strip() for v in values):
                        rows.append({headers[i]: '' if i>=len(values) or values[i] is None else str(values[i]).strip() for i in range(len(headers))})
                    if len(rows)>5000:raise ValueError('Tối đa 5.000 dòng/lần nhập.')
            if not headers:raise ValueError('Excel trống.')
        finally:wb.close()
    elif ext=='csv':
        content=raw.decode('utf-8-sig')
        first=content[:1024]
        try:dialect=csv.Sniffer().sniff(first,delimiters=',;\t')
        except csv.Error:dialect=csv.excel
        reader=csv.DictReader(io.StringIO(content),dialect=dialect)
        headers=validate_headers(reader.fieldnames or [])
        reader.fieldnames=headers
        for row in reader:
            if len(rows)>=5000:raise ValueError('Tối đa 5.000 dòng/lần nhập.')
            rows.append({str(k or ''):str(v or '') for k,v in row.items()})
    elif ext=='pdf':
        pdf=PdfReader(io.BytesIO(raw))
        content='\n'.join((p.extract_text() or '') for p in pdf.pages[:50])
        if not content.strip():raise ValueError('PDF chỉ có ảnh: cần dùng mô-đun OCR cài trên PC.')
        headers=['Nội dung PDF'];rows=[{'Nội dung PDF':x.strip()} for x in content.splitlines() if x.strip()][:5000]
    elif ext in ('png','jpg','jpeg','webp'):
        import pytesseract
        with Image.open(io.BytesIO(raw)) as img:
            if img.width*img.height>20_000_000:raise ValueError('Ảnh quá lớn.')
            content=pytesseract.image_to_string(img,lang='vie+eng')
        headers=['Văn bản OCR','Số CCCD nhận dạng']
        match=re.search(r'(?<!\d)\d{12}(?!\d)',content)
        rows=[{'Văn bản OCR':content,'Số CCCD nhận dạng':match.group(0) if match else ''}]
    else:raise ValueError('Định dạng hỗ trợ: XLSX, CSV, PDF, PNG, JPG, WEBP.')
    return {'headers':headers,'preview':rows[:10],'rows':rows,'mapping':automap(headers),'count':len(rows)}
