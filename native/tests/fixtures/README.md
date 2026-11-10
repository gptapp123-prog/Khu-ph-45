# Dữ liệu kiểm thử gốc

Hai tệp được khôi phục từ `QLKP_V0_19_P6_QA_CHECKPOINT.zip`.

| Tệp | SHA-256 của nội dung gốc |
| --- | --- |
| `tests/fixtures/schema_v0_7.sql` | `21adba6ccee80e6e0b943310d083d2170d95968db567c555bf551cc5bfdddb95` |
| `demo/qlkp_demo_fictional_v016_G.sqlite` | `e59114aba8fae1b7526854bac2c8e3bb73df8e1f3066e842dcbabdc5d53a783e` |

Manifest nhóm G v0.16 xác định kho SQLite là dữ liệu giả lập với 2.120 hộ, 9.540 nhân khẩu và 24.050 hồ sơ nghiệp vụ. Bản lưu dùng XZ. Ca kiểm thử giải nén trong thư mục tạm, kiểm tra mã băm rồi mở SQLite ở chế độ chỉ đọc.
