using System;
using System.Collections.Generic;
using System.Linq;
namespace QLKP.Desktop.Core {
 public sealed class FieldDefinition {
  public string Key {get;private set;} public string Label {get;private set;} public string Type {get;private set;}
  public bool Required {get;private set;}
  public FieldDefinition(string key,string label,string type="text",bool required=false) {
   Key=key; Label=label;Type=type;Required=required;
  }
 }
 public sealed class ModuleDefinition {
  public string Code {get;private set;} public string Label {get;private set;}
  public string[] Statuses {get;private set;} public FieldDefinition[] Fields {get;private set;}
  public ModuleDefinition(string code,string label,string[] statuses,params FieldDefinition[] fields) {
   Code=code;Label=label;Statuses=statuses;Fields=fields;
  }
 }
 // Whitelist: these are executable MODULE RECORD types, not just menu entries.
 public static class ModuleCatalog {
  static FieldDefinition F(string key,string label,string type="text",bool required=false) {
   return new FieldDefinition(key,label,type,required);
  }
  public static readonly ModuleDefinition[] Editable = {
   new ModuleDefinition("A02","Nhiệm vụ và tiến độ",new[]{"Mới","Đang xử lý","Chờ xác nhận","Hoàn thành nội bộ","Tạm dừng"},
    F("deadline","Hạn thực hiện (yyyy-MM-dd)","date"), F("progress","Tiến độ 0–100 (%)","percentage"), F("assignee_id","ID tài khoản phụ trách","account"),F("result","Kết quả xử lý")),
   new ModuleDefinition("A03","Kế hoạch và chỉ tiêu",new[]{"Dự thảo","Đang triển khai","Đã rà soát","Tạm dừng"},
    F("metric","Chỉ tiêu / tiêu chí", "text",true),F("target","Mục tiêu (số)","number"),F("actual","Kết quả đã ghi nhận (số)","number"), F("unit","Đơn vị"),F("deadline","Mốc thời gian (yyyy-MM-dd)","date")),
   new ModuleDefinition("A04","Lịch công tác",new[]{"Dự kiến","Đã xác nhận lịch","Đã diễn ra","Hủy"},
    F("event_date","Ngày công tác (yyyy-MM-dd)","date",true),F("event_time","Giờ (HH:mm)","time"),F("location","Địa điểm"),F("organizer","Đơn vị / người liên hệ")),
   new ModuleDefinition("A06","Bàn giao nhiệm kỳ",new[]{"Dự thảo","Đang rà soát","Đã bàn giao nội bộ"},
    F("handover_date","Ngày bàn giao (yyyy-MM-dd)","date"),F("outgoing","Bên bàn giao"),F("incoming","Bên nhận"),F("evidence","Thông tin tài liệu bàn giao")),
   new ModuleDefinition("B04","Địa bàn và địa chỉ",new[]{"Mới","Đang rà soát","Đã kiểm tra nội bộ"},
    F("house_number","Số nhà"),F("street","Đường / lối đi", "text",true),F("locality","Khu vực / phường"),F("address_kind","Loại địa chỉ")),
   new ModuleDefinition("B05","Nhà ở và công trình",new[]{"Mới","Đang kiểm tra","Đã cập nhật","Ngừng theo dõi"},
    F("construction_kind","Loại công trình","text",true),F("current_use","Mục đích sử dụng"),F("structure_notes","Thông tin công trình")),
   new ModuleDefinition("B06","Nhà trọ và cơ sở lưu trú",new[]{"Mới","Đang theo dõi","Tạm ngừng","Ngừng theo dõi"},
    F("lodging_kind","Loại hình lưu trú","text",true),F("rooms","Số phòng","nonnegative"),F("contact_note","Thông tin liên hệ do người dùng khai")),
   new ModuleDefinition("B07","Cơ sở kinh doanh",new[]{"Mới","Đang hoạt động","Tạm ngừng","Ngừng hoạt động"},
    F("business_kind","Nhóm ngành / loại hình","text",true),F("operator_name","Tên cơ sở / người đại diện khai báo"),F("registration_note","Thông tin giấy tờ do người dùng khai")),
   // Group C – document registers: internal tracking does NOT sign, publish or send documents.
   new ModuleDefinition("C01","Văn bản đến",new[]{"Tiếp nhận nội bộ","Đã phân công","Đang xử lý","Đã xử lý nội bộ","Đóng theo dõi"},
    F("doc_no","Số / ký hiệu văn bản","text",true),F("doc_date","Ngày văn bản (yyyy-MM-dd)","date",true),
    F("sender","Cơ quan / đơn vị gửi","text",true),F("received_on","Ngày tiếp nhận","date"),
    F("responsible_id","ID người phụ trách","account"),F("deadline","Hạn xử lý","date"),
    F("classification","Mức hạn chế truy cập","choice:Công khai nội bộ|Hạn chế|Chưa phân loại"),F("handling_note","Kết quả / ghi chú xử lý")),
   new ModuleDefinition("C02","Văn bản đi",new[]{"Dự thảo","Chờ rà soát","Đã xác nhận nội bộ","Đã ghi nhận gửi","Lưu hồ sơ"},
    F("doc_no","Số / ký hiệu","text"),F("doc_date","Ngày văn bản","date"),
    F("recipient","Nơi nhận","text"),F("sent_on","Ngày ghi nhận gửi","date"),
    F("send_evidence","Căn cứ ghi nhận gửi"),F("signing_note","Ghi chú người kiểm tra")),
   new ModuleDefinition("C03","Hồ sơ công việc",new[]{"Mở hồ sơ","Đang xử lý","Chờ tổng hợp","Đóng nội bộ"},
    F("owner_id","ID tài khoản phụ trách","account"),F("opened_on","Ngày mở","date"),
    F("deadline","Hạn xử lý","date"),F("result","Kết quả xử lý"),F("reference_note","Căn cứ hồ sơ")),
   new ModuleDefinition("C04","Họp dân và hội nghị",new[]{"Dự kiến","Đã thông báo nội bộ","Đã diễn ra","Hủy lịch"},
    F("meeting_date","Ngày họp","date",true),F("meeting_time","Giờ họp","time"),
    F("venue","Địa điểm","text",true),F("organizer","Người / bộ phận tổ chức"),F("attendance_note","Số lượng / ghi chú tham dự")),
   new ModuleDefinition("C05","Biên bản và quyết nghị",new[]{"Bản nháp","Chờ rà soát","Đã xác nhận nội bộ","Lưu hồ sơ"},
    F("minutes_date","Ngày lập biên bản","date",true),F("secretary","Người ghi biên bản","text",true),
    F("conclusion","Nội dung kết luận"),F("reviewer_note","Căn cứ xác nhận nội bộ")),
   new ModuleDefinition("C06","Kho mẫu biểu",new[]{"Mẫu nháp","Đang rà soát","Đã duyệt nội bộ","Ngừng sử dụng"},
    F("template_kind","Loại mẫu","text",true),F("template_version","Phiên bản mẫu","text",true),
    F("usage_note","Hướng dẫn sử dụng"),F("reviewer_note","Căn cứ duyệt nội bộ")),
   // Group D – separate purpose-limited personnel / organizational registers.
   new ModuleDefinition("D01","Nhân sự khu phố",new[]{"Hồ sơ nháp","Đang đảm nhiệm","Tạm ngừng","Đã kết thúc"},
    F("full_name","Họ tên theo hồ sơ","text",true),F("duty","Chức trách","text",true),
    F("account_id","ID tài khoản liên kết","account"),F("start_on","Bắt đầu","date"),
    F("end_on","Kết thúc","date"),F("appointment_reference","Căn cứ phân công / bổ nhiệm")),
   new ModuleDefinition("D02","Chi bộ và đảng viên",new[]{"Hồ sơ nội bộ","Chờ đối chiếu","Đã đối chiếu nguồn","Ngừng theo dõi"},
    F("full_name","Họ tên theo hồ sơ","text",true),F("branch_role","Chức trách trong chi bộ"),
    F("source_reference","Căn cứ hồ sơ do người có quyền cung cấp"),F("reviewed_on","Ngày đối chiếu","date")),
   new ModuleDefinition("D03","Ban Công tác Mặt trận",new[]{"Hồ sơ nháp","Đang tham gia","Tạm ngừng","Đã kết thúc"},
    F("full_name","Họ tên","text",true),F("duty","Vai trò / công việc"),
    F("start_on","Bắt đầu","date"),F("source_reference","Căn cứ phân công")),
   new ModuleDefinition("D04","Đoàn thể và chi hội",new[]{"Hồ sơ nháp","Đang hoạt động","Tạm ngừng","Ngừng hoạt động"},
    F("organization_kind","Tên đoàn thể / chi hội","text",true),F("representative","Người đại diện"),
    F("responsibility","Nhiệm vụ"),F("source_reference","Căn cứ / quyết định")),
   new ModuleDefinition("D05","Cộng tác viên",new[]{"Dự kiến","Đang phối hợp","Tạm dừng","Kết thúc"},
    F("full_name","Họ tên","text",true),F("assignment","Nhiệm vụ phối hợp","text",true),
    F("start_on","Ngày bắt đầu","date"),F("account_id","ID tài khoản liên kết","account"),
    F("source_reference","Căn cứ phân công")),

   // E group: coordination registers, not legal eligibility, medical records or payment authorizations.
   new ModuleDefinition("E01","An sinh xã hội",new[]{"Bản nháp","Đang tiếp nhận","Chờ đối chiếu","Đã chuyển cơ quan có thẩm quyền","Đã ghi nhận phản hồi"},
    F("program_kind","Nhóm chương trình","choice:Trợ giúp khẩn cấp|Hỗ trợ sinh kế|Tư vấn chính sách|Khác",true),
    F("received_on","Ngày tiếp nhận","date",true),F("source_reference","Nguồn, căn cứ tiếp nhận"),
    F("forwarding_reference","Mã phiếu chuyển / căn cứ chuyển"),F("feedback_reference","Căn cứ phản hồi"),
    F("followup_due","Ngày nhắc theo dõi","date")),
   new ModuleDefinition("E02","Người có công",new[]{"Hồ sơ nháp","Chờ đối chiếu","Đã đối chiếu hồ sơ","Đã chuyển xác minh"},
    F("source_reference","Nguồn hồ sơ được phép sử dụng"),F("reviewed_on","Ngày đối chiếu","date"),
    F("review_note","Ghi chú xác minh nội bộ"),F("forwarding_reference","Căn cứ chuyển xác minh")),
   new ModuleDefinition("E03","Người cao tuổi",new[]{"Hồ sơ nháp","Chờ đối chiếu","Đang theo dõi","Đã kết thúc"},
    F("support_program","Nhóm hỗ trợ","choice:Thăm hỏi|Chăm sóc cộng đồng|Thông tin chính sách|Khác"),
    F("source_reference","Nguồn xác minh"),F("contact_note","Cách liên hệ được phép"),
    F("followup_on","Ngày theo dõi tiếp","date")),
   new ModuleDefinition("E04","Trẻ em và học sinh",new[]{"Hồ sơ nháp","Chờ rà soát","Đang phối hợp","Đã kết thúc"},
    F("program_kind","Hoạt động","choice:Hỗ trợ học tập|Tư vấn giáo dục|Hoạt động cộng đồng|Khác",true),
    F("school_year","Năm học","text"),F("source_reference","Nguồn dữ liệu đã kiểm tra"),
    F("contact_note","Đầu mối phối hợp")),
   new ModuleDefinition("E05","Khuyến học",new[]{"Đề xuất nội bộ","Đang tổng hợp","Đã chuyển xét","Đã có phản hồi"},
    F("activity_kind","Loại hoạt động","choice:Học bổng|Khuyến tài|Vận động nguồn lực|Tuyên truyền",true),
    F("school_year","Năm học","text"),F("source_reference","Nguồn đề xuất"),
    F("forwarding_reference","Căn cứ chuyển xét"),F("feedback_reference","Căn cứ kết quả phản hồi")),
   new ModuleDefinition("E06","Y tế và dân số",new[]{"Dự kiến","Đang phối hợp","Đã tổng hợp","Kết thúc đợt"},
    F("activity_kind","Loại hoạt động cộng đồng","choice:Truyền thông sức khỏe|Vệ sinh phòng bệnh|Hoạt động dân số|Khác",true),
    F("held_on","Ngày thực hiện","date"),F("aggregate_count","Tổng lượt tham gia (không danh tính)","nonnegative"),
    F("source_reference","Nguồn tổng hợp"),F("coordinator","Đầu mối phụ trách")),
   new ModuleDefinition("E07","Việc làm và đào tạo",new[]{"Đề xuất","Đang kết nối","Đã giới thiệu thông tin","Đã nhận phản hồi"},
    F("activity_kind","Nhóm hoạt động","choice:Thông tin việc làm|Đào tạo nghề|Hướng nghiệp|Khác",true),
    F("provider","Đơn vị tổ chức / tư vấn"),F("event_on","Ngày hoạt động","date"),
    F("source_reference","Nguồn thông tin"),F("feedback_reference","Căn cứ phản hồi")),

   // Group F – coordination only; NOT police investigations, PCCC certification,
   // emergency dispatch, construction approval or military-service decisions.
   new ModuleDefinition("F01","An ninh và trật tự",new[]{"Ghi nhận nội bộ","Chờ kiểm tra nguồn","Đã chuyển đầu mối","Đã có phản hồi"},
    F("incident_kind","Nhóm sự việc","choice:Trật tự công cộng|Tuyên truyền an toàn|Phối hợp nắm tình hình|Khác",true),
    F("occurred_on","Ngày ghi nhận","date"),F("location_note","Địa điểm chung (không nêu cá nhân)"),
    F("source_reference","Nguồn ghi nhận"),F("forwarding_reference","Căn cứ chuyển đầu mối"),F("feedback_reference","Căn cứ phản hồi")),
   new ModuleDefinition("F02","Phòng cháy chữa cháy",new[]{"Ghi nhận nội bộ","Đang phối hợp","Đã chuyển đầu mối PCCC","Đã có phản hồi"},
    F("activity_kind","Nội dung phối hợp","choice:Tuyên truyền PCCC|Rà soát trang bị|Diễn tập|Phản ánh nguy cơ|Khác",true),
    F("activity_on","Ngày ghi nhận","date"),F("location_note","Vị trí chung"),
    F("source_reference","Nguồn hồ sơ"),F("forwarding_reference","Căn cứ chuyển đầu mối PCCC"),F("feedback_reference","Căn cứ phản hồi")),
   new ModuleDefinition("F03","Thiên tai và cứu nạn",new[]{"Ghi nhận","Đang phối hợp","Đã chuyển cơ quan chức năng","Đã ghi nhận phản hồi"},
    F("event_kind","Loại tình huống","choice:Ngập nước|Cây ngã|Sạt lở|Gió mạnh|Khác",true),
    F("reported_on","Ngày ghi nhận","date"),F("location_note","Khu vực ảnh hưởng"),
    F("source_reference","Nguồn thông tin"),F("forwarding_reference","Căn cứ chuyển"),F("feedback_reference","Căn cứ phản hồi")),
   new ModuleDefinition("F04","Môi trường",new[]{"Tiếp nhận","Đang xác minh nguồn","Đã chuyển xử lý","Đã có phản hồi"},
    F("issue_kind","Nhóm vấn đề","choice:Rác thải|Thoát nước|Tiếng ồn|Vệ sinh công cộng|Khác",true),
    F("observed_on","Ngày ghi nhận","date"),F("location_note","Vị trí chung"),
    F("source_reference","Nguồn ghi nhận"),F("forwarding_reference","Căn cứ chuyển xử lý"),F("feedback_reference","Căn cứ phản hồi")),
   new ModuleDefinition("F05","Hạ tầng và công trình công cộng",new[]{"Ghi nhận","Đang khảo sát nội bộ","Đã chuyển cơ quan phụ trách","Đã ghi nhận kết quả"},
    F("asset_kind","Hạng mục","choice:Đường đi|Chiếu sáng|Thoát nước|Cây xanh|Công trình công cộng|Khác",true),
    F("location_note","Vị trí"),F("observed_on","Ngày ghi nhận","date"),
    F("source_reference","Nguồn khảo sát"),F("forwarding_reference","Căn cứ chuyển"),F("feedback_reference","Căn cứ kết quả")),
   new ModuleDefinition("F06","Nghĩa vụ quân sự",new[]{"Kế hoạch phối hợp","Đang phối hợp","Đã tổng hợp số liệu","Kết thúc đợt"},
    F("activity_kind","Hoạt động tổng hợp","choice:Tuyên truyền|Lịch phối hợp|Tổng hợp số lượng|Khác",true),
    F("activity_on","Ngày hoạt động","date"),F("aggregate_count","Số lượt / số lượng không định danh","nonnegative"),
    F("source_reference","Nguồn số liệu tổng hợp"),F("coordinator","Đầu mối phối hợp")),
   new ModuleDefinition("F07","Văn hóa và phong trào",new[]{"Dự kiến","Đang tổ chức","Đã tổng kết nội bộ","Kết thúc"},
    F("activity_kind","Loại hình","choice:Văn nghệ|Thể thao|Tuyên truyền|Phong trào cộng đồng|Khác",true),
    F("event_on","Ngày tổ chức","date"),F("venue","Địa điểm"),F("source_reference","Nguồn kế hoạch / tổng kết"),
    F("aggregate_count","Tổng lượt tham gia (không định danh)","nonnegative")),

   // G – Reception and coordination, no personal case conclusions in plaintext PILOT.
   new ModuleDefinition("G01","Tiếp nhận phản ánh",new[]{"Tiếp nhận nội bộ","Chờ kiểm tra nguồn","Đã phân công","Đã chuyển đầu mối","Đã có phản hồi","Đóng theo dõi"},
    F("issue_kind","Nhóm nội dung","choice:Môi trường|Hạ tầng|Trật tự công cộng|Dịch vụ công|Khác",true),
    F("intake_channel","Kênh tiếp nhận","choice:Trực tiếp|Điện thoại|Văn bản|Ứng dụng|Khác",true),
    F("received_on","Ngày tiếp nhận","date",true),F("location_note","Địa điểm không định danh"),
    F("source_reference","Nguồn / căn cứ tiếp nhận"),F("assignee_id","ID người được phân công","account"),
    F("forwarding_reference","Mã phiếu/căn cứ chuyển"),F("feedback_reference","Căn cứ phản hồi"),
    F("closure_reference","Căn cứ đóng theo dõi")),
   new ModuleDefinition("G02","Hòa giải cơ sở",new[]{"Đề xuất theo dõi","Đang phối hợp","Đã lập biên bản nội bộ","Đã chuyển cơ quan phụ trách","Kết thúc theo dõi"},
    F("case_kind","Nhóm nội dung","choice:Mâu thuẫn sinh hoạt|Quan hệ láng giềng|Khác",true),
    F("opened_on","Ngày mở theo dõi","date",true),F("coordinator","Người phụ trách phối hợp"),
    F("source_reference","Nguồn / đề nghị phối hợp"),F("minutes_reference","Mã biên bản đã kiểm tra"),
    F("forwarding_reference","Căn cứ chuyển cơ quan"),F("closure_reference","Căn cứ kết thúc theo dõi")),
   new ModuleDefinition("G03","Tuyên truyền và vận động",new[]{"Dự thảo kế hoạch","Đã rà soát nội bộ","Đã ghi nhận thông báo","Đã tổng hợp kết quả"},
    F("campaign_kind","Chủ đề","choice:Môi trường|An toàn cộng đồng|Chính sách địa phương|Văn hóa|Khác",true),
    F("planned_on","Ngày dự kiến","date"),F("channel","Hình thức","choice:Trực tiếp|Văn bản|Bảng tin|Khác"),
    F("source_reference","Nguồn tài liệu được duyệt"),F("notice_reference","Căn cứ thông báo"),
    F("aggregate_count","Số lượt tổng hợp không định danh","nonnegative"),
    F("result_reference","Căn cứ kết quả")),
   new ModuleDefinition("G04","Phối hợp phường/cơ quan",new[]{"Nháp nội bộ","Đã chuyển đầu mối","Chờ phản hồi","Đã có phản hồi","Đóng theo dõi"},
    F("agency_kind","Đầu mối tiếp nhận","choice:Phường|Cơ quan chuyên môn|Đơn vị dịch vụ công|Khác",true),
    F("recipient","Tên đơn vị / bộ phận","text",true),F("transferred_on","Ngày ghi nhận chuyển","date"),
    F("source_reference","Nguồn yêu cầu"),F("forwarding_reference","Căn cứ chuyển"),
    F("feedback_reference","Căn cứ phản hồi"),F("closure_reference","Căn cứ đóng theo dõi")),


  };
  public static readonly string[] DashboardRead = {"A01","A05"};
  public static ModuleDefinition Get(string code) {
   var spec=Editable.FirstOrDefault(x=>x.Code==code);
   if(spec==null)throw new InvalidOperationException("Mô-đun chưa có quy trình tạo/sửa hồ sơ: "+code);
   return spec;
  }
  public static bool IsEditable(string code) {return Editable.Any(x=>x.Code==code);}
  public static List<string> AdditionalRights() {
   var rights=new List<string>{"B01:update","A01:read","A05:read","H01:read","H02:read","H03:read","H04:read","H05:read","H05:export","I05:read","I06:read","I06:export","I07:read","I07:manage","I09:read"};
   rights.Add("I03:read");rights.Add("I09:test");rights.Add("I09:export");rights.Add("I08:merge");
   foreach(var m in Editable) foreach(var action in new[]{"read","create","update"}) rights.Add(m.Code+":"+action);
   return rights;
  }
  // Typed outgoing references: no arbitrary cross-module navigation for Groups F/G.
  static readonly Dictionary<string,string[]> ScopedLinks = new Dictionary<string,string[]> {
   {"F01",new[]{"B04","A02"}},
   {"F02",new[]{"B05","B06","B07"}},
   {"F03",new[]{"B04","B05","A02"}},
   {"F04",new[]{"B04","B05"}},
   {"F05",new[]{"B04","B05","A02"}},
   {"F06",new string[0]},
   {"F07",new[]{"A04","C04"}},
   {"G01",new[]{"A02","B04","B05","F04","F05","G04"}},
   {"G02",new[]{"G01","C04","C05","A02"}},
   {"G03",new[]{"A04","C04","C06","F07"}},
   {"G04",new[]{"G01","A02","C01","C02","F04","F05"}}
  };
  public static string[] LinkedFeatures() {return Editable.Select(x=>x.Code).Concat(new[]{"B01","B02"}).ToArray();}
  public static string[] AllowedTargets(string sourceFeature) {
   string[] targets;
   return ScopedLinks.TryGetValue(sourceFeature,out targets)?targets:LinkedFeatures();
  }
 }
}
