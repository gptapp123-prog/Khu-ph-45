# QUẢN LÝ KHU PHỐ 7.5 — KHUNG GIAO DIỆN VÀ BẢNG CHỨC NĂNG

Ngày: 27/10/2026. Nguồn kế thừa: 7.4 từ gói bàn giao; không xây lại ứng dụng.

## I. ĐÃ LÀM ĐƯỢC

- Tổ chức menu thành Hệ thống, Dữ liệu dân cư, Quản trị và Phân hệ mở rộng.
- Thêm màn hình Bảng chức năng, đủ mã F-001–F-031, có lọc theo trạng thái/nhóm và tìm theo mã/tên.
- Chỉ tạo liên kết “Mở” tới màn hình đã có hàm xử lý. Ba phân hệ mở rộng hiển thị “Chưa triển khai” và bị vô hiệu hóa.
- Tổng quan có sáu thẻ phân hệ, phân biệt Đang vận hành, Đang xây dựng và Theo lộ trình.
- Khung mới tách thành `framework.js` và `framework.css`, giảm can thiệp vào luồng nghiệp vụ 7.4.
- 26/26 test pytest đạt trên Linux/Python 3.12.14; JavaScript syntax đạt; kiểm tra đủ 31 mã duy nhất.

## II. VẤN ĐỀ CÒN CHƯA XỬ LÝ

- Chưa kiểm thử hiển thị bằng trình duyệt thật vì môi trường hiện tại không có Chromium tại `/usr/bin/chromium`.
- Chưa kiểm thử Windows và Android thật; chưa được gọi là nghiệm thu GUI.
- Bảng chức năng là khung điều phối; không biến các mục “một phần” thành chức năng hoàn thiện.
- Phân hệ địa bàn, cơ sở kinh doanh, công việc/phản ánh mới có vị trí điều hướng, chưa có màn hình hoặc backend nghiệp vụ.

## III. CÁC BƯỚC CHƯA THỰC HIỆN

1. Kiểm thử giao diện thật ở desktop 1280×800 và mobile 360×800; xử lý chữ đè, cuộn và nút chạm.
2. Hoàn thiện lần lượt chức năng đang có một phần, ưu tiên F-029 dân cư/hộ và F-018/F-019 nhập liệu.
3. Chốt mâu thuẫn F-027/F-028 và trường chuẩn thứ 12 trước khi đổi cấu trúc dữ liệu.
4. Viết đặc tả riêng trước khi lập trình các phân hệ mở rộng.
