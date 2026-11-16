# Ký và xác minh bản cập nhật QLKP

Công cụ chạy thủ công, ngoại tuyến với Python 3.8 trở lên và OpenSSL 3 trên PATH. Lệnh xác minh kiểm tra chữ ký Ed25519, SHA-256 của bộ cài, tên tệp, nền tảng và phiên bản tối thiểu. Người vận hành tự chạy bộ cài sau khi xác minh.

## Khởi tạo trên máy phát hành

`python tools/qlkp_update.py init QLKP-Update-Key`

Lưu `update-private.pem` đã mã hóa trong thư mục cá nhân được bảo vệ, sao lưu riêng và giữ mật khẩu tách biệt. Bàn giao `update-public.pem` và lưu dấu vân tay SHA-256 của khóa công khai qua kênh tin cậy trước khi nhận bản cập nhật đầu tiên. Khi đổi khóa, đối chiếu lại dấu vân tay mới qua kênh đã xác thực.

## Ký và đối chiếu

```text
python tools/qlkp_update.py sign --target windows-x64 --version 0.21.5 --key QLKP-Update-Key/update-private.pem --package QLKP-PC-0.21.5.exe --manifest update-win.json
python tools/qlkp_update.py verify --target windows-x64 --installed 0.21.4 --key update-public.pem --fingerprint SHA256_KHOA_CONG_KHAI --package QLKP-PC-0.21.5.exe --manifest update-win.json
```

Gửi bộ cài, manifest và tệp `.json.sig` cùng nhau. Dùng `android-test` cho APK thử nghiệm và `android` cho APK phát hành. Kiểm tra phiên bản thực và mã ứng dụng trước khi ký manifest. Trên Android, bản cài đè còn phải có mã ứng dụng và chứng thư ký phù hợp với bản đã cài. Trên Windows, ký manifest và ký Authenticode là hai lớp xác thực riêng.
