using System.Collections.Generic;
namespace QLKP.Desktop.Core {
 public static class FeatureCatalog {
  public static readonly string[] GroupNames = {"A · Điều hành", "B · Dân cư", "C · Văn bản", "D · Tổ chức", "E · An sinh", "F · An toàn, đô thị", "G · Phản ánh", "H · Báo cáo", "I · Hệ thống"};
  public static readonly List<Feature> All = new List<Feature>{
   F("A01","Bàn làm việc"),F("A02","Nhiệm vụ và tiến độ"),F("A03","Kế hoạch và chỉ tiêu"),F("A04","Lịch công tác"),F("A05","Theo dõi tồn đọng"),F("A06","Bàn giao nhiệm kỳ"),
   F("B01","Hộ gia đình"),F("B02","Nhân khẩu"),F("B03","Biến động dân cư"),F("B04","Địa bàn và địa chỉ"),F("B05","Nhà ở và công trình"),F("B06","Nhà trọ và cơ sở lưu trú"),F("B07","Cơ sở kinh doanh"),
   F("C01","Văn bản đến"),F("C02","Văn bản đi"),F("C03","Hồ sơ công việc"),F("C04","Họp dân và hội nghị"),F("C05","Biên bản và quyết nghị"),F("C06","Kho mẫu biểu"),
   F("D01","Nhân sự khu phố"),F("D02","Chi bộ và đảng viên"),F("D03","Ban Công tác Mặt trận"),F("D04","Đoàn thể và chi hội"),F("D05","Cộng tác viên"),
   F("E01","An sinh xã hội"),F("E02","Người có công"),F("E03","Người cao tuổi"),F("E04","Trẻ em và học sinh"),F("E05","Khuyến học"),F("E06","Y tế và dân số"),F("E07","Việc làm và đào tạo"),
   F("F01","An ninh, trật tự"),F("F02","Phòng cháy chữa cháy"),F("F03","Thiên tai và cứu nạn"),F("F04","Môi trường"),F("F05","Hạ tầng và công trình công cộng"),F("F06","Nghĩa vụ quân sự"),F("F07","Văn hóa và phong trào"),
   F("G01","Tiếp nhận phản ánh"),F("G02","Hòa giải cơ sở"),F("G03","Tuyên truyền và vận động"),F("G04","Phối hợp phường/cơ quan"),
   F("H01","Báo cáo định kỳ"),F("H02","Báo cáo đột xuất"),F("H03","Thống kê nhiều chiều"),F("H04","Tổng hợp tiến độ"),F("H05","Xuất biểu mẫu"),
   F("I01","Tài khoản"),F("I02","Vai trò và phạm vi quyền"),F("I03","Nhật ký và truy vết"),F("I04","Sao lưu/khôi phục"),F("I05","OCR và QR"),F("I06","Đồng bộ PC–Android"),F("I07","Danh mục dùng chung"),F("I08","Chất lượng dữ liệu"),F("I09","Cập nhật/phiên bản")
  };
  private static Feature F(string code,string name){return new Feature {Code=code,Name=name};}
 }
}
