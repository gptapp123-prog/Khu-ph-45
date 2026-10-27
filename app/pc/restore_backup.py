"""Công cụ khôi phục offline; phải dừng máy chủ trước khi chạy."""
import argparse,getpass,sys
from pathlib import Path
from server.crypto import Vault

def main():
    parser=argparse.ArgumentParser(description='Khôi phục bản sao lưu mã hóa Quản lý Khu phố');parser.add_argument('--data',required=True);parser.add_argument('--backup',required=True);args=parser.parse_args()
    data=Path(args.data);vault=None;print('CẢNH BÁO: Chỉ chạy khi ứng dụng/máy chủ đã dừng hoàn toàn.')
    if input('Nhập KHOI PHUC để tiếp tục: ').strip()!='KHOI PHUC':print('Đã hủy; chưa thay đổi dữ liệu.');return 1
    try:
        vault=Vault(data,getpass.getpass('Mật khẩu kho dữ liệu PC: '));rollback=vault.restore(Path(args.backup),data.parent/'backups');print('Khôi phục thành công. Điểm rollback:',rollback);return 0
    except Exception as exc:print('Khôi phục thất bại:',exc,file=sys.stderr);return 2
    finally:
        if vault is not None:vault.conn.close()

if __name__=='__main__':sys.exit(main())
