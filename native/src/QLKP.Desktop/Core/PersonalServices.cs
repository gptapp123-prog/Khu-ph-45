using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace QLKP.Desktop.Core {
 internal sealed class PersonalServiceDefinition {
  internal PersonalServiceDefinition(string feature,string label,params FieldDefinition[] fields) {
   Feature=feature;Label=label;Fields=fields;
  }
  internal string Feature {get;}
  public string Label {get;}
  internal FieldDefinition[] Fields {get;}
 }

 internal sealed class PersonalServiceRecord {
  internal long Version {get;set;}
  internal Dictionary<string,string> Values {get;set;}=new Dictionary<string,string>(StringComparer.Ordinal);
 }

 public static partial class DataStore {
  internal static readonly PersonalServiceDefinition[] PersonalServices={
   new PersonalServiceDefinition("F06","Nghĩa vụ quân sự",
    new FieldDefinition("screened_on","Ngày sơ tuyển","date"),
    new FieldDefinition("classification","Phân loại theo hồ sơ","choice:I|II|III|IV|V|VI"),
    new FieldDefinition("disposition","Diện theo hồ sơ","choice:Đang rà soát|Tạm hoãn|Miễn"),
    new FieldDefinition("basis","Lý do hoặc căn cứ hoãn / miễn")),
   new PersonalServiceDefinition("E04","Trẻ em và học sinh",
    new FieldDefinition("guardian","Cha / mẹ hoặc người giám hộ"),
    new FieldDefinition("household_group","Nhóm hộ","choice:Hộ nghèo|Hộ cận nghèo|Khác"),
    new FieldDefinition("immunization","Tình trạng tiêm chủng","choice:Đầy đủ|Chưa đầy đủ|Chưa xác minh"),
    new FieldDefinition("attending","Đang đi học","choice:Có|Không|Chưa xác minh"),
    new FieldDefinition("school","Trường đang học")),
   new PersonalServiceDefinition("E06","Bảo hiểm y tế",
    new FieldDefinition("card_no","Số thẻ BHYT"),
    new FieldDefinition("card_group","Nhóm đối tượng"),
    new FieldDefinition("facility","Nơi đăng ký khám chữa bệnh ban đầu"),
    new FieldDefinition("expires_on","Ngày hết hạn","date")),
   new PersonalServiceDefinition("E02","Người có công",
    new FieldDefinition("category","Loại đối tượng"),
    new FieldDefinition("rating","Tỷ lệ thương tật / bệnh (%)","percentage"),
    new FieldDefinition("decision","Số quyết định"),
    new FieldDefinition("allowance","Trợ cấp theo hồ sơ (đồng/tháng)","amount"),
    new FieldDefinition("caregiver","Người quản lý / chăm sóc"))
  };

  internal static readonly string[] PersonalServiceRights={"F06:personal_read","F06:personal_write","E04:personal_read","E04:personal_write",
   "E06:personal_read","E06:personal_write","E02:personal_read","E02:personal_write"};

  internal static bool AllowsPersonalService(Session session,string feature,bool write) {
   using(var c=Open()) {
    var area=session?.AreaId;
    return HasPermission(c,null,session,"B02","read",area)&&HasPermission(c,null,session,feature,"personal_read",area)&&
     (!write||HasPermission(c,null,session,feature,"personal_write",area));
   }
  }

  static PersonalServiceDefinition AuthorizePersonalService(SQLiteConnection c,SQLiteTransaction tx,Session session,
    string personId,string feature,bool write) {
   var definition=PersonalServices.Single(x=>x.Feature==feature);
   var area=session?.AreaId;
   Require(c,tx,session,"B02","read",area);
   Require(c,tx,session,feature,"personal_read",area);
   if(write)Require(c,tx,session,feature,"personal_write",area);
   if(Convert.ToInt64(Scalar(c,tx,"SELECT count(*) FROM people WHERE id=@person AND area_id=@area",
    P("@person",personId),P("@area",area)))!=1)throw new UnauthorizedAccessException("Nhân khẩu ngoài phạm vi khu phố.");
   return definition;
  }

  internal static PersonalServiceRecord ReadPersonalService(Session session,string personId,string feature) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    AuthorizePersonalService(c,tx,session,personId,feature,false);
    var record=new PersonalServiceRecord();
    using(var q=new SQLiteCommand("SELECT payload,version FROM personal_service_records WHERE person_id=@person AND area_id=@area AND feature=@feature",c,tx)) {
     q.Parameters.AddRange(new[]{P("@person",personId),P("@area",session.AreaId),P("@feature",feature)});
     using(var r=q.ExecuteReader())if(r.Read()) {
      record.Values=XElement.Parse(r.GetString(0)).Elements("field").ToDictionary(x=>(string)x.Attribute("key"),x=>x.Value,StringComparer.Ordinal);
      record.Version=r.GetInt64(1);
     }
    }
    Audit(c,tx,session.Id,"PERSONAL_SERVICE_READ_"+feature,"person",personId);
    tx.Commit();return record;
   }
  }

  internal static void WritePersonalService(Session session,string personId,string feature,PersonalServiceRecord record) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    var definition=AuthorizePersonalService(c,tx,session,personId,feature,true);
    if(record.Values.Keys.Any(key=>!definition.Fields.Any(field=>field.Key==key)))
     throw new InvalidOperationException("Hồ sơ có trường ngoài danh mục.");
    var data=new XElement("record");
    foreach(var field in definition.Fields) {
     if(!record.Values.TryGetValue(field.Key,out string value)||String.IsNullOrWhiteSpace(value))continue;
     value=value.Trim();
     if(value.Length>250)throw new InvalidOperationException("Trường vượt 250 ký tự: "+field.Label);
     if(field.Type=="date")value=NeighborhoodDate(value);
     if(field.Type=="percentage"&&(!Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int ratio)||ratio>100))
      throw new InvalidOperationException("Tỷ lệ cần nằm từ 0 đến 100.");
     if(field.Type=="amount"&&!Int64.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out long amount))
      throw new InvalidOperationException("Số tiền cần là số nguyên không âm, đơn vị đồng.");
     if(field.Type.StartsWith("choice:",StringComparison.Ordinal)&&!field.Type.Substring(7).Split('|').Contains(value))
      throw new InvalidOperationException("Giá trị ngoài danh mục: "+field.Label);
     data.Add(new XElement("field",new XAttribute("key",field.Key),value));
    }
    if(!data.HasElements&&record.Version==0)throw new InvalidOperationException("Nhập ít nhất một thông tin trước khi lưu.");
    var arguments=new[]{P("@person",personId),P("@area",session.AreaId),P("@feature",feature),P("@payload",data.ToString(SaveOptions.DisableFormatting)),P("@utc",Now),P("@version",record.Version),P("@expiry",feature=="E06"?InsuranceExpiry(data):"")};
    string sql=record.Version==0
     ?"INSERT INTO personal_service_records(person_id,area_id,feature,payload,version,updated_utc,expiry_date) SELECT @person,@area,@feature,@payload,1,@utc,@expiry WHERE @version=0 AND NOT EXISTS(SELECT 1 FROM personal_service_records WHERE person_id=@person AND feature=@feature)"
     :"UPDATE personal_service_records SET payload=@payload,version=version+1,updated_utc=@utc,expiry_date=@expiry WHERE person_id=@person AND area_id=@area AND feature=@feature AND version=@version";
    using(var q=new SQLiteCommand(sql,c,tx)) {
     q.Parameters.AddRange(arguments);
     if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Hồ sơ đã thay đổi. Mở lại để đối chiếu trước khi lưu.");
    }
    Audit(c,tx,session.Id,"PERSONAL_SERVICE_WRITE_"+feature,"person",personId);
    tx.Commit();
   }
  }

  internal static string PersonalInsuranceState(PersonalServiceRecord record,DateTime today) {
   if(!record.Values.TryGetValue("expires_on",out string value)||
    !DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime expires))
    return "Chưa ghi ngày hết hạn";
   int days=(expires-today.Date).Days;
   return days<0?"Hết hạn":days<=30?"Sắp hết hạn ("+days+" ngày)":"Còn hạn";
  }
 }
}
