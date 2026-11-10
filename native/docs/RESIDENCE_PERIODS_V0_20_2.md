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

## Kết quả kiểm thử

Năm ca riêng chạy đạt bằng SQLite và kiểm tra XAML. Các ca dùng schema thật và truy vấn SQL lấy từ C#. Bộ kiểm thử nguồn chung chạy 64 ca, 60 đạt. Bốn ca còn lỗi cũng tái hiện trên P9 gốc, liên quan fixture cũ bị thiếu và các kiểm tra nguồn quyền cũ. Đã sửa riêng kiểm tra đồng bộ phiên bản Windows cho bản này.

Lượt Windows 42752092025 đã biên dịch WPF, chạy self-test 82/82 bước trước và sau khi cài, chạy giao diện 257/257 bước, và cài/chạy kiểm tra/gỡ 4/4 bước. Ca WPF mới mở lịch sử từ đúng nhân khẩu, thử địa chỉ rỗng, ghi địa chỉ hợp lệ và đọc lại. SHA-256 setup đã đối chiếu với bản kê trong artifact.

Bản build xác minh từ `fff5c1886d4d5e3749437059f2a530fcad10d457`, có cùng mã ứng dụng với `f725a45881d76be6887ccc6ec37560bc868721aa`. Hai lượt trước đã phát hiện lỗi cú pháp C# và ca I09 còn so với schema 12. Đã sửa cả hai trước lượt đạt.

[Bằng chứng Windows](https://github.com/gptapp123-prog/Khu-ph-45/actions/runs/42752092025)

Chưa thử nâng cấp trên kho thật của người dùng hoặc trên Windows 7. Bộ cài chưa ký nhà phát hành.
