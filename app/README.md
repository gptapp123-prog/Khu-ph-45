# QUẢN LÝ KHU PHỐ MỚI — NHÁNH PHÁT TRIỂN 7.5

**Ngày:** 27/10/2026  
**Phạm vi:** ứng dụng Quản lý Khu phố mới; hồ sơ 7.2–7.4 chỉ là nguồn kế thừa kỹ thuật, không phải ứng dụng khác.  
**Trạng thái (28/10/2026):** mã nguồn + script cài đặt thử; có script tạo thư mục gói PC với thư viện offline trên Windows. **CHƯA có EXE/MSI hoặc APK ký phát hành.**

GitHub Actions `Build application installers` đóng gói EXE cài Windows từ PyInstaller + Inno Setup, gồm Tesseract OCR và mô hình Việt/Anh, đồng thời tạo APK Android **debug** để thử nghiệm. Tải từ mục Artifacts của lần chạy workflow. EXE Windows **chưa ký mã**; APK debug dùng khóa thử nghiệm và **không cập nhật được** bản phát hành ký bằng khóa `qlkp-release`. Chỉ coi là gói thử nghiệm cho Windows 10/11 x64 và Android thử nghiệm cho đến khi nghiệm thu trên thiết bị thật. Chưa có cơ chế tự cập nhật hay bộ cài Windows 7.

Khóa cập nhật Android đã tạo và bàn giao riêng ngoài GitHub; Gradle yêu cầu đúng chứng chỉ đã ấn định khi ký bản release. Xem `docs/KHOA_CAP_NHAT_VA_PHAT_HANH.md`. Chưa build APK ký. PC cần chứng thư ký mã riêng khi làm EXE/MSI; không dùng khóa Android thay thế.

## Gói cài PC offline để thử nghiệm

Trên máy Windows mục tiêu đã cài Python phù hợp, chạy `Tao_goi_cai_PC_offline.bat` trong thư mục `app`. Script tải các wheel đúng kiến trúc/Python của **máy đang tạo gói**, kiểm tra khả năng giải phụ thuộc khi ngắt kết nối và tạo `dist\QuanLyKhuPho_PC_offline`. Chép nguyên thư mục đó sang PC cùng nền tảng, chạy `Cai_dat_PC.bat` trong thư mục đã chép. Khi có `wheelhouse`, script cài đặt bắt buộc dùng `--no-index`, nên sẽ báo lỗi nếu thiếu thư viện thay vì âm thầm tải qua mạng. Không dùng gói tạo trên Python/kiến trúc khác cho máy đích.

Script tạo gói chỉ công bố thư mục đích sau khi tải và kiểm tra xong; phần chưa hoàn chỉnh nằm ở `.building` để kiểm tra thủ công. Khi nâng cấp, script cài kiểm tra thư viện trước khi chép mã nguồn mới, sao chép vào `pc.next`, rồi giữ bản nguồn cũ tại `pc.previous`. **Tắt app trước khi nâng cấp.** Nếu `pc.next` hoặc `pc.previous` đã tồn tại, dừng cài đặt để kiểm tra và chuyển bản cũ sang nơi sao lưu trước khi thử lại. Kho dữ liệu trong `Data` không nằm trong các thư mục này. Việc cập nhật thư viện vào môi trường `.venv` hiện chưa bảo đảm rollback nguyên tử nếu pip bị ngắt giữa chừng; cần sao lưu cả thư mục `Program` trước khi nâng cấp bản đang sử dụng.

Tesseract OCR là chương trình độc lập, **không nằm trong wheelhouse**. Cần cài Tesseract trên PC, đưa vào PATH và kiểm tra mô hình tiếng Việt/Anh theo `pc/ocr_models/README.md`; nếu thiếu, app vẫn có thể chạy nhưng không đọc OCR ảnh. Đây chưa phải bộ cài độc lập một cú nhấp. Không đưa kho dữ liệu, ảnh hồ sơ hoặc bản sao dữ liệu thật vào gói.

Lượt kiểm thử mã nguồn gần nhất: **92 bài Python đạt**; chưa có nghiệm thu cài offline trên Windows thật.

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
11. Ngày sinh và trường ngày tùy biến dùng chuẩn hiển thị/nhập mới `DD-MM-YYYY`; dữ liệu ISO hoặc dấu gạch chéo cũ vẫn đọc được và được chuẩn hóa để không phá kho hiện hữu.
12. Giao diện hồ sơ sinh tự động điều khiển cho trường tùy biến theo kiểu văn bản, số, ngày, danh sách và đánh dấu; nhập/xuất cũng nhận các trường này.
13. Trường danh sách bắt buộc có lựa chọn hợp lệ; quản trị viên có thể đổi nhãn, lựa chọn, thứ tự và ẩn/kích hoạt trường mà không xóa dữ liệu hồ sơ đã lưu.
14. Không cho bỏ lựa chọn dropdown đang được hồ sơ sử dụng; giao diện hiển thị số hồ sơ đang dùng và hỗ trợ nút đưa trường lên/xuống mà không xóa giá trị.
15. Công cụ chuyển hàng loạt lựa chọn dropdown bắt buộc xem trước, đối chiếu lại số hồ sơ khi xác nhận, cập nhật nguyên tử và ghi nhật ký; chỉ quản trị gốc/hệ thống trên PC chính được thực hiện.
16. Xem trước chuyển dropdown hiển thị tối đa 50 hồ sơ cụ thể; lịch sử giao dịch lưu danh sách hồ sơ đã đổi và cho hoàn tác khi cấu hình/giá trị chưa bị sửa sau giao dịch.
17. Trung tâm rà soát hồ sơ nghi trùng so sánh từng cặp theo họ tên chuẩn hóa, ngày sinh và tổ; chỉ đưa vào danh sách khi ít nhất một hồ sơ thiếu CCCD, cho đánh dấu không trùng và ghi nhật ký mà không sửa dữ liệu gốc.
18. Gộp hồ sơ nghi trùng có xem trước xung đột, cho chọn giá trị theo từng trường, bổ sung ô trống từ hồ sơ phụ, chuyển CCCD an toàn, xóa mềm hồ sơ phụ, lưu ảnh chụp hai hồ sơ/trường tùy biến và cho hoàn tác khi dữ liệu chưa bị sửa sau gộp.
19. Có công cụ benchmark biệt lập cho CSV gần 10 MB/5.000 dòng, kho KPHO2 lớn, 500 cặp nghi trùng, mở lại kho và kiểm tra toàn vẹn; không dùng dữ liệu dân cư thật.
20. Ghi kho đồng bộ thêm mục thư mục trên POSIX; fault-injection xác nhận lỗi thay thế tệp giữ nguyên kho cũ và lỗi checksum không để lại backup thiếu cặp.
21. **58 bài kiểm thử đạt** trong môi trường phát triển.

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
