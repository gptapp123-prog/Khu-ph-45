# QUẢN LÝ KHU PHỐ MỚI — NHÁNH PHÁT TRIỂN 7.5

**Ngày:** 27/10/2026  
**Phạm vi:** ứng dụng Quản lý Khu phố mới; hồ sơ 7.2–7.4 chỉ là nguồn kế thừa kỹ thuật, không phải ứng dụng khác.  
**Trạng thái:** mã nguồn + script cài đặt thử; **CHƯA phải bộ cài Windows EXE/MSI hoặc APK ký phát hành.**

## 1. Đã làm được trong lượt này

1. Tách hai nhánh runtime: Windows 7 SP1/8 dùng Python 3.8 (**legacy, chưa nghiệm thu**); Windows 8.1/10/11 dùng Python 3.11–3.15 theo chính sách kiểm tra runtime hiện tại.
2. Sửa `Cai_dat_PC.bat` chọn file thư viện theo phiên bản Windows/Python, cài chương trình vào `%LOCALAPPDATA%\QuanLyKhuPho\Program` (tự tạo thư mục), còn dữ liệu luôn ở `%LOCALAPPDATA%\QuanLyKhuPho\Data`; có thể chỉ định thư mục chương trình bằng tham số thứ nhất.
3. Kho dữ liệu `KPHO2` mã hóa AES-256-GCM vẫn **chỉ ghi trên đĩa PC**, dùng API `sqlite3.Connection.iterdump`/`executescript` tương thích với Python 3.8; không phát sinh SQLite dữ liệu rõ trên đĩa. Kho `KPHO1` của 7.2 tự chuyển đổi trên runtime mới có `deserialize()` rồi ghi an toàn sang KPHO2.
4. Sửa mã nguồn phụ thuộc Pydantic 2 để tương thích Pydantic 1 ở nhánh cũ; bỏ `str.removeprefix()` (Python 3.9+). Giữ logic xác thực/ma trận 16 quyền/11 trường, phạm vi tổ, duyệt mobile của 7.2.
5. Giữ biểu tượng khiên quấn xích vàng (`pc/web/icon.svg`, `pc/web/QuanLyKhuPho.ico`, `android/.../app_icon.png`).
6. Backup/restore KPHO2 có checksum SHA-256, kiểm tra AES-GCM + SQLite, rollback và thay tệp atomic.
7. Nhập CSV kiểm tra tiêu đề trùng/rỗng; xuất CSV/XLSX vô hiệu hóa công thức nguy hiểm nhưng JSON giữ nguyên văn bản.
8. Excel nhiều sheet cùng cấu trúc được gộp; sheet lệch tiêu đề, công thức, ô gộp, tiêu đề đặt sai dòng và ánh xạ đích trùng bị từ chối; sheet ẩn không được tự động nhập.
9. Hồ sơ không có CCCD được cảnh báo trùng tiềm năng khi cùng họ tên chuẩn hóa, ngày sinh và tổ; hệ thống không tự gộp hoặc xóa hồ sơ.
10. Trường tùy biến được phân quyền mặc định an toàn, kiểm tra kiểu dữ liệu, lưu theo hồ sơ, tự ánh xạ khi nhập CSV/Excel và xuất được qua XLSX/CSV/JSON; người xem mặc định không thấy trường mới.
11. **39 bài kiểm thử đạt** trong môi trường phát triển.

## 2. Tồn tại cần giải quyết

- **Chưa có kết quả chạy thực tế trên Windows 7 SP1** (x86/x64); phiên bản cũ của OS/Python/thư viện hết bảo trì bảo mật, không được coi an toàn để nạp dữ liệu dân cư thật.
- **Chế độ Windows 7/8 chỉ cho khởi chạy nội bộ `127.0.0.1`**, chưa cho phép PC Windows 7 mở mạng LAN cho mobile vì chưa nghiệm thu TLS, firewall và hệ điều hành cũ. Windows 10/11 vẫn có đường LAN HTTPS khi cấu hình chứng chỉ.
- Chưa có bộ thư viện offline hoàn chỉnh; script `.bat` cần Python được cài trước và lần đầu có thể cần Internet để tải thư viện. Không có tệp EXE/MSI/APK đã ký.
- Chưa kiểm thử GUI end-to-end trên Windows, mobile trên thiết bị thật, nâng cấp với kho thực, tải cao và mất điện.
- Định dạng `KPHO2` dạng SQL dump cần đánh giá hiệu suất với kho dữ liệu lớn trước khi triển khai; luôn sao lưu **trên PC** trước khi nâng cấp kho 7.2.

## 3. Chưa thực hiện / kế hoạch lượt tiếp theo

1. Nghiệm thu ma trận OS trên máy Windows 7 SP1, Windows 8.1, Windows 10 và Windows 11; ghi rõ x86/x64, cài thư viện, khởi động, hồ sơ tiếng Việt và đường dẫn dữ liệu.
2. Hoàn thiện bộ cài EXE/MSI với trình kiểm tra hệ điều hành, thư viện offline và xử lý nâng cấp không mất dữ liệu.
3. Test UI thực và kết nối HTTPS LAN trên Windows được hỗ trợ; kiểm thử Android, ký APK.
4. Gia cố xác thực, lưu trữ, chứng chỉ, chứng thực thiết bị, khôi phục/rollback. Chỉ khi nghiệm thu mới dùng dữ liệu dân cư thật.

## Hướng dẫn cài chạy mã nguồn thử

**Windows 10/11:** cài Python 3.11–3.15, giải nén vào thư mục ổ cứng PC **không đồng bộ cloud**, chạy `Cai_dat_PC.bat`, sau đó chạy `%LOCALAPPDATA%\QuanLyKhuPho\Program\Khoi_dong_PC.bat`.

**Windows 7 SP1:** cài Python 3.8, chạy `Cai_dat_PC.bat`. Đây là **nhánh tương thích để thử nghiệm trên máy cô lập**, chỉ sử dụng giao diện PC nội bộ, không đưa lên mạng và không nạp dữ liệu nhạy cảm. Nếu nhận kho cũ `KPHO1`, cần chuyển đổi trên một PC hiện đại trước; không tự ý ghi đè dữ liệu nguồn.

**Đổi đường dẫn cài chương trình** (ví dụ ổ D): `Cai_dat_PC.bat "D:\UngDung\QuanLyKhuPho"` — dữ liệu vẫn ở đường dẫn PC cục bộ mặc định.

**Kiểm thử trên máy phát triển:**

```bash
python -m pip install -r requirements.txt -r requirements-dev.txt
PYTHONPATH=pc python -m pytest -q tests/test_foundation_accounts.py tests/test_stage2_permissions.py tests/test_stage3_windows.py
node --check pc/web/app.js
```

Chi tiết xem: `docs/GIAI_DOAN_3_TUONG_THICH_WINDOWS.md`.
