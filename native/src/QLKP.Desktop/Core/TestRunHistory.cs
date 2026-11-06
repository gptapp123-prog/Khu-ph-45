using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace QLKP.Desktop.Core {
 [DataContract] public sealed class TestRunStep {
  [DataMember(Name="code")] public string Code {get;set;}
  [DataMember(Name="status")] public string Status {get;set;}
  [DataMember(Name="started_utc")] public string StartedUtc {get;set;}
  [DataMember(Name="finished_utc")] public string FinishedUtc {get;set;}
  [DataMember(Name="duration_ms")] public long DurationMs {get;set;}
  [DataMember(Name="error_type")] public string ErrorType {get;set;}
  public string StatusDisplay {get{return TestRunHistory.StatusLabel(Status);}}
 }
 [DataContract] public sealed class TestRunReport {
  [DataMember(Name="format")] public string Format {get;set;}="QLKP_TEST_RUN_V1";
  [DataMember(Name="session_id")] public string Id {get;set;}
  [DataMember(Name="parent_session_id")] public string ParentId {get;set;}="";
  [DataMember(Name="kind")] public string Kind {get;set;}
  [DataMember(Name="layer")] public string Layer {get;set;}
  [DataMember(Name="status")] public string Status {get;set;}
  [DataMember(Name="started_utc")] public string StartedUtc {get;set;}
  [DataMember(Name="finished_utc")] public string FinishedUtc {get;set;}="";
  [DataMember(Name="duration_ms")] public long DurationMs {get;set;}
  [DataMember(Name="source_version")] public string SourceVersion {get;set;}
  [DataMember(Name="source_hash")] public string SourceHash {get;set;}="";
  [DataMember(Name="hash_kind")] public string HashKind {get;set;}="";
  [DataMember(Name="environment")] public string EnvironmentName {get;set;}
  [DataMember(Name="origin")] public string Origin {get;set;}="LOCAL";
  [DataMember(Name="original_session_id")] public string OriginalId {get;set;}="";
  [DataMember(Name="import_sha256")] public string ImportHash {get;set;}="";
  [DataMember(Name="last_sequence")] public long LastSequence {get;set;}
  [DataMember(Name="total")] public int Total {get;set;}
  [DataMember(Name="passed")] public int Passed {get;set;}
  [DataMember(Name="failed")] public int Failed {get;set;}
  [DataMember(Name="skipped")] public int Skipped {get;set;}
  [DataMember(Name="steps")] public List<TestRunStep> Steps {get;set;}=new List<TestRunStep>();
  public string StatusDisplay {get{return TestRunHistory.StatusLabel(Status)+(Origin=="IMPORTED"?" (nhập)":"");}}
  public string StartedDisplay {get{return DisplayLabels.UtcStamp(StartedUtc);}}
  public string LayerDisplay {get{return Layer=="source_sql"?"Nguồn/SQL":Layer=="repository_runtime"?"Dịch vụ":Layer=="build"?"Build":Layer??"";}}
 }
 [DataContract] internal sealed class TestRunEvent {
  [DataMember(Name="session_id")] public string Id;
  [DataMember(Name="sequence")] public long Sequence;
  [DataMember(Name="event")] public string Event;
  [DataMember(Name="at_utc")] public string AtUtc;
  [DataMember(Name="step")] public TestRunStep Step;
 }
 public sealed class TestRunRecorder:IDisposable {
  readonly string directory;readonly Stopwatch clock=Stopwatch.StartNew();Stopwatch stepClock;
  TestRunStep active;bool completed;
  public TestRunReport Report {get;private set;}
  public string DirectoryPath {get{return directory;}}
  internal static string UtcNow {get{return DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);}}
  public TestRunRecorder(string kind,string layer) {
   if(!Regex.IsMatch(kind??"",@"^[a-z_]{1,40}$")||!TestRunHistory.Layers.Contains(layer))throw new InvalidDataException("Loại phiên kiểm thử không hợp lệ.");
   Report=new TestRunReport{Id=Guid.NewGuid().ToString("N"),Kind=kind,Layer=layer,Status="RUNNING",StartedUtc=UtcNow,
    SourceVersion=Assembly.GetExecutingAssembly().GetName().Version+"-P6",EnvironmentName=Environment.OSVersion.Platform+" "+Environment.OSVersion.Version+" "+(Environment.Is64BitProcess?"x64":"x86")};
   string parent=Environment.GetEnvironmentVariable("QLKP_TEST_PARENT_ID");if(Regex.IsMatch(parent??"",@"^[0-9a-f]{32}$"))Report.ParentId=parent;
   string exe=Assembly.GetExecutingAssembly().Location;
   if(File.Exists(exe)){using(var f=File.OpenRead(exe))using(var hash=SHA256.Create())Report.SourceHash=BitConverter.ToString(hash.ComputeHash(f)).Replace("-","").ToLowerInvariant();Report.HashKind="exe_sha256";}
   directory=Path.Combine(TestRunHistory.Root,Report.Id);Directory.CreateDirectory(directory);
   // CreateNew protects against accidental reuse. No records or passwords are logged here.
   using(var f=new FileStream(Path.Combine(directory,"events.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))f.Flush(true);
   Save("SESSION_START",null);
  }
  public void BeginStep(string code) {
   if(completed)throw new InvalidOperationException("Phiên đã kết thúc.");
   if(String.IsNullOrWhiteSpace(code)||code.Length>256)throw new InvalidDataException("Mã bước không hợp lệ.");
   if(active!=null)EndStep("PASS");
   if(Report.Steps.Count>=5000)throw new InvalidDataException("Phiên vượt 5.000 bước.");
   active=new TestRunStep{Code=code,Status="RUNNING",StartedUtc=UtcNow,FinishedUtc="",ErrorType=""};Report.Steps.Add(active);stepClock=Stopwatch.StartNew();Save("STEP_START",active);
  }
  public void EndStep(string status,string errorType="") {
   if(active==null)return;
   if(!new[]{"PASS","FAIL","SKIP","INTERRUPTED"}.Contains(status))throw new InvalidDataException("Trạng thái bước không hợp lệ.");
   active.Status=status;active.FinishedUtc=UtcNow;active.DurationMs=stepClock.ElapsedMilliseconds;
   active.ErrorType=Regex.IsMatch(errorType??"",@"^[A-Za-z0-9_.]{0,120}$")?errorType:"Error";
   var step=active;active=null;Save("STEP_END",step);
  }
  public void Complete(string status,string errorType="") {
   if(completed)return;
   if(!new[]{"PASS","FAIL","INTERRUPTED","SKIP"}.Contains(status))throw new InvalidDataException("Trạng thái phiên không hợp lệ.");
   if(active!=null)EndStep(status=="PASS"?"PASS":status,errorType);
   if(Report.Steps.Any(x=>x.Status=="FAIL"))status="FAIL";
   else if(Report.Steps.Any(x=>x.Status=="INTERRUPTED"))status="INTERRUPTED";
   if(status=="PASS"&&Report.Steps.All(x=>x.Status=="SKIP"))status="SKIP";
   Report.Status=status;Report.FinishedUtc=UtcNow;Report.DurationMs=clock.ElapsedMilliseconds;Save("SESSION_END",null);completed=true;
  }
  void Save(string action,TestRunStep step) {
   Report.LastSequence++;Report.Total=Report.Steps.Count;Report.Passed=Report.Steps.Count(x=>x.Status=="PASS");Report.Failed=Report.Steps.Count(x=>x.Status=="FAIL");Report.Skipped=Report.Steps.Count(x=>x.Status=="SKIP");
   var evt=new TestRunEvent{Id=Report.Id,Sequence=Report.LastSequence,Event=action,AtUtc=UtcNow,Step=step};
   byte[] bytes=TestRunHistory.Serialize(evt);
   using(var f=new FileStream(Path.Combine(directory,"events.jsonl"),FileMode.Append,FileAccess.Write,FileShare.Read)){f.Write(bytes,0,bytes.Length);f.WriteByte(10);f.Flush(true);}
   TestRunHistory.WriteAtomic(Path.Combine(directory,"session.json"),Report);
  }
  public void Dispose(){if(!completed)Complete("INTERRUPTED");}
 }
 public static class TestRunHistory {
  internal static readonly string[] Layers={"source_sql","repository_runtime","build","ui_e2e","device","install"};
  public static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QLKP-V2-PILOT","test-runs");}}
  static DataContractJsonSerializer Serializer(Type type){return new DataContractJsonSerializer(type,new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=100000});}
  internal static byte[] Serialize<T>(T value){using(var m=new MemoryStream()){Serializer(typeof(T)).WriteObject(m,value);return m.ToArray();}}
  internal static void WriteAtomic(string path,TestRunReport report) {
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{byte[] bytes=Serialize(report);using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
  static void Require(Session actor,string action){if(!DataStore.Can(actor,"I09",action))throw new UnauthorizedAccessException("Chưa được cấp quyền I09:"+action+".");}
  static string RunPath(string id){if(!Regex.IsMatch(id??"",@"^[0-9a-f]{32}$"))throw new InvalidDataException("ID phiên không hợp lệ.");return Path.Combine(Root,id);}
  public static string StatusLabel(string value){return value=="PASS"?"Đạt":value=="FAIL"?"Không đạt":value=="SKIP"?"Bỏ qua":value=="RUNNING"?"Chưa kết thúc":value=="INTERRUPTED"?"Bị gián đoạn":"Không đọc được";}
  static bool Timestamp(string value){DateTimeOffset dt;return value!=null&&value.Length<=64&&value.Contains("T")&&DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out dt);}
  static TestRunReport Load(string path){byte[] raw;return Load(path,out raw);}
  static TestRunReport Load(string path,out byte[] raw) {
   var info=new FileInfo(path);if(!info.Exists||info.Length>4L*1024*1024)throw new InvalidDataException("Hồ sơ không tồn tại hoặc vượt 4 MB.");
   if((info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không nhận liên kết tệp.");
   TestRunReport report;
   using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)) {
    if(f.Length>4L*1024*1024)throw new InvalidDataException("Hồ sơ vượt 4 MB.");
    raw=new byte[checked((int)f.Length)];int offset=0;
    while(offset<raw.Length){int n=f.Read(raw,offset,raw.Length-offset);if(n==0)throw new EndOfStreamException();offset+=n;}
   }
   using(var m=new MemoryStream(raw))report=(TestRunReport)Serializer(typeof(TestRunReport)).ReadObject(m);
   if(report==null||report.Format!="QLKP_TEST_RUN_V1"||!Regex.IsMatch(report.Id??"",@"^[0-9a-f]{32}$")||!Layers.Contains(report.Layer)||
    !Regex.IsMatch(report.Kind??"",@"^[a-z_]{1,40}$")||!new[]{"RUNNING","PASS","FAIL","INTERRUPTED","SKIP"}.Contains(report.Status)||
    !new[]{"LOCAL","IMPORTED"}.Contains(report.Origin)||!Timestamp(report.StartedUtc)||report.Steps==null||report.Steps.Count>5000||report.DurationMs<0||report.LastSequence<0)
    throw new InvalidDataException("Hồ sơ kiểm thử sai cấu trúc.");
   if(!String.IsNullOrEmpty(report.ParentId)&&!Regex.IsMatch(report.ParentId,@"^[0-9a-f]{32}$"))throw new InvalidDataException("ID phiên cha không hợp lệ.");
   foreach(var hash in new[]{report.SourceHash,report.ImportHash})if(!String.IsNullOrEmpty(hash)&&!Regex.IsMatch(hash,@"^[0-9a-fA-F]{64}$"))throw new InvalidDataException("Mã băm không hợp lệ.");
   if(!Regex.IsMatch(report.HashKind??"",@"^[a-z0-9_]{0,40}$")||(report.FinishedUtc??"").Length>64)throw new InvalidDataException("Thông tin phiên không hợp lệ.");
   if(report.Status!="RUNNING"&&!Timestamp(report.FinishedUtc))throw new InvalidDataException("Thiếu thời điểm kết thúc.");
   if((report.SourceVersion??"").Length>100||(report.EnvironmentName??"").Length>300)throw new InvalidDataException("Thông tin phiên quá dài.");
   foreach(var step in report.Steps)if(step==null||String.IsNullOrWhiteSpace(step.Code)||step.Code.Length>256||!Timestamp(step.StartedUtc)||step.DurationMs<0||
    !new[]{"RUNNING","PASS","FAIL","SKIP","INTERRUPTED"}.Contains(step.Status)||(step.ErrorType??"").Length>120||(step.FinishedUtc??"").Length>64||
    (step.Status!="RUNNING"&&!Timestamp(step.FinishedUtc)))throw new InvalidDataException("Bước kiểm thử sai cấu trúc.");
   if(report.Total!=report.Steps.Count||report.Passed!=report.Steps.Count(x=>x.Status=="PASS")||report.Failed!=report.Steps.Count(x=>x.Status=="FAIL")||report.Skipped!=report.Steps.Count(x=>x.Status=="SKIP"))throw new InvalidDataException("Số liệu tổng hợp không khớp.");
   if(report.Status=="PASS"&&(report.Passed==0||report.Steps.Any(x=>x.Status=="FAIL"||x.Status=="INTERRUPTED"||x.Status=="RUNNING")))throw new InvalidDataException("Phiên không đủ điều kiện đạt.");
   return report;
  }
  public static RecordPage<TestRunReport> List(Session actor,int page,int size=50) {
   Require(actor,"read");if(page<0||size<1||size>100)throw new ArgumentOutOfRangeException("page");
   var dirs=Directory.Exists(Root)?Directory.GetDirectories(Root).Where(d=>Regex.IsMatch(Path.GetFileName(d),@"^[0-9a-f]{32}$")&&(File.GetAttributes(d)&FileAttributes.ReparsePoint)==0).OrderByDescending(Directory.GetLastWriteTimeUtc).ThenBy(Path.GetFileName).ToList():new List<string>();
   var result=new RecordPage<TestRunReport>{PageSize=size,TotalCount=dirs.Count,PageIndex=Math.Min(page,Math.Max(0,(dirs.Count-1)/size))};
   foreach(var d in dirs.Skip(result.PageIndex*size).Take(size))try{var record=Load(Path.Combine(d,"session.json"));if(record.Id!=Path.GetFileName(d))throw new InvalidDataException();result.Items.Add(record);}catch{result.Items.Add(new TestRunReport{Id=Path.GetFileName(d),Status="UNREADABLE",StartedUtc="",Kind="unreadable",Layer="",SourceVersion="",Steps=new List<TestRunStep>()});}
   return result;
  }
  public static TestRunReport Read(Session actor,string id){Require(actor,"read");string dir=RunPath(id);if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Không nhận thư mục liên kết.");var r=Load(Path.Combine(dir,"session.json"));if(r.Id!=id)throw new InvalidDataException("ID hồ sơ không khớp.");return r;}
  public static string Import(Session actor,string source) {
   Require(actor,"read");Require(actor,"test");byte[] raw;var record=Load(source,out raw);string hash;
   using(var h=SHA256.Create())hash=BitConverter.ToString(h.ComputeHash(raw)).Replace("-","").ToLowerInvariant();
   record.OriginalId=record.Id;record.Id=Guid.NewGuid().ToString("N");record.Origin="IMPORTED";record.ImportHash=hash;
   string dir=RunPath(record.Id);Directory.CreateDirectory(dir);WriteAtomic(Path.Combine(dir,"session.json"),record);return record.Id;
  }
  public static void Export(Session actor,string id,string destination,bool html) {
   Require(actor,"export");var r=Read(actor,id);string ext=Path.GetExtension(destination).ToLowerInvariant();if((html&&ext!=".html")||(!html&&ext!=".json"))throw new InvalidDataException("Chọn đúng định dạng xuất.");
   byte[] bytes=html?Encoding.UTF8.GetBytes(Html(r)):Serialize(r);
   using(var f=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}
  }
  static string Html(TestRunReport r) {
   Func<string,string> e=value=>WebUtility.HtmlEncode(value??"");var text=new StringBuilder("<!doctype html><html lang='vi'><meta charset='utf-8'><title>Hồ sơ kiểm thử</title><style>body{font:14px Arial;margin:32px;color:#102d59}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccd8e8;padding:8px;text-align:left}th{background:#e8f0fa}</style><h1>Hồ sơ kiểm thử</h1>");
   text.Append("<p>Phiên: ").Append(e(r.Id)).Append(" · ").Append(e(r.StatusDisplay)).Append(" · Lớp: ").Append(e(r.LayerDisplay)).Append("</p><p>Bắt đầu: ").Append(e(r.StartedDisplay)).Append(" · Kết thúc: ").Append(e(DisplayLabels.UtcStamp(r.FinishedUtc))).Append("</p><p>Đạt: ").Append(r.Passed).Append(" · Lỗi: ").Append(r.Failed).Append(" · Bỏ qua: ").Append(r.Skipped).Append("</p><p>Phiên bản: ").Append(e(r.SourceVersion)).Append(" · Môi trường: ").Append(e(r.EnvironmentName)).Append("</p><p>Hồ sơ là nhật ký kết quả, không tự chứng minh nghiệm thu UI hoặc chữ ký xác thực.</p><table><tr><th>Bước/ca</th><th>Kết quả</th><th>Thời gian (ms)</th><th>Loại lỗi</th></tr>");
   text.Append("<p>Mã băm (").Append(e(r.HashKind)).Append("): ").Append(e(r.SourceHash)).Append("</p>");
   foreach(var s in r.Steps)text.Append("<tr><td>").Append(e(s.Code)).Append("</td><td>").Append(e(s.StatusDisplay)).Append("</td><td>").Append(s.DurationMs).Append("</td><td>").Append(e(s.ErrorType)).Append("</td></tr>");return text.Append("</table></html>").ToString();
  }
  public static string RunDiagnostics(Session actor) {
   Require(actor,"read");Require(actor,"test");using(var run=new TestRunRecorder("local_diagnostics","repository_runtime")) {
    try{run.BeginStep("APPLICATION_FILES");StartupDiagnostics.CheckApplicationFiles();run.BeginStep("MAINTENANCE_ARTIFACTS");StartupDiagnostics.CheckPlaintextMaintenanceArtifacts();run.BeginStep("SQLITE_INTEGRITY");StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);run.Complete("PASS");}
    catch(Exception ex){run.Complete("FAIL",ex.GetType().Name);}
    return run.Report.Id;
   }
  }
 }
}
