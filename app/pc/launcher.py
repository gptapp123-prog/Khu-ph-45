"""Launcher for PC host. No networking beyond this PC unless explicitly enabled."""
import argparse,os,sys,getpass,ipaddress
from pathlib import Path
from server.app import configure_vault,app
import uvicorn
from win_compat import assess

def main():
    # A frozen console program can inherit cp1252 from Windows shells even
    # when the application uses Vietnamese UI and help text.
    if os.name=='nt':
        for stream in (sys.stdout,sys.stderr):
            if hasattr(stream,'reconfigure'):
                stream.reconfigure(encoding='utf-8',errors='replace')
    compatibility=assess()
    if not compatibility['ok']:
        print(compatibility['message'],file=sys.stderr)
        sys.exit(3)
    ap=argparse.ArgumentParser(description='Quản lý Khu phố — PC máy chủ dữ liệu')
    data_dir=Path(os.environ.get('LOCALAPPDATA',str(Path.home()))) / 'QuanLyKhuPho' / 'Data'
    ap.add_argument('--data',default=str(data_dir/'quanlykp.vault'))
    ap.add_argument('--host',default='127.0.0.1',help='Chỉ dùng 0.0.0.0 khi đã cấu hình HTTPS và mạng LAN an toàn.')
    ap.add_argument('--port',type=int,default=8765)
    ap.add_argument('--cert',help='PEM chứng chỉ TLS máy chủ, cấu hình cho ứng dụng mobile')
    ap.add_argument('--key',help='PEM khóa riêng TLS')
    args=ap.parse_args()
    if compatibility['track']=='legacy' and args.host not in ('127.0.0.1','::1','localhost'):
        ap.error('Windows 7/8: không được bật LAN trong giai đoạn chưa nghiệm thu bảo mật. Chỉ chạy nội bộ trên PC.')
    if args.host not in ('127.0.0.1','::1','localhost') and not (args.cert and args.key):
        ap.error('Cấm mở LAN bằng HTTP. Cần chỉ định --cert và --key để kích hoạt HTTPS.')
    if (args.cert and not args.key) or (args.key and not args.cert):ap.error('Phải có cả chứng chỉ và khóa riêng.')
    print('QUẢN LÝ KHU PHỐ — PC CHÍNH; dữ liệu lưu trên PC; không có cloud.')
    pw=getpass.getpass('Mật khẩu mở kho dữ liệu PC (>=12 ký tự): ')
    try:configure_vault(args.data,pw)
    except Exception as e:
        print('Không mở được kho dữ liệu:',e,file=sys.stderr);sys.exit(2)
    proto='https' if args.cert else 'http'
    print(f'Giao diện PC: {proto}://127.0.0.1:{args.port}')
    print('Giữ cửa sổ này mở khi dùng ứng dụng. Nhấn Ctrl+C để dừng.')
    uvicorn.run(app,host=args.host,port=args.port,ssl_certfile=args.cert,ssl_keyfile=args.key,access_log=False,server_header=False)

if __name__=='__main__':main()
