using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
namespace QLKP.Desktop.Core {
 /// <summary>
 /// I06 PILOT: one-way OFFLINE transfer of non-personal catalog labels from PC to Android.
 /// No LAN service, no bidirectional sync, no citizen data or automatic Android upload.
 /// Binary: QLKPSY01 | salt[16] | nonce[12] | rounds BE32 | GCM ciphertext + 16 tag.
 /// The 40-byte header is authenticated AAD; plaintext is UTF-8 text with a restricted schema.
 /// </summary>
 public static class OfflineCatalogTransfer {
  const int Rounds=310000;
  const int MaxPayload=512*1024;
  const int MaxRows=1000;
  static readonly byte[] Magic=Encoding.ASCII.GetBytes("QLKPSY01");
  static readonly string[] AllowedGroups={"LOAI_NHA","NHOM_CU_TRU","LOAI_CONG_TRINH","NHOM_HO_SO","LOAI_HOAT_DONG","TRANG_THAI"};
  static byte[] Random(int length) {var b=new byte[length];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return b;}
  static byte[] Transform(bool encrypt,byte[] header,byte[] input,byte[] key) {
   var cipher=new GcmBlockCipher(new AesEngine());
   var nonce=new byte[12];Buffer.BlockCopy(header,24,nonce,0,12);
   cipher.Init(encrypt,new AeadParameters(new KeyParameter(key),128,nonce,header));
   var outBytes=new byte[cipher.GetOutputSize(input.Length)];
   int n=cipher.ProcessBytes(input,0,input.Length,outBytes,0);n+=cipher.DoFinal(outBytes,n);
   if(n!=outBytes.Length)Array.Resize(ref outBytes,n);
   return outBytes;
  }
  internal static byte[] Seal(byte[] magic,byte[] plain,string phrase) {
   ValidatePassphrase(phrase);
   var header=new byte[40];
   Buffer.BlockCopy(magic,0,header,0,8);
   Buffer.BlockCopy(Random(16),0,header,8,16);
   Buffer.BlockCopy(Random(12),0,header,24,12);
   header[36]=(byte)(Rounds>>24);header[37]=(byte)((Rounds>>16)&255);
   header[38]=(byte)((Rounds>>8)&255);header[39]=(byte)(Rounds&255);
   return header.Concat(TransformPackage(true,header,plain,phrase)).ToArray();
  }
  internal static byte[] Unseal(byte[] magic,byte[] packet,string phrase,int maxPayload) {
   ValidatePassphrase(phrase);
   if(packet.Length<56||packet.Length>maxPayload+56||!packet.Take(8).SequenceEqual(magic)||
    ((packet[36]<<24)|(packet[37]<<16)|(packet[38]<<8)|packet[39])!=Rounds)
    throw new InvalidDataException("Sai định dạng hoặc kích thước gói.");
   try {return TransformPackage(false,packet.Take(40).ToArray(),packet.Skip(40).ToArray(),phrase);}
   catch(Org.BouncyCastle.Crypto.InvalidCipherTextException) {
    throw new InvalidDataException("Sai mật khẩu hoặc gói đã bị thay đổi.");
   }
  }
  static byte[] TransformPackage(bool encrypt,byte[] header,byte[] input,string phrase) {
   byte[] key;
   using(var derivation=new Rfc2898DeriveBytes(phrase,header.Skip(8).Take(16).ToArray(),Rounds,HashAlgorithmName.SHA256))
    key=derivation.GetBytes(32);
   try {return Transform(encrypt,header,input,key);}
   finally {Array.Clear(key,0,key.Length);}
  }
  static void ValidatePassphrase(string phrase) {
   if(String.IsNullOrWhiteSpace(phrase)||phrase.Length<14||phrase.Length>128 || phrase.Any(ch=>ch<33 || ch>126))
    throw new InvalidOperationException("Mật khẩu cần 14–128 ký tự ASCII (không dấu, không khoảng trắng) để tương thích Android. Chia sẻ qua kênh riêng.");
  }
  static bool SafeLabel(string text) {
   // Not a privacy certification: reject obvious contact/identifier patterns and prohibit all free-form notes.
   return !String.IsNullOrWhiteSpace(text) && text.Length<=80 && !text.Contains("\t") && !text.Contains("\r") &&
    !text.Contains("\n") && !Regex.IsMatch(text,@"\b\d{9,}\b|@|https?://",RegexOptions.IgnoreCase);
  }
  public static string Export(Session actor,string fullPath,string phrase) {
   return ExportCore(actor,fullPath,phrase,false);
  }
  // Operational route accepts only canonical, non-personal housing/residency categories.
  public static string ExportReferenceCatalog(Session actor,string fullPath,string phrase) {
   return ExportCore(actor,fullPath,phrase,true);
  }
  public static bool IsReferenceEntry(string group,string code,string label) {
   if(group=="LOAI_NHA") {
    var codes=new[]{"NHA_RIENG","NHA_CAP4","CAN_HO","PHONG_TRO","NHA_THUE","NHA_TAP_THE","O_CUNG_THAN_NHAN","CHUA_XAC_MINH"};
    return codes.Contains(code,StringComparer.Ordinal)&&label==ClassificationLabels.Housing(code.ToLowerInvariant());
   }
   if(group=="NHOM_CU_TRU") {
    var codes=new[]{"THUONG_TRU","TAM_TRU","LUU_TRU","TAM_VANG","CHUA_XAC_MINH"};
    return codes.Contains(code,StringComparer.Ordinal)&&label==ClassificationLabels.Residency(code.ToLowerInvariant());
   }
   return false;
  }
  public static CatalogEntry[] ReferenceEntries() {
   var entries=new System.Collections.Generic.List<CatalogEntry>();
   foreach(var code in new[]{"NHA_RIENG","NHA_CAP4","CAN_HO","PHONG_TRO","NHA_THUE","NHA_TAP_THE","O_CUNG_THAN_NHAN","CHUA_XAC_MINH"})
    entries.Add(new CatalogEntry{Category="LOAI_NHA",Code=code,Label=ClassificationLabels.Housing(code.ToLowerInvariant()),Active=true});
   foreach(var code in new[]{"THUONG_TRU","TAM_TRU","LUU_TRU","TAM_VANG","CHUA_XAC_MINH"})
    entries.Add(new CatalogEntry{Category="NHOM_CU_TRU",Code=code,Label=ClassificationLabels.Residency(code.ToLowerInvariant()),Active=true});
   return entries.ToArray();
  }
  private static string ExportCore(Session actor,string fullPath,string phrase,bool referenceOnly) {
   ValidatePassphrase(phrase);
   if(actor==null || String.IsNullOrWhiteSpace(actor.AreaId))
    throw new UnauthorizedAccessException("Chỉ tài khoản thuộc khu phố, có quyền I06 và I07, mới được xuất gói danh mục.");
   if(String.IsNullOrWhiteSpace(fullPath) || !Path.IsPathRooted(fullPath) ||
    !String.Equals(Path.GetExtension(fullPath),".qlkpsync",StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Chọn tệp .qlkpsync với đường dẫn đầy đủ.");
   if(File.Exists(fullPath))throw new IOException("Tệp đã tồn tại. Không được ghi đè bản đã xuất.");
   byte[] plain=null;
   using(var conn=DataStore.Open()) {
    DataStore.RequireForOfflineTransfer(conn,actor);
    try {
    var buffer=new StringBuilder(referenceOnly?"QLKP-CATALOG-V2\n":"QLKP-CATALOG-V1\n");int count=0;
    using(var cmd=new SQLiteCommand(@"SELECT category_code,item_code,item_label,active,version,scope_area
       FROM shared_catalog_items g WHERE active=1 AND ((@reference=1 AND scope_area='*') OR (@reference=0 AND (scope_area=@area OR (scope_area='*' AND NOT EXISTS(SELECT 1 FROM shared_catalog_items local WHERE local.scope_area=@area AND local.category_code=g.category_code AND local.item_code=g.item_code)))))
       AND category_code IN ('LOAI_NHA','NHOM_CU_TRU','LOAI_CONG_TRINH','NHOM_HO_SO','LOAI_HOAT_DONG','TRANG_THAI')
       AND (@reference=0 OR category_code IN ('LOAI_NHA','NHOM_CU_TRU'))
       ORDER BY category_code,item_code,scope_area LIMIT 1001",conn)) {
     cmd.Parameters.AddWithValue("@area",actor.AreaId);
     cmd.Parameters.AddWithValue("@reference",referenceOnly?1:0);
     using(var reader=cmd.ExecuteReader())while(reader.Read()) {
      string group=reader.GetString(0),code=reader.GetString(1),label=reader.GetString(2);
      if(!AllowedGroups.Contains(group,StringComparer.Ordinal))continue;
      if(referenceOnly) {
       if(!IsReferenceEntry(group,code,label))throw new InvalidDataException("Chỉ chuyển mã và nhãn chuẩn của LOAI_NHA/NHOM_CU_TRU. Hãy kiểm tra lại danh mục I07.");
      } else if(!SafeLabel(label))throw new InvalidDataException("Nhãn danh mục vượt giới hạn hoặc chứa địa chỉ liên hệ/định danh; không xuất gói.");
      if(++count>MaxRows)throw new InvalidDataException("Chỉ xuất tối đa 1.000 nhãn đã kiểm tra.");
      string packed=Convert.ToBase64String(Encoding.UTF8.GetBytes(label));
      string tier=reader.GetString(5)=="*"?"G":"L";
      buffer.Append(group).Append('\t').Append(code).Append('\t').Append(packed).Append('\t').Append(tier).Append('\n');
     }
    }
    if(count<1)throw new InvalidOperationException("Chưa có danh mục không nhạy cảm được phép chuyển.");
    plain=Encoding.UTF8.GetBytes(buffer.ToString());
    if(plain.Length>MaxPayload)throw new InvalidDataException("Dữ liệu xuất lớn hơn 512 KB.");
    byte[] packet=Seal(Magic,plain,phrase);
    string temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fullPath)),".qlkpsync-"+Guid.NewGuid().ToString("N")+".tmp");
    try {
     using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
      output.Write(packet,0,packet.Length);output.Flush(true);
     }
     File.Move(temp,fullPath);
    } finally {if(File.Exists(temp))File.Delete(temp);}
    // Scope-only audit entry: never log labels, IDs, passphrase or file path.
    try {DataStore.AuditOfflineCatalogTransfer(conn,actor.Id);}
    catch {if(File.Exists(fullPath))File.Delete(fullPath);throw;}
    return "Đã xuất "+count+" nhãn danh mục qua gói AES-256-GCM. Chưa đồng bộ hai chiều hoặc kết nối mạng.";
    } finally {
     if(plain!=null)Array.Clear(plain,0,plain.Length);
    }
   }
  }
 }
}
