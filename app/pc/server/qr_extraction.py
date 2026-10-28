"""Local QR decoding and conservative mapping to user-selected OCR columns."""
from __future__ import annotations
import io,json
from PIL import Image,ImageOps
from pypdf import PdfReader
from .ocr_extraction import extract_configured,normalized

RAW_LABELS={'noi dung qr','qr raw','qr content'}

def decode_qr_image(image):
    import zxingcpp
    image=ImageOps.exif_transpose(image)
    if image.width*image.height>20_000_000:raise ValueError('Ảnh quá lớn để đọc QR.')
    # QR is decoded from the original pixels, independently of OCR's
    # contrast/threshold transformations. The library handles rotation.
    return [item.text for item in zxingcpp.read_barcodes(
        image.convert('L'),formats=zxingcpp.BarcodeFormat.QRCode
    ) if item.text]

def decode_qr_file(raw,filename):
    if len(raw)>10*1024*1024:raise ValueError('Giới hạn tệp QR: 10 MB.')
    ext=filename.lower().rsplit('.',1)[-1] if '.' in filename else ''
    codes=[]
    if ext in ('png','jpg','jpeg','webp'):
        with Image.open(io.BytesIO(raw)) as image:
            codes.extend((1,text) for text in decode_qr_image(image))
    elif ext=='pdf':
        from pypdfium2 import PdfDocument
        reader=PdfReader(io.BytesIO(raw))
        if len(reader.pages)>50:raise ValueError('PDF tối đa 50 trang/lần nhập.')
        document=PdfDocument(raw)
        try:
            for number in range(len(reader.pages)):
                page=document[number]
                try:
                    width,height=page.get_size()
                    if width*height*4>20_000_000:raise ValueError('Trang PDF quá lớn để đọc QR.')
                    bitmap=page.render(scale=2)
                    try:
                        with bitmap.to_pil() as image:
                            codes.extend((number+1,text) for text in decode_qr_image(image))
                    finally:bitmap.close()
                finally:page.close()
                if len(codes)>10:raise ValueError('Tệp có hơn 10 mã QR; hãy tách tài liệu.')
        finally:document.close()
    else:raise ValueError('QR hỗ trợ PNG, JPG, WEBP hoặc PDF.')
    unique=list(dict.fromkeys(codes))
    if len(unique)>10:raise ValueError('Tệp có hơn 10 mã QR; hãy tách tài liệu.')
    return [{'page':page,'text':text[:4096]} for page,text in unique]

def qr_values(codes,fields):
    """No positional guesses: map only named JSON keys or labelled lines."""
    result={f['column']:'' for f in fields}
    candidates={f['column']:set() for f in fields}
    warnings=[]
    # Different payloads may belong to different people or documents. Never
    # combine their fields into one row, even when the fields do not conflict.
    distinct={code['text'].strip() for code in codes if code['text'].strip()}
    if len(distinct)>1:
        warnings.append('Có nhiều QR khác nội dung; không tự ghép vào một dòng. Xem từng mã QR gốc và nhập sau khi đối chiếu.')
        return result,warnings
    for code in codes:
        payload=code['text']
        try:parsed=json.loads(payload)
        except (ValueError,TypeError):parsed=None
        if isinstance(parsed,dict):
            lines=[]
            for key,value in parsed.items():
                if isinstance(key,str) and len(key)<=100 and not any(c in key for c in ':：\r\n') and isinstance(value,(str,int,float)) and len(str(value))<=500:
                    lines.append(key+': '+str(value).replace('\r',' ').replace('\n',' '))
            source='\n'.join(lines)
        else:source=payload
        selected=[f for f in fields if normalized(f['label']) not in RAW_LABELS]
        values,_=extract_configured(source,selected)
        for column,value in values.items():
            if value:candidates[column].add(value)
        for field in fields:
            if normalized(field['label']) in RAW_LABELS and payload.strip():
                candidates[field['column']].add(payload[:500].strip())
    for field in fields:
        column=field['column'];values=candidates[column]
        if len(values)==1:result[column]=next(iter(values))
        elif len(values)>1:warnings.append(field['label']+': nhiều QR cho giá trị khác nhau; để trống để đối chiếu.')
    if not codes:warnings.append('Không tìm thấy mã QR đọc được trong tệp.')
    elif not any(result.values()):warnings.append('QR không khớp nhãn trường đã chọn; xem nội dung QR gốc và nhập thủ công nếu cần.')
    return result,warnings
