# Quản lý Khu phố – Windows WPF và Android native

V0.19 P7 đã biên dịch thành bộ cài Windows và APK Android ký riêng. Lượt kiểm thử: https://github.com/gptapp123-prog/Khu-ph-45/actions/runs/42173871581 . Giữ 56 mã chức năng A01–I09, dữ liệu SQLite và phân quyền từ P6; không tự chèn dữ liệu minh họa khi mở ứng dụng. Lịch sử kiểm thử được lưu theo phiên.

- Windows: WPF .NET Framework 4.8, x64. `BUILD_SETUP_1_CLICK.cmd` biên dịch, chạy self-test trên kho tạm và tạo Inno Setup. `test-windows-install.ps1` kiểm tra cài, self-test bản đã cài và gỡ cài trên máy kiểm thử.
- Android: Kotlin Activity native, Android 8 trở lên; I06 hiện đọc gói danh mục mã hóa `.qlkpsync` từ PC. Không có camera OCR, ghép QR, LAN hoặc nhận/trả nhiệm vụ hai chiều.
- Phiên bản chung: `release.properties`. EXE Windows chưa ký nhà phát hành; APK bàn giao là release (không debuggable), ký bằng khóa riêng; khóa không nằm trong kho mã nguồn. APK debug của CI chỉ dùng kiểm thử.

CSDL chính vẫn chưa mã hóa. Chỉ dùng dữ liệu giả cho bản PILOT. Có mã và có bộ cài không có nghĩa 56 chức năng đã được nghiệm thu đầy đủ.

Workflow hiện chỉ chạy khi chủ dự án yêu cầu thủ công; không tự chạy khi đẩy mã, không có lịch định kỳ, theo dõi GitHub hay tự cập nhật ứng dụng. Artifact chỉ được tạo khi các bước bắt buộc thành công. Các workflow của bản ứng dụng cũ không nằm trong nhánh build này.

Kiểm thử thực: 58 bước self-test C# trước và sau cài, 106 bước UI WPF, 4 bước cài/gỡ, 10 ca Kotlin/JVM và 5 bước trên APK release trong Android 11 API 30 giả lập đều đạt. Có 56 route được mở; 37 phân hệ thử tạo/sửa qua sự kiện nút. Đây là smoke test, chưa nghiệm thu toàn bộ 56 quy trình, Windows 7 hoặc điện thoại thật.
