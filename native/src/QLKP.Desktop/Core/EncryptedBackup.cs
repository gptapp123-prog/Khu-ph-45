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
 /// <summary>
 /// PILOT: portable password-protected SQLite snapshots. This protects .qlkpbak exports,
 /// NOT the on-disk application database (SQLCipher remains a production gate).
 /// The archive uses PBKDF2-HMAC-SHA256 + AES-256-GCM with its header as AAD.
 /// Format: 8-byte magic, 16-byte salt, 12-byte nonce, 4-byte BE iteration count, ciphertext + 16-byte GCM tag.
 /// </summary>
 public static class EncryptedBackup {
  const int MaxSnapshotBytes = 128*1024*1024;
  const int Iterations=310000;
  const int HeaderSize=40;
  static readonly byte[] Magic=Encoding.ASCII.GetBytes("QLKPBK01");
  static readonly object RestoreLock = new object();

  static void CheckPassphrase(string phrase) {
   if(String.IsNullOrWhiteSpace(phrase)||phrase.Length<14||phrase.Length>1024)
    throw new InvalidOperationException("Mật khẩu phục hồi cần 14–1024 ký tự. Mất mật khẩu sẽ không thể phục hồi bản sao.");
  }
  static byte[] RandomBytes(int n) {
   var b=new byte[n]; using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return b;
  }
  static byte[] DeriveKey(string pass,byte[] salt,int rounds) {
   using(var kdf=new Rfc2898DeriveBytes(pass,salt,rounds,HashAlgorithmName.SHA256))return kdf.GetBytes(32);
  }
  static byte[] AuthenticatedTransform(bool encrypt,byte[] key,byte[] nonce,byte[] header,byte[] input) {
   // GCM uses a 128-bit authentication tag; any wrong key or tampering fails before plaintext is persisted.
   var gcm=new GcmBlockCipher(new AesEngine());
   gcm.Init(encrypt,new AeadParameters(new KeyParameter(key),128,nonce,header));
   byte[] output=new byte[gcm.GetOutputSize(input.Length)];
   int n=gcm.ProcessBytes(input,0,input.Length,output,0);
   n+=gcm.DoFinal(output,n);
   if(n!=output.Length)Array.Resize(ref output,n);
   return output;
  }
  static uint ParseBigEndian(byte[] b,int p) {
   return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];
  }
  static byte[] Header(byte[] salt,byte[] nonce) {
   var b=new byte[HeaderSize];
   Buffer.BlockCopy(Magic,0,b,0,8);Buffer.BlockCopy(salt,0,b,8,16);Buffer.BlockCopy(nonce,0,b,24,12);
   b[36]=(byte)(Iterations>>24);b[37]=(byte)(Iterations>>16);b[38]=(byte)(Iterations>>8);b[39]=(byte)Iterations;
   return b;
  }
  static void CheckSqliteSnapshot(string path) {
   if(!File.Exists(path))throw new FileNotFoundException("Không có dữ liệu sao lưu.");
   using(var file=File.OpenRead(path)) {
    var magic=new byte[16]; if(file.Read(magic,0,magic.Length)!=magic.Length ||
     Encoding.ASCII.GetString(magic)!="SQLite format 3\0")throw new InvalidDataException("Tệp khôi phục không phải SQLite.");
   }
   using(var c=new SQLiteConnection("Data Source="+path+";Version=3;Pooling=False;Read Only=True;")) {
    c.Open();
    using(var cmd=new SQLiteCommand("PRAGMA integrity_check;",c))
     if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new InvalidDataException("Kho SQLite sao lưu không toàn vẹn.");
    using(var cmd=new SQLiteCommand("PRAGMA user_version;",c))
     if(Convert.ToInt32(cmd.ExecuteScalar())>12)throw new InvalidDataException("Sao lưu có phiên bản schema mới hơn ứng dụng.");
    using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM accounts;",c))cmd.ExecuteScalar();
    using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM households;",c))cmd.ExecuteScalar();
    using(var cmd=new SQLiteCommand("PRAGMA foreign_key_check;",c))
     using(var result=cmd.ExecuteReader())if(result.Read())
      throw new InvalidDataException("Bản sao SQLite có khóa ngoại bị lỗi; từ chối khôi phục.");
   }
  }
  static string TempPath(string ext) {
   string folder=Path.Combine(DataStore.DataFolder,"maintenance");Directory.CreateDirectory(folder);
   return Path.Combine(folder,"qlkp-"+Guid.NewGuid().ToString("N")+ext);
  }
  public static void Create(Session admin,string filePath,string phrase) {
   CheckPassphrase(phrase);
   DataStore.RequireRootMaintenance(admin);
   if(String.IsNullOrWhiteSpace(filePath)||!Path.IsPathRooted(filePath))throw new InvalidOperationException("Chọn đường dẫn sao lưu đầy đủ.");
   if(!String.Equals(Path.GetExtension(filePath),".qlkpbak",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Tệp sao lưu phải có đuôi .qlkpbak.");
   if(File.Exists(filePath))throw new IOException("Đã tồn tại tệp sao lưu; không được ghi đè bản cũ.");
   string db=TempPath(".db"),cipherTemp=TempPath(".encrypted.tmp");
   byte[] plain=null,key=null,enc=null;
   try {
    using(var c=DataStore.Open())using(var cmd=new SQLiteCommand("VACUUM INTO @dst;",c)) {
     cmd.Parameters.AddWithValue("@dst",db);cmd.ExecuteNonQuery();
    }
    CheckSqliteSnapshot(db);
    long length=new FileInfo(db).Length;
    if(length>MaxSnapshotBytes)throw new InvalidOperationException("Bản thử nghiệm giới hạn 128 MB. Không tạo tệp sao lưu chưa an toàn cho dữ liệu lớn.");
    byte[] salt=RandomBytes(16),nonce=RandomBytes(12),header=Header(salt,nonce);
    plain=File.ReadAllBytes(db);key=DeriveKey(phrase,salt,Iterations);
    enc=AuthenticatedTransform(true,key,nonce,header,plain);
    using(var output=new FileStream(cipherTemp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
     output.Write(header,0,header.Length); output.Write(enc,0,enc.Length);output.Flush(true);
    }
    // The final file is encrypted and authenticated; never write plaintext to the selected destination.
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)));
    File.Move(cipherTemp,filePath);
    DataStore.LogMaintenance(admin,"ENCRYPTED_BACKUP_CREATED");
   } finally {
    if(plain!=null)Array.Clear(plain,0,plain.Length);
    if(key!=null)Array.Clear(key,0,key.Length);
    if(enc!=null)Array.Clear(enc,0,enc.Length);
    if(File.Exists(db))File.Delete(db); // Failure must fail the backup, not report success.
    try {if(File.Exists(cipherTemp))File.Delete(cipherTemp);}catch{}
   }
  }
  static byte[] DecryptPackage(string file,string phrase) {
   if(!File.Exists(file))throw new FileNotFoundException("Không tìm thấy tệp sao lưu.");
   var size=new FileInfo(file).Length;
   if(size<HeaderSize+16||size>MaxSnapshotBytes+HeaderSize+16)
    throw new InvalidDataException("Kích thước tệp sao lưu không phù hợp.");
   byte[] b=File.ReadAllBytes(file),key=null;
   try {
    if(!Magic.SequenceEqual(b.Take(8)))throw new InvalidDataException("Sai định dạng tệp sao lưu.");
    byte[] header=b.Take(HeaderSize).ToArray(),salt=b.Skip(8).Take(16).ToArray(),nonce=b.Skip(24).Take(12).ToArray();
    uint rounds=ParseBigEndian(b,36);
    if(rounds!=Iterations)throw new InvalidDataException("Thông số phục hồi không được hỗ trợ.");
    key=DeriveKey(phrase,salt,Iterations);
    return AuthenticatedTransform(false,key,nonce,header,b.Skip(HeaderSize).ToArray());
   } catch(InvalidCipherTextException) {throw new UnauthorizedAccessException("Sai mật khẩu hoặc tệp sao lưu đã bị thay đổi; không phục hồi.");}
   finally {if(key!=null)Array.Clear(key,0,key.Length);Array.Clear(b,0,b.Length);}
  }
  public static void Validate(Session admin,string filePath,string phrase) {
   CheckPassphrase(phrase);DataStore.RequireRootMaintenance(admin);
   string temp=TempPath(".validate.db");byte[] plaintext=null;
   try {
    plaintext=DecryptPackage(filePath,phrase);
    File.WriteAllBytes(temp,plaintext);
    CheckSqliteSnapshot(temp);
   } finally {
    if(plaintext!=null)Array.Clear(plaintext,0,plaintext.Length);
    if(File.Exists(temp))File.Delete(temp);
   }
  }
  public static void Restore(Session admin,string filePath,string phrase) {
   lock(RestoreLock) {
    CheckPassphrase(phrase);DataStore.RequireRootMaintenance(admin);
    string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");
    string stage=Path.Combine(DataStore.DataFolder,"qlkp-restore-stage-"+stamp+".db");
    string rollbackStage=Path.Combine(DataStore.DataFolder,"qlkp-rollback-stage-"+stamp+".db");
    string rollbackEncrypted=Path.Combine(DataStore.DataFolder,"maintenance","qlkp-pre-restore-"+stamp+".qlkpbak");
    string target=DataStore.DatabasePath;
    byte[] plain=null,rollbackPlain=null;
    bool replaced=false;
    try {
     plain=DecryptPackage(filePath,phrase);
     File.WriteAllBytes(stage,plain);
     CheckSqliteSnapshot(stage); // Verify incoming schema, integrity and foreign keys first.
     if(File.Exists(target+"-wal")||File.Exists(target+"-shm")||File.Exists(target+"-journal"))
      throw new IOException("Kho đang dùng WAL/journal; cần đóng mọi phiên ghi trước khi phục hồi.");
     if(!File.Exists(target))throw new FileNotFoundException("Không tìm thấy kho hiện tại để tạo rollback mã hóa.");
     // A tested, encrypted snapshot is retained BEFORE replacement. No unencrypted .db backup.
     Create(admin,rollbackEncrypted,phrase);
     Validate(admin,rollbackEncrypted,phrase);
     DataStore.LogMaintenance(admin,"RESTORE_REQUESTED");
     File.Replace(stage,target,null);replaced=true;
     CheckSqliteSnapshot(target);
     StartupDiagnostics.WriteEvent("RESTORE_COMPLETED_RELOGIN_REQUIRED");
    } catch {
     if(replaced && File.Exists(rollbackEncrypted)) {
      try {
       rollbackPlain=DecryptPackage(rollbackEncrypted,phrase);
       File.WriteAllBytes(rollbackStage,rollbackPlain);
       CheckSqliteSnapshot(rollbackStage);
       File.Replace(rollbackStage,target,null);
       StartupDiagnostics.WriteEvent("RESTORE_ROLLED_BACK_FROM_ENCRYPTED_COPY");
      } catch(Exception rollbackFailure) {
       StartupDiagnostics.WriteException("RESTORE_ROLLBACK_FAILED",rollbackFailure);
       // Preserve encrypted rollback file; never pretend the original DB was recovered.
      }
     }
     throw;
    } finally {
     if(plain!=null)Array.Clear(plain,0,plain.Length);
     if(rollbackPlain!=null)Array.Clear(rollbackPlain,0,rollbackPlain.Length);
     if(File.Exists(stage))File.Delete(stage);
     if(File.Exists(rollbackStage))File.Delete(rollbackStage);
    }
   }
  }
 }
}
