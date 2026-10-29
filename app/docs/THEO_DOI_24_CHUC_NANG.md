# Theo dõi 24 chức năng chưa hoàn thiện

Phạm vi: 20 mục có một phần và 4 mục chưa triển khai trong bảng gốc. F-028 cần quyết định nghiệp vụ được theo dõi riêng, không tính vào 24 mục này. Không coi mở màn hình hoặc kiểm thử giả lập là nghiệm thu toàn bộ nghiệp vụ.

| Mã | Chức năng | Trạng thái nghiệm thu |
|---|---|---|
| F-001 | Đăng nhập và xác thực | Chưa nghiệm thu đầy đủ |
| F-003 | Chặn ảnh màn hình mobile | Chưa nghiệm thu đầy đủ |
| F-005 | Mã hóa ảnh giấy tờ | Chưa nghiệm thu đầy đủ |
| F-007 | Duyệt thiết bị và phiên mobile | Chưa nghiệm thu đầy đủ |
| F-008 | Phát hiện root/emulator/hook | Chưa nghiệm thu đầy đủ |
| F-009 | Thu hồi truy cập mobile | Chưa nghiệm thu đầy đủ |
| F-010 | Sao lưu và khôi phục | Chưa nghiệm thu đầy đủ |
| F-012 | Thiết lập tài khoản | Chưa nghiệm thu đầy đủ |
| F-014 | Quyền theo trường | Chưa nghiệm thu đầy đủ |
| F-016 | Phát hiện cột mới | Chưa nghiệm thu đầy đủ |
| F-017 | Cấu hình bảo mật | Chưa nghiệm thu đầy đủ |
| F-018 | Nạp Excel | Chưa nghiệm thu đầy đủ |
| F-019 | Nạp CSV | Chưa nghiệm thu đầy đủ |
| F-020 | OCR ảnh CCCD | Chưa nghiệm thu đầy đủ |
| F-021 | Nạp PDF | Chưa nghiệm thu đầy đủ |
| F-022 | Phát hiện trường và độ tin cậy | Chưa nghiệm thu đầy đủ |
| F-023 | Quét nhiều ảnh | Chưa nghiệm thu đầy đủ |
| F-024 | Gợi ý và lưu quy tắc lọc | Chưa nghiệm thu đầy đủ |
| F-025 | Chọn trường dữ liệu | Chưa nghiệm thu đầy đủ |
| F-026 | Ánh xạ nguồn sang đích | Chưa nghiệm thu đầy đủ |
| F-027 | Thêm cột hoặc đổi thứ tự | Chưa nghiệm thu đầy đủ |
| F-029 | Bảng dữ liệu dân cư | Chưa nghiệm thu đầy đủ |
| F-030 | Mẫu nhập/xuất | Chưa nghiệm thu đầy đủ |
| F-031 | Xuất chọn trường và dòng | Chưa nghiệm thu đầy đủ |

## Công việc đang xử lý

- F-001: phân biệt tạo kho/mở kho, xác nhận mật khẩu mới (PR #29). Chưa có bộ cài cập nhật.
- F-010: sửa liên kết sao lưu; chưa có màn hình khôi phục hoàn chỉnh.
- F-031: bổ sung chọn từng hồ sơ xuất, chỉ xuất cột được chọn và giới hạn theo quyền/Tổ. Chưa nghiệm thu trình duyệt hoặc bộ cài.

## Điều kiện đóng từng mục

Phải có luồng giao diện thực thi, kiểm thử dữ liệu lưu/đọc lại, quyền truy cập, dữ liệu lỗi và xác minh trong bộ cài. Ghi bằng chứng theo từng mục; không áp dụng kết quả đạt của một mục cho toàn app.
