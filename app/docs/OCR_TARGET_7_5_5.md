# OCR/QR theo bảng trường vào file Excel đích

1. Quản trị trên PC chọn XLSX nguồn có tiêu đề ở hàng 1, bấm **Quét tiêu đề**, chọn sheet và các cột, rồi bấm **▦ Tạo bảng lệnh từ cột đã chọn**. App sinh từng dòng bảng lệnh theo đúng thứ tự cột đã chọn. Người dùng đánh dấu **Cần quét** ở cột muốn OCR/QR nhập dữ liệu (ví dụ 4 trong 10 cột), chỉnh nhãn/bí danh/kiểu/độ dài mã rồi lưu bảng. Có thể thêm cột thủ công.
2. Chọn ảnh/PDF cần quét. OCR chữ và QR chạy cục bộ, chỉ trích các cột đánh dấu Cần quét. Mỗi tệp hiển thị các cột trong bảng lệnh, giá trị từ OCR và QR, ảnh/văn bản gốc. Các cột không đánh dấu và giá trị xung đột để trống. Người dùng sửa, chọn nguồn và xác nhận từng dòng.
3. Bảng lệnh tạo XLSX mới với **tất cả cột đã chọn** theo đúng thứ tự; chỉ cột đánh dấu Cần quét có giá trị. Có thể chọn XLSX có sẵn để thêm dòng. Khi chọn, app đối chiếu hàng 1 của đúng một sheet hiển thị với **tên và thứ tự cột** của bảng lệnh; không khớp thì từ chối. App tải về **bản XLSX mới**, không ghi đè file nguồn hay tự nhập kho hồ sơ.
4. Trường chưa đọc rõ để trống. Giá trị xuất được chống diễn giải thành công thức Excel. Giới hạn 10 MB/file đích, 20 dòng được xác nhận/lượt; sheet có ô gộp hoặc nhiều sheet cùng hàng tiêu đề bị từ chối để tránh ghi sai.

Việc đọc đúng phụ thuộc chất lượng ảnh và cấu trúc tài liệu. Chưa nghiệm thu tỷ lệ chính xác trên bộ ảnh thực đa dạng hoặc trên Windows thật.
