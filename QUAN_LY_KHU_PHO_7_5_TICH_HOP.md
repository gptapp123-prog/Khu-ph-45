# Quản lý Khu phố 7.5 — nhánh tích hợp Codex

Ngày tạo nhánh: 27/10/2026

## Phạm vi đã kiểm chứng cục bộ

- Kế thừa gói bàn giao 7.3/7.4, không xây lại từ đầu.
- Khung giao diện 7.5 và bảng trạng thái F-001–F-031.
- Menu phân nhóm: Hệ thống, Dữ liệu dân cư, Quản trị, Phân hệ mở rộng.
- Các phân hệ chưa có backend được khóa, không tạo nút giả.
- 26/26 kiểm thử pytest đạt trên Linux/Python 3.12.14.
- Kiểm tra cú pháp app.js và framework.js đạt.

## Giới hạn bắt buộc ghi nhận

- Chưa có EXE/MSI và APK ký.
- Chưa kiểm thử Windows/Android thật.
- Design QA đang blocked vì trình duyệt Work không kết nối được preview terminal.local:4173.
- Không dùng dữ liệu dân cư thật.
- PC là nơi duy nhất lưu bền vững dữ liệu; mobile không có kho dữ liệu riêng.

## Bước tích hợp kế tiếp

1. Đưa mã nguồn 7.5 vào thư mục app/ trên nhánh này.
2. Bổ sung CI pytest + kiểm tra JavaScript.
3. Chạy E2E desktop/mobile và lưu ảnh bằng chứng.
4. Chỉ merge khi kiểm tra cấu trúc repository hiện có và không phá bộ E2E.
