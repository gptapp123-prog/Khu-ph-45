"""Launcher for PC host. No networking beyond this PC unless explicitly enabled."""
import argparse,os,sys,getpass,ipaddress
from pathlib import Path
from server.app import configure_vault,app
import uvicorn
from win_compat import assess
from vault_prompt import request_vault_password

def check_install():
    """Exercise the packaged OCR path without opening or creating a vault."""
    from PIL import Image,ImageDraw,ImageFont
    from server.parsers import ocr_image
    canvas=Image.new('RGB',(700,130),'white')
    draw=ImageDraw.Draw(canvas)
    try:font=ImageFont.truetype('arial.ttf',52)
    except OSError:font=ImageFont.load_default()
    draw.text((20,20),'12345 ABC',fill='black',font=font)
    recognized=ocr_image(canvas)
    if not recognized.strip():raise RuntimeError('OCR không đọc được ảnh kiểm tra.')
    print('OK: chương trình, Tesseract và mô hình OCR Việt/Anh đã chạy. Kết quả ảnh thử:',recognized.strip()[:100])

def main():
    # A frozen console program can inherit cp1252 from Windows shells even
    # when the application uses Vietnamese UI and help text.
    if os.name=='nt':
        for stream in (sys.stdout,sys.stderr):
            if hasattr(stream,'reconfigure'):
                stream.reconfigure(encoding='utf-8',errors='replace')
        bundled_ocr=Path(sys.executable).resolve().parent/'tesseract'/'tesseract.exe'
        if getattr(sys,'frozen',False) and bundled_ocr.is_file():
            os.environ['PATH']=str(bundled_ocr.parent)+os.pathsep+os.environ.get('PATH','')
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
    ap.add_argument('--check-install',action='store_true',help='Kiểm tra OCR cục bộ, không mở kho dữ liệu.')
    args=ap.parse_args()
    if args.check_install:
        try:check_install()
        except Exception as exc:
            print('KIỂM TRA CÀI ĐẶT LỖI:',exc,file=sys.stderr)
            if exc.__cause__ is not None:
                print('Chi tiết OCR ảnh thử:',repr(exc.__cause__),file=sys.stderr)
            sys.exit(4)
        return
    if compatibility['track']=='legacy' and args.host not in ('127.0.0.1','::1','localhost'):
        ap.error('Windows 7/8: không được bật LAN trong giai đoạn chưa nghiệm thu bảo mật. Chỉ chạy nội bộ trên PC.')
    if args.host not in ('127.0.0.1','::1','localhost') and not (args.cert and args.key):
        ap.error('Cấm mở LAN bằng HTTP. Cần chỉ định --cert và --key để kích hoạt HTTPS.')
    if (args.cert and not args.key) or (args.key and not args.cert):ap.error('Phải có cả chứng chỉ và khóa riêng.')
    print('QUẢN LÝ KHU PHỐ — PC CHÍNH; dữ liệu lưu trên PC; không có cloud.')
    try:
        pw=request_vault_password(args.data)
        configure_vault(args.data,pw)
    except (EOFError,KeyboardInterrupt):
        print('Đã hủy. Không tạo hoặc thay thế kho dữ liệu.',file=sys.stderr);sys.exit(2)
    except Exception as e:
        print('Không mở được kho dữ liệu:',e,file=sys.stderr);sys.exit(2)
    proto='https' if args.cert else 'http'
    from server.lan_link import configure as configure_lan_link
    configure_lan_link(args.host,args.port,args.cert)
    print(f'Giao diện PC: {proto}://127.0.0.1:{args.port}')
    print('Giữ cửa sổ này mở khi dùng ứng dụng. Nhấn Ctrl+C để dừng.')
    uvicorn.run(app,host=args.host,port=args.port,ssl_certfile=args.cert,ssl_keyfile=args.key,access_log=False,server_header=False)

if __name__=='__main__':main()
