# DESIGN QA — QUẢN LÝ KHU PHỐ 7.5

- Source visual truth: ảnh demo Tổng quan, Bảng chức năng và ba màn hình nghiệp vụ được tạo trong cuộc trò chuyện ngày 27/10/2026.
- Implementation: `http://terminal.local:4173/` từ mã nguồn `/workspace/scratch/48dfccb4a524/QuanLyKhuPho_7.5`.
- Intended viewport: desktop 1280×800; mobile 360×800.
- State: đăng nhập, Tổng quan, Bảng chức năng, Nhân khẩu, Hộ gia đình, Nhập liệu/OCR.

## Findings

- [P0] Không thu được ảnh render từ trình duyệt Work.
  - Evidence: cloud browser trả `net::ERR_CONNECTION_REFUSED` khi mở `http://terminal.local:4173/` dù Uvicorn báo chạy cục bộ ở cổng 4173.
  - Impact: chưa thể đối chiếu trực quan font, khoảng cách, màu, icon, responsive và nội dung thực với ảnh chuẩn.
  - Fix: khôi phục cầu nối preview tới terminal.local, sau đó chụp cùng viewport và trạng thái.

## Required fidelity surfaces

- Fonts and typography: blocked — chưa có ảnh render.
- Spacing and layout rhythm: blocked — chưa có ảnh render.
- Colors and visual tokens: mã dùng navy/xanh/trắng gần ảnh chuẩn, nhưng chưa thể xác nhận trực quan.
- Image quality and asset fidelity: icon khiên hiện dùng asset có sẵn; chưa kiểm tra render.
- Copy and content: test tĩnh xác nhận khung F-001–F-031 và nhãn tiếng Việt; chưa kiểm tra lỗi cắt/chồng chữ.

## Primary interactions tested

- Unit/integration/static tests trước đó: 26/26 đạt.
- Browser interactions: chưa thực hiện vì không mở được trang.
- Console errors: chưa kiểm tra được.

## Comparison history

- Lần 1: mở implementation tại `terminal.local:4173`; bị từ chối kết nối trước khi tải trang. Không có ảnh implementation để so sánh.

## Implementation checklist

1. Mở lại preview qua cầu nối Work.
2. Thiết lập tài khoản thử bằng dữ liệu giả.
3. Chụp desktop 1280×800 cho năm màn hình chính.
4. Chụp mobile 360×800, kiểm tra chữ đè, cuộn, bottom navigation và nút chạm.
5. Sửa P0/P1/P2, chụp lại và đối chiếu cùng ảnh chuẩn.

final result: blocked
