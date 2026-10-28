# GIAI ĐOẠN 3.2 — SAO LƯU, KIỂM TRA VÀ FAULT-INJECTION / BẢN 7.5

Ngày kiểm thử: 27/10/2026. Kế thừa 7.3; chưa thay thế nghiệm thu Windows thật.

## I. ĐÃ LÀM ĐƯỢC

- Backup KPHO2 mã hóa AES-256-GCM, ghi atomic + fsync tệp; trên POSIX đồng bộ thêm mục thư mục sau thay thế nguyên tử.
- Đọc lại file, xác thực AES-GCM, `PRAGMA integrity_check`; giữ ba cặp backup/checksum.
- Restore kiểm tra trước khi ghi và tạo rollback; có công cụ offline `pc/restore_backup.py`.
- Fault-injection xác nhận lỗi `os.replace` không ghi đè kho cũ, tệp tạm được dọn và kho cũ vẫn mở được.
- Nếu ghi checksum lỗi, cặp backup chưa hoàn chỉnh được xóa, không để lại tệp có vẻ hợp lệ nhưng thiếu kiểm chứng.

## II. VẤN ĐỀ CÒN CHƯA XỬ LÝ

- Đã benchmark kho lớn trên Linux; chưa rút điện/kill process trên Windows thật và chưa kiểm tra ACL NTFS.
- Backup cùng PC vẫn có thể mất cùng ổ đĩa. Restore chưa đưa vào GUI vì là thao tác phá hủy.

## III. CÁC BƯỚC CHƯA THỰC HIỆN

1. GĐ3.2B: chạy fault injection, mất điện và ACL/file-lock trên Windows 10/11 thật.
2. GĐ3.1: nghiệm thu Windows vật lý.
3. GĐ3.3: EXE/MSI offline và APK Android ký.
4. GĐ4: 2FA độc lập và kiểm thử bảo mật.
