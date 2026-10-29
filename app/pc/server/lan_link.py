"""Report only LAN URLs actually covered by the configured TLS certificate."""
import ipaddress
import socket
from datetime import datetime,timezone
from pathlib import Path
from cryptography import x509

CONFIG={'host':'127.0.0.1','port':8765,'cert':None}

def configure(host,port,cert):
    CONFIG.update(host=host,port=port,cert=str(cert) if cert else None)

def local_addresses():
    found=set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(),None,family=socket.AF_INET):
            found.add(info[4][0])
    except OSError:pass
    try:
        with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sock:
            sock.connect(('192.0.2.1',9))
            found.add(sock.getsockname()[0])
    except OSError:pass
    return sorted(ip for ip in found if is_lan(ip))

def is_lan(value):
    try:
        ip=ipaddress.ip_address(value)
        return ip.version==4 and (ip in ipaddress.ip_network('10.0.0.0/8') or
            ip in ipaddress.ip_network('172.16.0.0/12') or
            ip in ipaddress.ip_network('192.168.0.0/16'))
    except ValueError:return False

def report(addresses=None):
    host,port,cert=CONFIG['host'],CONFIG['port'],CONFIG['cert']
    if not cert or host in ('127.0.0.1','::1','localhost'):
        return {'enabled':False,'urls':[],'message':'Chưa bật LAN HTTPS trên PC chính.'}
    try:
        parsed=x509.load_pem_x509_certificate(Path(cert).read_bytes())
        if not (parsed.not_valid_before_utc <= datetime.now(timezone.utc) <= parsed.not_valid_after_utc):
            return {'enabled':True,'urls':[],'message':'Chứng chỉ HTTPS đã hết hạn hoặc chưa có hiệu lực.'}
        names=parsed.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
        valid_ips=set(names.get_values_for_type(x509.IPAddress))
        valid_dns=set(names.get_values_for_type(x509.DNSName))
    except (OSError,ValueError,x509.ExtensionNotFound):
        return {'enabled':True,'urls':[],'message':'Chứng chỉ thiếu SAN hợp lệ; chưa thể tạo địa chỉ liên kết.'}
    candidates=addresses if addresses is not None else local_addresses()
    if host!='0.0.0.0':candidates=[ip for ip in candidates if ip==host]
    urls=[f'https://{ip}:{port}' for ip in candidates if is_lan(ip) and ipaddress.ip_address(ip) in valid_ips]
    # Stable local DNS names require an administrator-configured LAN DNS record.
    names=[name.lower() for name in valid_dns if name.lower().endswith(('.lan','.internal','.home.arpa'))]
    resolved=[]
    if host=='0.0.0.0':
        for name in sorted(names):
            try:
                if set(socket.gethostbyname_ex(name)[2]).intersection(candidates):resolved.append(f'https://{name}:{port}')
            except OSError:pass
    urls=resolved+urls
    return {'enabled':True,'urls':urls,'message':('Chỉ dùng tên máy nếu DNS nội bộ đã trỏ đúng PC.' if names and urls else
        'Chứng chỉ chưa khớp IP LAN hiện tại; kiểm tra IP, SAN và cấu hình HTTPS.' if not urls else
        'IP do router cấp có thể đổi; đặt DHCP reservation để giữ địa chỉ này.')}
