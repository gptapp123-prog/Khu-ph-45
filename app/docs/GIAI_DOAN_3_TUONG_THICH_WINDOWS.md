# GIAI ĐOẠN 3 — WINDOWS 7 TỐI THIỂU / QUẢN LÝ KHU PHỐ 7.3

Ngày: 27/10/2026  
Mục tiêu: Giữ **một mã nguồn nghiệp vụ**, thêm nhánh tương thích hệ điều hành tối thiểu Windows 7 SP1, không bắt buộc mọi PC dùng Windows 7. Kế thừa nguyên vẹn hệ thống tài khoản và phân quyền 7.2.

## A — ĐÃ LÀM ĐƯỢC

| ID | Hạng mục | File liên quan | Bằng chứng / phạm vi |
|---|---|---|---|
| W73-01 | Phát hiện OS + Python, tách môi trường | `pc/win_compat.py` | Test giả lập ma trận Vista/Win7/Win8/Win8.1/Win10; chưa test Windows vật lý |
| W73-02 | Script cài chọn đúng requirements, hỗ trợ thư mục cài khác | `Cai_dat_PC.bat`, `requirements-win7-legacy.txt` | Kiểm tra cú pháp/chuỗi cài và test tĩnh; chưa chạy `.bat` trên Windows |
| W73-03 | Chuyển kho mã hóa cũ sang định dạng có thể nạp bởi Python 3.8 | `pc/server/crypto.py` | Test tạo/mở lại KPHO2, chống sửa file, Unicode, nhập KPHO1 rồi chuyển KPHO2; không có SQLite rõ trên đĩa |
| W73-04 | Tương thích API model Pydantic 1 và 2 | `pc/server/model_compat.py`, `pc/server/app.py` | Test mô hình Pydantic v1, kiểm thử API Pydantic v2; chưa chạy FastAPI legacy trên Win7 |
| W73-05 | Không làm mất kiểm soát đã có | `tests/test_foundation_accounts.py`, `tests/test_stage2_permissions.py` | Chạy lại kiểm thử 7.1/7.2, 17 ca đạt |
| W73-06 | Kiểm thử tính di động và cài đặt nguồn | `tests/test_stage3_windows.py`, `tests/fixtures/*` | 6 ca Stage3 đạt; fixture nâng cấp được mang theo trong ZIP |

**Kết quả kiểm thử phiên 7.3:** `23 passed`, kiểm tra cú pháp JS và Python thành công trên môi trường Linux/Python 3.13. Không có kết quả cài Windows 7, Windows 8/8.1, Windows 10/11 thật.

## B — VẤN ĐỀ CÒN CHƯA XỬ LÝ

1. Windows 7 hết hỗ trợ OS; Python 3.8 đã EOL, một số thư viện legacy cũ cũng có thể chứa lỗ hổng chưa vá. **Không khuyến nghị lưu dữ liệu cá nhân thực trên Windows 7.**
2. Cấu hình phụ thuộc Windows 7 chưa được chứng minh có đủ bánh xe cài đặt cho cả 32-bit/64-bit; không đưa ra xác nhận cài đặt thành công khi chưa có thử nghiệm thật.
3. PC Windows 7/8 chỉ được chạy nội bộ (`127.0.0.1`); nhánh mobile-LAN bị khóa nhằm không mở dịch vụ chứa dữ liệu dân cư trên OS hết bảo trì. Nếu bắt buộc Windows 7 làm máy chính phục vụ mobile, cần đánh giá lại rủi ro và kiểm thử riêng trước khi cấp quyền.
4. `KPHO2` xuất SQL dump rồi AES-GCM: hỗ trợ runtime cũ, nhưng hiệu năng/độ dài thời gian ghi trên dữ liệu lớn chưa được benchmark; chưa thực hiện kịch bản mất điện khi ghi dữ liệu.
5. Mật khẩu mở kho được nhập thủ công khi chạy; chưa có bộ cài EXE/MSI, chưa có ký số và tự động cài Python/thu viện offline.
6. 2FA độc lập vẫn chưa triển khai; phê duyệt phiên mobile của 7.2 vẫn không phải 2FA theo chuẩn.
7. Chưa chạy kiểm thử Windows GUI, trình duyệt Windows 7 cũ, thiết bị Android thật, LAN TLS và pentest.

## C — CÁC BƯỚC CHƯA THỰC HIỆN / THEO THỨ TỰ

1. **GĐ3.1** Dựng phòng thử máy Windows 7 SP1 x86/x64, Windows 8/8.1, Windows 10/11; xác nhận Python và yêu cầu thư viện thực tế, chỉnh các lỗi cụ thể.
2. **GĐ3.2** Quy trình cập nhật 7.2 → 7.3 với backup mã hóa trên PC, di trú dữ liệu và kiểm tra rollback; test mất điện, khóa tài khoản, người dùng Unicode.
3. **GĐ3.3** Ký và đóng gói EXE/MSI có nguồn thư viện offline, script kiểm tra môi trường; tạo APK Android release và kết nối LAN TLS trên Windows được hỗ trợ.
4. **GĐ4** Hoàn thiện 2FA độc lập, kiểm thử bảo mật API/GUI/mobile rồi mới triển khai các phân hệ kế tiếp. Không tự ý thêm chức năng ngoài quy trình.

## D — QUY TẮC BÁO CÁO CỐ ĐỊNH CHO MỌI LƯỢT LẬP TRÌNH

**1. Đã làm được**: nêu chức năng/tệp cụ thể, kết quả kiểm thử (thật, tĩnh, mô phỏng khác nhau).  
**2. Còn chưa xử lý**: liệt kê lỗi/rủi ro/tính năng chưa đạt và ảnh hưởng.  
**3. Các bước chưa thực hiện**: liệt kê đúng thứ tự triển khai kế tiếp; không đánh dấu đã làm khi chưa có bằng chứng.

Báo cáo phiên sau phải kế thừa phiên trước và ghi số phiên; không triển khai lại từ đầu.

## E — NGUỒN CHÍNH THỐNG DÙNG ĐỂ ĐÁNH GIÁ WIN7

- Python Windows documentation: https://docs.python.org/3/using/windows.html (Windows 7: Python 3.8).
- Python devguide, lifecycle: https://devguide.python.org/versions/ (Python 3.8 EOL 2024-10-07).
- Microsoft Lifecycle Windows 7: https://learn.microsoft.com/en-us/lifecycle/products/windows-7 (end of support).

**Ghi chú pháp lý/kỹ thuật:** `Cai_dat_PC.bat` hiện là script cài mã nguồn và thư viện, không phải bộ cài thương mại. Chưa xác minh độ an toàn theo chính sách dữ liệu nhạy cảm của đơn vị sử dụng.
