"""Extract configured labelled values without reducing identifiers to digits."""
import re
from .parsers import strip_accents


def normalized(value):
    return re.sub(r'\s+', ' ', strip_accents(value).replace('đ','d').casefold()).strip()


COMMON_LABELS=('họ và tên','họ tên','ngày sinh','năm sinh','giới tính','nơi sinh',
               'học sinh trường','trường','tại hội đồng thi','hội đồng thi',
               'xếp loại tốt nghiệp','xếp loại','số hiệu bằng','vào sổ cấp bằng số',
               'nghề nghiệp','địa chỉ','quê quán','thường trú','cccd')
SEPARATORS=':：-–—'


def _prefix(line,alias):
    """Match a heading and retain its untouched original suffix."""
    for end in range(1,min(len(line),len(alias)*3+12)+1):
        if normalized(line[:end])==alias:
            remainder=line[end:].lstrip()
            if not remainder or remainder[0] in SEPARATORS+'.…':return remainder
    return None


def _after_heading(remainder):
    if not remainder:return ''
    if remainder[0] in SEPARATORS:remainder=remainder[1:]
    return re.sub(r'^[\s.…·_,;:|]+','',remainder).strip()


def _split_next_label(value,labels):
    for start in range(1,len(value)):
        if not value[start].isspace():continue
        tail=value[start:].lstrip()
        for label in labels:
            rest=_prefix(tail,label)
            if rest is not None and rest and rest[0] in SEPARATORS:
                return value[:start].rstrip(' .…·_')
    return value


def _label_positions(line,aliases):
    """A printed form can contain two headings on one OCR line."""
    for start in range(len(line)):
        if start and not line[start-1].isspace():continue
        fragment=line[start:]
        for alias in aliases:
            remainder=_prefix(fragment,alias)
            if remainder is not None:
                yield remainder
                break


def extract_configured(text,fields):
    lines=[line.strip() for line in text.splitlines() if line.strip()]
    labels={normalized(a) for a in COMMON_LABELS}
    labels.update(normalized(a) for field in fields for a in [field['label'],*field['aliases']] if a.strip())
    labels=sorted(labels,key=len,reverse=True)
    result={};warnings=[]
    for field in fields:
        matches=[]
        aliases=sorted({normalized(a) for a in [field['label'],*field['aliases']] if a.strip()},key=len,reverse=True)
        for index,line in enumerate(lines):
            for remainder in _label_positions(line,aliases):
                value=_after_heading(remainder)
                if not value and index+1<len(lines):
                    following=lines[index+1].strip()
                    if len(following)>=3 and not following.startswith('[') and not any(_label_positions(following,labels)):value=following
                value=_split_next_label(value,labels).strip()
                if field['type']=='year':
                    years=re.findall(r'(?<!\d)(?:18|19|20)\d{2}(?!\d)',value)
                    value=years[0] if len(set(years))==1 else ''
                if value:matches.append(value[:500])
        unique=list(dict.fromkeys(matches));column=field['column']
        result[column]=unique[0] if len(unique)==1 else ''
        if len(unique)>1:warnings.append(f'{field["label"]}: có nhiều giá trị khác nhau; cần đối chiếu thủ công.')
        elif not unique:warnings.append(f'{field["label"]}: không tìm thấy giá trị rõ ràng.')
    return result,warnings
