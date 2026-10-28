# QUẢN LÝ KHU PHỐ — GIAI ĐOẠN 2: PHÂN QUYỀN & KIỂM SOÁT ĐĂNG NHẬP MOBILE

**Phiên bản:** 7.2.0-account-controls  
**Nền kế thừa:** mã nguồn 7.1.0-foundation / KP21 đặc tả 6.3.1  
**Thời điểm biên soạn:** 27/10/2026  
**Quy tắc không đổi:** PC là nơi duy nhất lưu bền vững dữ liệu nghiệp vụ. Mobile chỉ nhận dữ liệu trong bộ nhớ tạm phục vụ phiên hiện hành; không có kho dữ liệu cư dân trên Android, không dịch vụ đám mây.

## A. Đã lập trình, không phải mô phỏng

### A1. Bộ khung và dữ liệu

- Giữ cấu trúc `pc/server` (FastAPI), `pc/web` (giao diện Unicode tiếng Việt), `android` (mã nguồn ứng dụng con WebView), `tests` (kiểm thử tích hợp), không dựng lại kiến trúc.
- Thêm `role_permissions` trong cơ sở dữ liệu mã hóa PC; migration idempotent từ 7.1 lên 7.2 không xóa bảng/tài khoản cũ.
- Thêm **16 quyền thao tác** (capability): xem/sửa/xóa nhân khẩu; xem/tạo hộ; nhập/OCR; xuất; xem/quản lý tài khoản; xem/quản lý thiết bị; nhật ký/xác minh nhật ký; tạo trường; tạo mẫu; sao lưu.
- Server kiểm tra `role_permissions` ở từng API thuộc phạm vi triển khai; không dựa vào ẩn nút giao diện.
- Quyền sửa cấu hình: chỉ Super Admin thao tác từ chính PC. Chỉ có thể thu hồi hoặc khôi phục quyền mặc định, **không** nâng quyền vượt vai trò. Không tắt quyền Super Admin từ ma trận.
- Cho sửa quyền trường dữ liệu `required/optional/hidden/masked` từ giao diện PC: áp dụng vào danh sách cư dân; hạn chế xem đầy đủ CCCD/SĐT của Viewer; yêu cầu bảo mật ở tầng server.
- Khi sửa hồ sơ, trường ẩn/che bị từ chối nếu client cố thay dữ liệu; giá trị không gửi từ client được bảo toàn thay vì bị xóa nhầm. Trường cấu hình `required` được kiểm tra khi ghi.
- Sửa kiểm tra quyền theo tổ: Admin KP không được tạo hộ khác tổ; sửa hay xóa cư dân khác tổ; người dùng chỉ được thấy dữ liệu theo phạm vi server cho phép.

### A2. Phiên đăng nhập mobile cần PC duyệt từng lần

Luồng mới:

1. Mobile gửi tên đăng nhập, mật khẩu, mã thiết bị tới PC bằng **HTTPS**; nếu đúng, **không** cấp token truy cập ngay.
2. PC ghi nhận hoặc phát hiện thiết bị chờ duyệt, tạo `request_id` ngẫu nhiên và `proof` riêng; trạng thái chờ được giữ **chỉ trong RAM PC**, hết hạn sau 180 giây.
3. Quản trị viên tại PC vào **Thiết bị**, duyệt thiết bị (nếu chưa duyệt), sau đó **Cho phép phiên** hoặc **Từ chối**. Người duyệt phải cao quyền hơn người yêu cầu; Admin KP chỉ xử lý trong Tổ được giao.
4. Mobile kiểm tra trạng thái bằng `request_id` cùng proof (hash proof được giữ trong RAM PC); chỉ sau khi có quyết định duyệt, PC cấp một session token trong RAM. Mã yêu cầu chỉ đổi thành token **một lần**, chống replay.
5. Khi chặn thiết bị hoặc đặt lại/đổi mật khẩu, hệ thống xóa phiên/challenge có liên quan; phiên mobile hết hạn sau 15 phút không hoạt động.

**Giới hạn cốt yếu:** Đây là **phê duyệt phiên bởi quản trị viên trên PC**, **không phải xác thực hai yếu tố TOTP/FIDO2 chuẩn**. Tên/model và Android ID do thiết bị khai báo có thể bị giả mạo. Quản trị viên phải xác nhận yêu cầu với cán bộ bằng kênh nội bộ trước khi chấp thuận. Việc đòi hỏi thiết bị không lưu bất cứ khóa lâu dài nào buộc đánh đổi: không thể chứng minh định danh thiết bị bằng khóa riêng gắn phần cứng. Sử dụng approval cho từng phiên chỉ giảm rủi ro, không loại bỏ toàn bộ.

### A3. Giao diện đã nối API thật

- Trang đăng nhập: trạng thái chờ duyệt PC; tự kiểm tra lại 3,5 giây/lần, nút “Kiểm tra phê duyệt”; proof chỉ lưu RAM của trang, không lưu localStorage hoặc cookie.
- Màn hình **Thiết bị** trên PC: danh sách duyệt/chặn thiết bị và bảng yêu cầu đăng nhập; nút phê duyệt từng phiên, hành động đi qua API và ghi nhật ký.
- Màn hình **Ma trận phân quyền** trên PC: bảng 16 quyền theo 5 vai trò và bảng 11 trường cư dân chuẩn; thay đổi checkbox/select gọi API thật, ghi vào kho PC.
- Giữ trang tài khoản, tạo/sửa/khóa và đổi mật khẩu của 7.1. Giao diện phân quyền chỉ mở từ PC với Super Admin.
- Biểu tượng khiên quấn xích vàng kế thừa `pc/web/icon.svg`, Android drawable `app_icon.png`.

## B. Kiến trúc lưu trữ PC-only

```text
PC Windows
 ├─ Launcher → FastAPI → API xác thực / phân quyền / nghiệp vụ
 ├─ RAM: token phiên + yêu cầu mobile (TTL 180 giây) + SQLite khi mở kho
 ├─ Disk PC: Data/quanlykp.vault (AES-256-GCM)
 ├─ Disk PC: Data/backups/*.kpbak (nếu tự sao lưu)
 └─ Giao diện PC và màn hình duyệt bảo mật
                       ↑↓ HTTPS LAN
Android (mobile con)
 └─ WebView không lưu kho cư dân / token lâu dài; mã yêu cầu và token chỉ RAM
```

Mở `--host 0.0.0.0` **chỉ** khi có chứng chỉ HTTPS và kiểm soát Windows Firewall, không công khai Internet.

**Hệ quả bắt buộc của yêu cầu “chỉ lưu trên PC”:** chỉ sao lưu trên cùng PC sẽ không chống được mất/hỏng ổ cứng hoặc mất cả máy. Đây là rủi ro dữ liệu hiện hữu cần PC dự phòng theo quyết định chính sách mới; không tự ý bật lưu trữ bên ngoài.

## C. Kiểm thử thực tế chạy trong môi trường hiện tại

Chạy:

```bash
PYTHONPATH=pc python -m pytest -q tests/test_foundation_accounts.py tests/test_stage2_permissions.py
node --check pc/web/app.js
python -m compileall -q pc
```

**Kết quả:** 17/17 bài kiểm thử API đạt; JS cú pháp hợp lệ; Python biên dịch cú pháp hợp lệ.

Các ca tiêu biểu:

1. Thiết lập Super Admin một lần; kho PC mã hóa không chứa họ tên rõ trên đĩa.
2. Mật khẩu Argon2id, thay đổi mật khẩu, khóa sau 5 lần sai.
3. Admin KP không được vượt cấp, tạo tài khoản ngoài tổ, sửa/xóa hồ sơ cư dân ngoài tổ.
4. Mobile xin đăng nhập chưa có token; thiết bị chưa được duyệt không thể xin cho phép phiên.
5. Duyệt thiết bị → duyệt phiên → lấy token thành công một lần; lần dùng lại challenge trả 404.
6. Chặn thiết bị → thu hồi phiên; từ chối đăng nhập → không cấp token.
7. Mobile không thể gọi API quản trị chỉ PC cho dù có token quản trị.
8. Thu hồi capability trên PC → API của người sử dụng bị chặn 403 ngay; hoàn quyền → API hoạt động lại.
9. Không cấp vượt vai trò, không thu hồi quyền Super Admin từ ma trận.
10. Viewer chỉ nhận CCCD/SĐT che; đổi một trường thành `hidden` thực sự loại khỏi JSON API.
11. Reviewer bị cấu hình `masked` không thể ghi đè CCCD đang bị che; sửa trường khác thì CCCD giữ nguyên.
12. Mở lại kho 7.1 với schema 7.2: giữ nguyên tài khoản, thêm permission matrix.
13. Nhật ký hash chain được xác minh hợp lệ.

**Các kiểm thử chưa đạt/không được thực hiện:** GUI end-to-end bằng trình duyệt thực; Windows installer trên Windows 10/11; biên dịch/ký APK Android; kết nối LAN với TLS doanh nghiệp; pentest, rà soát tuân thủ pháp lý, tải cao, thử mất điện/khôi phục. Các mục này KHÔNG được đánh dấu hoàn thành.

## D. Đối chiếu trực tiếp với chức năng gốc 6.3.1

| Mã / Mảng | 7.2 | Bằng chứng/giới hạn |
|---|---|---|
| F-001 — Đăng nhập | Hoạt động bản tài khoản + token RAM + lockout | **Chưa có 2FA độc lập theo chuẩn** |
| F-002 — Che dữ liệu | Enforce phía server cho danh sách cư dân | Chưa chứng minh toàn bộ mọi màn hình |
| F-003 — Chống chụp | Android nguồn bật `FLAG_SECURE` | Chưa build/thử trên thiết bị thật |
| F-004 — Mã hóa CSDL | AES-GCM vault file snapshot trên PC | Khác SQLCipher gốc; chưa đánh giá tải lớn |
| F-006 — Nhật ký | Chuỗi hash, xác minh nội bộ | Không bất biến tuyệt đối với quản trị OS |
| F-007 — Duyệt thiết bị | Đã có duyệt/chặn và bổ sung duyệt **mỗi phiên** | Không có hardware-bound key proof |
| F-009 — Xóa từ xa | **Không triển khai FCM/wipe**; không có CSDL mobile để xóa | Thu hồi phiên + chặn thiết bị |
| F-010 — Sao lưu | Chỉ PC, vault mã hóa | Chưa quy trình khôi phục hai quản trị viên |
| F-011–F-012 — Tài khoản | CRUD, đổi/reset mật khẩu, phạm vi quản lý, giao diện | Chưa import danh sách cán bộ hàng loạt |
| F-013 — Ma trận vai trò | Đã có 16 capability có thể thu hồi/khôi phục, kiểm tra qua server | Không cho nâng vượt cấp |
| F-014 — Quyền theo trường | Đã có 11 trường chuẩn, API đọc/ghi chịu ràng buộc | Chưa áp cho trường tùy chỉnh và toàn bộ OCR |
| F-015–F-031 | Giữ mã nguồn từ 7.1, chưa nâng cấp/kiểm thử lại ở GĐ2 | Không coi là hoàn tất |

## E. Các rủi ro cần xử lý TRƯỚC khi dùng dữ liệu dân cư thật

**Mức ưu tiên cao**

- Xác thực hai yếu tố độc lập FIDO2/WebAuthn hoặc phương án được phê duyệt không phụ thuộc cloud; phân biệt với cơ chế phê duyệt hai người.
- Gia cố chống brute-force theo IP, cơ chế giới hạn tốc độ, quy trình phục hồi khẩn cấp và chính sách vòng đời tài khoản.
- Chứng thực danh tính thiết bị bằng mật mã nếu được phép lưu khóa riêng phần cứng trong Android Keystore (đây là khóa xác thực, không phải hồ sơ dân cư; cần quyết định riêng của chủ hệ thống).
- Kiểm thử an toàn đầu cuối, kiểm tra TLS và toàn bộ đường dẫn xuất tệp, truy xuất metadata, quyền theo field/row tại các API ngoài phạm vi giai đoạn 2.
- Ký APK và tạo bộ cài EXE/MSI; kiểm thử trên Windows, Android thật.
- Khả năng khôi phục kho dữ liệu khi mất ổ đĩa: quy tắc hiện tại cấm bản sao ngoài PC, rủi ro mất dữ liệu không thể triệt tiêu nếu không thay chính sách.

**Kết luận nghiệm thu:** Có mã nguồn chức năng + kiểm thử API cho bộ khung và hệ thống tài khoản / kiểm soát phiên mobile. Chưa nghiệm thu sản phẩm thương mại hay triển khai chính thức.
