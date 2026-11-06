using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
namespace QLKP.Desktop.Core {
 // V2 portable backups: authenticated AES envelope containing a database key and encrypted pages.
 // V1 encrypted backups remain readable via a RAM-only SQLite image.
 public static class EncryptedBackup {
  const int MaxSnapshotBytes=128*1024*1024,Iterations=310000,HeaderSize=40;
  static readonly object RestoreLock=new object();
  static void CheckPassphrase(string p){if(String.IsNullOrWhiteSpace(p)||p.Length<14||p.Length>1024)throw new InvalidOperationException("Mật khẩu phục hồi cần 14–1024 ký tự.");}
  static byte[] Random(int n){var b=new byte[n];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return b;}
  static byte[] Transform(bool encrypt,byte[] key,byte[] header,byte[] input) {
   var gcm=new GcmBlockCipher(new AesEngine());gcm.Init(encrypt,new AeadParameters(new KeyParameter(key),128,header.Skip(24).Take(12).ToArray(),header));
   var output=new byte[gcm.GetOutputSize(input.Length)];int n=gcm.ProcessBytes(input,0,input.Length,output,0);n+=gcm.DoFinal(output,n);if(n!=output.Length)Array.Resize(ref output,n);return output;
  }
  static string Stage(string tag){string folder=Path.Combine(DataStore.DataFolder,"maintenance");Directory.CreateDirectory(folder);return Path.Combine(folder,tag+"-"+Guid.NewGuid().ToString("N")+".qlkpvault");}
  static void WriteEnvelope(string path,string phrase,byte[] payload,string magic) {
   if(File.Exists(path))throw new IOException("Tệp đã tồn tại; không ghi đè bản sao cũ.");
   byte[] header=new byte[40],salt=Random(16),nonce=Random(12),key=null,cipher=null;
   Buffer.BlockCopy(Encoding.ASCII.GetBytes(magic),0,header,0,8);Buffer.BlockCopy(salt,0,header,8,16);Buffer.BlockCopy(nonce,0,header,24,12);
   header[36]=(byte)((Iterations>>24)&255);header[37]=(byte)((Iterations>>16)&255);header[38]=(byte)((Iterations>>8)&255);header[39]=(byte)(Iterations&255);
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    using(var kdf=new Rfc2898DeriveBytes(phrase,salt,Iterations,HashAlgorithmName.SHA256))key=kdf.GetBytes(32);
    cipher=Transform(true,key,header,payload);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
    using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(header,0,40);f.Write(cipher,0,cipher.Length);f.Flush(true);}File.Move(temp,path);
   }finally{if(key!=null)Array.Clear(key,0,key.Length);if(cipher!=null)Array.Clear(cipher,0,cipher.Length);if(File.Exists(temp))File.Delete(temp);}
  }
  sealed class Payload:IDisposable {
   public byte[] Data;public bool Legacy;
   public void Dispose(){if(Data!=null){Array.Clear(Data,0,Data.Length);Data=null;}}
  }
  static Payload ReadEnvelope(string path,string phrase) {
   CheckPassphrase(phrase);VaultStorage.GuardFile(path);
   if(!File.Exists(path))throw new FileNotFoundException("Không tìm thấy bản sao.");
   long size=new FileInfo(path).Length;if(size<56||size>MaxSnapshotBytes+128)throw new InvalidDataException("Kích thước bản sao không hợp lệ.");
   var b=File.ReadAllBytes(path);byte[] key=null;
   try {
    string magic=Encoding.ASCII.GetString(b,0,8);if(magic!="QLKPBK01"&&magic!="QLKPBK02")throw new InvalidDataException("Sai định dạng bản sao.");
    int rounds=(b[36]<<24)|(b[37]<<16)|(b[38]<<8)|b[39];if(rounds!=Iterations)throw new InvalidDataException("Thông số khóa không được hỗ trợ.");
    using(var kdf=new Rfc2898DeriveBytes(phrase,b.Skip(8).Take(16).ToArray(),rounds,HashAlgorithmName.SHA256))key=kdf.GetBytes(32);
    return new Payload{Legacy=magic=="QLKPBK01",Data=Transform(false,key,b.Take(40).ToArray(),b.Skip(40).ToArray())};
   }catch(InvalidCipherTextException){throw new UnauthorizedAccessException("Sai mật khẩu hoặc bản sao đã bị sửa.");}
   finally{Array.Clear(b,0,b.Length);if(key!=null)Array.Clear(key,0,key.Length);}
  }
  static void CheckSnapshot(SQLiteConnection c) {
   VaultStorage.Check(c);
   using(var q=new SQLiteCommand("PRAGMA user_version",c))if(Convert.ToInt32(q.ExecuteScalar())>12)throw new InvalidDataException("Schema bản sao mới hơn ứng dụng.");
   foreach(string table in new[]{"accounts","households","people"})using(var q=new SQLiteCommand("SELECT count(*) FROM "+VaultStorage.Quote(table),c))q.ExecuteScalar();
  }
  public static void Create(Session admin,string path,string phrase) {
   CheckPassphrase(phrase);DataStore.RequireRootMaintenance(admin);
   if(!Path.IsPathRooted(path)||!String.Equals(Path.GetExtension(path),".qlkpbak",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Chọn đường dẫn tệp .qlkpbak đầy đủ.");
   if(File.Exists(path))throw new IOException("Bản sao đã tồn tại.");
   string stage=Stage("backup");byte[] key=VaultStorage.CurrentKey(),bytes=null,payload=null;
   try {
    VaultStorage.Snapshot(stage,key);using(var c=VaultStorage.OpenEncrypted(stage,key))CheckSnapshot(c);
    if(VaultStorage.IsPlainSqlite(stage))throw new CryptographicException("Snapshot chưa mã hóa.");
    if(new FileInfo(stage).Length>MaxSnapshotBytes)throw new InvalidDataException("Bản sao vượt 128 MB.");
    bytes=File.ReadAllBytes(stage);payload=new byte[32+bytes.Length];Buffer.BlockCopy(key,0,payload,0,32);Buffer.BlockCopy(bytes,0,payload,32,bytes.Length);
    WriteEnvelope(path,phrase,payload,"QLKPBK02");DataStore.LogMaintenance(admin,"ENCRYPTED_BACKUP_CREATED");
   }finally{Array.Clear(key,0,key.Length);if(bytes!=null)Array.Clear(bytes,0,bytes.Length);if(payload!=null)Array.Clear(payload,0,payload.Length);if(File.Exists(stage))File.Delete(stage);}
  }
  // Creates an encrypted snapshot under the current machine key, regardless of backup generation.
  internal static string Materialize(string path,string phrase) {
   string stage=Stage("restore"),input=null;byte[] targetKey=VaultStorage.CurrentKey(),oldKey=null;
   try {
    if(String.Equals(Path.GetExtension(path),".qlkpvault",StringComparison.OrdinalIgnoreCase)) {
     VaultStorage.GuardFile(path);if(!File.Exists(path)||new FileInfo(path).Length>MaxSnapshotBytes||VaultStorage.IsPlainSqlite(path))throw new InvalidDataException("Bản phục hồi cục bộ không hợp lệ.");
     using(var source=VaultStorage.OpenEncrypted(path,targetKey,true))using(var target=VaultStorage.OpenEncrypted(stage,targetKey,false,true)){CheckSnapshot(source);VaultStorage.CopyTrusted(source,target);}
    } else
    using(var p=ReadEnvelope(path,phrase)) {
     if(p.Legacy) {
      using(var memory=new SqliteMemoryImage(p.Data))using(var target=VaultStorage.OpenEncrypted(stage,targetKey,false,true)){CheckSnapshot(memory.Connection);VaultStorage.CopyTrusted(memory.Connection,target);}
     } else {
      if(p.Data.Length<48)throw new InvalidDataException("Thiếu dữ liệu snapshot.");oldKey=p.Data.Take(32).ToArray();
      input=Stage("restore-source");
      using(var f=new FileStream(input,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(p.Data,32,p.Data.Length-32);f.Flush(true);}
      using(var old=VaultStorage.OpenEncrypted(input,oldKey,true))using(var target=VaultStorage.OpenEncrypted(stage,targetKey,false,true)){CheckSnapshot(old);VaultStorage.CopyTrusted(old,target);}
     }
    }
    if(VaultStorage.IsPlainSqlite(stage))throw new CryptographicException("Không tạo được snapshot mã hóa.");
    using(var c=VaultStorage.OpenEncrypted(stage,targetKey))CheckSnapshot(c);
    return stage;
   }catch{if(File.Exists(stage))File.Delete(stage);throw;}
   finally{Array.Clear(targetKey,0,targetKey.Length);if(oldKey!=null)Array.Clear(oldKey,0,oldKey.Length);if(input!=null&&File.Exists(input))File.Delete(input);}
  }
  public static void Validate(Session admin,string path,string phrase) {DataStore.RequireRootMaintenance(admin);string stage=Materialize(path,phrase);File.Delete(stage);}
  public static void Restore(Session admin,string path,string phrase) {
   lock(RestoreLock) {
    DataStore.RequireRootMaintenance(admin);string stage=null;string rollback=Stage("before-restore");byte[] key=VaultStorage.CurrentKey();bool replaced=false;
    try {
     stage=Materialize(path,phrase);VaultStorage.Snapshot(rollback,key);
     if(File.Exists(DataStore.DatabasePath+"-wal")||File.Exists(DataStore.DatabasePath+"-journal"))throw new IOException("Kho đang ghi dữ liệu; dừng và thử lại sau.");
     DataStore.LogMaintenance(admin,"RESTORE_REQUESTED");File.Replace(stage,DataStore.DatabasePath,null);stage=null;replaced=true;
     using(var c=VaultStorage.OpenEncrypted(DataStore.DatabasePath,key))CheckSnapshot(c);
     StartupDiagnostics.WriteEvent("RESTORE_COMPLETED_RELOGIN_REQUIRED");
    }catch{
     if(replaced&&File.Exists(rollback)){string repair=Stage("rollback");try{File.Copy(rollback,repair,false);File.Replace(repair,DataStore.DatabasePath,null);}finally{if(File.Exists(repair))File.Delete(repair);}}
     throw;
    }finally{Array.Clear(key,0,key.Length);if(stage!=null&&File.Exists(stage))File.Delete(stage);}
   }
  }
  internal static void WriteLegacyFixture(string path,string phrase,byte[] plainSqlite){WriteEnvelope(path,phrase,plainSqlite,"QLKPBK01");}
 }
}
