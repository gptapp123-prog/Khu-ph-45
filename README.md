# Quản lý Khu phố native – V0.19 P7

Nhánh triển khai Windows WPF và Android Kotlin nằm trong [native](native/README_NATIVE.md).

Bộ cài được kiểm thử tại [lượt build 42173871581](https://github.com/gptapp123-prog/Khu-ph-45/actions/runs/42173871581), mã nguồn ứng dụng ở commit `047adf1d4e611134e893107bc1a614e56ba2ca11`. Windows đã cài/gỡ và chạy self-test; APK release ký riêng đã thử trên Android 11 giả lập. Đây vẫn là PILOT, kho SQLite chính chưa mã hóa; chưa nghiệm thu toàn bộ 56 quy trình nghiệp vụ.

Workflow chỉ chạy khi có yêu cầu thủ công; không có lịch hoặc theo dõi nền. Khóa ký riêng không có trong kho mã nguồn. Tệp APK đầu vào kiểm thử đã được dọn khỏi nhánh hiện tại sau khi lưu artifact đã xác minh.
