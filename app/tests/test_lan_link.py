from datetime import datetime,timedelta,timezone
from ipaddress import ip_address
from cryptography import x509
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from server.lan_link import configure,report

def test_lan_link_requires_tls_san_and_live_address(tmp_path):
    key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
    cert=(x509.CertificateBuilder().subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'PC')]))
          .issuer_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'PC')]))
          .public_key(key.public_key()).serial_number(x509.random_serial_number())
          .not_valid_before(datetime.now(timezone.utc)-timedelta(days=1))
          .not_valid_after(datetime.now(timezone.utc)+timedelta(days=1))
          .add_extension(x509.SubjectAlternativeName([x509.IPAddress(ip_address('192.168.1.10'))]),critical=False)
          .sign(key,hashes.SHA256()))
    path=tmp_path/'cert.pem';path.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    try:
        configure('0.0.0.0',8765,path)
        assert report(['192.168.1.10','192.168.1.11'])['urls']==['https://192.168.1.10:8765']
        assert report(['192.168.1.11'])['urls']==[]
        configure('127.0.0.1',8765,path)
        assert report(['192.168.1.10'])['urls']==[]
    finally:configure('127.0.0.1',8765,None)
