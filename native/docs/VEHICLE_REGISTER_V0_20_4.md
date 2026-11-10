# Phương tiện F08 · Windows native 0.20.4

Mở **An toàn, đô thị → F08 Phương tiện**. Tìm theo biển số, tên chủ xe hoặc hãng. Thêm xe hoặc chọn một dòng để cập nhật. Trong cửa sổ hồ sơ, tìm nhân khẩu, kiểm tra tên và thông tin phân biệt, rồi bấm **Chọn làm chủ xe**. Trở lại danh sách và bấm **Mở hồ sơ chủ xe** để xem nhân khẩu theo đúng ID.

- Xe chở người nhập số chỗ. Xe tải nhập tải trọng theo kg. Xe máy và loại khác không có hai trường này. Biển số bỏ khoảng trắng, dấu gạch nối và dấu chấm trước khi kiểm tra trùng trong cùng khu phố.
- Có trạng thái đang theo dõi và ngừng theo dõi. Đổi chủ xe hoặc loại xe dùng cùng luồng cập nhật, có kiểm phiên bản và ghi nhật ký. Bản đầu chưa có bảng lịch sử chuyển chủ riêng.
- Xem cần F08 đọc và B02 đọc. Thêm và sửa cần quyền F08 tương ứng. Với kho cũ, nâng lược đồ lên 15 và cấp quyền F08 ban đầu theo quyền B02 đang có. Quyền được thay đổi sau đó trong I02.
- Xe và liên kết chủ được lưu trong kho PC. Nhóm Phương tiện có mặt trong đối chiếu hợp nhất I08. Biển số trùng và liên kết khác địa bàn bị chặn. Nên sao lưu trước khi nâng cấp kho. Android và bảo hiểm ngoài phạm vi bản này.

## Kiểm thử

Lượt Windows đầu phát hiện F08 còn thiếu trong bộ lọc điều hướng. Đã sửa và chạy lại. Lượt Windows 42755158561 đạt 84/84 bước self-test trước và sau khi cài, 262/262 bước WPF, và 4/4 bước cài/chạy/gỡ. Ca F08 qua nút thật đã chọn đúng nhân khẩu và đọc lại bản ghi xe vừa tạo. Kiểm tra SQL bổ sung xác nhận quyền đọc cũ không tự thành quyền ghi. SHA-256 bộ cài khớp bản kê. Bộ cài chưa ký, chưa thử Windows 7 hoặc dữ liệu thật.

[Bằng chứng Windows](https://github.com/gptapp123-prog/Khu-ph-45/actions/runs/42755158561)
