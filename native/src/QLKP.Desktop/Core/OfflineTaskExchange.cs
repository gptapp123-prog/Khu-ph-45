using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace QLKP.Desktop.Core {
 public sealed class OfflineTaskResult {
  internal OfflineTaskResult(string token,string result,string task) {Token=token;Result=result;Task=task;}
  internal string Token {get;}
  public string Result {get;}
  public string Task {get;}
 }

 public static partial class DataStore {
  const int TaskPayloadLimit=16384;
  static readonly Encoding TaskUtf8=new UTF8Encoding(false,true);
  static readonly byte[] TaskDispatchMagic=Encoding.ASCII.GetBytes("QLKPTD01");
  static readonly byte[] TaskResultMagic=Encoding.ASCII.GetBytes("QLKPTR01");

  public static void ExportOfflineTask(Session actor,string taskId,long selectedVersion,string briefing,string path,string password) {
   briefing=(briefing??"").Trim();
   if(briefing.Length<1||briefing.Length>2000||briefing.Any(ch=>Char.IsControl(ch)&&ch!='\n'&&ch!='\r'&&ch!='\t'))
    throw new InvalidDataException("Nội dung giao việc cần từ 1 đến 2.000 ký tự hợp lệ.");
   if(!Path.IsPathRooted(path)||!String.Equals(Path.GetExtension(path),".qlkptask",StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Chọn tệp nhiệm vụ .qlkptask.");
   string temp=Path.Combine(Path.GetDirectoryName(path),".qlkptask-"+Guid.NewGuid().ToString("N")+".tmp");
   try {
    using(var c=Open())using(var tx=c.BeginTransaction()) {
     Require(c,tx,actor,"I06","export",actor?.AreaId);
     Require(c,tx,actor,"A02","update",actor?.AreaId);
     var task=GetModuleRecord(c,tx,actor,"A02",taskId);
     ValidateTaskExchange(c,tx,actor,task,selectedVersion);
     string token=Guid.NewGuid().ToString("N");
     var plain=TaskUtf8.GetBytes(String.Join("\n",token,PackTaskField(task.Code),PackTaskField(task.DueDateDisplay),PackTaskField(briefing)));
     byte[] bytes;
     try {bytes=OfflineCatalogTransfer.Seal(TaskDispatchMagic,plain,password);}
     finally {Array.Clear(plain,0,plain.Length);}
     using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
      output.Write(bytes,0,bytes.Length);output.Flush(true);
     }
     Exec(c,tx,"DELETE FROM offline_task_dispatches WHERE record_id=@id AND record_version<>@v",P("@id",task.Id),P("@v",task.Version));
     Exec(c,tx,"INSERT INTO offline_task_dispatches(token,record_id,record_version) VALUES(@t,@id,@v)",
      P("@t",token),P("@id",task.Id),P("@v",task.Version));
     Audit(c,tx,actor.Id,"A02_OFFLINE_DISPATCH_PREPARED","module_record",task.Id);
     tx.Commit();
    }
    // The ticket is registered before publishing the file. A failed move leaves no usable package.
    File.Move(temp,path);
   } finally {if(File.Exists(temp))File.Delete(temp);}
  }

  static string PackTaskField(string text) {return Convert.ToBase64String(TaskUtf8.GetBytes(text??""));}

  static void ValidateTaskExchange(SQLiteConnection c,SQLiteTransaction tx,Session actor,ModuleRecord task,long version) {
   if(task.Version!=version)
    throw new InvalidOperationException("Nhiệm vụ đã thay đổi. Hãy tải lại và xuất gói mới.");
   if(task.Status=="Hoàn thành nội bộ"||task.Status=="Tạm dừng")
    throw new InvalidOperationException("Nhiệm vụ đã kết thúc hoặc đang tạm dừng.");
   if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM accounts WHERE id=@id AND area_id=@a AND active=1",
      P("@id",GetField(task,"assignee_id")),P("@a",actor.AreaId)))!=1)
    throw new InvalidOperationException("Cần lưu người phụ trách đang hoạt động trong cùng khu phố.");
  }

  static ModuleRecord ResolveOfflineTask(SQLiteConnection c,SQLiteTransaction tx,Session actor,string token) {
   Require(c,tx,actor,"I06","export",actor?.AreaId);
   Require(c,tx,actor,"A02","update",actor?.AreaId);
   string recordId;long version;
   using(var cmd=new SQLiteCommand(@"SELECT d.record_id,d.record_version FROM offline_task_dispatches d
     JOIN module_records m ON m.id=d.record_id WHERE d.token=@t AND m.area_id=@a AND m.feature='A02'",c,tx)) {
    cmd.Parameters.AddRange(new[]{P("@t",token),P("@a",actor.AreaId)});
    using(var reader=cmd.ExecuteReader()) {
     if(!reader.Read())throw new InvalidOperationException("Gói không thuộc khu phố này, chưa được phát hành hoặc đã được xác nhận.");
     recordId=reader.GetString(0);version=reader.GetInt64(1);
    }
   }
   var task=GetModuleRecord(c,tx,actor,"A02",recordId);
   ValidateTaskExchange(c,tx,actor,task,version);
   return task;
  }

  public static OfflineTaskResult PreviewOfflineTaskResult(Session actor,string path,string password) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"I06","export",actor?.AreaId);
    Require(c,tx,actor,"A02","update",actor?.AreaId);
    byte[] bytes;
    using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
     if(file.Length<56||file.Length>TaskPayloadLimit+56)throw new InvalidDataException("Kích thước gói không hợp lệ.");
     int length=(int)file.Length;
     using(var reader=new BinaryReader(file)) {
      bytes=reader.ReadBytes(length);
      if(bytes.Length!=length||reader.BaseStream.ReadByte()!=-1)
       throw new InvalidDataException("Không đọc được toàn bộ gói.");
     }
    }
    var plain=OfflineCatalogTransfer.Unseal(TaskResultMagic,bytes,password,TaskPayloadLimit);
    string[] fields;
    try {fields=TaskUtf8.GetString(plain).Split('\n');}
    finally {Array.Clear(plain,0,plain.Length);}
    Guid ticket;
    if(fields.Length!=2||!Guid.TryParseExact(fields[0],"N",out ticket)||fields[0]!=ticket.ToString("N"))
     throw new InvalidDataException("Sai cấu trúc gói kết quả.");
    string result=TaskUtf8.GetString(Convert.FromBase64String(fields[1])).Trim();
    if(result.Length<1||result.Length>500||result.Any(ch=>Char.IsControl(ch)&&ch!='\n'&&ch!='\r'&&ch!='\t'))
     throw new InvalidDataException("Kết quả cần từ 1 đến 500 ký tự hợp lệ.");
    var task=ResolveOfflineTask(c,tx,actor,fields[0]);
    tx.Commit();
    return new OfflineTaskResult(fields[0],result,task.Display);
   }
  }

  public static string ConfirmOfflineTaskResult(Session actor,OfflineTaskResult preview) {
   if(preview==null)throw new ArgumentNullException(nameof(preview));
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    var task=ResolveOfflineTask(c,tx,actor,preview.Token);
    task.Fields["result"]=preview.Result;
    task.Fields["progress"]="100";
    task.Status="Hoàn thành nội bộ";
    SaveModuleRecord(c,tx,actor,"A02",task,task.Version);
    Exec(c,tx,"DELETE FROM offline_task_dispatches WHERE record_id=@id",P("@id",task.Id));
    Audit(c,tx,actor.Id,"A02_OFFLINE_RESULT_CONFIRMED","module_record",task.Id);
    tx.Commit();
    return task.Id;
   }
  }
 }
}
