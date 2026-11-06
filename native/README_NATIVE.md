# Quản lý Khu phố – Windows WPF và Android native

Nhánh build độc lập cho mã nguồn V0.19 P7. Giữ 56 mã chức năng A01–I09, dữ liệu SQLite và phân quyền từ P6; không tự chèn dữ liệu minh họa khi mở ứng dụng. Lịch sử kiểm thử được lưu theo phiên.

- Windows: WPF .NET Framework 4.8, x64. `BUILD_SETUP_1_CLICK.cmd` biên dịch, chạy self-test trên kho tạm và tạo Inno Setup. `test-windows-install.ps1` kiểm tra cài, self-test bản đã cài và gỡ cài trên máy kiểm thử.
- Android: Kotlin Activity native, Android 8 trở lên; I06 hiện đọc gói danh mục mã hóa `.qlkpsync` từ PC. Không có camera OCR, ghép QR, LAN hoặc nhận/trả nhiệm vụ hai chiều.
- Phiên bản chung: `release.properties`. EXE Windows chưa ký nhà phát hành; APK debug chỉ dùng kiểm thử, không bảo đảm cập nhật qua các lần build do khóa debug của máy build có thể thay đổi.

CSDL chính vẫn chưa mã hóa. Chỉ dùng dữ liệu giả cho bản PILOT. Có mã và có bộ cài không có nghĩa 56 chức năng đã được nghiệm thu đầy đủ.

Workflow chỉ chạy trên thay đổi của nhánh `qlkp-native-build-20261106`; không có lịch định kỳ, theo dõi GitHub hay tự cập nhật ứng dụng. Artifact chỉ được tạo khi các bước bắt buộc thành công. Các workflow của bản ứng dụng cũ không nằm trong nhánh build này.
