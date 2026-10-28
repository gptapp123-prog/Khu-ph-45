# OCR và QR 7.5.4 — trích theo bảng trường

- Ở PC chính, tạo bảng trường và cột Excel đích. Nếu muốn lưu nguyên văn QR không có nhãn, thêm trường `Nội dung QR`.
- Chọn ảnh PNG/JPG/WEBP hoặc PDF scan, nhấn Đọc tệp. QR được giải mã cục bộ bằng zxing-cpp, OCR chữ vẫn xử lý độc lập. Nội dung gốc của từng QR và số trang được hiển thị để đối chiếu; ứng dụng không truy cập URL nằm trong QR.
- Chỉ khóa JSON đơn giản hoặc dòng `Nhãn: giá trị` khớp nhãn/bí danh đã cấu hình mới được gợi ý vào cột. Chuỗi QR không nhãn như dãy phân tách `|` không bị suy đoán thành dữ liệu cá nhân. Nếu nhiều QR cho một trường cho giá trị khác nhau, trường để trống.
- Giao diện hiển thị OCR, QR và ô giá trị cuối. Hai nguồn xung đột thì ô cuối để trống; người dùng chọn nguồn hoặc sửa thủ công và xác nhận từng tệp trước khi xuất XLSX mới. Không ghi vào file nguồn hoặc tự nhập hồ sơ.
- Giới hạn: 10 MB/tệp, 20 triệu điểm ảnh, PDF tối đa 50 trang, tối đa 10 QR/tệp và 20 tệp/lượt. QR quá mờ, hỏng hoặc có cấu trúc không gắn nhãn cần nhập thủ công; chưa nhận dạng dãy trường theo vị trí đặc thù của từng nhà phát hành.
