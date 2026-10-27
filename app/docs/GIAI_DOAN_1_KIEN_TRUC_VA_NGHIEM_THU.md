# QUẢN LÝ KHU PHỐ — GIAI ĐOẠN 1: BỘ KHUNG + TÀI KHOẢN

**Phiên bản:** 7.1.0-foundation  
**Căn cứ:** Hồ sơ KP21 6.3.1 (31 mã chức năng F-001–F-031) và yêu cầu kiến trúc PC chính / mobile con ngày 27/10/2026.

## 1. Phạm vi đã lập trình và kiểm thử

### Bộ khung
- PC chạy FastAPI, giao diện web nội bộ tiếng Việt Unicode; trang đăng nhập và các trang điều hướng dùng API thực tế.
- PC là máy chủ và điểm lưu **duy nhất**. CSDL SQLite chỉ tồn tại dưới dạng in-memory trong tiến trình; khi ghi ổ đĩa, `Vault` mã hóa snapshot bằng AES-256-GCM, scrypt dẫn xuất khóa từ mật khẩu mở kho.
- Tệp ứng dụng và dữ liệu đặt trên PC. Mặc định dữ liệu: `%LOCALAPPDATA%\QuanLyKhuPho\Data\quanlykp.vault`.
- Tài khoản và hồ sơ được nạp từ PC; không sử dụng Google Drive, Firebase Cloud Messaging, Zalo OA, SMS, máy chủ trung gian hoặc telemetries.
- PC mặc định nghe trên 127.0.0.1, không tự mở cổng LAN. LAN chỉ được khởi động khi có chứng chỉ và khóa TLS cung cấp bằng `--cert`/`--key`.
- Bộ nguồn Android Java WebView đặt trong `android/`, không có SQLite, SharedPreferences lưu hồ sơ, DOM storage, cache ngoại tuyến. Bật FLAG_SECURE; chặn điều hướng ra ngoài host PC. **Chưa build và chưa thử APK trên điện thoại.**
- Biểu tượng khiên quấn xích vàng tại `pc/web/icon.svg` và tệp drawable Android.

### Hệ thống tài khoản (trọng tâm giai đoạn 1)
- Tạo Super Admin tại PC một lần duy nhất.
- Đăng nhập tài khoản bằng tên đăng nhập ASCII định dạng chặt, mật khẩu Argon2id (mặc định từ 12 ký tự); tên cá nhân UTF-8 có dấu.
- Vai trò: Viewer, Rà soát, Admin KP, Admin Gốc, Super Admin.
- Tạo người dùng mới, gán tổ 1–15; quản trị cấp dưới; liên hệ gồm SĐT, Zalo ID, email.
- Đăng nhập lần đầu bắt buộc đổi mật khẩu, khóa mật khẩu theo 5 lần sai, 30 phút; đặt lại mật khẩu thu hồi phiên hiện hành.
- Phiên bằng token ngẫu nhiên, chỉ lưu RAM máy chủ, timeout sau 15 phút không có yêu cầu.
- Mobile đăng nhập lần đầu chờ được phê duyệt thiết bị; giới hạn hai thiết bị cho mỗi tài khoản, khi chặn sẽ thu hồi phiên.
- Quản lý tài khoản có phân tách địa bàn theo Tổ đối với Admin KP. Chức năng tạo/sửa/khóa tài khoản và duyệt/chặn thiết bị chỉ được thực hiện trên PC chính.
- Nhật ký thao tác đăng nhập/quản lý; cơ chế hash chain phát hiện sửa đổi đối với bảng nhật ký; kết quả kiểm thử kiểm tra chuỗi hash.
- Có endpoint người dùng tự đổi mật khẩu `POST /api/account/change-password` và buộc đăng nhập lại trên tất cả thiết bị.

## 2. Luồng dữ liệu

```text
PC Windows (ỨNG DỤNG CHÍNH)
  ├─ Kho dữ liệu vault được mã hóa [CHỈ PC]
  ├─ Tài khoản / quyền / phiên / nhật ký [CHỈ PC]
  ├─ FastAPI / Giao diện trình duyệt nội bộ
  └─ Mạng LAN nội bộ khi được cho phép (HTTPS)
           ↑↓ HTTPS, xác thực và phê duyệt thiết bị
Android (ỨNG DỤNG CON)
  └─ Chỉ hiển thị/tạo yêu cầu qua WebView; không DB, không cache ngoại tuyến
```

Thông tin trên mobile bắt buộc hiện tạm trong RAM để có thể xem; mô hình **không lưu bền vững** dữ liệu nghiệp vụ trên mobile, không có nghĩa người dùng không thể chụp thiết bị bằng camera khác hoặc hệ điều hành không lưu bất kỳ dấu vết hệ thống nào. Không thể tuyệt đối hóa "không rò rỉ".

## 3. Chính sách đã sửa so với đặc tả 6.3.1

| Hạng mục | Hồ sơ 6.3.1 | Hướng triển khai 7.1 |
|---|---|---|
| Kho dữ liệu | SQLite/SQLCipher + Drive tùy chọn | PC-only vault AES-GCM; không Drive |
| Mật khẩu | bcrypt cost 12 | Argon2id với salt riêng, tối thiểu 12 ký tự |
| JWT | 15 phút / refresh 7 ngày | Token không trạng thái bền vững, chỉ RAM PC, timeout sau 15 phút không hoạt động |
| OTP Zalo/SMS | Đặc tả bắt buộc với quản trị | Chưa tích hợp; dịch vụ ngoài không phù hợp không-cloud. Cần thiết kế 2FA PC-local riêng |
| Wipe qua FCM | Đặc tả | Không tích hợp vì mobile không giữ DB chính; có thu hồi phiên/chặn thiết bị |
| Backup Drive | Tùy chọn | Backup mã hóa trên chính PC, không gửi ra mạng |
| PC/Mobile | Android là chính | PC chính, Android là máy khách không lưu DB |

## 4. Danh mục kiểm thử

`PYTHONPATH=pc python -m pytest -q tests/test_foundation_accounts.py`

1. Khởi tạo Super Admin chỉ một lần, tài khoản chưa xác thực bị từ chối.
2. Lưu Unicode tên người; sửa mật khẩu bắt buộc; thu hồi các phiên cũ.
3. Chặn nâng quyền và thao tác vượt Tổ của Admin KP.
4. Thiết bị mới chờ duyệt, duyệt, chặn và thu hồi phiên.
5. Đăng nhập sai đủ 5 lần bị khóa và quản trị viên có thể đặt lại mật khẩu.
6. Kiểm chứng chuỗi audit log và mở lại vault sau khi mã hóa, từ chối mật khẩu sai.
7. Từ chối tên đăng nhập không đúng mẫu, số điện thoại sai, trùng tài khoản, quyền trái phép.
8. Ngăn thiết bị từ xa tạo/sửa tài khoản hoặc xuất tệp bằng cơ chế PC-only.

**Giới hạn kiểm thử:** Các bài này là kiểm thử tích hợp API bằng TestClient; chưa kiểm thử cài đặt, chạy dài ngày, hiệu năng nhiều người, bảo mật độc lập/pentest, APK trên điện thoại hoặc môi trường TLS doanh nghiệp. Đã tạo `tests/smoke_browser.py` cho kiểm thử trình duyệt thật, nhưng Chromium trong môi trường thực thi hiện tại từ chối truy cập loopback với thông báo `ERR_BLOCKED_BY_ADMINISTRATOR`, vì vậy **chưa có kết quả kiểm thử giao diện end-to-end thành công**. Kiểm tra cú pháp JavaScript bằng Node.js đã đạt.

## 5. Còn thiếu để nghiệm thu phân hệ Tài khoản ở mức vận hành sản xuất

- **2FA thực sự không phụ thuộc dịch vụ đám mây**: Chưa có; không được coi xác nhận thiết bị là 2FA.
- **Ma trận quyền có thể cấu hình**: Vai trò đã được thực thi bằng các quy tắc server, nhưng chưa có giao diện biên tập ma trận được nghiệm thu.
- **Quy trình phê duyệt hai người, phục hồi khẩn cấp**: chưa có.
- **Nhập tài khoản hàng loạt từ Excel**: chưa có trong giao diện tài khoản.
- **Bảo vệ brute force theo IP, phát hiện IP/thiết bị bất thường, chính sách mật khẩu lịch sử**: chưa hoàn thiện. Giới hạn 5 lần hiện hoạt động theo tài khoản, chưa có giới hạn theo địa chỉ mạng đối với tên đăng nhập không tồn tại.
- **Ràng buộc thiết bị dựa trên bằng chứng mật mã**: chưa có. Device ID do ứng dụng gửi lên có thể bị giả mạo nếu kẻ tấn công biết thông tin đăng nhập; việc phê duyệt hiện không tương đương chứng thực phần cứng.
- **Bộ cài Windows EXE/MSI và APK release có chữ ký**: chưa có; hiện có mã nguồn Android và script cài chương trình PC cần Python.
- **Kiểm thử LAN HTTPS với chứng chỉ CA tin cậy**: chưa thực hiện tại môi trường người dùng.
- **Nhật ký chống giả mạo trước quản trị viên có quyền truy cập toàn PC**: chain hash nội bộ không bảo đảm tính bất biến tuyệt đối. Cần kênh chứng thực/cất giữ bằng chứng PC-local độc lập.

## 6. Đánh giá mức độ hoàn thiện sau nâng cấp

- **Có kết quả thực nghiệm:** API máy chủ, tạo/đăng nhập/khóa/sửa tài khoản, cấp vai trò, giới hạn Tổ, phê duyệt thiết bị, mật khẩu, mã hóa vault PC, kiểm tra nhật ký.
- **Đã có mã nguồn nhưng chưa nghiệm thu:** giao diện trong browser/Android và các phân hệ dân cư, hộ gia đình, nhập OCR, xuất tệp ngoài phạm vi kiểm thử giai đoạn này.
- **Chưa xây dựng:** 2FA thuần nội bộ, quy trình hai người, trình sửa ma trận quyền, phát hành nhị phân và triển khai mạng doanh nghiệp.

Kết luận: Đủ để phát triển tiếp và kiểm thử tài khoản trên môi trường LAN được quản lý; **chưa đủ điều kiện kết luận an toàn cho dữ liệu dân cư thật hoặc đưa vào vận hành chính thức**.
