# GIAI ĐOẠN 3.2A — SAO LƯU, KIỂM TRA VÀ ROLLBACK / BẢN 7.4

Ngày kiểm thử: 27/10/2026. Kế thừa 7.3; chưa thay thế nghiệm thu Windows thật.

## I. ĐÃ LÀM ĐƯỢC

- Backup KPHO2 mã hóa AES-256-GCM, ghi atomic + fsync, tên tệp microsecond và checksum SHA-256.
- Đọc lại file, xác thực AES-GCM, `PRAGMA integrity_check`; giữ ba cặp backup/checksum.
- Restore kiểm tra trước khi ghi và tạo rollback; có công cụ offline `pc/restore_backup.py`.
- 26/26 test đạt trên Linux/Python 3.12.14, gồm hồi quy 7.1–7.3 và ba test mới.

## II. VẤN ĐỀ CÒN CHƯA XỬ LÝ

- Chưa rút điện/kill process trên Windows thật; chưa benchmark kho lớn; chưa kiểm tra ACL NTFS.
- Backup cùng PC vẫn có thể mất cùng ổ đĩa. Restore chưa đưa vào GUI vì là thao tác phá hủy.

## III. CÁC BƯỚC CHƯA THỰC HIỆN

1. GĐ3.2B: benchmark, fault injection và ACL/file-lock trên Windows 10/11 thật.
2. GĐ3.1: nghiệm thu Windows vật lý.
3. GĐ3.3: EXE/MSI offline và APK Android ký.
4. GĐ4: 2FA độc lập và kiểm thử bảo mật.
