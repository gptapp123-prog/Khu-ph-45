# Đường dẫn trong ứng dụng Quản lý Khu phố mới

Các đường dẫn dưới đây cùng tải giao diện PC/mobile nội bộ. Tải trang không cấp quyền: API vẫn yêu cầu đăng nhập, vai trò, thiết bị và giới hạn PC chính. Khi tải lại trang, phiên chỉ lưu trong RAM nên người dùng đăng nhập lại.

| Đường dẫn | Màn hình hiện tại | Trạng thái |
|---|---|---|
| `/cong-viec` | Thông báo chưa triển khai | Chưa có nghiệp vụ |
| `/to-chuc` | Thông báo chưa triển khai | Chưa có nghiệp vụ |
| `/reports` | Xuất báo cáo | Có, PC và quyền phù hợp |
| `/bao-cao-thang` | Thông báo chưa triển khai | Chưa có tổng hợp tháng |
| `/import` | Nhập dữ liệu và OCR/QR | Có, theo quyền |
| `/tro-ly` | Thông báo chưa triển khai | Chưa có trợ lý |
| `/lich-su` | Nhật ký hành động | Có, theo quyền |
| `/conflicts` | Thông báo và chỉ dẫn | Xung đột chỉ được rà soát ngay trong luồng nhập; chưa có hàng đợi riêng |
| `/sao-luu` | Sao lưu tại PC chính | Có cho quản trị hệ thống trên PC |
| `/tai-khoan` | Quản lý tài khoản | Có, theo quyền |
| `/cai-dat` | Thông báo chưa triển khai | Chưa có màn hình cấu hình đầy đủ |
| `/huong-dan` | Hướng dẫn cơ bản | Có thông tin ngắn; chưa có tài liệu toàn diện |
| `/login` | Đăng nhập | Có |
| `/setup`, `/owner-setup` | Thiết lập lần đầu tại PC | Cùng luồng thiết lập một quản trị gốc nếu kho còn trống; không tạo lại sau khi đã thiết lập |

Đường dẫn sâu không thay thế máy chủ LAN HTTPS, chứng chỉ TLS hoặc địa chỉ PC. Những màn hình chưa triển khai không ghi dữ liệu.
