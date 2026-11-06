namespace QLKP.Desktop.Core {
 // Labels are presentation only. Classification codes are explicit data, never inferred from names.
 public static class ClassificationLabels {
  public static string Housing(string code) {
   switch(code) {
    case "nha_rieng":return "Nhà riêng";case "nha_cap4":return "Nhà cấp 4";
    case "can_ho":return "Căn hộ";case "phong_tro":return "Phòng trọ";
    case "nha_thue":return "Nhà thuê";case "nha_tap_the":return "Nhà tập thể";
    case "o_cung_than_nhan":return "Ở cùng thân nhân";case "chua_xac_minh":return "Chưa xác minh";
    default:return "Chưa phân loại";
   }
  }
  public static string Residency(string code) {
   switch(code) {
    case "thuong_tru":return "Thường trú";
    case "tam_tru":return "Tạm trú";
    case "luu_tru":return "Lưu trú";
    case "tam_vang":return "Tạm vắng";
    case "chua_xac_minh":return "Chưa đối chiếu";
    default:return "Chưa phân loại";
   }
  }
 }
}
