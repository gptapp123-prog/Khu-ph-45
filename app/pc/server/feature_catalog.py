"""Danh mục trạng thái chức năng — chỉ mô tả bằng chứng hiện có, không suy diễn hoàn tất."""

STATUS_LABELS={
    'foundation':'Đã có nền và kiểm thử mã',
    'partial':'Đã có một phần',
    'planned':'Chưa triển khai',
    'decision':'Cần quyết định nghiệp vụ',
}

_ROWS=[
('F-001','Đăng nhập và xác thực','Bảo mật','partial','accounts','Có tài khoản, khóa mật khẩu và phiên RAM; chưa có 2FA độc lập.'),
('F-002','Che dữ liệu theo quyền','Bảo mật','foundation','permissions','Server che dữ liệu trên 11 trường; còn phải rà soát mọi API.'),
('F-003','Chặn ảnh màn hình mobile','Bảo mật','partial','devices','Có FLAG_SECURE trong nguồn Android; chưa kiểm thử APK thật.'),
('F-004','Mã hóa kho dữ liệu PC','Bảo mật','foundation','audit','Kho KPHO2 AES-GCM đã có kiểm thử mã hóa và chống sửa.'),
('F-005','Mã hóa ảnh giấy tờ','Bảo mật','planned',None,'Chưa có luồng ảnh giấy tờ hoàn chỉnh.'),
('F-006','Nhật ký hành động','Bảo mật','foundation','audit','Có audit hash-chain và kiểm tra chuỗi.'),
('F-007','Duyệt thiết bị và phiên mobile','Bảo mật','partial','devices','Có duyệt/chặn và phê duyệt phiên; chưa ràng buộc khóa phần cứng.'),
('F-008','Phát hiện root/emulator/hook','Bảo mật','planned',None,'Chưa kiểm thử thiết bị Android thật.'),
('F-009','Thu hồi truy cập mobile','Bảo mật','partial','devices','Có thu hồi phiên; mobile không giữ kho để remote wipe.'),
('F-010','Sao lưu và khôi phục','Bảo mật','partial','audit','Backup/checksum/restore offline đã có; chưa có workflow hai người.'),
('F-011','Quản lý tài khoản','Tài khoản & quyền','foundation','accounts','Có API và giao diện CRUD; chưa nhập tài khoản hàng loạt.'),
('F-012','Thiết lập tài khoản','Tài khoản & quyền','partial','accounts','Có màn hình; chưa nghiệm thu GUI Windows thật.'),
('F-013','Ma trận quyền thao tác','Tài khoản & quyền','foundation','permissions','Có 16 capability, 5 vai trò và cưỡng chế phía server.'),
('F-014','Quyền theo trường','Tài khoản & quyền','partial','permissions','Có 11 trường; hồ sơ gốc ghi 12 trường cần đối chiếu.'),
('F-015','Thêm cột tùy chỉnh','Cấu trúc dữ liệu','partial','fields','Có định nghĩa trường; chưa hoàn tất nhập/sửa giá trị.'),
('F-016','Phát hiện cột mới','Cấu trúc dữ liệu','planned',None,'Chưa có kiểm thử end-to-end.'),
('F-017','Cấu hình bảo mật','Bảo mật','partial','permissions','Có một phần chính sách; chưa đủ màn hình và kiểm thử.'),
('F-018','Nạp Excel','Nhập liệu & OCR','partial','import','Có parser và preview; chưa nghiệm thu đủ tình huống.'),
('F-019','Nạp CSV','Nhập liệu & OCR','partial','import','Có parser và preview; chưa nghiệm thu đủ tình huống.'),
('F-020','OCR ảnh CCCD','Nhập liệu & OCR','partial','import','Có mã OCR; chưa đo độ chính xác trên bộ dữ liệu chuẩn.'),
('F-021','Nạp PDF','Nhập liệu & OCR','partial','import','Có parser PDF text; PDF scan chưa nghiệm thu.'),
('F-022','Phát hiện trường và độ tin cậy','Nhập liệu & OCR','partial','import','Có nền preview; chưa benchmark confidence.'),
('F-023','Quét nhiều ảnh','Nhập liệu & OCR','planned',None,'Chưa kiểm thử luồng quét hàng loạt Android.'),
('F-024','Gợi ý và lưu quy tắc lọc','Nhập liệu & OCR','planned',None,'Chưa có bằng chứng triển khai đủ quy tắc.'),
('F-025','Chọn trường dữ liệu','Ánh xạ & xuất','partial','export','Có chọn trường khi xuất; luồng nhập chưa hoàn chỉnh.'),
('F-026','Ánh xạ nguồn sang đích','Ánh xạ & xuất','partial','import','Có mã liên quan; chưa kiểm thử đủ cột mới.'),
('F-027','Thêm cột hoặc đổi thứ tự','Ánh xạ & xuất','decision',None,'Đặc tả gốc mâu thuẫn giữa các phần.'),
('F-028','Đổi thứ tự hoặc lưu quy tắc','Ánh xạ & xuất','decision',None,'Đặc tả gốc mâu thuẫn giữa các phần.'),
('F-029','Bảng dữ liệu dân cư','Dữ liệu dân cư','partial','residents','Có danh sách và CRUD; chưa hoàn tất chỉnh ô/trùng/lệch.'),
('F-030','Mẫu nhập/xuất','Ánh xạ & xuất','partial','templates','Có API và UI lưu mẫu; chưa nghiệm thu tái sử dụng toàn luồng.'),
('F-031','Xuất chọn trường và dòng','Ánh xạ & xuất','partial','export','Có API xuất; chưa kiểm thử bảo mật mọi bộ lọc.'),
]

FEATURES=[{'id':i,'name':n,'group':g,'status':s,'page':p,'evidence':e} for i,n,g,s,p,e in _ROWS]

MODULES=[
 {'id':'population','name':'Dân cư và hộ gia đình','description':'Nhân khẩu, hộ, tìm kiếm và liên kết hồ sơ.','page':'residents','status':'active'},
 {'id':'ingestion','name':'Nhập liệu và OCR','description':'Excel, CSV, PDF, ảnh và bước xác nhận trước khi ghi.','page':'import','status':'partial'},
 {'id':'governance','name':'Tài khoản và phân quyền','description':'Vai trò, tổ, trường dữ liệu, thiết bị và nhật ký.','page':'accounts','status':'active'},
 {'id':'territory','name':'Địa bàn, đường và số nhà','description':'Khung phân hệ mở rộng; chưa được lập trình trong bản này.','page':None,'status':'planned'},
 {'id':'facilities','name':'Cơ sở và điểm kinh doanh','description':'Khung phân hệ mở rộng; chưa được lập trình trong bản này.','page':None,'status':'planned'},
 {'id':'work','name':'Công việc và phản ánh','description':'Khung phân hệ mở rộng; chưa được lập trình trong bản này.','page':None,'status':'planned'},
]

def catalog():
    counts={key:0 for key in STATUS_LABELS}
    for row in FEATURES:counts[row['status']]+=1
    return {'version':'7.5','status_labels':STATUS_LABELS,'counts':counts,'features':FEATURES,'modules':MODULES}
