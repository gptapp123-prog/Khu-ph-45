using System;
using System.Collections.Generic;
using System.Data.SQLite;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  private static RecordPage<T> ReadOperationalPage<T>(SQLiteConnection c,SQLiteTransaction tx,string sql,string order,
    int page,int size,Func<SQLiteDataReader,T> map,params SQLiteParameter[] parameters) {
   if(page<0||size<1||size>100)throw new InvalidOperationException("Trang không hợp lệ.");
   using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM ("+sql+")",c,tx)) {
    cmd.Parameters.AddRange(parameters);
    long count=Convert.ToInt64(cmd.ExecuteScalar()),pages=count==0?0:(count-1)/size+1;
    int current=(int)Math.Min(page,Math.Max(0,pages-1));
    var result=new RecordPage<T>{PageIndex=current,PageSize=size,TotalCount=count};
    cmd.CommandText="SELECT * FROM ("+sql+") ORDER BY "+order+" LIMIT @limit OFFSET @offset";
    cmd.Parameters.Add(P("@limit",size));cmd.Parameters.Add(P("@offset",(long)current*size));
    using(var r=cmd.ExecuteReader())while(r.Read())result.Items.Add(map(r));
    return result;
   }
  }
  private const string AuditPageSql=@"SELECT l.utc,a.username,l.event_code,l.entity_type,l.result,l.id
    FROM audit_log l JOIN accounts a ON a.id=l.actor_id WHERE (@all=1 OR a.area_id=@area)";
  public static RecordPage<AuditRow> GetAuditPage(Session actor,int page,int size=50) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"I03","read",actor==null?null:actor.AreaId);
    var result=ReadOperationalPage(c,tx,AuditPageSql,"1 DESC,6 DESC",page,size,
     r=>new AuditRow{Timestamp=r.GetString(0),Actor=r.GetString(1),EventCode=r.GetString(2),EntityType=r.GetString(3),Result=r.GetString(4)},
     P("@all",actor.Role=="ROOT_ADMIN"?1:0),P("@area",actor.AreaId));
    tx.Commit();return result;
   }
  }
  private const string QualityPageSql=@"SELECT 'Hộ',h.code,h.id,'B01','Cần kiểm tra',
    'Chưa có Chủ hộ đang liên kết; không tự tạo hoặc thay chủ hộ.',0 AS category
    FROM households h WHERE @households=1 AND h.area_id=@area AND NOT EXISTS
     (SELECT 1 FROM household_memberships m WHERE m.household_id=h.id AND m.relationship='Chủ hộ' AND m.end_utc IS NULL)
    UNION ALL SELECT 'Nhân khẩu',p.full_name,p.id,'B02','Cần kiểm tra',
    'Chưa có liên kết hộ đang hiệu lực; không tự ghép theo tên.',1
    FROM people p WHERE @people=1 AND p.area_id=@area AND NOT EXISTS
     (SELECT 1 FROM household_memberships m WHERE m.person_id=p.id AND m.end_utc IS NULL)
    UNION ALL SELECT 'Nhân khẩu',p.full_name,p.id,'B02','Cảnh báo',
    'Trùng tên sau chuẩn hóa dấu; chỉ gợi ý đối chiếu, KHÔNG tự gộp.',2
    FROM people p WHERE @people=1 AND p.area_id=@area AND p.search_name<>'' AND EXISTS
     (SELECT 1 FROM people other WHERE other.id<>p.id AND other.area_id=p.area_id AND other.search_name=p.search_name)";
  public static RecordPage<DataQualityIssue> GetDataQualityPage(Session actor,int page,int size=50) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"I08","read",actor==null?null:actor.AreaId);
    var result=ReadOperationalPage(c,tx,QualityPageSql,"7,2,3",page,size,
     r=>new DataQualityIssue{Kind=r.GetString(0),Subject=r.GetString(1),TargetId=r.GetString(2),TargetFeature=r.GetString(3),Severity=r.GetString(4),Finding=r.GetString(5)},
     P("@households",HasPermission(c,tx,actor,"B01","read",actor.AreaId)?1:0),
     P("@people",HasPermission(c,tx,actor,"B02","read",actor.AreaId)?1:0),P("@area",actor.AreaId));
    tx.Commit();return result;
   }
  }
  private const string ResidencyPageSql=@"SELECT rc.id,rc.occurred_utc,p.full_name,origin.code,dest.code,
    rc.relationship,rc.reason,rc.person_id,rc.from_household_id,rc.to_household_id
    FROM residency_changes rc JOIN people p ON p.id=rc.person_id AND p.area_id=rc.area_id
    JOIN households origin ON origin.id=rc.from_household_id AND origin.area_id=rc.area_id
    JOIN households dest ON dest.id=rc.to_household_id AND dest.area_id=rc.area_id
    WHERE rc.area_id=@area AND (p.search_name LIKE @q ESCAPE '\' OR p.full_name LIKE @q ESCAPE '\'
     OR origin.code LIKE @q ESCAPE '\' OR dest.code LIKE @q ESCAPE '\' OR rc.search_reason LIKE @q ESCAPE '\')";
  public static RecordPage<ResidencyChangeRow> GetResidencyChangePage(Session actor,string term,int page,int size=50) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"B03","read",actor==null?null:actor.AreaId);
    var result=ReadOperationalPage(c,tx,ResidencyPageSql,"2 DESC,1 DESC",page,size,
     r=>new ResidencyChangeRow{Id=r.GetString(0),Timestamp=r.GetString(1),PersonName=r.GetString(2),FromCode=r.GetString(3),ToCode=r.GetString(4),
      Relationship=r.GetString(5),Reason=r.GetString(6),PersonId=r.GetString(7),FromHouseholdId=r.GetString(8),ToHouseholdId=r.GetString(9)},
     P("@area",actor.AreaId),P("@q",SearchPattern(NormalizeSearch(term))));
    tx.Commit();return result;
   }
  }
 }
}
