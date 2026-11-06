using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QLKP.Desktop.Core {
 // SQLite3MC 2.6.1, SQLCipher AES-256/HMAC-SHA512 scheme, raw random keys.
 // Key protection is Windows DPAPI CurrentUser, not an application-wide hardcoded password.
 public static class VaultStorage {
  static readonly object Gate=new object();
  static readonly byte[] Entropy=Encoding.UTF8.GetBytes("QLKP|DATABASE|DPAPI|V1");
  static readonly byte[] KeyMagic=Encoding.ASCII.GetBytes("QLKPDK01");
  public static string KeyPath {get{return Path.Combine(DataStore.DataFolder,"qlkp-vault.key.dpapi");}}
  static byte[] RandomKey(){var b=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return b;}
  internal static string Hex(byte[] key){if(key==null||key.Length!=32)throw new InvalidDataException("Khóa kho không hợp lệ.");return BitConverter.ToString(key).Replace("-","");}
  internal static void GuardFile(string path){if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Không chấp nhận tệp liên kết cho kho dữ liệu.");}
  public static bool IsPlainSqlite(string path) {
   if(!File.Exists(path))return false;using(var f=File.OpenRead(path)){var b=new byte[16];return f.Read(b,0,16)==16&&Encoding.ASCII.GetString(b)=="SQLite format 3\0";}
  }
  internal static byte[] ReadKey(string keyPath) {
   GuardFile(keyPath);if(!File.Exists(keyPath))throw new CryptographicException("Thiếu khóa kho. Giữ nguyên kho và phục hồi từ bản sao mã hóa.");
   byte[] b=File.ReadAllBytes(keyPath);
   try {
    if(b.Length<32||b.Length>16384||!b.Take(8).SequenceEqual(KeyMagic))throw new CryptographicException("Tệp khóa không hợp lệ.");
    var key=ProtectedData.Unprotect(b.Skip(8).ToArray(),Entropy,DataProtectionScope.CurrentUser);
    if(key.Length!=32){Array.Clear(key,0,key.Length);throw new CryptographicException("Độ dài khóa không hợp lệ.");}return key;
   }finally{Array.Clear(b,0,b.Length);}
  }
  internal static byte[] CurrentKey(){return ReadKey(KeyPath);}
  static void EnsureKey(bool existingEncrypted) {
   if(File.Exists(KeyPath)){var test=CurrentKey();Array.Clear(test,0,test.Length);return;}
   if(existingEncrypted)throw new CryptographicException("Kho đã mã hóa nhưng thiếu khóa Windows. Không tạo khóa thay thế và không xóa dữ liệu.");
   var key=RandomKey();byte[] protectedKey=null;
   string temp=KeyPath+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    protectedKey=ProtectedData.Protect(key,Entropy,DataProtectionScope.CurrentUser);
    using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(KeyMagic,0,8);f.Write(protectedKey,0,protectedKey.Length);f.Flush(true);}
    File.Move(temp,KeyPath);
   }finally{Array.Clear(key,0,key.Length);if(protectedKey!=null)Array.Clear(protectedKey,0,protectedKey.Length);if(File.Exists(temp))File.Delete(temp);}
  }
  static SQLiteConnection Connect(string path,bool readOnly,bool create) {
   if(path!=":memory:")GuardFile(path);
   var builder=new SQLiteConnectionStringBuilder{DataSource=path,Version=3,Pooling=false,ReadOnly=readOnly,FailIfMissing=!create};
   var c=new SQLiteConnection(builder.ConnectionString);try{c.Open();return c;}catch{c.Dispose();throw;}
  }
  static void Execute(SQLiteConnection c,string sql){using(var cmd=new SQLiteCommand(sql,c))cmd.ExecuteNonQuery();}
  static void SelectCipher(SQLiteConnection c) {
   Execute(c,"PRAGMA cipher='sqlcipher';PRAGMA hmac_use=1;PRAGMA hmac_algorithm=2;PRAGMA plaintext_header_size=0;");
  }
  internal static SQLiteConnection OpenEncrypted(string path,byte[] key,bool readOnly=false,bool create=false) {
   var c=Connect(path,readOnly,create);
   try {
    SelectCipher(c);Execute(c,"PRAGMA key=\"x'"+Hex(key)+"'\";");
    using(var cmd=new SQLiteCommand("SELECT sqlite3mc_version()",c))if(Convert.ToString(cmd.ExecuteScalar())!="SQLite3 Multiple Ciphers 2.6.1")throw new CryptographicException("Không đúng thư viện mã hóa kho.");
    using(var cmd=new SQLiteCommand("SELECT count(*) FROM sqlite_master",c))cmd.ExecuteScalar();
    Execute(c,"PRAGMA trusted_schema=OFF;PRAGMA temp_store=MEMORY;PRAGMA busy_timeout=5000;");
    return c;
   }catch{c.Dispose();throw;}
  }
  internal static SQLiteConnection OpenPlain(string path,bool readOnly=true,bool create=false) {
   if(path!=":memory:"&&File.Exists(path)&&new FileInfo(path).Length>0&&!IsPlainSqlite(path))throw new InvalidDataException("Nguồn không phải SQLite chưa mã hóa.");
   var c=Connect(path,readOnly,create);try{Execute(c,"PRAGMA trusted_schema=OFF;PRAGMA temp_store=MEMORY;PRAGMA busy_timeout=5000;");return c;}catch{c.Dispose();throw;}
  }
  public static SQLiteConnection OpenCurrent() {
   var key=CurrentKey();try{return OpenEncrypted(DataStore.DatabasePath,key,false,false);}finally{Array.Clear(key,0,key.Length);}
  }
  internal static void Check(SQLiteConnection c) {
   using(var q=new SQLiteCommand("PRAGMA integrity_check",c))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Kho dữ liệu không toàn vẹn.");
   using(var q=new SQLiteCommand("PRAGMA foreign_key_check",c))using(var r=q.ExecuteReader())if(r.Read())throw new InvalidDataException("Liên kết dữ liệu bị lỗi.");
  }
  internal static string Quote(string name){if(!Regex.IsMatch(name??"",@"^[a-z][a-z0-9_]*$"))throw new InvalidDataException("Tên bảng/cột không hợp lệ.");return "\""+name+"\"";}
  internal static List<string> Tables(SQLiteConnection c) {
   var list=new List<string>();using(var cmd=new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",c))using(var r=cmd.ExecuteReader())while(r.Read()){Quote(r.GetString(0));list.Add(r.GetString(0));}return list;
  }
  internal static List<string> Columns(SQLiteConnection c,string table) {
   var list=new List<string>();using(var cmd=new SQLiteCommand("PRAGMA table_info("+Quote(table)+")",c))using(var r=cmd.ExecuteReader())while(r.Read()){Quote(r.GetString(1));list.Add(r.GetString(1));}return list;
  }
  // Only application-shipped DDL is executed, never DDL/triggers supplied by an import.
  internal static IEnumerable<string> SchemaStatements() {
   string text=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"schema.sql"),Encoding.UTF8);
   var current=new StringBuilder();
   foreach(string line in text.Split('\n')) {
    if(line.TrimStart().StartsWith("--",StringComparison.Ordinal))continue;
    current.AppendLine(line);
    if(line.TrimEnd().EndsWith(";",StringComparison.Ordinal)&&sqlite3_complete(Utf8(current.ToString()))!=0){var statement=current.ToString().Trim();current.Clear();if(statement.Length>0)yield return statement;}
   }
   if(!String.IsNullOrWhiteSpace(current.ToString()))throw new InvalidDataException("Schema SQL chưa kết thúc.");
  }
  internal static void CopyTrusted(SQLiteConnection source,SQLiteConnection destination) {
   Check(source);
   var statements=SchemaStatements().ToList();
   Execute(destination,"PRAGMA foreign_keys=OFF;");
   using(var sourceTx=source.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))using(var tx=destination.BeginTransaction()) {
    foreach(string sql in statements.Where(x=>x.StartsWith("CREATE TABLE",StringComparison.OrdinalIgnoreCase)))using(var q=new SQLiteCommand(sql,destination,tx))q.ExecuteNonQuery();
    var allowed=new HashSet<string>(Tables(destination),StringComparer.Ordinal);
    foreach(string table in Tables(source)) {
     if(!allowed.Contains(table))throw new InvalidDataException("Kho chứa bảng không thuộc ứng dụng; giữ nguồn và dừng chuyển đổi.");
     var columns=Columns(source,table);var targetColumns=new HashSet<string>(Columns(destination,table),StringComparer.Ordinal);
     if(columns.Any(x=>!targetColumns.Contains(x)))throw new InvalidDataException("Kho có cột chưa được hỗ trợ; không bỏ dữ liệu.");
     string fields=String.Join(",",columns.Select(Quote));
     using(var read=new SQLiteCommand("SELECT "+fields+" FROM "+Quote(table),source,sourceTx))using(var r=read.ExecuteReader())
     using(var put=new SQLiteCommand("INSERT INTO "+Quote(table)+"("+fields+") VALUES("+String.Join(",",columns.Select((x,i)=>"@v"+i))+")",destination,tx)) {
      for(int i=0;i<columns.Count;i++)put.Parameters.Add(new SQLiteParameter("@v"+i));
      while(r.Read()){for(int i=0;i<columns.Count;i++)put.Parameters[i].Value=r.GetValue(i);put.ExecuteNonQuery();}
     }
    }
    foreach(string sql in statements.Where(x=>!x.StartsWith("CREATE TABLE",StringComparison.OrdinalIgnoreCase)&&!x.StartsWith("PRAGMA",StringComparison.OrdinalIgnoreCase)))using(var q=new SQLiteCommand(sql,destination,tx))q.ExecuteNonQuery();
    long version;using(var q=new SQLiteCommand("PRAGMA user_version",source,sourceTx))version=Convert.ToInt64(q.ExecuteScalar());
    using(var q=new SQLiteCommand("PRAGMA user_version="+version.ToString(CultureInfo.InvariantCulture),destination,tx))q.ExecuteNonQuery();
    using(var q=new SQLiteCommand("PRAGMA foreign_key_check",destination,tx))using(var r=q.ExecuteReader())if(r.Read())throw new InvalidDataException("Chuyển kho làm sai liên kết; đã hủy.");
    tx.Commit();sourceTx.Commit();
   }
   Execute(destination,"PRAGMA foreign_keys=ON;");Check(destination);
  }
  internal static string Fingerprint(SQLiteConnection c,IEnumerable<string> sourceTables=null,Dictionary<string,List<string>> sourceColumns=null) {
   using(var hash=SHA256.Create()) {
    Action<byte[]> add=b=>{var n=BitConverter.GetBytes(b.Length);hash.TransformBlock(n,0,n.Length,n,0);hash.TransformBlock(b,0,b.Length,b,0);};
    foreach(string table in (sourceTables??Tables(c)).OrderBy(x=>x,StringComparer.Ordinal)) {
     add(Encoding.UTF8.GetBytes(table));var columns=sourceColumns==null?Columns(c,table):sourceColumns[table];
     string names=String.Join(",",columns.Select(Quote));
     using(var cmd=new SQLiteCommand("SELECT "+names+" FROM "+Quote(table)+" ORDER BY "+names,c))using(var r=cmd.ExecuteReader())while(r.Read())for(int i=0;i<columns.Count;i++) {
      object v=r.GetValue(i);add(v==DBNull.Value?new byte[]{0}:v is byte[]?(new byte[]{1}).Concat((byte[])v).ToArray():Encoding.UTF8.GetBytes(v.GetType().FullName+":"+Convert.ToString(v,CultureInfo.InvariantCulture)));
     }
    }
    hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
   }
  }
  internal static string Snapshot(string path,byte[] key) {
   if(File.Exists(path))throw new IOException("Tệp snapshot đã tồn tại.");
   var sourceKey=CurrentKey();try{using(var source=OpenEncrypted(DataStore.DatabasePath,sourceKey,true))using(var target=OpenEncrypted(path,key,false,true)){CopyTrusted(source,target);return Fingerprint(source);}}finally{Array.Clear(sourceKey,0,sourceKey.Length);}
  }
  public static void EnsureCurrent() {
   lock(Gate) {
    Directory.CreateDirectory(DataStore.DataFolder);GuardFile(DataStore.DatabasePath);
    bool exists=File.Exists(DataStore.DatabasePath)&&new FileInfo(DataStore.DatabasePath).Length>0;
    bool plain=exists&&IsPlainSqlite(DataStore.DatabasePath);EnsureKey(exists&&!plain);
    var key=CurrentKey();string stage=null;
    try {
     if(!exists){using(var c=OpenEncrypted(DataStore.DatabasePath,key,false,true))Execute(c,"PRAGMA user_version=0;");return;}
     if(!plain){using(var c=OpenEncrypted(DataStore.DatabasePath,key))Check(c);return;}
     if(File.Exists(DataStore.DatabasePath+"-wal")||File.Exists(DataStore.DatabasePath+"-journal"))throw new IOException("Kho cũ còn giao dịch chưa đóng. Mở bản cũ, đóng đúng cách rồi chuyển đổi; không xóa tệp giao dịch.");
     string folder=Path.Combine(DataStore.DataFolder,"maintenance");Directory.CreateDirectory(folder);
     string stamp=Guid.NewGuid().ToString("N");stage=Path.Combine(folder,"encrypt-"+stamp+".qlkpvault");
     string retained=Path.Combine(folder,"before-encryption-"+stamp+".qlkpvault");
     using(var locked=new FileStream(DataStore.DatabasePath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
     using(var source=OpenPlain(DataStore.DatabasePath)) {
      var tables=Tables(source);var cols=tables.ToDictionary(x=>x,x=>Columns(source,x));string before=Fingerprint(source,tables,cols);
      using(var target=OpenEncrypted(stage,key,false,true)){CopyTrusted(source,target);if(before!=Fingerprint(target,tables,cols))throw new InvalidDataException("Chuyển mã hóa thay đổi dữ liệu; giữ kho cũ.");}
      source.Close();
      if(IsPlainSqlite(stage))throw new CryptographicException("Snapshot chưa được mã hóa; không chuyển kho.");
      File.Copy(stage,retained,false);
      // Deny concurrent writers through conversion and replacement. The old logical data
      // has already been retained as a verified encrypted snapshot.
      File.Replace(stage,DataStore.DatabasePath,null);stage=null;
     }
     using(var c=OpenEncrypted(DataStore.DatabasePath,key))Check(c);
     StartupDiagnostics.WriteEvent("VAULT_ENCRYPTION_MIGRATION_COMPLETE");
    }finally{Array.Clear(key,0,key.Length);if(stage!=null&&File.Exists(stage))File.Delete(stage);}
   }
  }
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_complete(byte[] sql);
  static byte[] Utf8(string value){return Encoding.UTF8.GetBytes(value+"\0");}
 }
}
