# Quản lý Khu phố 7.5 — mã nguồn tích hợp

Mã ứng dụng PC và Android nằm trong `app/`. Đây là app mới kế thừa kỹ thuật từ gói bàn giao, độc lập với cổng public ở thư mục gốc.

## Tính năng đã triển khai trong mã

- Bảng 31 chức năng, menu PC/mobile, quản lý hồ sơ và kho KPHO2 mã hóa trên PC chính.
- Bảng lệnh OCR: quét tiêu đề hàng đầu của XLSX, chọn cột, tạo bảng lệnh, đánh dấu cột cần OCR/QR, cấu hình nhãn/bí danh/kiểu và độ dài mã.
- OCR ảnh chụp, PDF scan/văn bản, QR trong ảnh/PDF; hiển thị nguồn OCR/QR và yêu cầu đối chiếu khi xung đột. Chỉ cột được đánh dấu mới nhận giá trị; các cột khác trong file để trống.
- Xuất XLSX mới hoặc thêm dòng vào bản sao của XLSX đã chọn sau khi kiểm tra đúng tiêu đề và thứ tự cột. Không ghi đè file nguồn.

## Kiểm chứng và giới hạn

- CI trên nhánh PR #14: 90 bài Python, kiểm tra cú pháp JavaScript và E2E trình duyệt PC/mobile đạt ở run 40692473086.
- Chưa nghiệm thu tỷ lệ OCR trên bộ tài liệu thực đa dạng; trường mờ/chữ viết tay cần đối chiếu. Chưa kiểm thử trên PC Windows/Android thật, chưa có EXE/MSI/APK ký.
- Chỉ dùng dữ liệu tổng hợp trong kiểm thử. Không đưa ảnh giấy tờ hoặc dữ liệu cá nhân thật vào repository.
