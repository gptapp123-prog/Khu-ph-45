using System;
using System.Data.SQLite;
using System.IO;
using System.Text;

namespace QLKP.Desktop.Core {
 /// <summary>Fail-closed startup checks. No automatic deletion or repair of citizen records.</summary>
 public static class StartupDiagnostics {
  private static readonly object LockObj=new object();
  public static string LogPath { get { return Path.Combine(DataStore.DataFolder,"startup-events.log"); } }
  public static void CheckApplicationFiles() {
   string root=AppDomain.CurrentDomain.BaseDirectory;
   if(!File.Exists(Path.Combine(root,"schema.sql")))throw new FileNotFoundException("schema.sql");
   if(!File.Exists(Path.Combine(root,"System.Data.SQLite.dll")))throw new FileNotFoundException("System.Data.SQLite.dll");
   if(!File.Exists(Path.Combine(root,"e_sqlite3.dll")))throw new FileNotFoundException("e_sqlite3.dll");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new PlatformNotSupportedException("Cần Windows.");
   if(IntPtr.Size!=8)throw new PlatformNotSupportedException("Ứng dụng yêu cầu Windows x64.");
   // Version 6.1 SP1 is the compatibility target. Do not present Windows 7 as a secure OS.
   if(Environment.OSVersion.Version.Major<6)throw new PlatformNotSupportedException("Phiên bản Windows quá cũ.");
  }
  // Recovery gate: do not silently delete plaintext SQLite stages left by a crashed
  // backup/restore. Such leftovers may contain personal records even when the backup
  // itself is encrypted. Block normal startup and require a supervised security review.
  public static void CheckPlaintextMaintenanceArtifacts() {
   string dataDir=DataStore.DataFolder;
   if(!Directory.Exists(dataDir))return;
   string[] rootPatterns={
    "qlkp-restore-stage-*.db","qlkp-rollback-stage-*.db",
    "qlkp-restore-stage-*.db-journal","qlkp-rollback-stage-*.db-journal",
    "qlkp-restore-stage-*.db-wal","qlkp-rollback-stage-*.db-wal",
    "qlkp-restore-stage-*.db-shm","qlkp-rollback-stage-*.db-shm"};
   foreach(string pattern in rootPatterns)
    if(Directory.GetFiles(dataDir,pattern,SearchOption.TopDirectoryOnly).Length>0)
     throw new InvalidDataException("Phát hiện tệp SQLite tạm chưa mã hóa sau khôi phục; ngừng khởi chạy và yêu cầu xử lý bảo mật có giám sát.");
   string maintenance=Path.Combine(dataDir,"maintenance");
   if(!Directory.Exists(maintenance))return;
   string[] maintenancePatterns={"qlkp-*.db","qlkp-*.db-journal","qlkp-*.db-wal","qlkp-*.db-shm"};
   foreach(string pattern in maintenancePatterns)
    if(Directory.GetFiles(maintenance,pattern,SearchOption.TopDirectoryOnly).Length>0)
     throw new InvalidDataException("Phát hiện ảnh SQLite tạm chưa mã hóa trong thư mục bảo trì; từ chối khởi chạy để bảo vệ dữ liệu.");
  }
  public static void CheckDatabase(string path) {
   if(!File.Exists(path))throw new FileNotFoundException("Không tìm thấy cơ sở dữ liệu sau khởi tạo.");
   var dir=new DirectoryInfo(Path.GetDirectoryName(path));
   var drive=new DriveInfo(Path.GetPathRoot(dir.FullName));
   if(drive.IsReady && drive.AvailableFreeSpace<20L*1024L*1024L)throw new IOException("Không đủ dung lượng ổ đĩa.");
   if(!String.Equals(Path.GetFullPath(path),Path.GetFullPath(DataStore.DatabasePath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Đường dẫn kho kiểm tra không trùng kho đã cấu hình.");
   using(var c=DataStore.Open()) {
    using(var cmd=new SQLiteCommand("PRAGMA quick_check;",c)) {
     var result=Convert.ToString(cmd.ExecuteScalar());
     if(!String.Equals(result,"ok",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Cơ sở dữ liệu không toàn vẹn.");
    }
    using(var cmd=new SQLiteCommand("PRAGMA user_version;",c)) {
     long ver=Convert.ToInt64(cmd.ExecuteScalar());
     if(ver>DataStore.SchemaVersion)throw new InvalidDataException("Kho có phiên bản mới hơn ứng dụng; không hạ phiên bản tự động.");
    }
   }
  }
  public static void WriteEvent(string code) {
   lock(LockObj) {
    try {
     Directory.CreateDirectory(DataStore.DataFolder);
     // Never log passwords, SQL values, paths of citizens' files, or record contents.
     File.AppendAllText(LogPath,DateTime.UtcNow.ToString("o")+" | "+code+" | 0.21.0-PILOT"+Environment.NewLine,Encoding.UTF8);
    } catch { /* Logging failures must not cause data deletion. */ }
   }
  }
  public static void WriteException(string code,Exception ex) {
   // Only exception type is logged; exception.Message may contain sensitive inputs.
   string kind=ex==null?"unknown":ex.GetType().Name;
   WriteEvent(code+" | "+kind);
  }
 }
}
