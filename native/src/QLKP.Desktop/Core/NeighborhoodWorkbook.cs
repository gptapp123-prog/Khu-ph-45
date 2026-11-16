using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QLKP.Desktop.Core {
 internal sealed class NeighborhoodWorkbookRow {
  internal ModuleRecord Record {get;set;}
  public string Code {get{return Record.Code;}}
  public string Title {get{return Record.Title;}}
  public string Status {get{return Record.Status;}}
  public string SourceStatus {get{return Record.Fields.TryGetValue("site_status",out string status)?status:"";}}
  public string Date {get{return DisplayLabels.Date(Record.Fields.TryGetValue("deadline",out string date)?date:Record.Fields["planned_end"]);}}
  public string Area {get{return Record.Fields.TryGetValue("source_area",out string area)?area:Record.Fields["site_address"];}}
 }

 internal sealed class ResidentWorkbookReview {
  public int Record {get;set;}
  public string Name {get;set;}
  public string BirthDate {get;set;}
  public string Issue {get;set;}
 }

 public static partial class DataStore {
  static readonly string[] NeighborhoodTaskColumns={
   "STT","Ngày giao","Công việc","Lĩnh vực","Người phụ trách","Địa bàn (xóm/thôn)",
   "Hạn hoàn thành","Mức độ ưu tiên","Trạng thái","Kết quả thực hiện","Ghi chú"
  };
  static readonly string[] NeighborhoodProjectColumns={
   "Ma DA","Ten du an","Dia diem","Chu dau tu","Ngay khoi cong","Ngay hoan thanh (KH)",
   "Tien do (%)","Ngan sach (VND)","Da chi (VND)","Trang thai"
  };
  static readonly Dictionary<string,string> NeighborhoodTaskStatuses=new Dictionary<string,string>(StringComparer.Ordinal) {
   {"Chưa thực hiện","Mới"},{"Đang thực hiện","Đang xử lý"},
   {"Trễ hạn","Đang xử lý"},{"Hoàn thành","Hoàn thành nội bộ"}
  };

  static void CheckDateOrder(string from,string to,string message) {
   if(from.Length>0&&to.Length>0&&String.CompareOrdinal(from,to)>0)
    throw new InvalidOperationException(message);
  }

  static string NeighborhoodDate(string value) {
   if(value.Length==0)return "";
   if(!DateTime.TryParseExact(value,new[]{"yyyy-MM-dd","dd/MM/yyyy","dd-MM-yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date))
    throw new InvalidDataException("Ngày không hợp lệ: "+value);
   return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  }

  static string NeighborhoodTaskCode(string[] row,string assignedOn) {
   // A row number alone is reused by every workbook. Use source date, area and title.
   var input=String.Join("\u001f",assignedOn,NormalizeSearch(row[5]),NormalizeSearch(row[2]));
   using(var hash=SHA256.Create())
    return "CV-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(input)),0,16).Replace("-","");
  }

  static ModuleRecord NeighborhoodTask(string[] row) {
   if(String.IsNullOrWhiteSpace(row[2]))throw new InvalidDataException("Thiếu tên công việc.");
   if(!NeighborhoodTaskStatuses.TryGetValue(row[8],out string status))
    throw new InvalidDataException("Trạng thái công việc không thuộc mẫu.");
   var assignedOn=NeighborhoodDate(row[1]);
   if(assignedOn.Length==0)throw new InvalidDataException("Thiếu ngày giao.");
   return new ModuleRecord {
    Feature="A02",Code=NeighborhoodTaskCode(row,assignedOn),Title=row[2],Status=status,Notes=row[10],
    Fields=new Dictionary<string,string> {
     {"assigned_on",assignedOn},{"work_category",row[3]},{"source_assignee",row[4]},
     {"source_area",row[5]},{"deadline",NeighborhoodDate(row[6])},{"priority",row[7]},{"result",row[9]}
    }
   };
  }

  static ModuleRecord NeighborhoodProject(string[] row,string constructionKind) {
   if(row[0].Length==0||row[1].Length==0||row[2].Length==0)
    throw new InvalidDataException("Cần mã dự án, tên dự án và địa điểm.");
   if(String.IsNullOrWhiteSpace(constructionKind))throw new InvalidDataException("Cần chọn loại công trình cho lô nhập.");
   return new ModuleRecord {
    Feature="B05",Code=row[0],Title=row[1],Status="Mới",
    Fields=new Dictionary<string,string> {
     {"construction_kind",constructionKind.Trim()},{"site_address",row[2]},{"project_owner",row[3]},
     {"started_on",NeighborhoodDate(row[4])},{"planned_end",NeighborhoodDate(row[5])},
     {"progress",row[6]},{"site_status",row[9]}
    }
   };
  }

  internal static List<NeighborhoodWorkbookRow> ParseNeighborhoodWorkbook(string feature,string path,string constructionKind) {
   bool tasks=feature=="A02";
   if(!tasks&&feature!="B05")throw new InvalidOperationException("Chỉ hỗ trợ nhập công việc hoặc công trình địa bàn.");
   var columns=tasks?NeighborhoodTaskColumns:NeighborhoodProjectColumns;
   var table=ReadIntakeTable(XlsxIntakeReader.ReadNamedSheet(path,tasks?"Công việc":"Du an-cong trinh",
    tasks?null:new HashSet<int>{7,8}));
   if(!table[0].Select(NormalizeSearch).SequenceEqual(columns.Select(NormalizeSearch)))
    throw new InvalidDataException("Tiêu đề hoặc thứ tự cột khác mẫu Excel đã chọn.");
   var rows=new List<NeighborhoodWorkbookRow>();
   var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int index=1;index<table.Count;index++) {
    var row=table[index];
    if(tasks&&row.Skip(1).All(String.IsNullOrEmpty))continue;
    try {
     var record=tasks?NeighborhoodTask(row):NeighborhoodProject(row,constructionKind);
     if(!codes.Add(record.Code))throw new InvalidDataException("Mã hoặc đầu việc trùng trong lô.");
     rows.Add(new NeighborhoodWorkbookRow {Record=record});
    } catch(InvalidDataException ex) {
     throw new InvalidDataException("Bản ghi "+index+": "+ex.Message,ex);
    }
   }
   if(rows.Count==0)throw new InvalidDataException("Trang Excel chưa có bản ghi.");
   return rows;
  }

  internal static List<ResidentWorkbookReview> ReviewResidentWorkbook(Session session,string path) {
   using(var c=Open())Require(c,null,session,"B02","read",session?.AreaId);
   string[] headers={"STT","Họ và tên","Ngày sinh","Tuổi","Giới tính","Số CCCD","Địa chỉ (xóm/thôn)",
    "Hộ khẩu (chủ hộ)","Thuộc diện NVQS (18-27)","Thuộc diện Trẻ em (<16)","Tham gia BHYT","Loại thẻ BHYT",
    "Người có công","Loại đối tượng NCC","Ghi chú"};
   var table=ReadIntakeTable(XlsxIntakeReader.ReadNamedSheet(path,"Danh sách dân cư",new HashSet<int>{3,8,9}));
   if(!table[0].Select(NormalizeSearch).SequenceEqual(headers.Select(NormalizeSearch)))
    throw new InvalidDataException("Tiêu đề hoặc thứ tự cột khác mẫu Danh sách dân cư.");
   var result=new List<ResidentWorkbookReview>();
   var identities=table.Skip(1).Where(row=>row[5].Length>0).GroupBy(row=>row[5],StringComparer.Ordinal)
    .ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
   for(int index=1;index<table.Count;index++) {
    var row=table[index];
    if(row.Skip(1).All(String.IsNullOrEmpty))continue;
    var issues=new List<string>();
    if(row[1].Length==0)issues.Add("Thiếu họ tên");
    string date="";
    try {date=NeighborhoodDate(row[2]);if(date.Length==0)issues.Add("Thiếu ngày sinh");}
    catch(InvalidDataException){issues.Add("Ngày sinh không hợp lệ");}
    if(row[5].Length==0)issues.Add("Thiếu CCCD");
    else if(row[5].Length!=12||row[5].Any(ch=>ch<'0'||ch>'9'))issues.Add("CCCD không đủ 12 chữ số");
    else if(identities[row[5]]>1)issues.Add("CCCD lặp trong nguồn");
    issues.Add(row[7].Length==0?"Thiếu mã hộ":"Chủ hộ ghi bằng tên, cần xác định mã hộ");
    result.Add(new ResidentWorkbookReview {Record=index,Name=row[1],BirthDate=DisplayLabels.Date(date),Issue=String.Join(" · ",issues)});
   }
   return result;
  }

  internal static string TrackPublicProject(Session session,string projectId) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    var source=GetModuleRecord(c,tx,session,"B05",projectId);
    Require(c,tx,session,"F05","read",session.AreaId);
    var matches=new List<string>();
    using(var query=new System.Data.SQLite.SQLiteCommand(
     @"SELECT m.id FROM module_records m WHERE m.area_id=@area AND m.feature='F05' AND
       ((m.related_feature='B05' AND m.related_id=@source)
        OR EXISTS(SELECT 1 FROM module_relations l WHERE l.area_id=m.area_id AND l.source_id=m.id
         AND l.target_feature='B05' AND l.target_id=@source)) LIMIT 2",c,tx)) {
     query.Parameters.AddRange(new[]{P("@area",session.AreaId),P("@source",source.Id)});
     using(var reader=query.ExecuteReader())while(reader.Read())matches.Add(reader.GetString(0));
    }
    if(matches.Count>1)throw new InvalidOperationException("Công trình đã có nhiều hồ sơ theo dõi. Mở F05 để chọn hồ sơ phù hợp.");
    string linked=matches.SingleOrDefault();
    if(linked==null) {
     Require(c,tx,session,"F05","create",session.AreaId);
     string code="HT-"+source.Id;
     var draft=new ModuleRecord {Code=code,Title=source.Title,Status="Ghi nhận",RelatedFeature="B05",RelatedId=source.Id};
     draft.Fields["asset_kind"]="Công trình công cộng";
     draft.Fields["location_note"]=GetField(source,"site_address");
     linked=SaveModuleRecord(c,tx,session,"F05",draft,0);
    }
    tx.Commit();
    return linked;
   }
  }

  internal static void CheckOrCommitNeighborhoodWorkbook(Session session,string feature,IReadOnlyList<NeighborhoodWorkbookRow> rows,bool commit) {
   if(feature!="A02"&&feature!="B05")throw new InvalidOperationException("Loại lô nhập không hợp lệ.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,session,feature,"read",session==null?null:session.AreaId);
    Require(c,tx,session,feature,"create",session.AreaId);
    foreach(var row in rows) {
     var record=row.Record;
     if(record.Feature!=feature)throw new InvalidOperationException("Lô nhập có dữ liệu sai phân hệ.");
     if(Convert.ToInt64(Scalar(c,tx,
      "SELECT count(*) FROM module_records WHERE area_id=@area AND feature=@feature AND record_code=@code COLLATE NOCASE",
      P("@area",session.AreaId),P("@feature",feature),P("@code",record.Code)))>0)
      throw new InvalidOperationException("Mã đã tồn tại trong khu phố: "+record.Code+". Hãy đối chiếu trước khi nhập.");
     try {
      if(commit)SaveModuleRecord(c,tx,session,feature,record,0);
      else CheckModuleDraft(c,tx,session,feature,record);
     } catch(InvalidOperationException ex) {
      throw new InvalidOperationException(record.Code+": "+ex.Message,ex);
     }
    }
    tx.Commit();
   }
  }
 }
}
