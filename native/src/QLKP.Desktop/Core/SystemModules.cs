using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace QLKP.Desktop.Core {
 public class CatalogEntry {
  public string Id {get;set;}
  public string Scope {get;set;}
  public string Category {get;set;}
  public string Code {get;set;}
  public string Label {get;set;}
  public string Details {get;set;}
  public bool Active {get;set;}
  public long Version {get;set;}
 }
 public static partial class DataStore {
  private static string CatalogCode(string input) {
   var code=(input??"").Trim().ToUpperInvariant();
   if(!Regex.IsMatch(code,@"^[A-Z0-9_]{2,32}$"))
    throw new InvalidOperationException("Mã danh mục gồm 2–32 ký tự A-Z, 0-9 hoặc dấu gạch dưới.");
   return code;
  }
  private static string CatalogScope(Session s,bool workArea=false) { return s.Role=="ROOT_ADMIN"&&!workArea?"*":s.AreaId; }
  public static RecordPage<CatalogEntry> ListCatalog(Session s,string term,int pageIndex,int pageSize=50,bool workArea=false) {
   if(pageIndex<0 || pageSize<1 || pageSize>100)throw new ArgumentOutOfRangeException("pageIndex");
   using(var c=Open()) {
    Require(c,null,s,"I07","read",s==null?null:s.AreaId);
    var scope=CatalogScope(s,workArea)??"";
    var search=NormalizeSearch(term??"").Replace("%","\\%").Replace("_","\\_");
    // Escape wildcards in the user's search term. Names are catalog metadata only, not citizen records.
    var where="(scope_area=@scope OR (scope_area='*' AND @scope<>'*' AND NOT EXISTS(SELECT 1 FROM shared_catalog_items local WHERE local.scope_area=@scope AND local.category_code=shared_catalog_items.category_code AND local.item_code=shared_catalog_items.item_code))) AND (UPPER(category_code) LIKE @term ESCAPE '\\' OR UPPER(item_code) LIKE @term ESCAPE '\\' OR search_label LIKE @term ESCAPE '\\')";
    var match="%"+search+"%";
    var result=new RecordPage<CatalogEntry>{PageIndex=pageIndex,PageSize=pageSize};
    result.TotalCount=Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM shared_catalog_items WHERE "+where,
     P("@scope",scope),P("@term",match)));
    result.Items=new List<CatalogEntry>();
    using(var cmd=new SQLiteCommand("SELECT id,scope_area,category_code,item_code,item_label,details,active,version FROM shared_catalog_items WHERE "+where+" ORDER BY category_code,item_code,scope_area LIMIT @limit OFFSET @offset",c)) {
     cmd.Parameters.AddRange(new[]{P("@scope",scope),P("@term",match),P("@limit",pageSize),P("@offset",pageIndex*pageSize)});
     using(var row=cmd.ExecuteReader())while(row.Read())result.Items.Add(new CatalogEntry {
      Id=row.GetString(0),Scope=row.GetString(1),Category=row.GetString(2),Code=row.GetString(3),
      Label=row.GetString(4),Details=row.GetString(5),Active=row.GetInt32(6)==1,Version=row.GetInt64(7)
     });
    }
    return result;
   }
  }
  public static string SaveCatalog(Session s,CatalogEntry row,bool workArea=false) {
   if(row==null)throw new ArgumentNullException("row");
   string category=CatalogCode(row.Category),code=CatalogCode(row.Code);
   string label=(row.Label??"").Trim(),details=(row.Details??"").Trim();
   if(label.Length<1||label.Length>150||details.Length>500)
    throw new InvalidOperationException("Nhãn danh mục phải từ 1–150 ký tự; mô tả tối đa 500 ký tự.");
   if(s==null)throw new UnauthorizedAccessException("Chưa đăng nhập.");
   string scope=CatalogScope(s,workArea);
   if(String.IsNullOrEmpty(scope))throw new UnauthorizedAccessException("Tài khoản không có địa bàn quản lý.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"I07","manage",s.AreaId);
    string id=row.Id;
    if(!String.IsNullOrWhiteSpace(id)) {
     string existingScope=Convert.ToString(Scalar(c,tx,"SELECT scope_area FROM shared_catalog_items WHERE id=@id",P("@id",id)));
     if(existingScope=="*"&&scope!="*")id=null;
     else if(existingScope!=scope)throw new UnauthorizedAccessException("Danh mục thuộc khu phố khác.");
    }
    if(String.IsNullOrWhiteSpace(id)) {
     id=Id();
     Exec(c,tx,@"INSERT INTO shared_catalog_items
       (id,scope_area,category_code,item_code,item_label,search_label,details,active,version,changed_by,updated_utc)
       VALUES(@id,@scope,@category,@code,@label,@search,@details,@active,1,@actor,@now)",
       P("@id",id),P("@scope",scope),P("@category",category),P("@code",code),
       P("@label",label),P("@search",NormalizeSearch(label)),P("@details",details),P("@active",row.Active?1:0),P("@actor",s.Id),P("@now",Now));
     Audit(c,tx,s.Id,"I07_CATALOG_CREATED","catalog",id);
    } else {
     if(row.Version<1)throw new InvalidOperationException("Thiếu phiên bản danh mục; không ghi đè.");
     using(var cmd=new SQLiteCommand(@"UPDATE shared_catalog_items SET item_label=@label,search_label=@search,details=@details,
         active=@active,version=version+1,changed_by=@actor,updated_utc=@now
         WHERE id=@id AND scope_area=@scope AND category_code=@category AND item_code=@code AND version=@version",c,tx)) {
      cmd.Parameters.AddRange(new[]{P("@label",label),P("@search",NormalizeSearch(label)),P("@details",details),P("@active",row.Active?1:0),
        P("@actor",s.Id),P("@now",Now),P("@id",id),P("@scope",scope),P("@category",category),
        P("@code",code),P("@version",row.Version)});
      if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Danh mục đã thay đổi hoặc thuộc phạm vi khác; tải lại trước khi sửa.");
     }
     Audit(c,tx,s.Id,"I07_CATALOG_UPDATED","catalog",id);
    }
    tx.Commit();return id;
   }
  }
  public static string VersionDiagnostics(Session s) {
   using(var c=Open()) {
    Require(c,null,s,"I09","read",s==null?null:s.AreaId);
    string app=Assembly.GetExecutingAssembly().GetName().Version.ToString();
    string schema=Convert.ToString(Scalar(c,null,"PRAGMA user_version;"),CultureInfo.InvariantCulture);
    string integrity=Convert.ToString(Scalar(c,null,"PRAGMA quick_check;"));
    long bytes=File.Exists(DatabasePath)?new FileInfo(DatabasePath).Length:0;
    return "Phiên bản nguồn: "+app+" (PILOT)\nLược đồ SQLite: "+schema+
      "\nKho: AES-256 / HMAC-SHA512 · Khóa Windows DPAPI"+
      "\nKiểm tra SQLite: "+integrity+"\nDung lượng kho (byte): "+bytes.ToString(CultureInfo.InvariantCulture)+
      "\nCập nhật: chỉ kiểm tra gói. Chưa có cơ chế cài bản cập nhật đã ký số.";
   }
  }
  public static string VerifyPackageSha256(Session s,string file,string expected) {
   using(var c=Open())Require(c,null,s,"I09","read",s==null?null:s.AreaId);
   if(String.IsNullOrWhiteSpace(file)||!File.Exists(file))throw new FileNotFoundException("Không tìm thấy tệp cần kiểm tra.");
   if(new FileInfo(file).Length>200L*1024L*1024L)throw new InvalidDataException("Tệp vượt ngưỡng 200 MB.");
   string ext=Path.GetExtension(file).ToLowerInvariant();
   if(ext!=".zip"&&ext!=".exe")throw new InvalidDataException("Chỉ cho kiểm tra gói ZIP/EXE; không thực thi tệp.");
   using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
   using(var hash=SHA256.Create()) {
    string actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    var control=(expected??"").Trim().ToLowerInvariant();
    if(!Regex.IsMatch(control,"^[a-f0-9]{64}$"))
     return "SHA-256: "+actual+"\nCHƯA XÁC THỰC: phải nhập SHA-256 do nhà phát hành tin cậy cung cấp.";
    return "SHA-256: "+actual+"\n"+(String.Equals(actual,control,StringComparison.Ordinal)?
       "KHỚP mã băm đã nhập; không chứng minh chữ ký/nguồn phát hành, chưa cho cài đặt.":
       "KHÔNG KHỚP mã băm: từ chối sử dụng gói.");
   }
  }
  public static Tuple<string,string> ParseLocalQrReference(Session s,string payload) {
   if(!Can(s,"I05","read"))throw new UnauthorizedAccessException("Không được cấp quyền I05:read.");
   string input=(payload??"").Trim();
   var match=Regex.Match(input,@"^QLKP1\|(B01|B02)\|([0-9a-fA-F]{32})$",RegexOptions.CultureInvariant);
   if(!match.Success)throw new InvalidDataException("QR không thuộc định dạng liên kết hồ sơ đã phê duyệt. Không mở đường dẫn bên ngoài.");
   string feature=match.Groups[1].Value,id=match.Groups[2].Value.ToLowerInvariant();
   if(!Can(s,feature,"read"))throw new UnauthorizedAccessException("Không có quyền đọc đích liên kết QR.");
   if(feature=="B01")GetHousehold(s,id);else GetPerson(s,id);
   return Tuple.Create(feature,id);
  }
 }
}
