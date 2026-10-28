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

OCR_LABELS={'full_name':('ho va ten','ho ten','full name'),
            'dob':('ngay thang nam sinh','ngay sinh','date of birth'),
            'gender':('gioi tinh','sex'),
            'hometown':('que quan','place of origin'),
            'permanent_address':('noi thuong tru','thuong tru','place of residence'),
            'cccd':('so can cuoc','so dinh danh ca nhan','so cccd','cccd','identity number')}
OCR_HEADERS={'full_name':'Họ tên OCR','dob':'Ngày sinh OCR','gender':'Giới tính OCR',
             'hometown':'Quê quán OCR','permanent_address':'Thường trú OCR','cccd':'Số CCCD nhận dạng'}

def extract_ocr_fields(content):
    """Extract only clearly labelled values; ambiguous values require review."""
    found={k:[] for k in OCR_HEADERS}
    lines=content.splitlines()
    for index,line in enumerate(lines):
        line=line.strip()
        if not line:continue
        normalized=strip_accents(line)
        matches=[]
        for field,labels in OCR_LABELS.items():
            for label in labels:
                match=re.match(r'^'+re.escape(label)+r'\s*[:：-]\s*(.*)$',normalized,re.I)
                if match:
                    value=line[match.start(1):].strip()
                    if not value and index+1<len(lines):
                        following=lines[index+1].strip()
                        next_label=strip_accents(following).split(':',1)[0].strip()
                        if next_label not in {name for group in OCR_LABELS.values() for name in group}:
                            value=following
                    matches.append((field,value))
                    break
        if len(matches)==1:found[matches[0][0]].append(matches[0][1])
    candidates=set(re.findall(r'(?<!\d)\d{12}(?!\d)',content))
    result={};warnings=[]
    for field,values in found.items():
        unique={v.strip() for v in values if v.strip()}
        if field=='cccd':
            unique={v for value in unique for v in re.findall(r'(?<!\d)\d{12}(?!\d)',value)}
            if len(candidates)>1:unique=set()
            if not unique and len(candidates)==1:unique=candidates
        if field=='dob':
            unique={v for v in unique if re.fullmatch(r'\d{2}[-/]\d{2}[-/]\d{4}',v)}
        if len(unique)==1:result[field]=next(iter(unique))
        elif len(unique)>1:warnings.append('Có nhiều giá trị cho '+OCR_HEADERS[field]+'; cần đối chiếu ảnh.')
    if len(candidates)>1:warnings.append('Có nhiều số 12 chữ số; không tự chọn CCCD.')
    return result,warnings

def review_result(texts,source):
    content='\n'.join(texts)
    extracted,warnings=extract_ocr_fields(content)
    headers=list(OCR_HEADERS.values())
    rows=[{label:extracted.get(field,'') for field,label in OCR_HEADERS.items()}]
    result={'headers':headers,'preview':rows,'rows':rows,'count':1,
            'mapping':{label:field for field,label in OCR_HEADERS.items() if extracted.get(field)},
            'ocr_review':True,'raw_text':content[:12000],
            'warnings':['Nội dung '+source+' chỉ là gợi ý. Đối chiếu bản gốc và chọn từng trường trước khi ghi.']+warnings}
    if len(texts)>1:result['warnings'].append('Tài liệu nhiều trang: kiểm tra thông tin thuộc cùng một người trước khi nhập.')
    return result

def parse_file(raw:bytes,filename:str):
    if len(raw)>10*1024*1024:raise ValueError('Giới hạn tệp nhập: 10 MB.')
    ext=filename.lower().rsplit('.',1)[-1] if '.' in filename else ''
    rows=[]
    if ext in ('xlsx','xlsm'):
        # Normal worksheet mode is intentional: read-only worksheets do not expose
        # merged-cell ranges, while rejecting merged input is required to prevent
        # columns from being shifted into the wrong resident fields. The upload is
        # already capped at 10 MB and row ingestion at 5,000.
        wb=load_workbook(io.BytesIO(raw),read_only=False,data_only=False)
        headers=[]
        try:
            for sh in wb.worksheets:
                # Hidden sheets are commonly lookup/configuration data. Importing them
                # silently can disclose or duplicate records, so only visible sheets
                # participate in a resident import.
                if sh.sheet_state!='visible':continue
                if sh.merged_cells.ranges:
                    raise ValueError(f'Sheet "{sh.title}" có ô gộp. Hãy bỏ gộp ô trước khi nhập.')
                it=sh.iter_rows()
                first=next(it,None)
                if not first:continue
                if not any(c.value is not None and str(c.value).strip() for c in first):
                    # Do not guess which later row is the header: that can map data to
                    # the wrong fields. Empty visible sheets are ignored; populated
                    # sheets must put the header on row 1.
                    if any(any(c.value is not None and str(c.value).strip() for c in row)
                           for row in it):
                        raise ValueError(f'Sheet "{sh.title}" phải đặt tiêu đề ở dòng đầu tiên.')
                    continue
                if any(c.data_type=='f' for c in first):raise ValueError('Tiêu đề Excel không được chứa công thức.')
                if any(c.value is None or not str(c.value).strip() for c in first):
                    raise ValueError(f'Sheet "{sh.title}" có cột không có tiêu đề.')
                sheet_headers=validate_headers([str(c.value).strip() if c.value is not None else f'Cột {i+1}' for i,c in enumerate(first)])
                if headers and [strip_accents(x) for x in sheet_headers]!=[strip_accents(x) for x in headers]:
                    raise ValueError('Các sheet Excel phải có cùng tiêu đề và thứ tự cột.')
                if not headers:headers=sheet_headers
                for cells in it:
                    if any(c.data_type=='f' for c in cells):raise ValueError('Excel chứa công thức. Hãy chuyển công thức thành giá trị trước khi nhập.')
                    values=[c.value for c in cells]
                    if any(v is not None and str(v).strip() for v in values):
                        if any(v is not None and str(v).strip() for v in values[len(headers):]):
                            raise ValueError(f'Sheet "{sh.title}" có dữ liệu ngoài các cột tiêu đề.')
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
            if None in row:raise ValueError('CSV có dòng nhiều cột hơn tiêu đề.')
            rows.append({str(k or ''):str(v or '') for k,v in row.items()})
    elif ext=='pdf':
        pdf=PdfReader(io.BytesIO(raw))
        if len(pdf.pages)>50:raise ValueError('PDF tối đa 50 trang/lần nhập.')
        texts=[]
        for page in pdf.pages:
            content=(page.extract_text() or '').strip()
            if not content:
                import pytesseract
                images=list(page.images)
                if not images:raise ValueError('Trang PDF scan không có ảnh đọc được; hãy chuyển sang PNG/JPG.')
                if len(images)>10:raise ValueError('Trang PDF có quá 10 ảnh; hãy tách tài liệu để tránh bỏ sót.')
                page_text=[]
                for item in images[:10]:
                    with Image.open(io.BytesIO(item.data)) as img:
                        if img.width*img.height>20_000_000:raise ValueError('Ảnh trong PDF quá lớn.')
                        page_text.append(pytesseract.image_to_string(img,lang='vie+eng'))
                content='\n'.join(page_text)
            texts.append(content)
        if not any(x.strip() for x in texts):raise ValueError('Không đọc được nội dung PDF.')
        return review_result(texts,'PDF')
    elif ext in ('png','jpg','jpeg','webp'):
        import pytesseract
        with Image.open(io.BytesIO(raw)) as img:
            if img.width*img.height>20_000_000:raise ValueError('Ảnh quá lớn.')
            content=pytesseract.image_to_string(img,lang='vie+eng')
        return review_result([content],'OCR ảnh')
    else:raise ValueError('Định dạng hỗ trợ: XLSX, CSV, PDF, PNG, JPG, WEBP.')
    result={'headers':headers,'preview':rows[:10],'rows':rows,'mapping':automap(headers),'count':len(rows)}
    return result
