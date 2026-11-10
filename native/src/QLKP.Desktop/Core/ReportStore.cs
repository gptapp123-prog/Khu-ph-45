using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Text;
namespace QLKP.Desktop.Core {
 public sealed class ReportLine {
  public string Feature {get;set;}
  public string RecordId {get;set;}
  public string Code {get;set;}
  public string Title {get;set;}
  public string Status {get;set;}
  public string Date {get;set;}
  public string DateDisplay {get{return DisplayLabels.Date(Date);}}
  public long Count {get;set;}
  // For aggregate rows, never expose exact counts of 1-4 in the table.
  // Detail rows H02/H04 have a RecordId and retain their ordinary count of 1.
  public string CountDisplay { get {
    if(String.IsNullOrEmpty(RecordId) && Count>0 && Count<5) return "Ẩn (1–4)";
    return Count.ToString(CultureInfo.InvariantCulture);
  } }
  public string DueState { get {
    DateTime due;
    if(String.IsNullOrWhiteSpace(RecordId) || !DateTime.TryParseExact(Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out due))return "";
    if(Status=="Hoàn thành nội bộ")return "Đã hoàn thành";
    return due<DateTime.Today?"Quá hạn":"Trong hạn";
  } }
  public string DisplayFeature {get {var f=FeatureCatalog.All.FirstOrDefault(x=>x.Code==Feature);return f==null?Feature:Feature+" · "+f.Name;}}
 }
 public static partial class DataStore {
  // Reportable register codes are a hard-coded allowlist, not user-supplied SQL identifiers.
  // Exclude especially sensitive D02/E02/E04/F06: future release needs a separate privacy review.
  static readonly HashSet<string> ReportSources=new HashSet<string>(ModuleCatalog.Editable.Select(x=>x.Code)
    .Where(x=>x!="D02"&&x!="E02"&&x!="E04"&&x!="F06"),StringComparer.Ordinal);
  static readonly string[] DedicatedReportSources={"B03","F08"};

  static bool CanReadNativeReportSource(SQLiteConnection c,SQLiteTransaction tx,Session session,string feature) {
   return HasPermission(c,tx,session,feature,"read",session.AreaId)&&
    (feature!="F08"||HasPermission(c,tx,session,"B02","read",session.AreaId));
  }

  // The vehicle register has a last-updated timestamp. A period result counts vehicles
  // by that timestamp. Residency changes use the actual recorded event date.
  static string DedicatedReportQueries(IEnumerable<string> sources,bool preview,bool byStatus,bool filterStatus) {
   var sql=new StringBuilder();
   foreach(string source in sources) {
    string table=source=="B03"?"residency_changes":"vehicles";
    string date=source=="B03"?"occurred_utc":"updated_utc";
    string status=source=="B03"?"'Đã ghi nhận'":"CASE active WHEN 1 THEN 'Đang theo dõi' ELSE 'Ngừng theo dõi' END";
    string label=byStatus?status:(source=="B03"?"'Tổng biến động đã ghi nhận'":"'Tổng phương tiện cập nhật trong kỳ'");
    sql.Append(" UNION ALL SELECT '").Append(source).Append("',");
    if(preview)sql.Append("'','','',");
    sql.Append(label).Append(',');
    if(preview)sql.Append("'',");
    sql.Append("COUNT(*) FROM ").Append(table).Append(" WHERE area_id=@area AND ").Append(date)
       .Append(">=@from AND ").Append(date).Append("<@to");
    if(filterStatus)sql.Append(" AND ").Append(status).Append("=@status");
    if(byStatus&&source=="F08")sql.Append(" GROUP BY active");
    sql.Append(" HAVING COUNT(*)>0");
   }
   return sql.ToString();
  }
  public static List<string> GetReportableSources(Session s,string reportFeature){
   if(!new[]{"H01","H02","H03","H04","H05"}.Contains(reportFeature))throw new ArgumentException("Sai loại báo cáo.");
   using(var c=Open()){
    Require(c,null,s,reportFeature,"read",s==null?null:s.AreaId);
    var result=ReportSources.Where(x=>HasPermission(c,null,s,x,"read",s.AreaId)).OrderBy(x=>x).ToList();
    if(reportFeature=="H04")return result.Where(x=>x=="A02").ToList();
    if(reportFeature!="H02"){
     if(HasPermission(c,null,s,"B01","read",s.AreaId))result.Add("B01");
     if(HasPermission(c,null,s,"B02","read",s.AreaId))result.Add("B02");
     result.AddRange(DedicatedReportSources.Where(x=>CanReadNativeReportSource(c,null,s,x)));
    }
    return result.OrderBy(x=>x).ToList();
   }
  }
  static Tuple<string,string> ReportPeriod(string from,string to){
   DateTime a,b;
   if(!DateTime.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out a) ||
      !DateTime.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out b) ||
      a>b || (b-a).TotalDays>366)
    throw new InvalidOperationException("Kỳ báo cáo cần ngày đầu/cuối hợp lệ, tối đa 366 ngày.");
   return Tuple.Create(a.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),b.AddDays(1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
  }
  static string InClause(SQLiteCommand cmd,IEnumerable<string> codes){
   var list=new List<string>();int i=0;
   foreach(var code in codes){string key="@f"+(i++).ToString(CultureInfo.InvariantCulture);cmd.Parameters.Add(P(key,code));list.Add(key);}
   return list.Count==0?"('')":"("+String.Join(",",list)+")";
  }
  static List<ReportLine> ReadReport(SQLiteCommand cmd){
   var lines=new List<ReportLine>();using(var r=cmd.ExecuteReader())while(r.Read()){
    lines.Add(new ReportLine {Feature=r.GetString(0),RecordId=r.IsDBNull(1)?"":r.GetString(1),
      Code=r.IsDBNull(2)?"":r.GetString(2),Title=r.IsDBNull(3)?"":r.GetString(3),
      Status=r.IsDBNull(4)?"":r.GetString(4),Date=r.IsDBNull(5)?"":r.GetString(5),
      Count=r.IsDBNull(6)?0:r.GetInt64(6)});
   }return lines;
  }
  // Database enforces area scope. Caller's report right + source READ right are both mandatory.
  public static RecordPage<ReportLine> GetReportPage(Session s,string reportFeature,string from,string to,
    string sourceFilter,string statusFilter,int pageIndex,int pageSize=50){
   if(!new[]{"H01","H02","H03","H04","H05"}.Contains(reportFeature))throw new InvalidOperationException("Loại báo cáo không hợp lệ.");
   if(pageIndex<0||pageSize<1||pageSize>100)throw new InvalidOperationException("Phân trang không hợp lệ.");
   var period=ReportPeriod(from,to);
   using(var c=Open())using(var tx=c.BeginTransaction()){
    Require(c,tx,s,reportFeature,"read",s==null?null:s.AreaId);
    var sources=ReportSources.Where(x=>HasPermission(c,tx,s,x,"read",s.AreaId)).OrderBy(x=>x).ToList();
    bool detail=reportFeature=="H02"||reportFeature=="H04";
    var dedicated=detail?new List<string>():DedicatedReportSources.Where(x=>CanReadNativeReportSource(c,tx,s,x)).ToList();
    bool household=!detail&&HasPermission(c,tx,s,"B01","read",s.AreaId);
    bool people=!detail&&HasPermission(c,tx,s,"B02","read",s.AreaId);
    if(reportFeature=="H04")sources=sources.Where(x=>x=="A02").ToList();
    if(!String.IsNullOrWhiteSpace(sourceFilter)&&sourceFilter!="(Tất cả)"){
     bool permitted=sources.Contains(sourceFilter)||dedicated.Contains(sourceFilter)||(sourceFilter=="B01"&&household)||(sourceFilter=="B02"&&people);
     if(!permitted)throw new UnauthorizedAccessException("Không được xem dữ liệu nguồn này.");
     sources=sources.Where(x=>x==sourceFilter).ToList();
     dedicated=dedicated.Where(x=>x==sourceFilter).ToList();
     household=household&&sourceFilter=="B01";
     people=people&&sourceFilter=="B02";
    }
    bool byStatus=reportFeature!="H01";
    string projection=detail?"feature,id,record_code,title,status,"+
      (reportFeature=="H04"?"due_date":"substr(created_utc,1,10)")+",1":
       "feature,'','','',"+(byStatus?"status":"'Tổng hồ sơ'")+",'',COUNT(*)";
    string where="area_id=@area AND feature IN {IN} AND created_utc>=@from AND created_utc<@to";
    if(reportFeature=="H04"){
     // Include tasks overdue before the period, not only tasks whose deadlines fall within it.
     where="area_id=@area AND feature IN {IN} AND due_date<>'' AND "+
      "((due_date>=@from AND due_date<@to) OR (due_date<@from AND status<>'Hoàn thành nội bộ'))";
    }
    if(!String.IsNullOrWhiteSpace(statusFilter))where+=" AND status=@status";
    string grouping=detail?"":(byStatus?" GROUP BY feature,status":" GROUP BY feature");
    string ordering=detail?" ORDER BY 6 DESC,2 DESC":" ORDER BY 1,5";
    var result=new RecordPage<ReportLine>{PageIndex=pageIndex,PageSize=pageSize};
    using(var cmd=new SQLiteCommand("",c,tx)){
     cmd.Parameters.Add(P("@area",s.AreaId));cmd.Parameters.Add(P("@from",period.Item1));cmd.Parameters.Add(P("@to",period.Item2));
     if(!String.IsNullOrWhiteSpace(statusFilter))cmd.Parameters.Add(P("@status",statusFilter.Trim()));
     string sqlWhere=where.Replace("{IN}",InClause(cmd,sources));
     string baseQuery="SELECT "+projection+" FROM module_records WHERE "+sqlWhere+grouping;
     if(household){
      baseQuery+=" UNION ALL SELECT 'B01','','','',"+(byStatus?"status":"'Tổng hộ'")+",'',COUNT(*) FROM households "+
       "WHERE area_id=@area AND created_utc>=@from AND created_utc<@to"+
       (String.IsNullOrWhiteSpace(statusFilter)?"":" AND status=@status")+(byStatus?" GROUP BY status":"");
     }
     if(people && (String.IsNullOrWhiteSpace(statusFilter)||statusFilter.Trim()=="Tổng nhân khẩu")){
      baseQuery+=" UNION ALL SELECT 'B02','','','','Tổng nhân khẩu','',COUNT(*) FROM people "+
       "WHERE area_id=@area AND created_utc>=@from AND created_utc<@to HAVING COUNT(*)>0";
     }
     baseQuery+=DedicatedReportQueries(dedicated,true,byStatus,!String.IsNullOrWhiteSpace(statusFilter));
     cmd.CommandText="SELECT COUNT(*) FROM ("+baseQuery+")";
     result.TotalCount=Convert.ToInt64(cmd.ExecuteScalar());
     if(result.TotalCount==0){tx.Commit();return result;}
     long pages=(result.TotalCount+pageSize-1)/pageSize;
     if(pageIndex>=pages)result.PageIndex=(int)(pages-1);
     cmd.Parameters.Add(P("@limit",pageSize));cmd.Parameters.Add(P("@offset",result.PageIndex*pageSize));
     cmd.CommandText="SELECT * FROM ("+baseQuery+") "+ordering+" LIMIT @limit OFFSET @offset";
     result.Items=ReadReport(cmd);
    }
    tx.Commit();return result;
   }
  }
  // H05 exports aggregate rows only, shared by CSV and XLSX.
  // CSV is limited to 10,000 aggregate rows and has formula injection protection.
  static string SafeCsv(string value){
   value=(value??"").Replace("\r"," ").Replace("\n"," ").Trim();
   if(value.Length>500)value=value.Substring(0,500);
   if(value.Length>0 && "=+-@\t".IndexOf(value[0])>=0)value="'"+value;
   return "\""+value.Replace("\"","\"\"")+"\"";
  }
  public static string ExportReportAggregateCsv(Session s,string from,string to,string sourceFilter,string statusFilter){
   var sb=new StringBuilder("Ma_khu_pho,Ma_phan_he,Trang_thai,So_ho_so_hoac_an_duoi_5,Tu_ngay,Den_ngay,Loai_so_lieu\r\n");
   foreach(var row in AggregateRows(s,from,to,sourceFilter,statusFilter))
    sb.Append(String.Join(",",row.Select(v=>SafeCsv(Convert.ToString(v,CultureInfo.InvariantCulture))))).Append("\r\n");
   return sb.ToString();
  }
  public static void ExportReportAggregateXlsx(Session s,string from,string to,string sourceFilter,string statusFilter,string path) {
   var rows=AggregateRows(s,from,to,sourceFilter,statusFilter);
   rows.Insert(0,new object[]{"Mã khu phố","Phân hệ","Trạng thái","Số hồ sơ","Từ ngày","Đến ngày","Loại số liệu"});
   XlsxWorkbook.Save(path,new Dictionary<string,IEnumerable<object[]>>{{"Tổng hợp",rows}});
  }
  static List<object[]> AggregateRows(Session s,string from,string to,string sourceFilter,string statusFilter){
   var period=ReportPeriod(from,to);
   using(var c=Open())using(var tx=c.BeginTransaction()){
    Require(c,tx,s,"H05","read",s.AreaId);Require(c,tx,s,"H05","export",s.AreaId);
    var sources=ReportSources.Where(x=>HasPermission(c,tx,s,x,"read",s.AreaId)).OrderBy(x=>x).ToList();
    var dedicated=DedicatedReportSources.Where(x=>CanReadNativeReportSource(c,tx,s,x)).ToList();
    bool household=HasPermission(c,tx,s,"B01","read",s.AreaId);
    bool people=HasPermission(c,tx,s,"B02","read",s.AreaId);
    if(!String.IsNullOrWhiteSpace(sourceFilter)&&sourceFilter!="(Tất cả)"){
     if(!sources.Contains(sourceFilter)&&!dedicated.Contains(sourceFilter)&&!(sourceFilter=="B01"&&household)&&!(sourceFilter=="B02"&&people))
      throw new UnauthorizedAccessException("Không có quyền dữ liệu nguồn xuất báo cáo.");
     sources=sources.Where(x=>x==sourceFilter).ToList();
     dedicated=dedicated.Where(x=>x==sourceFilter).ToList();
     household=household&&sourceFilter=="B01";people=people&&sourceFilter=="B02";
    }
    var rows=new List<object[]>();
    using(var cmd=new SQLiteCommand("",c,tx)){
     cmd.Parameters.Add(P("@area",s.AreaId));cmd.Parameters.Add(P("@from",period.Item1));cmd.Parameters.Add(P("@to",period.Item2));
     if(!String.IsNullOrWhiteSpace(statusFilter))cmd.Parameters.Add(P("@status",statusFilter.Trim()));
     string where="area_id=@area AND feature IN "+InClause(cmd,sources)+" AND created_utc>=@from AND created_utc<@to";
     if(!String.IsNullOrWhiteSpace(statusFilter))where+=" AND status=@status";
     string query="SELECT feature,status,COUNT(*) FROM module_records WHERE "+where+" GROUP BY feature,status";
     if(household)query+=" UNION ALL SELECT 'B01',status,COUNT(*) FROM households WHERE area_id=@area "+
       "AND created_utc>=@from AND created_utc<@to"+
       (String.IsNullOrWhiteSpace(statusFilter)?"":" AND status=@status")+" GROUP BY status";
     if(people && (String.IsNullOrWhiteSpace(statusFilter)||statusFilter.Trim()=="Tổng nhân khẩu"))
       query+=" UNION ALL SELECT 'B02','Tổng nhân khẩu',COUNT(*) FROM people WHERE area_id=@area "+
       "AND created_utc>=@from AND created_utc<@to HAVING COUNT(*)>0";
     query+=DedicatedReportQueries(dedicated,false,true,!String.IsNullOrWhiteSpace(statusFilter));
     cmd.CommandText="SELECT * FROM ("+query+") ORDER BY 1,2 LIMIT 10001";
     using(var r=cmd.ExecuteReader())while(r.Read()){
      if(rows.Count>=10000)throw new InvalidOperationException("Báo cáo vượt giới hạn xuất an toàn.");
      long count=r.GetInt64(2);
      string source=r.GetString(0);
      string basis=source=="F08"?"Phương tiện cập nhật trong kỳ, trạng thái hiện tại":
       source=="B03"?"Biến động được ghi nhận trong kỳ":"Hồ sơ tạo trong kỳ, trạng thái hiện tại";
      rows.Add(new object[]{s.AreaId,source,r.GetString(1),count<5?(object)"Ẩn (1–4)":count,from,to,basis});
     }
    }
    tx.Commit();return rows;
   }
  }
  public static void LogReportExport(Session s){
   using(var c=Open())using(var tx=c.BeginTransaction()){
    Require(c,tx,s,"H05","export",s.AreaId);
    Audit(c,tx,s.Id,"EXPORT_AGGREGATE_H05","report_summary",s.AreaId);tx.Commit();
   }
  }
 }
}
