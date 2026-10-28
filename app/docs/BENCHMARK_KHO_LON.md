# Benchmark kho lớn KPHO2

## Phạm vi

`tools/benchmark_large_dataset.py` tạo kho tạm hoàn toàn biệt lập và chỉ dùng dữ liệu tổng hợp. Công cụ đo:

- đọc trước CSV gần 10 MB với tối đa 5.000 dòng;
- xác thực và nhập 5.000 hồ sơ qua API thật;
- mã hóa/persist kho KPHO2;
- tạo 500 cặp hồ sơ nghi trùng và quét trung tâm rà soát;
- tải trang danh sách 200 dòng;
- mở lại kho mã hóa và chạy `PRAGMA integrity_check`.

Kho tạm bị xóa khi kết thúc. Không đọc hoặc sao chép dữ liệu dân cư thật.

## Cách chạy

Từ thư mục `app`:

```bash
PYTHONPATH=pc:. python tools/benchmark_large_dataset.py --output benchmark-result.json
```

Có thể chạy nhanh để kiểm tra công cụ:

```bash
PYTHONPATH=pc:. python tools/benchmark_large_dataset.py --rows 100 --note-length 100 --duplicate-pairs 10
```

Kết quả JSON tách rõ thông tin môi trường, kích thước đầu vào, thời gian từng công đoạn và các điều kiện chấp nhận. Thời gian chỉ có giá trị đối với đúng máy, ổ đĩa và phiên bản Python được ghi trong kết quả; không được suy diễn thành kết quả Windows/Android thật.

## Tiêu chí bắt buộc của công cụ

- Nhập đủ số dòng và không có dòng không hợp lệ.
- Số cặp nghi trùng đúng dữ liệu đã tạo.
- Tổng số hồ sơ trong kho đúng dự kiến.
- Kho mở lại thành công và `integrity_check` trả về `ok`.

Chưa đặt ngưỡng giây cứng trong CI vì máy chạy GitHub Actions và máy PC triển khai có cấu hình khác nhau. Ngưỡng vận hành phải được xác lập sau khi chạy trên PC đích Windows 10/11 với ổ đĩa thực tế.

## Kết quả tham chiếu ngày 28-10-2026

Môi trường phát triển Linux 6.18.55, Python 3.12.3, 9 CPU logic:

| Hạng mục | Kết quả |
|---|---:|
| CSV đầu vào | 5.000 dòng / 9.357.047 byte |
| Đọc trước CSV | 0,214 giây |
| Nhập và mã hóa 5.000 hồ sơ | 0,465 giây |
| Tạo thêm 500 cặp nghi trùng | 0,513 giây |
| Quét danh sách nghi trùng | 0,104 giây |
| Tải danh sách 200 dòng | 0,016 giây |
| Mở lại kho KPHO2 | 0,167 giây |
| Kích thước kho mã hóa | 10.538.397 byte |
| Tổng hồ sơ | 6.000 |
| Bộ nhớ đỉnh được báo cáo | 296.944 KiB |

Kết quả: nhập đủ 5.000/5.000 dòng, không có dòng lỗi, tìm đúng 500 cặp, kho mở lại được và kiểm tra toàn vẹn trả về `ok`. Đây chỉ là số liệu tham chiếu Linux trong môi trường phát triển, chưa thay thế benchmark trên PC Windows đích.
