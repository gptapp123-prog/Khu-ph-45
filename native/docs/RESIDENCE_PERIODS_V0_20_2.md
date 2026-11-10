# Windows native 0.20.2 · Kỳ địa chỉ cư trú

Cơ sở phát triển là nhánh P9 ở commit `bfcd59a7965d56d92a819f49139bd45e5f34ec07`.

## Thao tác

Mở **Dân cư → B02 Nhân khẩu → chọn hồ sơ → Lịch sử địa chỉ cư trú**. Cửa sổ riêng hiển thị bảng có phân trang. Người có quyền ghi B03 có thể chọn Thường trú hoặc Tạm trú theo khai báo, nhập địa chỉ, ngày bắt đầu và mốc kết thúc nếu có. Ngày theo dd-mm-yyyy. Mốc kết thúc là ngày đầu tiên kỳ không còn hiệu lực.

Kỳ mới phải nối tiếp các kỳ cùng loại. Kỳ để trống mốc kết thúc được hiển thị kết thúc khi có kỳ kế tiếp cùng loại. Địa chỉ và thời điểm ghi ban đầu được giữ. Hai loại có thể đồng thời có hiệu lực. Thao tác này độc lập với chuyển hộ B03 và nhóm cư trú đang lưu trên hồ sơ B02. Không tự chuyển địa chỉ hộ cũ thành kỳ cư trú.

## Dữ liệu và phân quyền

- Xem cần B02 đọc và B03 đọc. Ghi cần thêm B03 tạo. Quyền, phiên và phạm vi khu phố được kiểm tra trong DataStore. Khóa ngoại và trigger bảo vệ quan hệ nhân khẩu, địa bàn, ngày và bản ghi lịch sử.
- Schema 13 bổ sung bảng trong kho mã hóa hiện có. Việc ghi kỳ và audit dùng cùng giao dịch. Trước khi nâng cấp cần có bản sao phục hồi. Bản Windows cũ từ chối mở schema mới.
- Sao lưu dùng chung cơ chế kho hiện có. Hợp nhất I08 nhận kỳ địa chỉ theo phạm vi, bảo toàn địa chỉ/ngày, chặn chèn ngược làm thay đổi lịch sử đích và giữ dấu vết tài khoản gốc trong sổ hợp nhất. Kho nguồn cũ có thể chưa có bảng mới.
- Android tiếp tục dùng định dạng danh mục hiện có. Luồng kỳ địa chỉ chạy trên PC. Chưa triển khai đính chính lịch sử, xác minh giấy tờ hoặc nhập hồ sơ lịch sử từ dữ liệu web.

## Kết quả tại môi trường phát triển

Năm ca riêng chạy đạt bằng SQLite và kiểm tra XAML. Các ca dùng schema thật và truy vấn SQL lấy từ C#. Bộ kiểm thử nguồn chung chạy 64 ca, 60 đạt. Bốn ca còn lỗi cũng tái hiện trên P9 gốc, liên quan fixture cũ bị thiếu và các kiểm tra nguồn quyền cũ. Đã sửa riêng kiểm tra đồng bộ phiên bản Windows cho bản này.

Đã nối ca C# vào self-test kho cô lập và mở rộng hồi quy hợp nhất. Môi trường hiện tại chưa chạy WPF, SQLite3MC/DPAPI hoặc bộ cài. Trên Windows, chạy `build-on-windows.ps1 -Installer -SmokeTest`, kiểm tra nhật ký bước `B03_RESIDENCE_PERIODS` và chạy `test-windows-install.ps1` trước khi phát hành. Dùng hồ sơ giả để thử thao tác trực tiếp với tài khoản có quyền và bị từ chối.
