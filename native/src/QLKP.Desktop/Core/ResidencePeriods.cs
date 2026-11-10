using System;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace QLKP.Desktop.Core {
 public sealed class ResidencePeriodRow {
  public string Id {get; internal set;}
  public string Kind {get; internal set;}
  public string Address {get; internal set;}
  public string ValidFrom {get; internal set;}
  public string ValidUntil {get; internal set;}
  public string RecordedUtc {get; internal set;}
  public string KindDisplay {get{return Kind=="thuong_tru"?"Thường trú (khai báo)":"Tạm trú (khai báo)";}}
  public string ValidFromDisplay {get{return DisplayLabels.Date(ValidFrom);}}
  public string ValidUntilDisplay {get{return DisplayLabels.Date(ValidUntil);}}
  public string RecordedDisplay {get{return DisplayLabels.UtcStamp(RecordedUtc);}}
  public string StatusDisplay {
   get {
    string today=DataStore.LocalDateKey;
    if(String.CompareOrdinal(ValidFrom,today)>0)return "Chưa hiệu lực";
    return String.IsNullOrEmpty(ValidUntil)||String.CompareOrdinal(ValidUntil,today)>0?"Đang hiệu lực":"Đã kết thúc";
   }
  }
 }

 public static partial class DataStore {
  internal static string LocalDateKey {get{return DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}}

  static void RequireResidencePerson(SQLiteConnection c,SQLiteTransaction tx,Session actor,string personId,string action) {
   Require(c,tx,actor,"B02","read",actor==null?null:actor.AreaId);
   Require(c,tx,actor,"B03",action,actor.AreaId);
   if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM people WHERE id=@id AND area_id=@area",
      P("@id",personId),P("@area",actor.AreaId)))!=1)
    throw new UnauthorizedAccessException("Hồ sơ nhân khẩu nằm ngoài phạm vi được cấp.");
  }

  public static RecordPage<ResidencePeriodRow> GetResidencePeriods(Session actor,string personId,int page,int size=25) {
   const string sql=@"SELECT p.id,p.kind,p.address,p.valid_from,
      COALESCE(p.valid_until,(SELECT MIN(n.valid_from) FROM person_residence_periods n
        WHERE n.person_id=p.person_id AND n.kind=p.kind AND n.area_id=p.area_id AND n.valid_from>p.valid_from)),
      p.recorded_utc
      FROM person_residence_periods p WHERE p.person_id=@person AND p.area_id=@area";
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequireResidencePerson(c,tx,actor,personId,"read");
    var rows=ReadOperationalPage(c,tx,sql,"4 DESC,1 DESC",page,size,r=>new ResidencePeriodRow {
     Id=r.GetString(0),Kind=r.GetString(1),Address=r.GetString(2),ValidFrom=r.GetString(3),
     ValidUntil=r.IsDBNull(4)?null:r.GetString(4),RecordedUtc=r.GetString(5)
    },P("@person",personId),P("@area",actor.AreaId));
    tx.Commit();return rows;
   }
  }

  public static string AddResidencePeriod(Session actor,string personId,string kind,string address,string fromDisplay,string untilDisplay) {
   if(kind!="thuong_tru"&&kind!="tam_tru")throw new InvalidOperationException("Chọn loại cư trú.");
   address=(address??"").Trim();
   if(address.Length<1||address.Length>255||address.Any(Char.IsControl))
    throw new InvalidOperationException("Địa chỉ phải từ 1 đến 255 ký tự hợp lệ.");
   string start=DisplayLabels.IsoDate(fromDisplay),end=DisplayLabels.IsoDate(untilDisplay);
   if(start.Length==0||String.CompareOrdinal(start,LocalDateKey)>0)
    throw new InvalidOperationException("Nhập ngày bắt đầu không sau hôm nay.");
   if(end.Length!=0&&String.CompareOrdinal(end,start)<=0)
    throw new InvalidOperationException("Mốc kết thúc phải sau ngày bắt đầu.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequireResidencePerson(c,tx,actor,personId,"read");
    Require(c,tx,actor,"B03","create",actor.AreaId);
    if(Convert.ToInt32(Scalar(c,tx,@"SELECT COUNT(*) FROM person_residence_periods
       WHERE person_id=@person AND area_id=@area AND kind=@kind
       AND (valid_from>=@start OR valid_until>@start)",P("@person",personId),P("@area",actor.AreaId),
       P("@kind",kind),P("@start",start)))!=0)
     throw new InvalidOperationException("Kỳ mới phải nối tiếp và không chồng lấn kỳ cùng loại.");
    string id=Id();
    Exec(c,tx,@"INSERT INTO person_residence_periods(id,area_id,person_id,kind,address,valid_from,valid_until,actor_id,recorded_utc)
      VALUES(@id,@area,@person,@kind,@address,@start,@end,@actor,@utc)",P("@id",id),P("@area",actor.AreaId),
      P("@person",personId),P("@kind",kind),P("@address",address),P("@start",start),
      P("@end",end.Length==0?null:end),P("@actor",actor.Id),P("@utc",Now));
    Audit(c,tx,actor.Id,"RESIDENCE_PERIOD_ADDED","residence_period",id);
    tx.Commit();return id;
   }
  }
 }
}
