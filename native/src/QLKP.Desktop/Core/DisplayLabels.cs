using System;
using System.Globalization;
namespace QLKP.Desktop.Core {
 public static class DisplayLabels {
  public static string Role(string value) {
   return value=="ROOT_ADMIN"?"Quản trị hệ thống":value=="KP_ADMIN"?"Quản trị khu phố":value=="ASSIGNED_USER"?"Người dùng nghiệp vụ":value??"";
  }
  public static string Date(string value) {
   DateTime date;
   if(String.IsNullOrWhiteSpace(value))return "—";
   return DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?date.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture):value;
  }
  public static string UtcStamp(string value) {
   DateTimeOffset stamp;
   if(String.IsNullOrWhiteSpace(value))return "—";
   return DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out stamp)?
    stamp.UtcDateTime.ToString("dd-MM-yyyy HH:mm",CultureInfo.InvariantCulture)+" UTC":value;
  }
  public static string IsoDate(string value) {
   if(String.IsNullOrWhiteSpace(value))return "";
   DateTime date;
   if(!DateTime.TryParseExact(value.Trim(),"dd-MM-yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))
    throw new InvalidOperationException("Ngày phải theo dd-MM-yyyy, ví dụ 06-11-2026.");
   return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  }
 }
}
