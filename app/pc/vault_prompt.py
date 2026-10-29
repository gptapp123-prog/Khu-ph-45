"""First-run password selection without touching the encrypted vault."""
from pathlib import Path
from getpass import getpass


def request_vault_password(path, prompt=None):
    prompt = prompt or getpass
    target = Path(path)
    if target.exists():
        print('MỞ KHO DỮ LIỆU ĐÃ CÓ: ' + str(target))
        return prompt('Nhập mật khẩu đã đặt cho kho: ')
    print('TẠO KHO DỮ LIỆU MỚI: ' + str(target))
    print('Hãy tự đặt và lưu lại mật khẩu. Không có mật khẩu mặc định.')
    password = prompt('Đặt mật khẩu kho mới (ít nhất 12 ký tự): ')
    if len(password) < 12:
        raise ValueError('Mật khẩu phải có ít nhất 12 ký tự. Chưa tạo kho.')
    if password != prompt('Nhập lại mật khẩu kho mới: '):
        raise ValueError('Hai lần nhập không khớp. Chưa tạo kho.')
    return password
