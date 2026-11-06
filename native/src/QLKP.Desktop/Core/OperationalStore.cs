using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
namespace QLKP.Desktop.Core {
 public sealed class ModuleRecord {
  public string Id {get;set;}
  public string Feature {get;set;}
  public string Code {get;set;}
  public string Title {get;set;}
  public string Notes {get;set;}
  public string Status {get;set;}
  public string DueDate {get;set;}
  public string DueDateDisplay {get{return DisplayLabels.Date(DueDate);}}
  public string RelatedFeature {get;set;}
  public string RelatedId {get;set;}
  public long Version {get;set;}
  public string UpdatedUtc {get;set;}
  public Dictionary<string,string> Fields {get;set;} = new Dictionary<string,string>();
  public string Display {get{return Code+" · "+Title;}}
 }
 public sealed class ModuleOverview {
  public string Feature {get;set;} public string Label {get;set;}
  public long Count {get;set;} public long Open {get;set;}
 }
 public sealed class ModuleHistory {
  public string OccurredUtc {get;set;} public string Actor {get;set;}
  public string OccurredDisplay {get{return DisplayLabels.UtcStamp(OccurredUtc);}}
  public string Before {get;set;} public string After {get;set;}
  public long Version {get;set;}
 }
 public sealed class ModuleRelation {
  public string Feature {get;set;}
  public string TargetId {get;set;}
  public string Kind {get;set;}
  public string CreatedUtc {get;set;}
  public string Display { get { return Kind+" · "+Feature+" · ID "+(TargetId??"").Substring(0,Math.Min(12,(TargetId??"").Length)); } }
 }
 public static partial class DataStore {
  private static void CheckModuleDraft(SQLiteConnection c,SQLiteTransaction tx,Session s,string code,ModuleRecord draft) {
   var spec=ModuleCatalog.Get(code);
   if(draft==null||String.IsNullOrWhiteSpace(draft.Code)||String.IsNullOrWhiteSpace(draft.Title))
    throw new InvalidOperationException("Mã hồ sơ và tiêu đề không được để trống.");
   if(draft.Code.Trim().Length>50||draft.Title.Trim().Length>240||(draft.Notes??"").Length>4000)
    throw new InvalidOperationException("Trường nhập vượt giới hạn.");
   if(!spec.Statuses.Contains(draft.Status))throw new InvalidOperationException("Trạng thái không thuộc quy trình của "+code);
   foreach(var pair in draft.Fields??new Dictionary<string,string>()) {
    var fld=spec.Fields.FirstOrDefault(x=>x.Key==pair.Key);
    if(fld==null)throw new InvalidOperationException("Trường chưa được cấu hình: "+pair.Key);
    if((pair.Value??"").Length>500)throw new InvalidOperationException("Giá trị của "+fld.Label+" quá dài.");
    DateTime parsed; decimal quantity; int count;
    if(!String.IsNullOrWhiteSpace(pair.Value)) {
     if(fld.Type=="date"&&!DateTime.TryParseExact(pair.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))
      throw new InvalidOperationException("Ngày phải theo yyyy-MM-dd: "+fld.Label);
     if(fld.Type=="time"&&!DateTime.TryParseExact(pair.Value,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))
      throw new InvalidOperationException("Giờ phải theo HH:mm: "+fld.Label);
     if(fld.Type=="number"&&(!Decimal.TryParse(pair.Value,NumberStyles.Number,CultureInfo.InvariantCulture,out quantity)||quantity<0))
      throw new InvalidOperationException("Chỉ tiêu phải là số không âm dùng dấu thập phân .");
     if(fld.Type=="percentage"&&(!Int32.TryParse(pair.Value,out count)||count<0||count>100))
      throw new InvalidOperationException("Tiến độ phải nằm trong khoảng 0–100; 100% không tự là nghiệm thu.");
     if(fld.Type=="nonnegative"&&(!Int32.TryParse(pair.Value,out count)||count<0))
      throw new InvalidOperationException("Số phòng không hợp lệ.");
     if(fld.Type.StartsWith("choice:",StringComparison.Ordinal) &&
       !fld.Type.Substring(7).Split('|').Contains(pair.Value))
      throw new InvalidOperationException("Giá trị phân loại không thuộc danh mục cho phép: "+fld.Label);
     if(fld.Type=="account" && Convert.ToInt32(Scalar(c,tx,
      "SELECT COUNT(*) FROM accounts WHERE id=@id AND area_id=@area AND active=1",
      P("@id",pair.Value),P("@area",s.AreaId)))!=1)
      throw new InvalidOperationException("Người phụ trách phải là ID tài khoản đang hoạt động, cùng khu phố.");
    }
   }
   foreach(var fld in spec.Fields.Where(x=>x.Required)) {
    string val;
    if(draft.Fields==null||!draft.Fields.TryGetValue(fld.Key,out val)||String.IsNullOrWhiteSpace(val))
     throw new InvalidOperationException("Thiếu trường: "+fld.Label);
   }
   var due=DueField(code,draft);
   if(code=="A02"&&draft.Status=="Hoàn thành nội bộ"&&String.IsNullOrWhiteSpace(GetField(draft,"result")))
    throw new InvalidOperationException("Cần nhập kết quả công việc trước khi đánh dấu hoàn thành nội bộ.");
   if(code=="A06"&&draft.Status=="Đã bàn giao nội bộ" && (String.IsNullOrWhiteSpace(GetField(draft,"incoming"))||String.IsNullOrWhiteSpace(GetField(draft,"outgoing"))))
    throw new InvalidOperationException("Bàn giao nội bộ cần xác định hai bên.");
   // Group C: verified input vs workflow state. Nothing here signs or dispatches a document.
   if(code=="C01" && GetField(draft,"classification")=="Hạn chế")
    throw new NotSupportedException("Văn bản hạn chế chưa có phân quyền cấp hồ sơ độc lập; không cho ghi để tránh lộ dữ liệu.");
   if(code=="C01" && draft.Status=="Đã phân công" && String.IsNullOrWhiteSpace(GetField(draft,"responsible_id")))
    throw new InvalidOperationException("Văn bản đến chỉ được ghi 'Đã phân công' khi có ID tài khoản phụ trách.");
   if(code=="C02" && draft.Status=="Đã ghi nhận gửi" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"doc_no")) || String.IsNullOrWhiteSpace(GetField(draft,"recipient")) ||
      String.IsNullOrWhiteSpace(GetField(draft,"sent_on")) || String.IsNullOrWhiteSpace(GetField(draft,"send_evidence"))))
    throw new InvalidOperationException("Ghi nhận gửi cần số văn bản, nơi nhận, thời gian và căn cứ; phần mềm không tự gửi.");
   if(code=="C05" && draft.Status=="Đã xác nhận nội bộ" &&
     (draft.RelatedFeature!="C04" || String.IsNullOrWhiteSpace(draft.RelatedId) || String.IsNullOrWhiteSpace(GetField(draft,"reviewer_note"))))
    throw new InvalidOperationException("Biên bản xác nhận nội bộ phải liên kết đúng buổi họp C04 và có căn cứ rà soát.");
   if(code=="C06" && draft.Status=="Đã duyệt nội bộ" && String.IsNullOrWhiteSpace(GetField(draft,"reviewer_note")))
    throw new InvalidOperationException("Mẫu biểu được duyệt nội bộ cần căn cứ xác nhận.");
   if(code=="D02" && draft.Status=="Đã đối chiếu nguồn" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"reviewed_on"))))
    throw new InvalidOperationException("Hồ sơ chi bộ chưa được đối chiếu nguồn hợp lệ; không tự gán trạng thái.");
   if(code=="D01" && draft.Status=="Đang đảm nhiệm" && String.IsNullOrWhiteSpace(GetField(draft,"appointment_reference")))
    throw new InvalidOperationException("Chức trách cần căn cứ phân công đã nhập.");
   // E: Administrative coordination only. NO automated benefit approval, medical diagnosis or payments.
   // Database encryption does not replace per-record permission and business-rule checks.
   if(code=="E01" && draft.Status=="Đã chuyển cơ quan có thẩm quyền" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("Chuyển hồ sơ an sinh cần nguồn và mã chứng từ chuyển hợp lệ.");
   if(code=="E01" && draft.Status=="Đã ghi nhận phản hồi" && String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("Không tự xác nhận phản hồi nếu thiếu căn cứ.");
   if(code=="E02" && draft.Status=="Đã đối chiếu hồ sơ" &&
     (draft.RelatedFeature!="B02" || String.IsNullOrWhiteSpace(draft.RelatedId) ||
      String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"reviewed_on"))))
    throw new InvalidOperationException("Đối chiếu người có công yêu cầu liên kết cá nhân đúng ID, nguồn và ngày kiểm tra; không kết luận đủ điều kiện.");
   if(code=="E02" && draft.Status=="Đã chuyển xác minh" && String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference")))
    throw new InvalidOperationException("Chuyển xác minh cần căn cứ chuyển.");
   if(code=="E03" && draft.Status=="Đang theo dõi" &&
     (draft.RelatedFeature!="B02" || String.IsNullOrWhiteSpace(draft.RelatedId) || String.IsNullOrWhiteSpace(GetField(draft,"source_reference"))))
    throw new InvalidOperationException("Theo dõi người cao tuổi yêu cầu ID cá nhân và nguồn đối chiếu, không tự phân loại theo tên.");
   if(code=="E04" && draft.Status=="Đang phối hợp" &&
     (draft.RelatedFeature!="B02" || String.IsNullOrWhiteSpace(draft.RelatedId) || String.IsNullOrWhiteSpace(GetField(draft,"source_reference"))))
    throw new InvalidOperationException("Hồ sơ trẻ em/học sinh phải có ID và nguồn kiểm tra; tránh suy đoán theo tuổi.");
   if(code=="E05" && draft.Status=="Đã chuyển xét" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("Chuyển xét khuyến học không đồng nghĩa với đã cấp học bổng.");
   if(code=="E05" && draft.Status=="Đã có phản hồi" && String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("Thiếu căn cứ ghi nhận phản hồi khuyến học.");
   if(code=="E06" && (!String.IsNullOrWhiteSpace(draft.RelatedFeature) || !String.IsNullOrWhiteSpace(draft.RelatedId)))
    throw new InvalidOperationException("E06 chỉ tổng hợp hoạt động cộng đồng; không liên kết hồ sơ y tế cá nhân khi chưa có phân quyền hồ sơ y tế riêng.");
   if(code=="E06" && draft.Status=="Đã tổng hợp" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"aggregate_count")) || String.IsNullOrWhiteSpace(GetField(draft,"source_reference"))))
    throw new InvalidOperationException("Tổng hợp y tế cần số liệu gộp và nguồn; không được suy đoán.");
   if(code=="E07" && draft.Status=="Đã nhận phản hồi" && String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("Không ghi phản hồi việc làm nếu không có căn cứ.");
   // Group F is a scoped coordination log; no automatic enforcement or legal finding.
   if(code=="F01" && draft.Status=="Đã chuyển đầu mối" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("F01: chuyển đầu mối cần nguồn và mã phiếu/căn cứ; không tự kết luận vi phạm.");
   if(code=="F02" && draft.Status=="Đã chuyển đầu mối PCCC" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("F02: cần căn cứ chuyển đầu mối PCCC; không xác nhận đạt chuẩn PCCC.");
   if(code=="F03" && draft.Status=="Đã chuyển cơ quan chức năng" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("F03: cần nguồn và căn cứ chuyển; phần mềm không điều phối cứu nạn khẩn cấp.");
   if(code=="F04" && draft.Status=="Đã chuyển xử lý" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("F04: không ghi trạng thái chuyển nếu thiếu nguồn và căn cứ.");
   if(code=="F05" && draft.Status=="Đã chuyển cơ quan phụ trách" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("F05: cần nguồn và căn cứ chuyển; không cấp phép công trình.");
   if(new[]{"F01","F02","F03","F04","F05"}.Contains(code) &&
     new[]{"Đã có phản hồi","Đã ghi nhận phản hồi","Đã ghi nhận kết quả"}.Contains(draft.Status) &&
     String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("F01–F05: không ghi nhận phản hồi/kết quả khi thiếu căn cứ; trạng thái không phải quyết định chuyên ngành.");
   if(code=="F06" && (!String.IsNullOrWhiteSpace(draft.RelatedFeature) || !String.IsNullOrWhiteSpace(draft.RelatedId)))
    throw new InvalidOperationException("F06 chỉ lưu hoạt động tổng hợp, chưa xử lý danh sách nghĩa vụ quân sự cá nhân.");
   if(code=="F06" && draft.Status=="Đã tổng hợp số liệu" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"aggregate_count")) || String.IsNullOrWhiteSpace(GetField(draft,"source_reference"))))
    throw new InvalidOperationException("F06 cần số liệu không định danh và nguồn khi tổng hợp.");
   if(code=="F07" && draft.Status=="Đã tổng kết nội bộ" && String.IsNullOrWhiteSpace(GetField(draft,"source_reference")))
    throw new InvalidOperationException("F07: ghi nhận tổng kết cần nguồn nội bộ, không tự công nhận khen thưởng.");
   // G01–G04 are tracking/coordination only: transitions must carry a recorded basis.
   if(code=="G01" && draft.Status=="Đã phân công" && String.IsNullOrWhiteSpace(GetField(draft,"assignee_id")))
    throw new InvalidOperationException("G01 cần ID tài khoản phụ trách khi phân công.");
   if(code=="G01" && draft.Status=="Đã chuyển đầu mối" &&
      (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("G01 chỉ ghi đã chuyển khi có nguồn và căn cứ chuyển.");
   if(code=="G01" && draft.Status=="Đã có phản hồi" && String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("G01 cần căn cứ phản hồi; không tự xác nhận xử lý xong.");
   if(code=="G01" && draft.Status=="Đóng theo dõi" && String.IsNullOrWhiteSpace(GetField(draft,"closure_reference")))
    throw new InvalidOperationException("G01 cần căn cứ đóng theo dõi nội bộ.");
   if(code=="G02" && draft.Status=="Đã lập biên bản nội bộ" &&
     (draft.RelatedFeature!="C05" || String.IsNullOrWhiteSpace(draft.RelatedId) || String.IsNullOrWhiteSpace(GetField(draft,"minutes_reference"))))
    throw new InvalidOperationException("G02 cần biên bản C05 đã kiểm tra theo ID và mã tham chiếu.");
   if(code=="G02" && draft.Status=="Đã chuyển cơ quan phụ trách" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("G02 chuyển cơ quan cần nguồn và căn cứ, không tự kết luận hòa giải thành.");
   if(code=="G02" && draft.Status=="Kết thúc theo dõi" && String.IsNullOrWhiteSpace(GetField(draft,"closure_reference")))
    throw new InvalidOperationException("G02 cần căn cứ kết thúc theo dõi.");
   if(code=="G03" && draft.Status=="Đã ghi nhận thông báo" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"notice_reference"))))
    throw new InvalidOperationException("G03 cần nguồn tài liệu và căn cứ đã thông báo.");
   if(code=="G03" && draft.Status=="Đã tổng hợp kết quả" &&
     (String.IsNullOrWhiteSpace(GetField(draft,"aggregate_count")) || String.IsNullOrWhiteSpace(GetField(draft,"result_reference"))))
    throw new InvalidOperationException("G03 cần số liệu tổng hợp không định danh và căn cứ kết quả.");
   if(code=="G04" && new[]{"Đã chuyển đầu mối","Chờ phản hồi"}.Contains(draft.Status) &&
     (String.IsNullOrWhiteSpace(GetField(draft,"source_reference")) || String.IsNullOrWhiteSpace(GetField(draft,"forwarding_reference"))))
    throw new InvalidOperationException("G04 chưa có nguồn và căn cứ chuyển cơ quan.");
   if(code=="G04" && draft.Status=="Đã có phản hồi" && String.IsNullOrWhiteSpace(GetField(draft,"feedback_reference")))
    throw new InvalidOperationException("G04 cần căn cứ phản hồi được ghi nhận.");
   if(code=="G04" && draft.Status=="Đóng theo dõi" && String.IsNullOrWhiteSpace(GetField(draft,"closure_reference")))
    throw new InvalidOperationException("G04 cần căn cứ đóng theo dõi nội bộ.");
   if(!String.IsNullOrWhiteSpace(due)) {
    DateTime d;
    if(!DateTime.TryParseExact(due,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d))
     throw new InvalidOperationException("Ngày công việc không hợp lệ.");
   }
   if(String.IsNullOrWhiteSpace(draft.RelatedFeature)!=String.IsNullOrWhiteSpace(draft.RelatedId))
    throw new InvalidOperationException("Liên kết phải có cả loại hồ sơ và ID.");
   if(!String.IsNullOrWhiteSpace(draft.RelatedFeature)) {
    if(!String.IsNullOrWhiteSpace(draft.Id) && draft.RelatedId==draft.Id)
     throw new InvalidOperationException("Không tự liên kết hồ sơ với chính nó.");
    if(!ModuleCatalog.AllowedTargets(code).Contains(draft.RelatedFeature))
     throw new InvalidOperationException("Liên kết chưa có loại hồ sơ hỗ trợ.");
    Require(c,tx,s,draft.RelatedFeature,"read",s.AreaId);
    if(draft.RelatedFeature=="B01") {
     if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM households WHERE id=@id AND area_id=@a",P("@id",draft.RelatedId),P("@a",s.AreaId)))!=1)
      throw new InvalidOperationException("Không tìm thấy hộ liên quan trong địa bàn.");
    } else if(draft.RelatedFeature=="B02") {
     if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM people WHERE id=@id AND area_id=@a",P("@id",draft.RelatedId),P("@a",s.AreaId)))!=1)
      throw new InvalidOperationException("Không tìm thấy nhân khẩu liên quan trong địa bàn.");
    } else {
     if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM module_records WHERE id=@id AND area_id=@a AND feature=@f",P("@id",draft.RelatedId),P("@a",s.AreaId),P("@f",draft.RelatedFeature)))!=1)
      throw new InvalidOperationException("Không tìm thấy hồ sơ liên quan theo ID và loại.");
    }
   }
  }
  static string GetField(ModuleRecord r,string name) {
   string v;return r.Fields!=null && r.Fields.TryGetValue(name,out v)?(v??"").Trim():"";
  }
  static string DueField(string code,ModuleRecord r){
   return new[]{"A02","A03","C01","C03"}.Contains(code)?GetField(r,"deadline"):
    code=="A04"?GetField(r,"event_date"):code=="C04"?GetField(r,"meeting_date"):"";
  }
  static string SearchModuleRecord(ModuleRecord r) {
   // Deterministic index; no automatic identity merging.
   return NormalizeSearch((r.Title??"")+" "+GetField(r,"doc_no")+" "+GetField(r,"sender")+" "+
      GetField(r,"full_name")+" "+GetField(r,"organization_kind")+" "+GetField(r,"duty")+" "+
      GetField(r,"program_kind")+" "+GetField(r,"activity_kind")+" "+GetField(r,"school_year"));
  }
  // CRUD with version-based optimistic concurrency and same-area authorization.
  public static string SaveModuleRecord(Session s,string code,ModuleRecord draft,long expectedVersion=0) {
   ModuleCatalog.Get(code);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,code,expectedVersion==0?"create":"update",s==null?null:s.AreaId);
    CheckModuleDraft(c,tx,s,code,draft);
    var now=Now;string recordId;string oldStatus="";long oldVersion=0;
    if(expectedVersion==0) {
     recordId=Id();
     Exec(c,tx,@"INSERT INTO module_records(id,area_id,feature,record_code,title,search_title,notes,status,due_date,related_feature,related_id,version,created_by,updated_by,created_utc,updated_utc)
      VALUES(@id,@area,@feature,@code,@title,@search,@notes,@status,@due,@rf,@rid,1,@actor,@actor,@utc,@utc)",
      P("@id",recordId),P("@area",s.AreaId),P("@feature",code),P("@code",draft.Code.Trim()),
      P("@title",draft.Title.Trim()),P("@search",SearchModuleRecord(draft)),P("@notes",draft.Notes??""),P("@status",draft.Status),P("@due",DueField(code,draft)),
      P("@rf",draft.RelatedFeature??""),P("@rid",draft.RelatedId??""),P("@actor",s.Id),P("@utc",now));
     Audit(c,tx,s.Id,"MODULE_CREATE_"+code,"module_record",recordId);
    } else {
     if(String.IsNullOrWhiteSpace(draft.Id))throw new InvalidOperationException("Chưa xác định ID hồ sơ cần sửa.");
     recordId=draft.Id;
     using(var cmd=new SQLiteCommand("SELECT status,version,record_code FROM module_records WHERE id=@id AND feature=@f AND area_id=@a",c,tx)) {
      cmd.Parameters.Add(P("@id",recordId));cmd.Parameters.Add(P("@f",code));cmd.Parameters.Add(P("@a",s.AreaId));
      using(var r=cmd.ExecuteReader()) {
       if(!r.Read())throw new UnauthorizedAccessException("Hồ sơ không có trong khu phố hoặc đã đổi phạm vi.");
       oldStatus=r.GetString(0);oldVersion=r.GetInt64(1);
       if(!String.Equals(r.GetString(2),draft.Code,StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Không thể thay đổi mã định danh của hồ sơ.");
      }
     }
     if(oldVersion!=expectedVersion)throw new InvalidOperationException("Hồ sơ đã được người khác cập nhật. Tải lại trước khi ghi.");
     using(var cmd=new SQLiteCommand(@"UPDATE module_records SET title=@title,search_title=@search,notes=@notes,status=@status,due_date=@due,related_feature=@rf,related_id=@rid,version=version+1,updated_by=@actor,updated_utc=@utc
        WHERE id=@id AND feature=@f AND area_id=@area AND version=@ver",c,tx)) {
      cmd.Parameters.AddRange(new[]{P("@title",draft.Title.Trim()),P("@search",SearchModuleRecord(draft)),P("@notes",draft.Notes??""),P("@status",draft.Status),P("@due",DueField(code,draft)),
       P("@rf",draft.RelatedFeature??""),P("@rid",draft.RelatedId??""),P("@actor",s.Id),P("@utc",now),P("@id",recordId),P("@f",code),P("@area",s.AreaId),P("@ver",expectedVersion)});
      if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Ghi hồ sơ thất bại do thay đổi đồng thời.");
     }
     Exec(c,tx,"INSERT INTO module_record_history(id,record_id,actor_id,old_version,new_version,old_status,new_status,occurred_utc) VALUES(@id,@r,@actor,@old,@new,@os,@ns,@utc)",
      P("@id",Id()),P("@r",recordId),P("@actor",s.Id),P("@old",oldVersion),P("@new",oldVersion+1),P("@os",oldStatus),P("@ns",draft.Status),P("@utc",now));
     Audit(c,tx,s.Id,"MODULE_UPDATE_"+code,"module_record",recordId);
    }
    // Replace the typed field set only after a version match; still within one transaction.
    Exec(c,tx,"DELETE FROM module_field_values WHERE record_id=@id",P("@id",recordId));
    foreach(var f in ModuleCatalog.Get(code).Fields) {
     Exec(c,tx,"INSERT INTO module_field_values(record_id,field_key,field_value) VALUES(@id,@field,@value)",
      P("@id",recordId),P("@field",f.Key),P("@value",GetField(draft,f.Key)));
    }
    tx.Commit();return recordId;
   }
  }
  static ModuleRecord ReadModuleRecord(SQLiteDataReader r) {
   return new ModuleRecord {Id=r.GetString(0),Feature=r.GetString(1),Code=r.GetString(2),Title=r.GetString(3),
    Notes=r.GetString(4),Status=r.GetString(5),DueDate=r.GetString(6),RelatedFeature=r.GetString(7),RelatedId=r.GetString(8),Version=r.GetInt64(9),UpdatedUtc=r.GetString(10)};
  }
  private const string ModuleColumns="m.id,m.feature,m.record_code,m.title,m.notes,m.status,m.due_date,m.related_feature,m.related_id,m.version,m.updated_utc";
  public static ModuleRecord GetModuleRecord(Session s,string code,string id) {
   ModuleCatalog.Get(code);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,code,"read",s==null?null:s.AreaId);
    ModuleRecord value;
    using(var cmd=new SQLiteCommand("SELECT "+ModuleColumns+" FROM module_records m WHERE m.feature=@f AND m.id=@id AND m.area_id=@a",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@f",code),P("@id",id),P("@a",s.AreaId)});
     using(var r=cmd.ExecuteReader()) {if(!r.Read())throw new UnauthorizedAccessException("Không có hồ sơ phù hợp ID trong phạm vi truy cập.");value=ReadModuleRecord(r);}
    }
    using(var cmd=new SQLiteCommand("SELECT field_key,field_value FROM module_field_values WHERE record_id=@id ORDER BY field_key",c,tx)) {
     cmd.Parameters.Add(P("@id",id));using(var r=cmd.ExecuteReader())while(r.Read())value.Fields[r.GetString(0)]=r.GetString(1);
    }
    tx.Commit();return value;
   }
  }
  public static RecordPage<ModuleRecord> GetModulePage(Session s,string code,string keyword,int pageIndex,int pageSize=50,bool onlyBacklog=false) {
   ModuleCatalog.Get(code);
   if(pageSize<1||pageSize>200||pageIndex<0)throw new InvalidOperationException("Trang không hợp lệ.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,code,"read",s==null?null:s.AreaId);
    if(onlyBacklog) {
     if(code!="A02")throw new InvalidOperationException("Chỉ nhiệm vụ được theo dõi tồn đọng.");
     Require(c,tx,s,"A05","read",s.AreaId);
    }
    string filter="m.area_id=@area AND m.feature=@f AND (m.record_code LIKE @q ESCAPE '\\' OR m.search_title LIKE @q ESCAPE '\\')";
    if(onlyBacklog)filter+=" AND m.due_date<>'' AND m.due_date < @today AND m.status NOT IN ('Hoàn thành nội bộ')";
    string q=SearchPattern(NormalizeSearch(keyword));string today=DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    long total=Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM module_records m WHERE "+filter,
     P("@area",s.AreaId),P("@f",code),P("@q",q),P("@today",today)));
    long pages=total==0?0:((total-1)/pageSize)+1;
    if(pages<=pageIndex)pageIndex=(int)Math.Max(0,pages-1);
    var result=new RecordPage<ModuleRecord>{PageIndex=pageIndex,PageSize=pageSize,TotalCount=total};
    using(var cmd=new SQLiteCommand("SELECT "+ModuleColumns+" FROM module_records m WHERE "+filter+
     " ORDER BY m.updated_utc DESC,m.id DESC LIMIT @limit OFFSET @offset",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@area",s.AreaId),P("@f",code),P("@q",q),P("@today",today),P("@limit",pageSize),P("@offset",pageIndex*pageSize)});
     using(var r=cmd.ExecuteReader())while(r.Read())result.Items.Add(ReadModuleRecord(r));
    }
    tx.Commit();return result;
   }
  }
  public static List<ModuleHistory> GetModuleHistory(Session s,string code,string id) {
   ModuleCatalog.Get(code);
   using(var c=Open()) {
    Require(c,null,s,code,"read",s==null?null:s.AreaId);
    var rows=new List<ModuleHistory>();
    using(var cmd=new SQLiteCommand(@"SELECT h.occurred_utc,a.username,h.old_status,h.new_status,h.new_version
      FROM module_record_history h JOIN module_records m ON m.id=h.record_id JOIN accounts a ON a.id=h.actor_id
      WHERE m.id=@id AND m.feature=@f AND m.area_id=@area ORDER BY h.occurred_utc DESC,h.id DESC LIMIT 100",c)) {
     cmd.Parameters.AddRange(new[]{P("@id",id),P("@f",code),P("@area",s.AreaId)});
     using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(new ModuleHistory{
      OccurredUtc=r.GetString(0),Actor=r.GetString(1),Before=r.GetString(2),After=r.GetString(3),Version=r.GetInt64(4)});
    }
    return rows;
   }
  }
  public static List<AccountRow> GetModuleAssignees(Session s,string code="A02") {
   using(var c=Open()) {
    ModuleCatalog.Get(code);
    Require(c,null,s,code,"read",s==null?null:s.AreaId);
    var list=new List<AccountRow>();
    using(var cmd=new SQLiteCommand("SELECT id,username,role,area_id,active FROM accounts WHERE area_id=@area AND active=1 ORDER BY username LIMIT 200",c)) {
     cmd.Parameters.Add(P("@area",s.AreaId));
     using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new AccountRow{Id=r.GetString(0),Username=r.GetString(1),Role=r.GetString(2),AreaId=r.GetString(3),Status="Hoạt động"});
    }
    return list;
   }
  }
  public static List<ModuleOverview> GetModuleOverview(Session s) {
   using(var c=Open()) {
    Require(c,null,s,"A01","read",s==null?null:s.AreaId);
    var items=new List<ModuleOverview>();
    foreach(var d in ModuleCatalog.Editable) {
     if(!HasPermission(c,null,s,d.Code,"read",s.AreaId))continue;
     using(var cmd=new SQLiteCommand("SELECT COUNT(*),SUM(CASE WHEN status NOT IN ('Hoàn thành nội bộ','Đã diễn ra','Ngừng theo dõi','Ngừng hoạt động') THEN 1 ELSE 0 END) FROM module_records WHERE area_id=@area AND feature=@f",c)) {
      cmd.Parameters.AddRange(new[]{P("@area",s.AreaId),P("@f",d.Code)});
      using(var r=cmd.ExecuteReader())if(r.Read())items.Add(new ModuleOverview{Feature=d.Code,Label=d.Label,Count=r.GetInt64(0),Open=r.IsDBNull(1)?0:r.GetInt64(1)});
     }
    }
    return items;
   }
  }
  // Create an internal task from an incoming document, never send/approve the source.
  private static string ExistingSourceTask(Session s,ModuleRecord task) {
   using(var c=Open()) {
    Require(c,null,s,"A02","create",s==null?null:s.AreaId);
    return Convert.ToString(Scalar(c,null,@"SELECT id FROM module_records WHERE feature='A02'
      AND area_id=@area AND record_code=@code AND related_feature=@source AND related_id=@sourceId",
      P("@area",s.AreaId),P("@code",task.Code),P("@source",task.RelatedFeature),P("@sourceId",task.RelatedId)));
   }
  }
  private static string SaveSourceTask(Session s,ModuleRecord task) {
   string existing=ExistingSourceTask(s,task);
   if(!String.IsNullOrEmpty(existing))return existing;
   try{return SaveModuleRecord(s,"A02",task);}
   catch(SQLiteException) {
    // A concurrent click may have committed the same stable source link first.
    // A conflicting code pointing elsewhere is never accepted as this source's task.
    existing=ExistingSourceTask(s,task);if(String.IsNullOrEmpty(existing))throw;return existing;
   }
  }
  public static string CreateTaskFromIncoming(Session s,string incomingId) {
   var incoming=GetModuleRecord(s,"C01",incomingId); // read authorization + exact ID
   if(!Can(s,"A02","create"))throw new UnauthorizedAccessException("Chưa được cấp quyền tạo nhiệm vụ A02.");
   string stableCode="NV-C01-"+incoming.Id;
   string title="Xử lý VB đến: "+incoming.Title;
   if(title.Length>240)title=title.Substring(0,240);
   var task=new ModuleRecord{Code=stableCode,Title=title,Status="Mới",RelatedFeature="C01",RelatedId=incoming.Id,
      Notes="Nhiệm vụ nội bộ sinh từ hồ sơ có ID xác thực; chưa được phê duyệt."};
   string responsible;
   if(incoming.Fields.TryGetValue("responsible_id",out responsible) && !String.IsNullOrWhiteSpace(responsible))
    task.Fields["assignee_id"]=responsible;
   string deadline;
   if(incoming.Fields.TryGetValue("deadline",out deadline) && !String.IsNullOrWhiteSpace(deadline))
    task.Fields["deadline"]=deadline;
   return SaveSourceTask(s,task);
  }
  // Feedback -> A02. Only a stable reference and a synthetic-neutral title are copied;
  // do not duplicate the reporter's personal details into unrelated task records.
  public static string CreateTaskFromFeedback(Session s,string feedbackId) {
   var source=GetModuleRecord(s,"G01",feedbackId);
   if(!Can(s,"A02","create"))throw new UnauthorizedAccessException("Chưa được cấp quyền tạo A02 từ G01.");
   var task=new ModuleRecord{Code="NV-G01-"+source.Id,Title="Xử lý phản ánh mã "+source.Code,
    Status="Mới",RelatedFeature="G01",RelatedId=source.Id,
    Notes="Nhiệm vụ nội bộ tham chiếu mã phản ánh; không sao chép thông tin người phản ánh."};
   string assignee;
   if(source.Fields.TryGetValue("assignee_id",out assignee) && !String.IsNullOrWhiteSpace(assignee))
    task.Fields["assignee_id"]=assignee;
   return SaveSourceTask(s,task);
  }
  // V0.13: multiple typed relations; server/database recheck every ID and authorization.
  public static string AddModuleRelation(Session s,string sourceFeature,string sourceId,string relationKind,
    string targetFeature,string targetId,long expectedVersion) {
   ModuleCatalog.Get(sourceFeature);
   if(!new[]{"THAM_CHIEU","NGUON","TRA_LOI","PHAN_CONG","BIEN_BAN","PHOI_HOP"}.Contains(relationKind))
    throw new InvalidOperationException("Loại liên kết không hợp lệ.");
   if(!ModuleCatalog.LinkedFeatures().Contains(targetFeature) || String.IsNullOrWhiteSpace(targetId) ||
      String.IsNullOrWhiteSpace(sourceId) || sourceId==targetId)
    throw new InvalidOperationException("Phải chọn hồ sơ đích khác hồ sơ nguồn theo ID.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,sourceFeature,"update",s==null?null:s.AreaId);
    if(sourceFeature=="E06" || sourceFeature=="F06")
     throw new InvalidOperationException("E06 không hỗ trợ liên kết y tế cá nhân; F06 không hỗ trợ liên kết hồ sơ cá nhân.");
    if(!ModuleCatalog.AllowedTargets(sourceFeature).Contains(targetFeature))
     throw new InvalidOperationException("Loại liên kết không được phép đối với mô-đun nguồn.");
    Require(c,tx,s,targetFeature,"read",s.AreaId);
    string targetTable=targetFeature=="B01"?"households":targetFeature=="B02"?"people":"module_records";
    string targetSql="SELECT COUNT(*) FROM "+targetTable+" WHERE id=@id AND area_id=@a"+
       (targetTable=="module_records"?" AND feature=@f":"");
    if(Convert.ToInt32(Scalar(c,tx,targetSql,P("@id",targetId),P("@a",s.AreaId),P("@f",targetFeature)))!=1)
     throw new UnauthorizedAccessException("ID đích không tồn tại trong phạm vi cho phép.");
    string oldStatus;
    using(var cmd=new SQLiteCommand("SELECT status FROM module_records WHERE id=@id AND feature=@f AND area_id=@a AND version=@v",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@id",sourceId),P("@f",sourceFeature),P("@a",s.AreaId),P("@v",expectedVersion)});
     oldStatus=Convert.ToString(cmd.ExecuteScalar());
    }
    if(String.IsNullOrEmpty(oldStatus))throw new InvalidOperationException("Hồ sơ nguồn đã thay đổi; cần tải lại.");
    string relationId=Id(),now=Now;
    Exec(c,tx,@"INSERT INTO module_relations(id,area_id,source_id,target_feature,target_id,relation_kind,created_by,created_utc)
      VALUES(@id,@area,@source,@feature,@target,@kind,@actor,@utc)",P("@id",relationId),P("@area",s.AreaId),P("@source",sourceId),
      P("@feature",targetFeature),P("@target",targetId),P("@kind",relationKind),P("@actor",s.Id),P("@utc",now));
    using(var cmd=new SQLiteCommand("UPDATE module_records SET version=version+1,updated_by=@actor,updated_utc=@utc WHERE id=@id AND feature=@f AND area_id=@a AND version=@v",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@actor",s.Id),P("@utc",now),P("@id",sourceId),P("@f",sourceFeature),P("@a",s.AreaId),P("@v",expectedVersion)});
     if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Nguồn thay đổi đồng thời; hủy liên kết.");
    }
    Exec(c,tx,@"INSERT INTO module_record_history(id,record_id,actor_id,old_version,new_version,old_status,new_status,occurred_utc)
      VALUES(@id,@source,@actor,@old,@new,@status,@status,@utc)",P("@id",Id()),P("@source",sourceId),P("@actor",s.Id),
      P("@old",expectedVersion),P("@new",expectedVersion+1),P("@status",oldStatus),P("@utc",now));
    Audit(c,tx,s.Id,"MODULE_LINK_"+sourceFeature,"module_record",sourceId);
    tx.Commit();return relationId;
   }
  }
  public static List<ModuleRelation> GetModuleRelations(Session s,string sourceFeature,string sourceId) {
   ModuleCatalog.Get(sourceFeature);
   using(var c=Open()) {
    Require(c,null,s,sourceFeature,"read",s==null?null:s.AreaId);
    if(Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM module_records WHERE area_id=@a AND feature=@f AND id=@id",
      P("@a",s.AreaId),P("@f",sourceFeature),P("@id",sourceId)))!=1)
     throw new UnauthorizedAccessException("Không có ID nguồn trong phạm vi cho phép.");
    var result=new List<ModuleRelation>();
    var candidates=new List<ModuleRelation>();
    using(var cmd=new SQLiteCommand(@"SELECT target_feature,target_id,relation_kind,created_utc FROM module_relations
      WHERE area_id=@a AND source_id=@id ORDER BY created_utc DESC LIMIT 200",c)) {
     cmd.Parameters.AddRange(new[]{P("@a",s.AreaId),P("@id",sourceId)});
     using(var r=cmd.ExecuteReader())while(r.Read()){
      string feature=r.GetString(0);
      candidates.Add(new ModuleRelation{Feature=feature,TargetId=r.GetString(1),Kind=r.GetString(2),CreatedUtc=r.GetString(3)});
     }
    }
    // A task or outgoing document can point back to its source through the original typed link.
    using(var cmd=new SQLiteCommand(@"SELECT feature,id FROM module_records
      WHERE area_id=@a AND related_feature=@f AND related_id=@id ORDER BY updated_utc DESC LIMIT 200",c)) {
     cmd.Parameters.AddRange(new[]{P("@a",s.AreaId),P("@f",sourceFeature),P("@id",sourceId)});
     using(var r=cmd.ExecuteReader())while(r.Read()){
      string feature=r.GetString(0);
      candidates.Add(new ModuleRelation{Feature=feature,TargetId=r.GetString(1),Kind="HỒ SƠ THAM CHIẾU",CreatedUtc=""});
     }
    }
    foreach(var candidate in candidates)
     if(HasPermission(c,null,s,candidate.Feature,"read",s.AreaId))result.Add(candidate);
    return result;
   }
  }

 }
}
