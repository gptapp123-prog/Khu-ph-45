using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QLKP.Desktop.Core {
 // Uncommitted information remains in memory. A preview is not an authorization token.
 public sealed class IntakePreviewRow {
  public int LineNumber {get;set;}
  public string SheetName {get;set;}
  public string TargetKey {get;set;}
  public string Operation {get;set;}
  public string Notice {get;set;}
  internal bool CandidateWarning {get;set;}
 }
 public sealed class IntakePreview {
  public string RequestId {get;set;}
  public string Mode {get;set;}
  public string SourceKind {get;set;}
  public string SourceLabel {get;set;}
  public string Payload {get;set;}
  public string Digest {get;set;}
  public string ActorId {get;set;}
  public string AreaId {get;set;}
  public bool HasCandidateWarnings {get;set;}
  public List<IntakePreviewRow> Rows {get;set;} = new List<IntakePreviewRow>();
 }
 public static partial class DataStore {
  const int IntakeMaxRows=5000;
  const int IntakeMaxChars=2*1024*1024;
  static readonly HashSet<string> IntakeKinds=new HashSet<string>(StringComparer.Ordinal){"CSV_UTF8","XLSX_LOCAL","CLIPBOARD","MANUAL","OCR_DRAFT","QR_DRAFT"};
  static readonly HashSet<string> IntakeModes=new HashSet<string>(StringComparer.Ordinal){"CREATE_HOUSEHOLD","ADD_MEMBER","ENRICH_PERSON"};

  // Parse quoted CSV/TSV/semicolon including escaped quotes and embedded line breaks.
  // Delimiter is inferred from the HEADER, never from data rows; no spreadsheet formulas are evaluated.
  static List<string[]> ReadIntakeTable(string input,bool allowHeaderOnly=false,int maxColumns=20) {
   if(String.IsNullOrWhiteSpace(input))throw new InvalidDataException("Không có dữ liệu để thu thập.");
   if(input.Length>IntakeMaxChars)throw new InvalidDataException("Nguồn vượt 2 MiB ký tự. Chia thành nhiều lô.");
   string raw=input.TrimStart('\ufeff');
   char delimiter=DetectHeaderDelimiter(raw);
   var table=new List<string[]>();var fields=new List<string>();var cell=new StringBuilder();
   bool quoted=false, endedQuote=false, atCellStart=true;
   for(int i=0;i<raw.Length;i++) {
    char ch=raw[i];
    if(quoted) {
     if(ch=='"' && i+1<raw.Length && raw[i+1]=='"'){cell.Append('"');i++;}
     else if(ch=='"'){quoted=false;endedQuote=true;}
     else cell.Append(ch);
     continue;
    }
    if(ch=='"' && atCellStart){quoted=true;atCellStart=false;continue;}
    if(ch=='"')throw new InvalidDataException("Dấu nháy kép không hợp lệ trong CSV.");
    if(ch==delimiter || ch=='\n' || ch=='\r') {
     fields.Add(cell.ToString().Trim());cell.Clear();atCellStart=true;endedQuote=false;
     if(ch==delimiter)continue;
     if(ch=='\r' && i+1<raw.Length && raw[i+1]=='\n')i++;
     if(fields.Any(v=>v.Length>0))table.Add(fields.ToArray());
     fields=new List<string>();
     if(table.Count>IntakeMaxRows+1)throw new InvalidDataException("Lô tối đa 5.000 dòng dữ liệu.");
     continue;
    }
    if(endedQuote && !Char.IsWhiteSpace(ch))throw new InvalidDataException("CSV có ký tự sau dấu nháy đóng.");
    if(!Char.IsWhiteSpace(ch))endedQuote=false;
    if(Char.IsControl(ch) && ch!='\t')throw new InvalidDataException("Dữ liệu có ký tự điều khiển không hợp lệ.");
    cell.Append(ch);atCellStart=false;
   }
   if(quoted)throw new InvalidDataException("CSV có trường đặt trong nháy kép chưa đóng.");
   fields.Add(cell.ToString().Trim());
   if(fields.Any(v=>v.Length>0))table.Add(fields.ToArray());
   if(table.Count<(allowHeaderOnly?1:2))throw new InvalidDataException("Cần tiêu đề cột và ít nhất một dòng dữ liệu.");
   int width=table[0].Length;
   if(width>maxColumns || width<2)throw new InvalidDataException("Số cột nguồn không hợp lệ (2–"+maxColumns+").");
   for(int i=1;i<table.Count;i++)if(table[i].Length!=width)
    throw new InvalidDataException("Dòng "+(i+1)+" có "+table[i].Length+" cột; tiêu đề có "+width+" cột.");
   return table;
  }
  static char DetectHeaderDelimiter(string input) {
   int tab=0,semi=0,comma=0;bool quoted=false;
   for(int i=0;i<input.Length;i++){
    char ch=input[i];
    if(ch=='"') {
     if(quoted && i+1<input.Length && input[i+1]=='"')i++;
     else quoted=!quoted;
     continue;
    }
    if(!quoted && (ch=='\r'||ch=='\n'))break;
    if(!quoted){if(ch=='\t')tab++;else if(ch==';')semi++;else if(ch==',')comma++;}
   }
   if(tab>0)return '\t';if(semi>0)return ';';if(comma>0)return ',';
   throw new InvalidDataException("Không xác định được cột; cần CSV, TSV hoặc dấu chấm phẩy.");
  }
  static string CanonicalHeader(string header) {
   string h=NormalizeSearch(header).Replace('_',' ').Replace('-',' ').Trim();
   switch(h) {
    case "ma ho": case "ma ho gia dinh": case "household code":return "household_code";
    case "dia chi": case "address":return "address";
    case "chu ho": case "ten chu ho": case "head":return "head";
    case "ho ten": case "ho va ten": case "full name":return "full_name";
    case "ngay sinh": case "date of birth": case "dob":return "birth_date";
    case "gioi tinh": case "gender":return "gender";
    case "quan he": case "quan he voi chu ho": case "relationship":return "relationship";
    case "ma nguon v2.1":return "legacy_person_key";
    case "ma ho so": case "person id": case "ma nhan khau":return "person_id";
    case "phien ban": case "version":case "expected version":return "expected_version";
   }
   return "!"+h; // unknown columns are NOT silently discarded
  }
  internal static string[] IntakeHeaders(string mode) {
   if(mode=="CREATE_HOUSEHOLD")return new[]{"Mã hộ","Địa chỉ","Chủ hộ"};
   if(mode=="ADD_MEMBER")return new[]{"Mã hộ","Họ tên","Ngày sinh","Giới tính","Quan hệ"};
   if(mode=="ENRICH_PERSON")return new[]{"Mã hồ sơ","Phiên bản","Ngày sinh","Giới tính"};
   throw new InvalidOperationException("Chế độ nhập không hợp lệ.");
  }
  static HashSet<string> ExpectedFields(string mode) {
   if(mode=="CREATE_HOUSEHOLD")return new HashSet<string>(new[]{"household_code","address","head","birth_date","gender"});
   if(mode=="ADD_MEMBER")return new HashSet<string>(new[]{"household_code","full_name","birth_date","gender","relationship"});
   return new HashSet<string>(new[]{"person_id","expected_version","birth_date","gender"});
  }
  static List<Dictionary<string,string>> ParseIntakeRows(string mode,string input,bool allowSourceKeys=false) {
   if(!IntakeModes.Contains(mode))throw new InvalidOperationException("Chế độ thu dữ liệu không hợp lệ.");
   var matrix=ReadIntakeTable(input);
   var headers=matrix[0].Select(CanonicalHeader).ToArray();
   var expected=ExpectedFields(mode);
   if(allowSourceKeys&&mode!="ENRICH_PERSON")expected.Add("legacy_person_key");
   if(headers.Distinct(StringComparer.Ordinal).Count()!=headers.Length)throw new InvalidDataException("Tiêu đề bị trùng sau ánh xạ.");
   if(headers.Any(x=>!expected.Contains(x)))throw new InvalidDataException("Có cột chưa được ánh xạ: "+String.Join(", ",headers.Where(x=>!expected.Contains(x))));
   var required=mode=="CREATE_HOUSEHOLD"?new[]{"household_code","address","head"}:
     mode=="ADD_MEMBER"?new[]{"household_code","full_name","relationship"}:new[]{"person_id","expected_version"};
   foreach(var col in required)if(!headers.Contains(col))throw new InvalidDataException("Thiếu cột bắt buộc: "+col);
   var result=new List<Dictionary<string,string>>();
   for(int i=1;i<matrix.Count;i++){
    var row=new Dictionary<string,string>(StringComparer.Ordinal);
    for(int k=0;k<headers.Length;k++){
     string value=matrix[i][k];
     if(value.Length>300 || value.Any(ch=>Char.IsControl(ch)))throw new InvalidDataException("Dòng "+(i+1)+": trường quá dài hoặc chứa điều khiển.");
     row[headers[k]]=value;
    }
    foreach(var col in required)if(String.IsNullOrWhiteSpace(GetIntake(row,col)))throw new InvalidDataException("Dòng "+(i+1)+": thiếu "+col);
    if(row.ContainsKey("birth_date") && GetIntake(row,"birth_date")!="")row["birth_date"]=CanonicalBirthDate(row["birth_date"],i+1);
    string genderValue=GetIntake(row,"gender");
    if(genderValue.Length>0){
     string normalizedGender=NormalizeSearch(genderValue);
     if(normalizedGender=="nam")row["gender"]="Nam";
     else if(normalizedGender=="nu")row["gender"]="Nữ";
     else if(normalizedGender=="khac")row["gender"]="Khác";
     else if(normalizedGender=="khong xac dinh")row["gender"]="Không xác định";
     else throw new InvalidDataException("Dòng "+(i+1)+": giới tính ngoài danh mục.");
    }
    if(mode=="ADD_MEMBER" && GetIntake(row,"relationship").Length>60)throw new InvalidDataException("Dòng "+(i+1)+": quan hệ quá dài.");
    if(mode=="ENRICH_PERSON"){
     long version;
     if(!Int64.TryParse(GetIntake(row,"expected_version"),NumberStyles.None,CultureInfo.InvariantCulture,out version)||version<1)
      throw new InvalidDataException("Dòng "+(i+1)+": phiên bản hồ sơ phải là số nguyên dương.");
     if(GetIntake(row,"birth_date")=="" && GetIntake(row,"gender")=="")
      throw new InvalidDataException("Dòng "+(i+1)+": không có trường nào cần bổ sung.");
    }
    result.Add(row);
   }
   if(result.Count==0 || result.Count>IntakeMaxRows)throw new InvalidDataException("Số dòng cần từ 1 đến 5.000.");
   if(mode=="CREATE_HOUSEHOLD" && result.Select(x=>NormalizeSearch(x["household_code"])).Distinct(StringComparer.Ordinal).Count()!=result.Count)
    throw new InvalidDataException("Mã hộ trùng nhau trong cùng lô.");
   if(mode=="ENRICH_PERSON" && result.Select(x=>x["person_id"]).Distinct(StringComparer.Ordinal).Count()!=result.Count)
    throw new InvalidDataException("Một ID nhân khẩu xuất hiện nhiều lần trong cùng lô.");
   return result;
  }
  static string CanonicalBirthDate(string value,int line) {
   DateTime dt;
   if(!DateTime.TryParseExact(value,new[]{"yyyy-MM-dd","dd-MM-yyyy","dd/MM/yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out dt)||dt.Date>DateTime.Today)
    throw new InvalidDataException("Dòng "+line+": ngày sinh không hợp lệ.");
   return dt.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  }
  static string GetIntake(IDictionary<string,string> row,string field) {
   string value;return row.TryGetValue(field,out value)?value:"";
  }
  internal static bool CanUseIntakeMode(Session session,string mode) {
   switch(mode) {
    case "CREATE_HOUSEHOLD": return Can(session,"B01","create");
    case "ADD_MEMBER": return Can(session,"B01","read")&&Can(session,"B02","create");
    case "ENRICH_PERSON": return Can(session,"B02","read")&&Can(session,"B02","update");
    default: return false;
   }
  }
  static void RequireIntakeMode(SQLiteConnection c,SQLiteTransaction tx,Session session,string mode) {
   if(session==null || session.AreaId==null)throw new UnauthorizedAccessException("Cần tài khoản có phạm vi khu phố.");
   if(mode=="CREATE_HOUSEHOLD")Require(c,tx,session,"B01","create",session.AreaId);
   else if(mode=="ADD_MEMBER") {Require(c,tx,session,"B01","read",session.AreaId);Require(c,tx,session,"B02","create",session.AreaId);}
   else if(mode=="ENRICH_PERSON") {Require(c,tx,session,"B02","read",session.AreaId);Require(c,tx,session,"B02","update",session.AreaId);}
   else throw new InvalidOperationException("Chế độ nhập không có quyền hợp lệ.");
  }
  static string IntakeDigest(string mode,string sourceKind,string payload) {
   using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(mode+"\n"+sourceKind+"\n"+payload))).Replace("-","");
  }
  static string IntakeSourceLabel(string raw) {
   if(String.IsNullOrWhiteSpace(raw))throw new InvalidDataException("Cần ghi nguồn thu dữ liệu.");
   string label=Path.GetFileName(raw.Trim());
   if(label.Length>80||label.Any(Char.IsControl))throw new InvalidDataException("Nhãn nguồn không hợp lệ.");
   return label;
  }
  static string ResolveHousehold(SQLiteConnection c,SQLiteTransaction tx,Session s,string code) {
   string id=Convert.ToString(Scalar(c,tx,"SELECT id FROM households WHERE area_id=@area AND code=@code COLLATE NOCASE",P("@area",s.AreaId),P("@code",code)));
   if(String.IsNullOrEmpty(id))throw new InvalidOperationException("Không có mã hộ chính xác trong khu phố: "+code);
   return id;
  }
  static int SuspectedDuplicateCount(SQLiteConnection c,SQLiteTransaction tx,string householdId,string name,string dob) {
   if(dob.Length==0)return 0; // no DOB => insufficient evidence; never auto-merge
   return Convert.ToInt32(Scalar(c,tx,@"SELECT COUNT(*) FROM people p JOIN household_memberships hm ON hm.person_id=p.id AND hm.end_utc IS NULL
    WHERE hm.household_id=@h AND p.search_name=@n AND p.date_of_birth=@b",
    P("@h",householdId),P("@n",NormalizeSearch(name)),P("@b",dob)));
  }
  static string EnsurePersonEnrichment(SQLiteConnection c,SQLiteTransaction tx,Session session,Dictionary<string,string> row,bool write) {
   string id=GetIntake(row,"person_id");
   long requested=Int64.Parse(GetIntake(row,"expected_version"),CultureInfo.InvariantCulture);
   string oldDob=null,oldGender=null;long version=0;
   using(var cmd=new SQLiteCommand("SELECT date_of_birth,gender,version FROM people WHERE id=@id AND area_id=@area",c,tx)) {
    cmd.Parameters.Add(P("@id",id));cmd.Parameters.Add(P("@area",session.AreaId));
    using(var r=cmd.ExecuteReader()){
     if(!r.Read())throw new UnauthorizedAccessException("ID nhân khẩu không tồn tại hoặc ngoài địa bàn.");
     oldDob=r.IsDBNull(0)?"":r.GetString(0);oldGender=r.IsDBNull(1)?"":r.GetString(1);version=r.GetInt64(2);
    }
   }
   if(version!=requested)throw new InvalidOperationException("Phiên bản hồ sơ đã thay đổi: "+id);
   string dob=GetIntake(row,"birth_date"),gender=GetIntake(row,"gender");
   if(dob.Length>0 && oldDob.Length>0 && dob!=oldDob || gender.Length>0 && oldGender.Length>0 && gender!=oldGender)
    throw new InvalidOperationException("Có giá trị đã tồn tại khác nguồn; cần xử lý xung đột thủ công cho ID "+id);
   bool changed=(dob.Length>0 && oldDob.Length==0)||(gender.Length>0 && oldGender.Length==0);
   if(write && changed) {
    int affected;
    using(var cmd=new SQLiteCommand(@"UPDATE people SET date_of_birth=CASE WHEN COALESCE(date_of_birth,'')='' THEN NULLIF(@dob,'') ELSE date_of_birth END,
      gender=CASE WHEN COALESCE(gender,'')='' THEN NULLIF(@gender,'') ELSE gender END,
      version=version+1,updated_utc=@utc WHERE id=@id AND area_id=@area AND version=@version",c,tx)){
     cmd.Parameters.Add(P("@dob",dob));cmd.Parameters.Add(P("@gender",gender));cmd.Parameters.Add(P("@utc",Now));
     cmd.Parameters.Add(P("@id",id));cmd.Parameters.Add(P("@area",session.AreaId));cmd.Parameters.Add(P("@version",requested));
     affected=cmd.ExecuteNonQuery();
    }
    if(affected!=1)throw new InvalidOperationException("Xung đột cập nhật phiên bản nhân khẩu.");
    Audit(c,tx,session.Id,"INTAKE_ENRICH_PERSON","person",id);
   }
   return changed?"Bổ sung trường trống":"Không thay đổi (đã có cùng dữ liệu)";
  }
  public static IntakePreview PreviewIntake(Session session,string mode,string sourceKind,string sourceLabel,string payload) {
   using(var c=Open())using(var tx=c.BeginTransaction()){
    var preview=PreviewIntake(c,tx,session,mode,sourceKind,sourceLabel,payload,null);
    tx.Commit();return preview;
   }
  }
  static IntakePreview PreviewIntake(SQLiteConnection c,SQLiteTransaction tx,Session session,string mode,
   string sourceKind,string sourceLabel,string payload,HashSet<string> plannedHouseholds,bool allowSourceKeys=false) {
   if(!IntakeKinds.Contains(sourceKind))throw new InvalidDataException("Nguồn thu chưa được hỗ trợ.");
   var source=IntakeSourceLabel(sourceLabel);
   var rows=ParseIntakeRows(mode,payload,allowSourceKeys);
   var preview=new IntakePreview {RequestId=Guid.NewGuid().ToString("N"),Mode=mode,SourceKind=sourceKind,
    SourceLabel=source,Payload=payload,Digest=IntakeDigest(mode,sourceKind,payload),ActorId=session==null?null:session.Id,AreaId=session==null?null:session.AreaId};
    RequireIntakeMode(c,tx,session,mode);
    var seenPersonCandidates=new HashSet<string>(StringComparer.Ordinal);
    for(int i=0;i<rows.Count;i++){
     var row=rows[i];string key="",notice="Đủ điều kiện (chưa ghi)",operation="";
     bool candidateWarning=false;
     if(mode=="CREATE_HOUSEHOLD"){
      key=row["household_code"];operation="Tạo hộ + chủ hộ";
      if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM households WHERE area_id=@area AND code=@code COLLATE NOCASE",P("@area",session.AreaId),P("@code",key)))>0)
       throw new InvalidDataException("Dòng "+(i+2)+": mã hộ đã tồn tại, không được tự ghi đè: "+key);
      if(row["address"].Length>300||row["head"].Length>200||key.Length>40)throw new InvalidDataException("Dòng "+(i+2)+": trường vượt giới hạn.");
     }else if(mode=="ADD_MEMBER"){
      key=row["household_code"];operation="Bổ sung người vào hộ";
      bool planned=plannedHouseholds!=null&&plannedHouseholds.Contains(WorkbookHouseholdKey(key));
      string h=planned?"new:"+WorkbookHouseholdKey(key):ResolveHousehold(c,tx,session,key);
      var draft=new MemberDraft{Name=row["full_name"],DateOfBirth=GetIntake(row,"birth_date"),Gender=GetIntake(row,"gender"),Relationship=row["relationship"]};
      ValidateMemberDraft(draft,i+2);
      if(draft.Relationship=="Chủ hộ" && (planned || Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM household_memberships WHERE household_id=@h AND relationship='Chủ hộ' AND end_utc IS NULL",P("@h",h)))>0))
       throw new InvalidDataException("Dòng "+(i+2)+": hộ đã có chủ hộ.");
      string candidateKey=h+"|"+NormalizeSearch(draft.Name)+"|"+draft.DateOfBirth;
      bool sameInBatch=draft.DateOfBirth.Length>0 && !seenPersonCandidates.Add(candidateKey);
      if(sameInBatch || (!planned&&SuspectedDuplicateCount(c,tx,h,draft.Name,draft.DateOfBirth)>0)){
       preview.HasCandidateWarnings=true;candidateWarning=true;
       notice="Nghi trùng họ tên + ngày sinh trong hộ hoặc lô; cần xác minh";
      }
     }else{
      key=row["person_id"];operation="Bổ sung trường còn thiếu theo ID";
      notice=EnsurePersonEnrichment(c,tx,session,row,false);
     }
     preview.Rows.Add(new IntakePreviewRow{LineNumber=i+2,TargetKey=key,Operation=operation,Notice=notice,CandidateWarning=candidateWarning});
    }
   return preview;
  }
  public static int CommitIntake(Session session,IntakePreview approved,bool allowCandidateDuplicates) {
   using(var c=Open())using(var tx=c.BeginTransaction()){
    int count=CommitIntake(c,tx,session,approved,allowCandidateDuplicates);
    tx.Commit();return count;
   }
  }
  static int CommitIntake(SQLiteConnection c,SQLiteTransaction tx,Session session,IntakePreview approved,bool allowCandidateDuplicates,Dictionary<string,string> newPeople=null) {
   if(session==null || approved==null || approved.Payload==null || approved.ActorId!=session.Id || approved.AreaId!=session.AreaId)
    throw new UnauthorizedAccessException("Bản xem trước không thuộc phiên và khu phố hiện tại.");
   if(approved.HasCandidateWarnings && !allowCandidateDuplicates)throw new InvalidOperationException("Có nghi trùng: cần người có quyền đối chiếu và xác nhận trước khi ghi.");
   var rows=ParseIntakeRows(approved.Mode,approved.Payload,newPeople!=null);
   if(approved.Digest!=IntakeDigest(approved.Mode,approved.SourceKind,approved.Payload))throw new InvalidDataException("Nguồn đã thay đổi sau xem trước.");
   Guid id;
   if(!Guid.TryParseExact(approved.RequestId,"N",out id))throw new InvalidDataException("Mã lần thu không hợp lệ.");
    RequireIntakeMode(c,tx,session,approved.Mode);
    using(var cmd=new SQLiteCommand("SELECT actor_id,area_id,mode,source_kind,payload_sha256,row_count FROM intake_batches WHERE request_id=@id",c,tx)) {
     cmd.Parameters.Add(P("@id",approved.RequestId));
     using(var r=cmd.ExecuteReader())if(r.Read()) {
      if(r.GetString(0)!=session.Id || r.GetString(1)!=session.AreaId || r.GetString(2)!=approved.Mode || r.GetString(3)!=approved.SourceKind || r.GetString(4)!=approved.Digest)
       throw new InvalidOperationException("Mã lần nhập đã sử dụng với nguồn hoặc người khác.");
      return r.GetInt32(5); // idempotent retry
     }
    }
    for(int i=0;i<rows.Count;i++){
     var row=rows[i];
     string createdPerson=null;
     if(approved.Mode=="CREATE_HOUSEHOLD")createdPerson=CreateHouseholdInTransaction(c,tx,session,row["household_code"],row["address"],row["head"],GetIntake(row,"birth_date"),GetIntake(row,"gender"));
     else if(approved.Mode=="ADD_MEMBER"){
      string household=ResolveHousehold(c,tx,session,row["household_code"]);
      string name=row["full_name"],birth=GetIntake(row,"birth_date"),gender=GetIntake(row,"gender"),rel=row["relationship"];
      ValidateMemberDraft(new MemberDraft{Name=name,DateOfBirth=birth,Gender=gender,Relationship=rel},i+2);
      if(rel=="Chủ hộ" && Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM household_memberships WHERE household_id=@h AND relationship='Chủ hộ' AND end_utc IS NULL",P("@h",household)))>0)
       throw new InvalidDataException("Hộ đã có chủ hộ. Không cho phép tự gộp.");
      if(SuspectedDuplicateCount(c,tx,household,name,birth)>0 &&
       (!allowCandidateDuplicates||i>=approved.Rows.Count||!approved.Rows[i].CandidateWarning))
       throw new InvalidDataException("Phát hiện nghi trùng mới; cần xem trước lại.");
      string person=Id(),member=Id(),now=Now;
      Exec(c,tx,"INSERT INTO people(id,area_id,full_name,search_name,date_of_birth,gender,created_utc,updated_utc) VALUES(@id,@area,@name,@search,@birth,@gender,@utc,@utc)",
       P("@id",person),P("@area",session.AreaId),P("@name",name),P("@search",NormalizeSearch(name)),P("@birth",birth.Length==0?null:birth),P("@gender",gender.Length==0?null:gender),P("@utc",now));
      Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@id,@h,@p,@rel,@utc,@utc)",
       P("@id",member),P("@h",household),P("@p",person),P("@rel",rel),P("@utc",now));
      Audit(c,tx,session.Id,"INTAKE_ADD_MEMBER","person",person);
      createdPerson=person;
     }else EnsurePersonEnrichment(c,tx,session,row,true);
     string sourceKey=GetIntake(row,"legacy_person_key");
     if(sourceKey.Length>0)newPeople.Add(WorkbookHouseholdKey(sourceKey),createdPerson);
    }
    // Only provenance metadata is persisted; the original imported PII is NOT retained.
    Exec(c,tx,@"INSERT INTO intake_batches(request_id,actor_id,area_id,mode,source_kind,source_label,payload_sha256,row_count,created_utc)
      VALUES(@id,@actor,@area,@mode,@kind,@label,@sha,@count,@utc)",
     P("@id",approved.RequestId),P("@actor",session.Id),P("@area",session.AreaId),P("@mode",approved.Mode),P("@kind",approved.SourceKind),
     P("@label",approved.SourceLabel),P("@sha",approved.Digest),P("@count",rows.Count),P("@utc",Now));
    Audit(c,tx,session.Id,"INTAKE_BATCH_APPROVED","intake_batch",approved.RequestId);
    return rows.Count;
  }
 }
}
