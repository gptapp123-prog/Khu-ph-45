using System;
using System.Data.SQLite;
using System.Linq;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  public static void UpdateHouseholdAddress(Session actor,string householdId,long expectedVersion,string address) {
   string value=(address??"").Trim();
   if(String.IsNullOrWhiteSpace(householdId)||expectedVersion<1)
    throw new InvalidOperationException("Thiếu ID hoặc phiên bản hộ.");
   if(value.Length<1||value.Length>300||value.Any(Char.IsControl))
    throw new InvalidOperationException("Địa chỉ phải từ 1–300 ký tự và không chứa ký tự điều khiển.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"B01","read",actor==null?null:actor.AreaId);
    Require(c,tx,actor,"B01","update",actor.AreaId);
    using(var cmd=new SQLiteCommand(@"UPDATE households SET address=@address,search_address=@search,
      version=version+1,updated_utc=@utc WHERE id=@id AND area_id=@area AND version=@version",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@address",value),P("@search",NormalizeSearch(value)),P("@utc",Now),
      P("@id",householdId),P("@area",actor.AreaId),P("@version",expectedVersion)});
     if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Hộ đã thay đổi hoặc không thuộc phạm vi. Tải lại trước khi sửa.");
    }
    Audit(c,tx,actor.Id,"UPDATE_HOUSEHOLD_ADDRESS","household",householdId);
    tx.Commit();
   }
  }
 }
}
