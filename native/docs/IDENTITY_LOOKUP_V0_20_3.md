# Tra cứu CCCD trên Windows native 0.20.3

Trong I02, cấp riêng **Tra cứu CCCD chính xác** hoặc **Ghi hoặc sửa CCCD** cho tài khoản cần thao tác. Quyền nghiệp vụ mặc định không tự cấp hai mục này. Người ghi cũng cần quyền B02 đọc và cập nhật. Hãy đăng nhập lại sau khi cấp quyền.

- Trong B02, chọn đúng nhân khẩu, bấm **Ghi hoặc sửa CCCD**, nhập 12 chữ số và xác nhận hồ sơ. Hộp thoại chỉ hiện bốn số cuối của giá trị đã lưu. Xung đột trong cùng khu phố và phiên bản hồ sơ cũ bị chặn.
- Bấm **Tra cứu CCCD**, nhập đủ số và mở hồ sơ khớp trong khu phố đang làm việc. Giữ số 0 ở đầu. Chức năng tra cứu dữ liệu đã nhập tại PC, chưa xác minh qua cơ sở dữ liệu bên ngoài.
- Số được lưu trong kho mã hóa hiện có, ngoài danh sách nhân khẩu. Nhật ký ghi thao tác và ID hồ sơ, không ghi số CCCD. Gói danh mục Android không mang dữ liệu này. Sao lưu toàn bộ kho vẫn bao gồm dữ liệu CCCD trong phần mã hóa.
- Hợp nhất kho có nhóm định danh và yêu cầu thêm quyền ghi. Nếu trùng hoặc xung đột, hủy toàn bộ lô. Lược đồ 14 tạo bảng trống trên kho cũ. Tạo bản sao trước khi nâng cấp.
- Chưa nối nhập CCCD hàng loạt, OCR, quét căn cước hoặc tra cứu trên Android. Phương tiện và PC–Android hai chiều thuộc phạm vi khác.

## Kiểm thử

Windows 42753488891 đạt 83/83 bước self-test trước và sau cài, 258/258 bước WPF và 4/4 bước cài/chạy/gỡ. Ca riêng kiểm tra tra cứu đúng khu phố, giữ số 0 đầu, phiên cũ sau thu hồi quyền và mở đúng nhân khẩu bằng nút WPF. Đã so mã băm bộ cài và rà 18 tệp log bằng số thử. Bộ cài chưa ký nhà phát hành. Chưa thử Windows 7 hoặc kho dữ liệu thật.

[Bằng chứng kiểm thử](https://github.com/gptapp123-prog/Khu-ph-45/actions/runs/42753488891)
