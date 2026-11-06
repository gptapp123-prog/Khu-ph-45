# QLKP 0.20 P8 — Khởi tạo, mã hóa và hợp nhất kho

## Đã thực hiện

Màn hình đầu tiên tự nhận diện kho rỗng. Kho rỗng hiển thị Khởi tạo hệ thống, yêu cầu tài khoản, mật khẩu tối thiểu 12 ký tự, nhập lại mật khẩu, mã và tên khu phố. Lỗi nằm cạnh nút thực hiện. Enter tạo tài khoản ở chế độ khởi tạo; tạo xong vào ứng dụng ngay. Kho đã có tài khoản chuyển sang Đăng nhập; không cho tạo quản trị gốc thứ hai hoặc xóa kho để khởi tạo lại.

CSDL đang dùng được mã hóa AES-256/HMAC-SHA512 theo cơ chế SQLCipher của SQLite3 Multiple Ciphers 2.6.1. Đây là SQLite3MC, không phải bản thương mại do Zetetic phát hành. Khóa ngẫu nhiên 256 bit do Windows DPAPI bảo vệ theo tài khoản Windows hiện tại. Người dùng vẫn đăng nhập bằng tài khoản ứng dụng; không phát sinh mật khẩu mở kho riêng chưa từng được tạo.

Kho SQLite native cũ được sao chép bằng schema tin cậy vào tệp mã hóa. Hệ thống đối chiếu dấu vân tay logic các bảng, cột và giá trị; giữ snapshot phục hồi mã hóa rồi thay thế nguyên tử. Giữ ID, tài khoản, quyền và quan hệ dữ liệu. Nguồn có bảng/cột không hỗ trợ, liên kết lỗi hoặc giao dịch WAL/journal chưa đóng sẽ dừng chuyển đổi; không tự bỏ dữ liệu. Không có cam kết xóa dấu vết vật lý của tệp rõ cũ trên SSD hay các bản sao do người dùng tự lưu.

## Cài bản mới và đăng nhập

1. Đóng ứng dụng cũ. Cài setup Windows 0.20 P8, rồi mở bằng cùng tài khoản Windows đã dùng trước đây.
2. Nếu kho chưa có tài khoản, nhập đủ 5 trường và bấm Khởi tạo; có thể dùng Enter. Mật khẩu và phần nhập lại phải giống nhau.
3. Nếu kho đã có tài khoản, dùng thông tin đăng nhập cũ. Tài khoản không tự thay đổi sau nâng cấp.
4. Quản trị hệ thống vào Tài khoản để cấp quản trị khu phố. Phân quyền tại I02; chỉ cấp quyền phù hợp với địa bàn và nghiệp vụ.
5. Tạo bản sao `.qlkpbak` tại I04 và giữ mật khẩu phục hồi trước khi chuyển máy hoặc cài lại Windows.

Cài/gỡ chương trình không xóa thư mục dữ liệu. Không đổi tên, di chuyển hay xóa kho và tệp khóa để thử sửa lỗi đăng nhập. Nhật ký chẩn đoán nằm tại `%LOCALAPPDATA%\QLKP-V2-PILOT\startup-events.log`.

## Hợp nhất dữ liệu

Quản trị hệ thống cấp `I08:read`, `I08:merge` cho quản trị khu phố và cấp quyền đọc/tạo của từng phân hệ có trong nguồn; danh mục khu phố cần `I07:read/manage`. Quyền mới không tự được cấp cho tài khoản đang tồn tại.

Đăng nhập quản trị khu phố → I08 Chất lượng dữ liệu → Hợp nhất kho dữ liệu → Chọn nguồn → Đối chiếu → xem số dòng mới và trùng hoàn toàn → Áp dụng hợp nhất.

Nguồn hỗ trợ:

- Kho `.db` của QLKP native đã đóng. Kho mã hóa cần tệp khóa DPAPI đi kèm, mở được bằng tài khoản Windows hiện tại.
- Bản sao `.qlkpbak` V1 hoặc V2 và mật khẩu phục hồi. Bản V1 được đọc vào RAM, không ghi tệp SQLite rõ tạm.
- Mã khu phố phải khớp đúng một khu phố trong nguồn. Chỉ dữ liệu thuộc khu phố đó được đưa vào kho đang quản lý.

| Nhóm dữ liệu nhập | Xử lý |
|---|---|
| Hộ, nhân khẩu, phân loại hộ/người | Giữ ID, thông tin và phạm vi khu phố |
| Thành viên hộ, biến động cư trú | Giữ quan hệ và lịch sử gắn với ID |
| Lịch sử nhập liệu, nhập nhân khẩu | Giữ khóa chống gửi lặp và thông tin lô |
| Hồ sơ 37 phân hệ và các trường chi tiết | Kiểm tra lại quy tắc của màn hình nhập thông thường |
| Liên kết và lịch sử hồ sơ | Giữ ID, quan hệ; ghi nhận việc tiếp nhận từ kho nguồn |
| Danh mục riêng khu phố | Đối chiếu theo phạm vi, mã và ID |

Tài khoản, mật khẩu, quyền và trần ủy quyền của kho đích được giữ nguyên. Không nhập tài khoản/quyền hoặc danh mục toàn hệ thống của nguồn. Các ID tác giả gốc được giữ trong dấu vết nhập; người thực hiện tiếp nhận được ghi là người đang hợp nhất. Trường giao việc liên kết tài khoản phải trỏ tới tài khoản có sẵn, hoạt động và cùng khu phố; phần mềm không tự thay người phụ trách.

Dòng cùng ID phải trùng hoàn toàn mới được bỏ qua. Cùng ID nhưng khác nội dung, trùng mã nghiệp vụ với ID khác, sai quyền, thiếu liên kết hoặc sai quy tắc nghiệp vụ sẽ dừng cả lô. Không tự gộp người bằng tên. Muốn tiếp nhận các nguồn khác hệ thống như Python/web, phải chuyển đổi schema riêng; không đổi đuôi tệp để nhập.

Đối chiếu sử dụng một bản ứng viên mã hóa riêng; kho chính chưa đổi. Khi áp dụng, hệ thống kiểm tra kho chính chưa thay đổi kể từ lúc đối chiếu và kiểm tra lại bản ứng viên, giữ bản phục hồi mã hóa, rồi thay thế nguyên tử. Nếu người khác đã ghi dữ liệu, phải đối chiếu lại. Nguồn gốc tệp, số dòng và dấu vết được lưu trong CSDL mã hóa; không ghi hồ sơ hoặc mật khẩu vào nhật ký chẩn đoán.

## Sao lưu và phục hồi

- `.qlkpbak` V2: vỏ AES-256-GCM/PBKDF2-SHA256 310.000 vòng, chứa khóa và các trang CSDL đã mã hóa. Mật khẩu sao lưu 14–1024 ký tự. Có thể chuyển máy. Sai mật khẩu hoặc sửa tệp bị từ chối.
- `.qlkpbak` V1 của bản cũ: tiếp tục đọc và phục hồi qua ảnh CSDL trong RAM; đầu ra vẫn là kho mã hóa.
- `.qlkpvault`: snapshot phục hồi cục bộ trước mã hóa/hợp nhất/khôi phục; chỉ mở khi còn khóa Windows tương ứng. I04 cho chọn loại tệp này; không cần mật khẩu sao lưu riêng.
- Khi chuyển máy: tạo kho mới và quản trị tạm, đăng nhập quản trị hệ thống, vào I04 chọn `.qlkpbak`, nhập mật khẩu và Khôi phục. Ứng dụng đóng; mở lại rồi đăng nhập bằng tài khoản có trong bản sao.
- Nếu kho đã mã hóa nhưng mất/không giải được khóa DPAPI, ứng dụng dừng, không sinh khóa đè và không xóa kho. Cần bản `.qlkpbak` và mật khẩu để phục hồi trên kho mới.

## Kiểm chứng và phạm vi

Kiểm thử bắt buộc chạy bằng EXE WPF trên Windows, gồm: mở lại kho mã hóa; từ chối thiếu khóa/sai khóa/tệp bị sửa; chuyển kho rõ và so sánh dữ liệu; phục hồi V1; chuyển khóa phục hồi V2; hợp nhất, chống gửi lặp, chặn xung đột, chặn sai khu phố/quyền/phiên bản; giữ tài khoản và phục hồi trước hợp nhất. Cửa sổ WPF được mở, nút được phát sự kiện và kết quả được đọc lại. Bộ cài được cài, chạy self-test của bản đã cài và gỡ trên máy kiểm thử.

Đây là kiểm thử dữ liệu tổng hợp trong kho cô lập. Không có kho thật trên PC của Anh Toàn trong phiên làm việc, nên không khẳng định dữ liệu cá nhân đã được mã hóa/hợp nhất từ xa. Việc chuyển kho thật diễn ra trên máy khi mở bản mới; việc hợp nhất diễn ra khi chọn nguồn và áp dụng.

Android 0.20 giữ chức năng đọc gói danh mục mã hóa `.qlkpsync`. Phiên bản APK được tăng để đồng nhất phát hành, không thêm đồng bộ hồ sơ hai chiều, QR ghép thiết bị, LAN hay OCR. Windows 7 và điện thoại thật chưa được xác minh ở lượt này. EXE chưa có chữ ký nhà phát hành. Có bộ cài và kiểm thử đạt không đồng nghĩa nghiệm thu đủ mọi luồng của 56 chức năng.

Nguồn kỹ thuật: [SQLite3MC](https://github.com/utelle/SQLite3MultipleCiphers-NuGet), [thứ tự cấu hình và khóa](https://utelle.github.io/SQLite3MultipleCiphers/docs/configuration/config_sql_pragmas/), [cơ chế AES/HMAC](https://utelle.github.io/SQLite3MultipleCiphers/docs/ciphers/cipher_sqlcipher/). Không có tuyên bố chứng nhận của cơ quan nhà nước cho ứng dụng.
