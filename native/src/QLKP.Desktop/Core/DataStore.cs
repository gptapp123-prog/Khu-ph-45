using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  internal const int SchemaVersion=16;
  private static readonly string DefaultDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QLKP-V2-PILOT");
  private static string _dataFolder = DefaultDataFolder;
  public static string DataFolder { get { return _dataFolder; } }
  public static string DatabasePath { get { return Path.Combine(_dataFolder, "qlkp-pilot.db"); } }
  // Only --self-test may isolate storage; never clean or overwrite the production AppData directory.
  internal static string BeginIsolatedSelfTest() {
   if(_dataFolder!=DefaultDataFolder)throw new InvalidOperationException("Đã khởi tạo kho tự kiểm tra.");
   string sandbox=Path.Combine(Path.GetTempPath(),"QLKP-SELFTEST-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(sandbox);
   _dataFolder=sandbox;
   return sandbox;
  }
  internal static void EndIsolatedSelfTest(string sandbox) {
   if(String.IsNullOrWhiteSpace(sandbox) || !String.Equals(_dataFolder,sandbox,StringComparison.Ordinal))throw new InvalidOperationException("Đường dẫn tự kiểm tra không hợp lệ.");
   _dataFolder=DefaultDataFolder;
   string name=Path.GetFileName(sandbox);
   if(!name.StartsWith("QLKP-SELFTEST-",StringComparison.Ordinal) || name.Length!=46 || !Directory.Exists(sandbox))return;
   Directory.Delete(sandbox,true);
  }
  static string Now { get { return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); } }
  static string Id() { return Guid.NewGuid().ToString("N"); }
  static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n,v ?? DBNull.Value); }
  public static SQLiteConnection Open() {
   var c = VaultStorage.OpenCurrent();
   using(var cmd = new SQLiteCommand("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;",c)) cmd.ExecuteNonQuery();
   return c;
  }
  static void Exec(SQLiteConnection c, SQLiteTransaction tx, string sql, params SQLiteParameter[] pars) {
   using(var cmd=new SQLiteCommand(sql,c,tx)) { cmd.Parameters.AddRange(pars);cmd.ExecuteNonQuery(); }
  }
  static object Scalar(SQLiteConnection c, SQLiteTransaction tx, string sql, params SQLiteParameter[] pars) {
   using(var cmd=new SQLiteCommand(sql,c,tx)) {cmd.Parameters.AddRange(pars);return cmd.ExecuteScalar();}
  }
  public static void Initialize() {
   Directory.CreateDirectory(DataFolder);
   VaultStorage.EnsureCurrent();
   string schema=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"schema.sql");
   if(!File.Exists(schema)) throw new FileNotFoundException("Thiếu schema.sql; chưa tạo CSDL.",schema);
   string sql=File.ReadAllText(schema,Encoding.UTF8);
   using(var c=Open()) {
    long installed=Convert.ToInt64(Scalar(c,null,"PRAGMA user_version;"));
    if(installed>SchemaVersion)throw new InvalidDataException("Dữ liệu mới hơn phần mềm; không cho phép hạ cấp.");
    // The old v7 module_records CHECK cannot be ALTERed. Rebuild its table transactionally,
    // temporarily disabling FKs only while the table is replaced; verify before commit.
    bool rebuild=installed>0 && installed<11 && Convert.ToInt32(Scalar(c,null,
       "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='module_records'"))==1;
    if(rebuild)Exec(c,null,"PRAGMA foreign_keys=OFF");
    try {
     using(var tx=c.BeginTransaction()) {
      if(rebuild) {
       const string marker="CREATE TABLE IF NOT EXISTS module_records (";
       int begin=sql.IndexOf(marker,StringComparison.Ordinal);
       if(begin<0)throw new InvalidDataException("Không tìm thấy định nghĩa module_records cần nâng cấp.");
       int finish=sql.IndexOf("\n);",begin,StringComparison.Ordinal);
       if(finish<0)throw new InvalidDataException("Không xác định được ranh giới bảng module_records.");
       string definition=sql.Substring(begin,finish+3-begin);
       string copySql=definition.Replace(marker,"CREATE TABLE module_records_rebuild (");
       Exec(c,tx,copySql);
       Exec(c,tx,@"INSERT INTO module_records_rebuild
        (id,area_id,feature,record_code,title,search_title,notes,status,due_date,related_feature,related_id,version,created_by,updated_by,created_utc,updated_utc)
        SELECT id,area_id,feature,record_code,title,search_title,notes,status,due_date,related_feature,related_id,version,created_by,updated_by,created_utc,updated_utc FROM module_records");
       // Triggers on households/people refer to module_records. Drop and recreate
       // them from the verified schema in this same transaction before table replacement.
       foreach(string trigger in new[]{"household_module_target_delete_guard","person_module_target_delete_guard",
         "household_module_target_scope_guard","person_module_target_scope_guard",
         "module_relations_guard_insert","e06_relation_source_guard",
         "f_typed_multi_relation_insert","f06_relation_source_guard","g_typed_multi_insert"})
        Exec(c,tx,"DROP TRIGGER IF EXISTS "+trigger);
       Exec(c,tx,"DROP TABLE module_records");
       Exec(c,tx,"ALTER TABLE module_records_rebuild RENAME TO module_records");
      }
      using(var cmd=new SQLiteCommand(sql,c,tx))cmd.ExecuteNonQuery();
      EnsureSearchColumn(c,tx,"people","search_name");
      EnsureSearchColumn(c,tx,"households","search_address");
      EnsureSearchColumn(c,tx,"residency_changes","search_reason");
      BackfillSearchKeys(c,tx);
      if(installed<15)UpgradeVehicleRights(c,tx);
      Exec(c,tx,"CREATE INDEX IF NOT EXISTS idx_people_search_area ON people(area_id,search_name)");
      Exec(c,tx,"CREATE INDEX IF NOT EXISTS idx_households_search_area ON households(area_id,search_address)");
      Exec(c,tx,"CREATE INDEX IF NOT EXISTS idx_residency_reason_area ON residency_changes(area_id,search_reason)");
      using(var check=new SQLiteCommand("PRAGMA foreign_key_check",c,tx))
       using(var reader=check.ExecuteReader())if(reader.Read())throw new InvalidDataException("Nâng cấp phá vỡ khóa ngoại; đã hủy giao dịch.");
      using(var cmd=new SQLiteCommand("PRAGMA user_version="+SchemaVersion+";",c,tx))cmd.ExecuteNonQuery();
      tx.Commit();
     }
    } finally {if(rebuild)Exec(c,null,"PRAGMA foreign_keys=ON");}
   }
   UpgradeBusinessDefaults();
  }
  // Migration for existing V1–V3 databases. All schema updates and search-key backfill
  // occur inside the same SQLite transaction, so a failure never leaves half a migration.
  static void EnsureSearchColumn(SQLiteConnection c,SQLiteTransaction tx,string table,string column) {
   bool exists=false;
   using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",c,tx))
    using(var reader=cmd.ExecuteReader())while(reader.Read())
     if(String.Equals(reader.GetString(1),column,StringComparison.Ordinal))exists=true;
   if(!exists)Exec(c,tx,"ALTER TABLE "+table+" ADD COLUMN "+column+" TEXT NOT NULL DEFAULT ''");
  }
  public static string NormalizeSearch(string input) {
   if(String.IsNullOrEmpty(input))return "";
   string decomposed=input.Trim().Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD);
   var result=new StringBuilder(decomposed.Length);
   foreach(char ch in decomposed)
    if(CharUnicodeInfo.GetUnicodeCategory(ch)!=UnicodeCategory.NonSpacingMark)
     result.Append(Char.ToLowerInvariant(ch));
   return result.ToString().Normalize(NormalizationForm.FormC);
  }
  static void BackfillSearchKeys(SQLiteConnection c,SQLiteTransaction tx) {
   foreach(string table in new[]{"people","households","residency_changes"}) {
    string source=table=="people"?"full_name":table=="households"?"address":"reason";
    string search=table=="people"?"search_name":table=="households"?"search_address":"search_reason";
    var changed=new List<Tuple<string,string>>();
    using(var cmd=new SQLiteCommand("SELECT id,"+source+","+search+" FROM "+table,c,tx))
     using(var r=cmd.ExecuteReader())while(r.Read()) {
      string expected=NormalizeSearch(r.GetString(1));
      if(!String.Equals(expected,r.GetString(2),StringComparison.Ordinal))
       changed.Add(Tuple.Create(r.GetString(0),expected));
     }
    foreach(var item in changed)Exec(c,tx,"UPDATE "+table+" SET "+search+"=@key WHERE id=@id",P("@key",item.Item2),P("@id",item.Item1));
   }
  }
  public static bool IsFirstRun() { using(var c=Open()) return Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM accounts"))==0; }
  static string Hash(string password, byte[] salt) {
   using(var kdf=new Rfc2898DeriveBytes(password,salt,210000,HashAlgorithmName.SHA256))
    return Convert.ToBase64String(kdf.GetBytes(32));
  }
  static bool EqualBytes(byte[] x, byte[] y) {
   if(x==null||y==null||x.Length!=y.Length)return false;
   int mismatch=0;for(int i=0;i<x.Length;i++)mismatch|=x[i]^y[i];return mismatch==0;
  }
  static Tuple<string,string> NewPassword(string password) {
   if(String.IsNullOrWhiteSpace(password)||password.Length<12) throw new InvalidOperationException("Mật khẩu phải có ít nhất 12 ký tự.");
   byte[] salt=new byte[16];using(var random=RandomNumberGenerator.Create())random.GetBytes(salt);
   return Tuple.Create(Hash(password,salt),Convert.ToBase64String(salt));
  }
  public static void SetupOwner(string username,string password,string areaCode,string areaName) {
   if(String.IsNullOrWhiteSpace(username)||String.IsNullOrWhiteSpace(areaCode)||String.IsNullOrWhiteSpace(areaName))throw new InvalidOperationException("Cần tên đăng nhập, mã khu phố và tên khu phố.");
   if(username.Trim().Length>128||areaCode.Trim().Length>64||areaName.Trim().Length>200)throw new InvalidOperationException("Tên tài khoản tối đa 128 ký tự; mã khu phố 64 ký tự; tên khu phố 200 ký tự.");
   var secret=NewPassword(password);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM accounts"))!=0)throw new InvalidOperationException("ADMIN gốc đã được tạo; không thể khởi tạo lại.");
    string a=Id(),u=Id();
    Exec(c,tx,"INSERT INTO areas(id,code,name,created_utc) VALUES(@i,@c,@n,@t)",P("@i",a),P("@c",areaCode.Trim()),P("@n",areaName.Trim()),P("@t",Now));
    Exec(c,tx,"INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES(@i,@u,@h,@s,'ROOT_ADMIN',NULL,@t)",P("@i",u),P("@u",username.Trim()),P("@h",secret.Item1),P("@s",secret.Item2),P("@t",Now));
    EnsureReferenceRows(c,tx,u,"*");
    Audit(c,tx,u,"FIRST_OWNER_SETUP","account",u);
    tx.Commit();
   }
  }
  // Pilot protection against repeated incorrect passwords. No default credentials.
  public static Session Login(string username,string password) {
   if(String.IsNullOrWhiteSpace(username)||String.IsNullOrEmpty(password)||username.Length>128)return null;
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    string id=null, user=null,hash=null,salt64=null,role=null,area=null;
    long version=0;int active=0;
    using(var cmd=new SQLiteCommand("SELECT id,username,hash_b64,salt_b64,role,area_id,policy_version,active FROM accounts WHERE username=@u COLLATE NOCASE LIMIT 1",c,tx)) {
     cmd.Parameters.Add(P("@u",username.Trim()));
     using(var r=cmd.ExecuteReader())if(r.Read()) {
      id=r.GetString(0);user=r.GetString(1);hash=r.GetString(2);salt64=r.GetString(3);
      role=r.GetString(4);area=r.IsDBNull(5)?null:r.GetString(5);version=r.GetInt64(6);active=r.GetInt32(7);
     }
    }
    if(id==null||active!=1){tx.Commit();return null;}
    string blocked=Convert.ToString(Scalar(c,tx,"SELECT blocked_until_utc FROM login_throttle WHERE account_id=@id",P("@id",id)));
    DateTime blockUntil;
    if(!String.IsNullOrEmpty(blocked) && DateTime.TryParse(blocked,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out blockUntil)) {
     if(blockUntil.ToUniversalTime()>DateTime.UtcNow){tx.Commit();return null;}
     // Expired throttling window: a fresh sequence of attempts may begin.
     Exec(c,tx,"DELETE FROM login_throttle WHERE account_id=@id",P("@id",id));
    }
    bool matched=EqualBytes(Convert.FromBase64String(hash),Convert.FromBase64String(Hash(password,Convert.FromBase64String(salt64))));
    if(!matched) {
     Exec(c,tx,"INSERT OR IGNORE INTO login_throttle(account_id,failed_count,updated_utc) VALUES(@id,0,@t)",P("@id",id),P("@t",Now));
     Exec(c,tx,@"UPDATE login_throttle SET failed_count=failed_count+1,updated_utc=@t,
      blocked_until_utc=CASE WHEN failed_count+1>=5 THEN @until ELSE NULL END WHERE account_id=@id",
      P("@id",id),P("@t",Now),P("@until",DateTime.UtcNow.AddMinutes(15).ToString("o",CultureInfo.InvariantCulture)));
     Audit(c,tx,id,"LOGIN_DENIED","account",id);
     tx.Commit();return null;
    }
    Exec(c,tx,"DELETE FROM login_throttle WHERE account_id=@id",P("@id",id));
    Audit(c,tx,id,"LOGIN_SUCCESS","account",id);
    tx.Commit();
    if(role=="ROOT_ADMIN")area=Convert.ToString(Scalar(c,null,"SELECT id FROM areas ORDER BY created_utc,id LIMIT 1"));
    return new Session {Id=id,Username=user,Role=role,AreaId=area,PolicyVersion=version,ExpiresUtc=DateTime.UtcNow.AddHours(8)};
   }
  }
  public static string AreaName(Session s) {
   if(s.AreaId==null)return "Quản trị hệ thống";
   using(var c=Open())return Convert.ToString(Scalar(c,null,"SELECT name FROM areas WHERE id=@a",P("@a",s.AreaId)));
  }
  static bool HasPermission(SQLiteConnection c,SQLiteTransaction tx,Session s,string feature,string action,string areaId) {
   if(s==null||s.ExpiresUtc<=DateTime.UtcNow)return false;
   using(var identity=new SQLiteCommand("SELECT role,area_id,policy_version FROM accounts WHERE id=@id AND active=1",c,tx)) {
    identity.Parameters.Add(P("@id",s.Id));
    using(var r=identity.ExecuteReader()) {
     if(!r.Read())return false;
     string realRole=r.GetString(0),realArea=r.IsDBNull(1)?null:r.GetString(1);
     if(realRole!=s.Role||(realRole!="ROOT_ADMIN"&&realArea!=s.AreaId)||r.GetInt64(2)!=s.PolicyVersion)return false;
    }
   }
   if(s.Role=="ROOT_ADMIN") {
    if(((feature=="I01"||feature=="I02"||feature=="I04"||feature=="I07")&&action=="manage")||((feature=="I03"||feature=="I07"||feature=="I09")&&action=="read")||(feature=="I09"&&(action=="test"||action=="export")))return true;
    if(String.IsNullOrEmpty(areaId)||areaId!=s.AreaId||Convert.ToInt32(Scalar(c,tx,"SELECT count(*) FROM areas WHERE id=@a",P("@a",areaId)))!=1)return false;
    return (action=="read"&&new[]{"I01","I02","I04"}.Contains(feature))||BusinessDefaultRights().Contains(feature+":"+action);
   }
   if(feature=="I04"&&action=="manage")return false;
   if((feature=="I01"||feature=="I02")&&action=="manage"&&s.Role!="KP_ADMIN")return false;
   if(String.IsNullOrEmpty(areaId)||areaId!=s.AreaId)return false;
   if(action=="read"&&new[]{"I01","I02","I04"}.Contains(feature))return true;
   return Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM permissions WHERE account_id=@u AND feature=@f AND action=@x AND area_id=@a",P("@u",s.Id),P("@f",feature),P("@x",action),P("@a",areaId)))>0;
  }
  static void Require(SQLiteConnection c,SQLiteTransaction tx,Session s,string feature,string action,string areaId) {
   if(!HasPermission(c,tx,s,feature,action,areaId))throw new UnauthorizedAccessException("Tài khoản không có quyền " + feature + ":"+action+" ở phạm vi này.");
  }
  public static void RequireRootMaintenance(Session s) {
   using(var c=Open())Require(c,null,s,"I04","manage",s==null?null:s.AreaId);
  }
  public static void LogMaintenance(Session s,string eventCode) {
   if(eventCode!="ENCRYPTED_BACKUP_CREATED" && eventCode!="RESTORE_REQUESTED")
    throw new InvalidOperationException("Mã nhật ký bảo trì không hợp lệ.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"I04","manage",s.AreaId);
    Audit(c,tx,s.Id,eventCode,"database", "managed-database");tx.Commit();
   }
  }
  // I06: dual authorization on every export; no generic data-read shortcut for administrators.
  internal static void RequireForOfflineTransfer(SQLiteConnection c,Session actor) {
   Require(c,null,actor,"I06","export",actor==null?null:actor.AreaId);
   Require(c,null,actor,"I07","read",actor==null?null:actor.AreaId);
  }
  internal static void AuditOfflineCatalogTransfer(SQLiteConnection c,string actorId) {
   using(var tx=c.BeginTransaction()) {
    Audit(c,tx,actorId,"I06_OFFLINE_CATALOG_EXPORTED","catalog","nonpersonal-catalog");
    tx.Commit();
   }
  }
  public static bool Can(Session s,string feature,string action) {
   using(var c=Open())return HasPermission(c,null,s,feature,action,s==null?null:s.AreaId);
  }
  static void Audit(SQLiteConnection c,SQLiteTransaction tx,string actor,string ev,string kind,string entityId) {
   Exec(c,tx,"INSERT INTO audit_log(id,actor_id,event_code,entity_type,entity_id,utc,result) VALUES(@i,@u,@e,@k,@n,@t,'OK')",P("@i",Id()),P("@u",actor),P("@e",ev),P("@k",kind),P("@n",entityId),P("@t",Now));
  }
  static void InsertAccount(SQLiteConnection c,SQLiteTransaction tx,string username,string password,string role,string areaId,string actorId,out string userId) {
   if(String.IsNullOrWhiteSpace(username))throw new InvalidOperationException("Chưa nhập tên tài khoản.");
   var secret=NewPassword(password);userId=Id();
   Exec(c,tx,"INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES(@i,@u,@h,@s,@r,@a,@t)",P("@i",userId),P("@u",username.Trim()),P("@h",secret.Item1),P("@s",secret.Item2),P("@r",role),P("@a",areaId),P("@t",Now));
   Audit(c,tx,actorId,"CREATE_ACCOUNT","account",userId);
  }
  static void Grant(SQLiteConnection c,SQLiteTransaction tx,string target,string feature,string action,string areaId,string actorId) {
   Exec(c,tx,"INSERT OR IGNORE INTO permissions(account_id,feature,action,area_id,granted_by) VALUES(@u,@f,@x,@a,@g)",P("@u",target),P("@f",feature),P("@x",action),P("@a",areaId),P("@g",actorId));
  }
  public static void CreateAccount(Session actor,string username,string password,IList<string> requestedRights) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"I01","manage",actor.AreaId);
    string target, area;
    if(actor.Role=="ROOT_ADMIN") {
     area=actor.AreaId;
     if(String.IsNullOrEmpty(area))throw new InvalidOperationException("Chưa có khu phố.");
     InsertAccount(c,tx,username,password,"KP_ADMIN",area,actor.Id,out target);
     GrantBusinessDefaults(c,tx,target,area,actor.Id,true);
     Grant(c,tx,target,"I01","manage",area,actor.Id);
     Grant(c,tx,target,"I02","manage",area,actor.Id);
    } else if(actor.Role=="KP_ADMIN") {
     area=actor.AreaId;
     var valid = new HashSet<string>(new[]{"B01:read","B01:create","B02:read","B02:create","B02:update","B03:read","B03:create","I08:read"});
     var unique=new HashSet<string>(requestedRights==null||requestedRights.Count==0?BusinessDefaultRights():requestedRights);
     valid.UnionWith(ModuleCatalog.AdditionalRights());
     valid.UnionWith(PersonIdentityRights);
     foreach(var perm in unique) {
      if(!valid.Contains(perm))throw new UnauthorizedAccessException("Quyền không hợp lệ: "+perm);
      string[] pair=perm.Split(':');
      if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM delegation_ceiling WHERE admin_id=@u AND feature=@f AND action=@x AND area_id=@a",P("@u",actor.Id),P("@f",pair[0]),P("@x",pair[1]),P("@a",area)))!=1 ||
       !HasPermission(c,tx,actor,pair[0],pair[1],area))
       throw new UnauthorizedAccessException("Quyền vượt giới hạn ủy quyền hoặc quyền hiện có.");
     }
     InsertAccount(c,tx,username,password,"ASSIGNED_USER",area,actor.Id,out target);
     foreach(var perm in unique) {var pair=perm.Split(':');Grant(c,tx,target,pair[0],pair[1],area,actor.Id);}
    } else throw new UnauthorizedAccessException("Tài khoản cấp dưới không được tạo tài khoản.");
    tx.Commit();
   }
  }
  // Scope check applies equally to lookups and writes: no self-grant, no cross-area editing.
  static string CheckTarget(SQLiteConnection c,SQLiteTransaction tx,Session actor,string targetId) {
   Require(c,tx,actor,"I02","manage",actor.AreaId);
   if(String.IsNullOrWhiteSpace(targetId)||actor.Id==targetId)throw new UnauthorizedAccessException("Không được tự sửa quyền hoặc hồ sơ ngoài phạm vi.");
   using(var cmd=new SQLiteCommand("SELECT role,area_id FROM accounts WHERE id=@id",c,tx)) {
    cmd.Parameters.Add(P("@id",targetId));
    using(var r=cmd.ExecuteReader()) {
     if(!r.Read())throw new UnauthorizedAccessException("Tài khoản không tồn tại.");
     string role=r.GetString(0),area=r.IsDBNull(1)?null:r.GetString(1);
     if(actor.Role=="ROOT_ADMIN" && role=="KP_ADMIN")return area;
     if(actor.Role=="KP_ADMIN" && role=="ASSIGNED_USER" && area==actor.AreaId)return area;
     throw new UnauthorizedAccessException("Không được quản trị tài khoản khác tầng hoặc địa bàn.");
    }
   }
  }
  static HashSet<string> NormalizeRights(IEnumerable<string> rights) {
   var allowed=new HashSet<string>(StringComparer.Ordinal){"B01:read","B01:create","B02:read","B02:create","B02:update","B03:read","B03:create","I08:read"};
   var result=new HashSet<string>(rights??Enumerable.Empty<string>(),StringComparer.Ordinal);
   allowed.UnionWith(ModuleCatalog.AdditionalRights());
   allowed.UnionWith(PersonIdentityRights);
   if(result.Any(x=>!allowed.Contains(x)))throw new UnauthorizedAccessException("Danh sách quyền ngoài danh mục đã duyệt.");
   return result;
  }
  public static List<string> GetAccountRights(Session actor,string targetId) {
   using(var c=Open()) {
    CheckTarget(c,null,actor,targetId);
    var rights=new List<string>();
    using(var cmd=new SQLiteCommand("SELECT feature,action FROM permissions WHERE account_id=@id AND feature IN ('B01','B02','B03','I08','I03','A01','A02','A03','A04','A05','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','F08','G01','G02','G03','G04','H01','H02','H03','H04','H05','I05','I06','I07','I09') ORDER BY feature,action",c)) {
     cmd.Parameters.Add(P("@id",targetId));
     using(var r=cmd.ExecuteReader())while(r.Read())rights.Add(r.GetString(0)+":"+r.GetString(1));
    }
    return rights;
   }
  }
  public static void SetAccountRights(Session actor,string targetId,IEnumerable<string> rights) {
   var wanted=NormalizeRights(rights);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    string area=CheckTarget(c,tx,actor,targetId);
    if(actor.Role=="KP_ADMIN") {
     foreach(string right in wanted) {
      var pair=right.Split(':');
      if(Convert.ToInt32(Scalar(c,tx,@"SELECT COUNT(*) FROM delegation_ceiling
        WHERE admin_id=@id AND area_id=@area AND feature=@f AND action=@act",P("@id",actor.Id),P("@area",area),P("@f",pair[0]),P("@act",pair[1])))!=1 ||
       !HasPermission(c,tx,actor,pair[0],pair[1],area))
       throw new UnauthorizedAccessException("Quyền vượt phạm vi ủy quyền hoặc quyền hiện có của ADMIN.");
     }
    }
    // Only editable B01/B02 rights are replaced. Management rights are kept separate.
    Exec(c,tx,"DELETE FROM permissions WHERE account_id=@id AND feature IN ('B01','B02','B03','I08','I03','A01','A02','A03','A04','A05','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','F08','G01','G02','G03','G04','H01','H02','H03','H04','H05','I05','I06','I07','I09')",P("@id",targetId));
    if(actor.Role=="ROOT_ADMIN")
     Exec(c,tx,"DELETE FROM delegation_ceiling WHERE admin_id=@id AND feature IN ('B01','B02','B03','I08','I03','A01','A02','A03','A04','A05','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','F08','G01','G02','G03','G04','H01','H02','H03','H04','H05','I05','I06','I07','I09')",P("@id",targetId));
    foreach(string right in wanted) {
     var pair=right.Split(':');
     Grant(c,tx,targetId,pair[0],pair[1],area,actor.Id);
     if(actor.Role=="ROOT_ADMIN")Exec(c,tx,
      "INSERT INTO delegation_ceiling(admin_id,feature,action,area_id) VALUES(@id,@f,@act,@area)",
      P("@id",targetId),P("@f",pair[0]),P("@act",pair[1]),P("@area",area));
    }
    if(actor.Role=="ROOT_ADMIN") {
     // Lowering a delegated ceiling revokes downstream grants that no longer fit it.
     var affected=new List<string>();
     using(var cmd=new SQLiteCommand(@"SELECT DISTINCT p.account_id FROM permissions p
       WHERE p.granted_by=@admin AND p.feature IN ('B01','B02','B03','I08','I03','A01','A02','A03','A04','A05','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','F08','G01','G02','G03','G04','H01','H02','H03','H04','H05','I05','I06','I07','I09')
       AND NOT EXISTS (SELECT 1 FROM delegation_ceiling d WHERE d.admin_id=@admin
         AND d.area_id=p.area_id AND d.feature=p.feature AND d.action=p.action)",c,tx)) {
      cmd.Parameters.Add(P("@admin",targetId));
      using(var r=cmd.ExecuteReader())while(r.Read())affected.Add(r.GetString(0));
     }
     Exec(c,tx,@"DELETE FROM permissions WHERE granted_by=@admin AND feature IN ('B01','B02','B03','I08','I03','A01','A02','A03','A04','A05','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','F08','G01','G02','G03','G04','H01','H02','H03','H04','H05','I05','I06','I07','I09')
       AND NOT EXISTS (SELECT 1 FROM delegation_ceiling d WHERE d.admin_id=@admin
         AND d.area_id=permissions.area_id AND d.feature=permissions.feature AND d.action=permissions.action)",P("@admin",targetId));
     foreach(var userId in affected)
      Exec(c,tx,"UPDATE accounts SET policy_version=policy_version+1 WHERE id=@id",P("@id",userId));
    }
    Exec(c,tx,"UPDATE accounts SET policy_version=policy_version+1 WHERE id=@id",P("@id",targetId));
    Audit(c,tx,actor.Id,"CHANGE_ACCOUNT_RIGHTS","account",targetId);
    tx.Commit();
   }
  }
  public static void SetAccountStatus(Session actor,string targetId,bool enabled) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    CheckTarget(c,tx,actor,targetId);
    if(Convert.ToString(Scalar(c,tx,"SELECT role FROM accounts WHERE id=@id",P("@id",targetId)))!="ASSIGNED_USER")throw new UnauthorizedAccessException("Tài khoản quản trị được bảo vệ; không khóa hoặc thay thế bằng thao tác thông thường.");
    Exec(c,tx,"UPDATE accounts SET active=@active,policy_version=policy_version+1 WHERE id=@id",
      P("@active",enabled?1:0),P("@id",targetId));
    Audit(c,tx,actor.Id,enabled?"ENABLE_ACCOUNT":"DISABLE_ACCOUNT","account",targetId);
    tx.Commit();
   }
  }
  public static List<AuditRow> ListAudit(Session actor) {
   using(var c=Open()) {
    Require(c,null,actor,"I03","read",actor.AreaId);
    const string sql=@"SELECT l.utc, a.username,l.event_code,l.entity_type,l.result FROM audit_log l
      JOIN accounts a ON a.id=l.actor_id
      WHERE (@all=1 OR a.area_id=@area) ORDER BY l.utc DESC,l.id DESC LIMIT 200";
    using(var cmd=new SQLiteCommand(sql,c)) {
     cmd.Parameters.Add(P("@all",actor.Role=="ROOT_ADMIN"?1:0));
     cmd.Parameters.Add(P("@area",actor.AreaId));
     var rows=new List<AuditRow>();
     using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(new AuditRow{
       Timestamp=r.GetString(0),Actor=r.GetString(1),EventCode=r.GetString(2),EntityType=r.GetString(3),Result=r.GetString(4)});
     return rows;
    }
   }
  }
  public static List<AccountRow> ListAccounts(Session actor) {
   using(var c=Open()) {
    if(!HasPermission(c,null,actor,"I01","manage",actor==null?null:actor.AreaId))
     Require(c,null,actor,"I01","read",actor==null?null:actor.AreaId);
    string sql=actor.Role=="ROOT_ADMIN"?"SELECT id,username,role,COALESCE(area_id,''),active FROM accounts ORDER BY role,username":"SELECT id,username,role,COALESCE(area_id,''),active FROM accounts WHERE role='ROOT_ADMIN' OR area_id=@a ORDER BY role,username";
    using(var cmd=new SQLiteCommand(sql,c)) {
     if(actor.Role!="ROOT_ADMIN")cmd.Parameters.Add(P("@a",actor.AreaId));
     var list=new List<AccountRow>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new AccountRow{Id=r.GetString(0),Username=r.GetString(1),Role=r.GetString(2),AreaId=r.GetString(3),Status=r.GetInt32(4)==1?"Hoạt động":"Bị khóa"});
     return list;
    }
   }
  }
  static string SearchPattern(string term) {
   string q=(term??"").Trim();
   if(q.Length>120)throw new InvalidOperationException("Từ khóa quá dài.");
   return "%"+q.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
  }
  public static List<HouseholdRow> FindHouseholds(Session s,string term) {
   using(var c=Open()) {
    Require(c,null,s,"B01","read",s.AreaId);
    const string sql=@"SELECT h.id,h.code,h.address,h.status,h.area_id,h.version,
      COALESCE((SELECT p.full_name FROM household_memberships hm JOIN people p ON p.id=hm.person_id WHERE hm.household_id=h.id AND hm.relationship='Chủ hộ' AND hm.end_utc IS NULL LIMIT 1),'Chưa có'),
      (SELECT COUNT(*) FROM household_memberships hm WHERE hm.household_id=h.id AND hm.end_utc IS NULL)
      FROM households h WHERE h.area_id=@a AND
      (h.code LIKE @q ESCAPE '\' OR h.search_address LIKE @q ESCAPE '\' OR EXISTS(SELECT 1 FROM household_memberships hm JOIN people p ON p.id=hm.person_id WHERE hm.household_id=h.id AND hm.end_utc IS NULL AND hm.relationship='Chủ hộ' AND p.area_id=h.area_id AND p.search_name LIKE @q ESCAPE '\'))
      ORDER BY h.code LIMIT 200";
    using(var cmd=new SQLiteCommand(sql,c)){
     cmd.Parameters.Add(P("@a",s.AreaId));cmd.Parameters.Add(P("@q",SearchPattern(NormalizeSearch(term))));
     var list=new List<HouseholdRow>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new HouseholdRow{Id=r.GetString(0),Code=r.GetString(1),Address=r.GetString(2),Status=r.GetString(3),AreaId=r.GetString(4),Version=r.GetInt64(5),Head=r.GetString(6),MemberCount=Convert.ToInt32(r.GetValue(7))});
     return list;
    }
   }
  }
  public static HouseholdRow GetHousehold(Session s,string householdId) {
   using(var c=Open()) {
    Require(c,null,s,"B01","read",s.AreaId);
    const string sql=@"SELECT h.id,h.code,h.address,h.status,h.area_id,h.version,
       COALESCE((SELECT p.full_name FROM household_memberships m JOIN people p ON m.person_id=p.id WHERE m.household_id=h.id AND m.relationship='Chủ hộ' AND m.end_utc IS NULL LIMIT 1),'Chưa có'),
       (SELECT COUNT(*) FROM household_memberships m WHERE m.household_id=h.id AND m.end_utc IS NULL)
       FROM households h WHERE h.id=@id AND h.area_id=@area";
    using(var cmd=new SQLiteCommand(sql,c)) {
     cmd.Parameters.Add(P("@id",householdId));cmd.Parameters.Add(P("@area",s.AreaId));
     using(var r=cmd.ExecuteReader()) {
      if(!r.Read())throw new UnauthorizedAccessException("Không thể mở hộ ngoài phạm vi được cấp.");
      return new HouseholdRow{Id=r.GetString(0),Code=r.GetString(1),Address=r.GetString(2),Status=r.GetString(3),AreaId=r.GetString(4),Version=r.GetInt64(5),Head=r.GetString(6),MemberCount=Convert.ToInt32(r.GetValue(7))};
     }
    }
   }
  }
  public static PersonRow GetPerson(Session s,string personId) {
   using(var c=Open()) {
    Require(c,null,s,"B02","read",s.AreaId);
    using(var cmd=new SQLiteCommand(@"SELECT p.id,p.full_name,COALESCE(p.gender,''),COALESCE(p.date_of_birth,''),COALESCE(m.relationship,''),COALESCE(h.code,''),COALESCE(h.id,''),p.version
       FROM people p LEFT JOIN household_memberships m ON m.person_id=p.id AND m.end_utc IS NULL
       LEFT JOIN households h ON h.id=m.household_id AND h.area_id=p.area_id
       WHERE p.id=@id AND p.area_id=@area",c)) {
     cmd.Parameters.Add(P("@id",personId));cmd.Parameters.Add(P("@area",s.AreaId));
     using(var r=cmd.ExecuteReader()) {
      if(!r.Read())throw new UnauthorizedAccessException("Không thể mở nhân khẩu ngoài phạm vi được cấp.");
      return new PersonRow{Id=r.GetString(0),FullName=r.GetString(1),Gender=r.GetString(2),BirthDate=r.GetString(3),Relationship=r.GetString(4),HouseholdCode=r.GetString(5),HouseholdId=r.GetString(6),Version=r.GetInt64(7)};
     }
    }
   }
  }
  public static List<PersonRow> FindPeople(Session s,string term) {
   using(var c=Open()){
    Require(c,null,s,"B02","read",s.AreaId);
    const string sql=@"SELECT p.id,p.full_name,COALESCE(p.gender,''),COALESCE(p.date_of_birth,''),
      COALESCE(m.relationship,''),COALESCE(h.code,''),COALESCE(h.id,'') FROM people p
      LEFT JOIN household_memberships m ON m.person_id=p.id AND m.end_utc IS NULL
      LEFT JOIN households h ON h.id=m.household_id AND h.area_id=p.area_id
      WHERE p.area_id=@a AND (p.search_name LIKE @q ESCAPE '\' OR h.code LIKE @q ESCAPE '\')
      ORDER BY p.full_name,p.id LIMIT 200";
    using(var cmd=new SQLiteCommand(sql,c)){
     cmd.Parameters.Add(P("@a",s.AreaId));cmd.Parameters.Add(P("@q",SearchPattern(NormalizeSearch(term))));
     var list=new List<PersonRow>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new PersonRow{Id=r.GetString(0),FullName=r.GetString(1),Gender=r.GetString(2),BirthDate=r.GetString(3),Relationship=r.GetString(4),HouseholdCode=r.GetString(5),HouseholdId=r.GetString(6)});
     return list;
    }
   }
  }
  public static List<PersonRow> HouseholdMembers(Session s,string householdId) {
   using(var c=Open()){
    Require(c,null,s,"B01","read",s==null?null:s.AreaId);
    Require(c,null,s,"B02","read",s==null?null:s.AreaId);
    if(Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM households WHERE id=@h AND area_id=@a",P("@h",householdId),P("@a",s.AreaId)))!=1)throw new UnauthorizedAccessException("Hồ sơ hộ không tồn tại trong phạm vi truy cập.");
    using(var cmd=new SQLiteCommand(@"SELECT p.id,p.full_name,COALESCE(p.gender,''),COALESCE(p.date_of_birth,''),m.relationship FROM people p JOIN household_memberships m ON m.person_id=p.id AND m.end_utc IS NULL JOIN households h ON h.id=m.household_id WHERE h.id=@h AND h.area_id=@a AND p.area_id=h.area_id ORDER BY CASE WHEN m.relationship='Chủ hộ' THEN 0 ELSE 1 END,p.full_name",c)){
     cmd.Parameters.Add(P("@h",householdId));cmd.Parameters.Add(P("@a",s.AreaId));
     var list=new List<PersonRow>();using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new PersonRow{Id=r.GetString(0),FullName=r.GetString(1),Gender=r.GetString(2),BirthDate=r.GetString(3),Relationship=r.GetString(4)});
     return list;
    }
   }
  }
  // V0.9: server-side paging for B01/B02. Never infer access from a disabled WPF control.
  static void ValidatePage(int index,int size) {
   if(index<0 || size<10 || size>100)
    throw new ArgumentOutOfRangeException("page", "Trang không hợp lệ; kích thước 10-100 dòng.");
  }
  const string HouseholdFilter=@"h.area_id=@a AND
   (h.code LIKE @q ESCAPE '\' OR h.search_address LIKE @q ESCAPE '\' OR EXISTS
    (SELECT 1 FROM household_memberships m JOIN people p ON p.id=m.person_id
     WHERE m.household_id=h.id AND m.end_utc IS NULL AND m.relationship='Chủ hộ'
      AND p.area_id=h.area_id AND p.search_name LIKE @q ESCAPE '\'))";
  const string PersonFilter=@"p.area_id=@a AND
    (p.search_name LIKE @q ESCAPE '\' OR h.code LIKE @q ESCAPE '\')";
  // Count and page are inside one read transaction, preventing mismatched totals.
  public static RecordPage<HouseholdRow> GetHouseholdsPage(Session s,string term,int pageIndex,int pageSize=50,string category="") {
   ValidatePage(pageIndex,pageSize);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"B01","read",s==null?null:s.AreaId);
    string scopedFilter=HouseholdFilter+" AND (@category='' OR EXISTS (SELECT 1 FROM household_profiles hp WHERE hp.household_id=h.id AND hp.housing_type=@category))";
    string countSql="SELECT COUNT(*) FROM households h WHERE "+scopedFilter;
    string q=SearchPattern(NormalizeSearch(term));
    long total=Convert.ToInt64(Scalar(c,tx,countSql,P("@a",s.AreaId),P("@q",q),P("@category",category??"")));
    long pages=total==0?0:((total-1)/pageSize)+1;
    int index=(int)Math.Min(pageIndex,Math.Max(0,pages-1));
    var page=new RecordPage<HouseholdRow>{PageIndex=index,PageSize=pageSize,TotalCount=total};
    const string cols=@"SELECT h.id,h.code,h.address,h.status,h.area_id,h.version,
     COALESCE((SELECT p.full_name FROM household_memberships m JOIN people p ON p.id=m.person_id
      WHERE m.household_id=h.id AND m.relationship='Chủ hộ' AND m.end_utc IS NULL AND p.area_id=h.area_id LIMIT 1),'Chưa có'),
     (SELECT COUNT(*) FROM household_memberships m WHERE m.household_id=h.id AND m.end_utc IS NULL),
     COALESCE((SELECT hp.housing_type FROM household_profiles hp WHERE hp.household_id=h.id),''),
     COALESCE((SELECT hp.review_status FROM household_profiles hp WHERE hp.household_id=h.id),'PENDING_REVIEW')
     FROM households h WHERE ";
    using(var cmd=new SQLiteCommand(cols+scopedFilter+" ORDER BY h.code,h.id LIMIT @size OFFSET @offset",c,tx)) {
     cmd.Parameters.Add(P("@a",s.AreaId));cmd.Parameters.Add(P("@q",q));cmd.Parameters.Add(P("@category",category??""));
     cmd.Parameters.Add(P("@size",pageSize));cmd.Parameters.Add(P("@offset",(long)index*pageSize));
     using(var r=cmd.ExecuteReader())while(r.Read())page.Items.Add(new HouseholdRow{
      Id=r.GetString(0),Code=r.GetString(1),Address=r.GetString(2),Status=r.GetString(3),
      AreaId=r.GetString(4),Version=r.GetInt64(5),Head=r.GetString(6),MemberCount=Convert.ToInt32(r.GetValue(7)),HousingTypeCode=r.GetString(8),ReviewStatusCode=r.GetString(9)
     });
    }
    tx.Commit();return page;
   }
  }
  public static RecordPage<PersonRow> GetPeoplePage(Session s,string term,int pageIndex,int pageSize=50,string category="") {
   ValidatePage(pageIndex,pageSize);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"B02","read",s==null?null:s.AreaId);
    string from=@" FROM people p LEFT JOIN household_memberships m ON m.person_id=p.id AND m.end_utc IS NULL
     LEFT JOIN households h ON h.id=m.household_id AND h.area_id=p.area_id WHERE ";
    string q=SearchPattern(NormalizeSearch(term));
    string scopedFilter=PersonFilter+" AND (@category='' OR EXISTS (SELECT 1 FROM person_profiles pp WHERE pp.person_id=p.id AND pp.residency_group=@category))";
    long total=Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*)"+from+scopedFilter,P("@a",s.AreaId),P("@q",q),P("@category",category??"")));
    long pages=total==0?0:((total-1)/pageSize)+1;
    int index=(int)Math.Min(pageIndex,Math.Max(0,pages-1));
    var page=new RecordPage<PersonRow>{PageIndex=index,PageSize=pageSize,TotalCount=total};
    const string cols=@"SELECT p.id,p.full_name,COALESCE(p.gender,''),COALESCE(p.date_of_birth,''),
     COALESCE(m.relationship,''),COALESCE(h.code,''),COALESCE(h.id,''),p.version,
     COALESCE((SELECT pp.residency_group FROM person_profiles pp WHERE pp.person_id=p.id),''),
     COALESCE((SELECT pp.review_status FROM person_profiles pp WHERE pp.person_id=p.id),'PENDING_REVIEW')";
    using(var cmd=new SQLiteCommand(cols+from+scopedFilter+" ORDER BY p.full_name,p.id LIMIT @size OFFSET @offset",c,tx)) {
     cmd.Parameters.Add(P("@a",s.AreaId));cmd.Parameters.Add(P("@q",q));cmd.Parameters.Add(P("@category",category??""));
     cmd.Parameters.Add(P("@size",pageSize));cmd.Parameters.Add(P("@offset",(long)index*pageSize));
     using(var r=cmd.ExecuteReader())while(r.Read())page.Items.Add(new PersonRow{
      Id=r.GetString(0),FullName=r.GetString(1),Gender=r.GetString(2),BirthDate=r.GetString(3),
      Relationship=r.GetString(4),HouseholdCode=r.GetString(5),HouseholdId=r.GetString(6),Version=r.GetInt64(7),ResidencyGroupCode=r.GetString(8),ReviewStatusCode=r.GetString(9)
     });
    }
    tx.Commit();return page;
   }
  }
  // Deep-link location by exact ID, independent of any other person's identical name.
  public static int GetHouseholdPageIndexById(Session s,string id,int pageSize=50) {
   ValidatePage(0,pageSize);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"B01","read",s==null?null:s.AreaId);
    string code=Convert.ToString(Scalar(c,tx,"SELECT code FROM households WHERE id=@id AND area_id=@a",
     P("@id",id),P("@a",s.AreaId)));
    if(code==null)throw new UnauthorizedAccessException("Không tìm thấy hồ sơ hộ theo ID/phạm vi.");
    long before=Convert.ToInt64(Scalar(c,tx,@"SELECT COUNT(*) FROM households
     WHERE area_id=@a AND (code<@code OR (code=@code AND id<@id))",
     P("@a",s.AreaId),P("@code",code),P("@id",id)));
    tx.Commit();return (int)(before/pageSize);
   }
  }
  public static int GetPersonPageIndexById(Session s,string id,int pageSize=50) {
   ValidatePage(0,pageSize);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"B02","read",s==null?null:s.AreaId);
    string name=Convert.ToString(Scalar(c,tx,"SELECT full_name FROM people WHERE id=@id AND area_id=@a",
     P("@id",id),P("@a",s.AreaId)));
    if(name==null)throw new UnauthorizedAccessException("Không tìm thấy nhân khẩu theo ID/phạm vi.");
    long before=Convert.ToInt64(Scalar(c,tx,@"SELECT COUNT(*) FROM people
     WHERE area_id=@a AND (full_name<@name OR (full_name=@name AND id<@id))",
     P("@a",s.AreaId),P("@name",name),P("@id",id)));
    tx.Commit();return (int)(before/pageSize);
   }
  }
  static void CreateHouseholdInTransaction(SQLiteConnection c,SQLiteTransaction tx,Session s,string code,string address,string owner,string birth="",string gender="") {
   if(String.IsNullOrWhiteSpace(code)||String.IsNullOrWhiteSpace(address)||String.IsNullOrWhiteSpace(owner))throw new InvalidOperationException("Thiếu mã hộ, địa chỉ hoặc họ tên chủ hộ.");
   if(code.Trim().Length>40 || address.Trim().Length>300 || owner.Trim().Length>200)throw new InvalidOperationException("Mã hộ, địa chỉ hoặc họ tên vượt giới hạn thử nghiệm.");
   string household=Id(),person=Id(),membership=Id(),now=Now;
   Exec(c,tx,"INSERT INTO households(id,area_id,code,address,search_address,created_utc,updated_utc) VALUES(@i,@a,@c,@d,@search,@t,@t)",P("@i",household),P("@a",s.AreaId),P("@c",code.Trim()),P("@d",address.Trim()),P("@search",NormalizeSearch(address)),P("@t",now));
   Exec(c,tx,"INSERT INTO people(id,area_id,full_name,search_name,date_of_birth,gender,created_utc,updated_utc) VALUES(@i,@a,@n,@search,@dob,@gender,@t,@t)",P("@i",person),P("@a",s.AreaId),P("@n",owner.Trim()),P("@search",NormalizeSearch(owner)),P("@dob",birth.Length==0?null:birth),P("@gender",gender.Length==0?null:gender),P("@t",now));
   Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@i,@h,@p,'Chủ hộ',@t,@t)",P("@i",membership),P("@h",household),P("@p",person),P("@t",now));
   Audit(c,tx,s.Id,"CREATE_HOUSEHOLD","household",household);
  }
  public static void AddHousehold(Session s,string code,string address,string owner) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,"B01","create",s.AreaId);
    CreateHouseholdInTransaction(c,tx,s,code,address,owner);tx.Commit();
   }
  }
  public static void AddMember(Session s,string householdId,string name,string relationship,string dob,string gender) {
   var draft=new MemberDraft{Name=(name??"").Trim(),Relationship=(relationship??"").Trim(),DateOfBirth=(dob??"").Trim(),Gender=(gender??"").Trim()};
   ValidateMemberDraft(draft,1);
   using(var c=Open())using(var tx=c.BeginTransaction()){
    Require(c,tx,s,"B02","create",s.AreaId);Require(c,tx,s,"B01","read",s.AreaId);
    if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM households WHERE id=@i AND area_id=@a",P("@i",householdId),P("@a",s.AreaId)))!=1)throw new UnauthorizedAccessException("Không tìm thấy hộ hợp lệ trong địa bàn.");
    if(relationship=="Chủ hộ" && Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM household_memberships WHERE household_id=@h AND relationship='Chủ hộ' AND end_utc IS NULL",P("@h",householdId)))>0)throw new InvalidOperationException("Hộ đã có chủ hộ. Chuyển chủ hộ cần quy trình biến động riêng.");
    string person=Id(),mid=Id(),now=Now;
    Exec(c,tx,"INSERT INTO people(id,area_id,full_name,search_name,date_of_birth,gender,created_utc,updated_utc) VALUES(@i,@a,@n,@search,@b,@g,@t,@t)",P("@i",person),P("@a",s.AreaId),P("@n",draft.Name),P("@search",NormalizeSearch(draft.Name)),P("@b",draft.DateOfBirth.Length==0?null:draft.DateOfBirth),P("@g",draft.Gender.Length==0?null:draft.Gender),P("@t",now));
    Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@i,@h,@p,@r,@t,@t)",P("@i",mid),P("@h",householdId),P("@p",person),P("@r",draft.Relationship),P("@t",now));
    Audit(c,tx,s.Id,"ADD_MEMBER","person",person);tx.Commit();
   }
  }
  // B02: shared quick-entry service. Draft values never link records by name.
  // TSV / simple semicolon grid, exactly 4 columns. Not a quoted CSV parser.
  sealed class MemberDraft {
   public string Name, DateOfBirth, Gender, Relationship;
  }
  static void ValidateMemberDraft(MemberDraft d,int line) {
   if(String.IsNullOrWhiteSpace(d.Name)||String.IsNullOrWhiteSpace(d.Relationship))
    throw new InvalidOperationException("Dòng "+line+": thiếu họ tên hoặc quan hệ.");
   if(d.Name.Length>200||d.Relationship.Length>60||d.Gender.Length>30)
    throw new InvalidOperationException("Dòng "+line+": dữ liệu vượt độ dài cho phép.");
   if(d.Name.Any(Char.IsControl)||d.Relationship.Any(Char.IsControl)||d.Gender.Any(Char.IsControl))
    throw new InvalidOperationException("Dòng "+line+": có ký tự điều khiển không hợp lệ.");
   if(d.Gender.Length>0 && d.Gender!="Nam" && d.Gender!="Nữ" && d.Gender!="Khác" && d.Gender!="Không xác định")
    throw new InvalidOperationException("Dòng "+line+": giới tính không thuộc danh mục.");
   if(d.DateOfBirth.Length>0) {
    DateTime birth;
    if(!DateTime.TryParseExact(d.DateOfBirth,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out birth)
     || birth.Date>DateTime.Today)
     throw new InvalidOperationException("Dòng "+line+": ngày sinh phải là yyyy-MM-dd và không được ở tương lai.");
   }
  }
  static List<MemberDraft> ParseMemberBatch(string input) {
   if(String.IsNullOrWhiteSpace(input))throw new InvalidOperationException("Chưa có dữ liệu nhân khẩu.");
   if(input.Length>256*1024)throw new InvalidOperationException("Lô vượt giới hạn ký tự thử nghiệm.");
   var result=new List<MemberDraft>();int lineNo=0;
   foreach(var raw in input.Split(new[]{"\r\n","\n"},StringSplitOptions.None)) {
    lineNo++;if(String.IsNullOrWhiteSpace(raw))continue;
    string[] col=raw.Contains("\t")?raw.Split('\t'):raw.Split(';');
    if(col.Length!=4)throw new InvalidOperationException("Dòng "+lineNo+": cần 4 cột Họ tên / Ngày sinh / Giới tính / Quan hệ.");
    for(int j=0;j<col.Length;j++)col[j]=col[j].Trim();
    if(result.Count==0 && col[0].Equals("Họ tên",StringComparison.OrdinalIgnoreCase)
       && col[3].Equals("Quan hệ",StringComparison.OrdinalIgnoreCase))continue;
    var draft=new MemberDraft {Name=col[0],DateOfBirth=col[1],Gender=col[2],Relationship=col[3]};
    ValidateMemberDraft(draft,lineNo);result.Add(draft);
    if(result.Count>1000)throw new InvalidOperationException("Một lô thử nghiệm tối đa 1000 người.");
   }
   if(result.Count==0)throw new InvalidOperationException("Chưa có bản ghi cần nhập.");
   // Do not deduplicate by matching names: people with the same name may be different.
   if(result.Count(x=>x.Relationship=="Chủ hộ")>1)
    throw new InvalidOperationException("Không cho phép nhiều Chủ hộ trong một lô.");
   return result;
  }
  static void RequireMemberDestination(SQLiteConnection c,SQLiteTransaction tx,Session s,string householdId) {
   Require(c,tx,s,"B02","create",s==null?null:s.AreaId);
   Require(c,tx,s,"B01","read",s==null?null:s.AreaId);
   if(String.IsNullOrWhiteSpace(householdId)||
      Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM households WHERE id=@h AND area_id=@a",P("@h",householdId),P("@a",s.AreaId)))!=1)
    throw new UnauthorizedAccessException("Hộ được chọn không còn trong phạm vi truy cập.");
  }
  public static int CheckMembersBatch(Session s,string householdId,string input) {
   var drafts=ParseMemberBatch(input);
   using(var c=Open()) {
    RequireMemberDestination(c,null,s,householdId);
    if(drafts.Any(x=>x.Relationship=="Chủ hộ") &&
      Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM household_memberships WHERE household_id=@h AND relationship='Chủ hộ' AND end_utc IS NULL",P("@h",householdId)))!=0)
      throw new InvalidOperationException("Hộ đã có Chủ hộ; cần quy trình thay đổi chủ hộ riêng.");
   }
   return drafts.Count;
  }
  public static int ImportMembersBatch(Session s,string householdId,string input,string idempotencyKey) {
   var drafts=ParseMemberBatch(input);
   Guid parsedKey;
   if(String.IsNullOrWhiteSpace(idempotencyKey)||!Guid.TryParseExact(idempotencyKey,"N",out parsedKey))
    throw new InvalidOperationException("Thiếu mã lần gửi hợp lệ; cần kiểm tra lô trước khi ghi.");
   byte[] bytes=Encoding.UTF8.GetBytes(input);string digest;
   using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequireMemberDestination(c,tx,s,householdId);
    using(var chk=new SQLiteCommand("SELECT household_id,actor_id,payload_hash,record_count FROM member_import_batches WHERE idempotency_key=@key",c,tx)) {
     chk.Parameters.Add(P("@key",idempotencyKey));
     using(var r=chk.ExecuteReader())if(r.Read()) {
      if(r.GetString(0)!=householdId||r.GetString(1)!=s.Id||r.GetString(2)!=digest)
       throw new InvalidOperationException("Mã lần gửi đã được sử dụng với nội dung hoặc hồ sơ khác.");
      return r.GetInt32(3); // confirmed duplicate delivery, no new people are inserted
     }
    }
    Exec(c,tx,"INSERT INTO member_import_batches(idempotency_key,household_id,actor_id,payload_hash,record_count,created_utc) VALUES(@key,@h,@actor,@digest,@n,@utc)",
      P("@key",idempotencyKey),P("@h",householdId),P("@actor",s.Id),P("@digest",digest),P("@n",drafts.Count),P("@utc",Now));
    foreach(var draft in drafts) {
     // Must check again inside the write transaction; preview is not authorization.
     if(draft.Relationship=="Chủ hộ" &&
       Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM household_memberships WHERE household_id=@h AND relationship='Chủ hộ' AND end_utc IS NULL",P("@h",householdId)))>0)
       throw new InvalidOperationException("Hộ đã có Chủ hộ; toàn bộ lô không được ghi.");
     string person=Id(),member=Id(),now=Now;
     Exec(c,tx,"INSERT INTO people(id,area_id,full_name,search_name,date_of_birth,gender,created_utc,updated_utc) VALUES(@id,@area,@name,@search,@dob,@gender,@now,@now)",
       P("@id",person),P("@area",s.AreaId),P("@name",draft.Name),P("@search",NormalizeSearch(draft.Name)),P("@dob",draft.DateOfBirth.Length==0?null:draft.DateOfBirth),P("@gender",draft.Gender.Length==0?null:draft.Gender),P("@now",now));
     Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@id,@h,@p,@relation,@now,@now)",
       P("@id",member),P("@h",householdId),P("@p",person),P("@relation",draft.Relationship),P("@now",now));
     Audit(c,tx,s.Id,"ADD_MEMBER_BATCH","person",person);
    }
    tx.Commit();return drafts.Count;
   }
  }

  // B03: household transfer is an explicit, auditable transaction by immutable ID.
  // Head-of-household changes need a separate approval workflow and are blocked here.
  // I08: data-quality findings are advisory only. Never auto-merge by matching names.
  // Explicit service authorization is needed for both the diagnostic list and the target record.
  public static List<DataQualityIssue> ListDataQualityIssues(Session actor) {
   using(var c=Open()) {
    Require(c,null,actor,"I08","read",actor==null?null:actor.AreaId);
    bool canReadHouseholds=HasPermission(c,null,actor,"B01","read",actor.AreaId);
    bool canReadPeople=HasPermission(c,null,actor,"B02","read",actor.AreaId);
    var result=new List<DataQualityIssue>();
    if(canReadHouseholds)
    using(var cmd=new SQLiteCommand(@"SELECT h.id,h.code FROM households h WHERE h.area_id=@area
     AND NOT EXISTS (SELECT 1 FROM household_memberships m
      WHERE m.household_id=h.id AND m.relationship='Chủ hộ' AND m.end_utc IS NULL)
     ORDER BY h.code LIMIT 200",c)) {
     cmd.Parameters.Add(P("@area",actor.AreaId));
     using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new DataQualityIssue {
      Kind="Hộ",Subject=r.GetString(1),TargetId=r.GetString(0),TargetFeature="B01",
      Severity="Cần kiểm tra",Finding="Chưa có Chủ hộ đang liên kết; không tự tạo hoặc thay chủ hộ."
     });
    }
    if(canReadPeople)
    using(var cmd=new SQLiteCommand(@"SELECT p.id,p.full_name FROM people p WHERE p.area_id=@area
     AND NOT EXISTS (SELECT 1 FROM household_memberships m
      WHERE m.person_id=p.id AND m.end_utc IS NULL)
     ORDER BY p.full_name,p.id LIMIT 200",c)) {
     cmd.Parameters.Add(P("@area",actor.AreaId));
     using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new DataQualityIssue {
      Kind="Nhân khẩu",Subject=r.GetString(1),TargetId=r.GetString(0),TargetFeature="B02",
      Severity="Cần kiểm tra",Finding="Chưa có liên kết hộ đang hiệu lực; không tự ghép theo tên."
     });
    }
    if(canReadPeople)
    using(var cmd=new SQLiteCommand(@"SELECT p.id,p.full_name FROM people p WHERE p.area_id=@area
     AND p.search_name<>'' AND EXISTS (SELECT 1 FROM people other
      WHERE other.id<>p.id AND other.area_id=p.area_id AND other.search_name=p.search_name)
     ORDER BY p.full_name,p.id LIMIT 200",c)) {
     cmd.Parameters.Add(P("@area",actor.AreaId));
     using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new DataQualityIssue {
      Kind="Nhân khẩu",Subject=r.GetString(1),TargetId=r.GetString(0),TargetFeature="B02",
      Severity="Cảnh báo",Finding="Trùng tên sau chuẩn hóa dấu; chỉ gợi ý đối chiếu, KHÔNG tự gộp."
     });
    }
    return result;
   }
  }
  public static List<ResidencyChangeRow> ListPersonResidencyHistory(Session actor,string personId) {
   using(var c=Open()) {
    Require(c,null,actor,"B02","read",actor==null?null:actor.AreaId);
    Require(c,null,actor,"B03","read",actor.AreaId);
    if(String.IsNullOrWhiteSpace(personId) ||
     Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM people WHERE id=@id AND area_id=@area",P("@id",personId),P("@area",actor.AreaId)))!=1)
     throw new UnauthorizedAccessException("Hồ sơ nhân khẩu nằm ngoài phạm vi được cấp.");
    using(var cmd=new SQLiteCommand(@"SELECT rc.id,rc.occurred_utc,p.full_name,h1.code,h2.code,
     rc.relationship,rc.reason,rc.person_id,rc.from_household_id,rc.to_household_id
     FROM residency_changes rc JOIN people p ON rc.person_id=p.id AND rc.area_id=p.area_id
     JOIN households h1 ON h1.id=rc.from_household_id AND h1.area_id=rc.area_id
     JOIN households h2 ON h2.id=rc.to_household_id AND h2.area_id=rc.area_id
     WHERE rc.area_id=@area AND rc.person_id=@person ORDER BY rc.occurred_utc DESC,rc.id DESC LIMIT 200",c)) {
     cmd.Parameters.Add(P("@area",actor.AreaId));cmd.Parameters.Add(P("@person",personId));
     var list=new List<ResidencyChangeRow>();
     using(var r=cmd.ExecuteReader())while(r.Read())list.Add(new ResidencyChangeRow {
      Id=r.GetString(0),Timestamp=r.GetString(1),PersonName=r.GetString(2),FromCode=r.GetString(3),ToCode=r.GetString(4),
      Relationship=r.GetString(5),Reason=r.GetString(6),PersonId=r.GetString(7),FromHouseholdId=r.GetString(8),ToHouseholdId=r.GetString(9)
     });
     return list;
    }
   }
  }
  public static List<ResidencyChangeRow> ListResidencyChanges(Session actor,string term=null) {
   using(var c=Open()) {
    Require(c,null,actor,"B03","read",actor==null?null:actor.AreaId);
    const string sql=@"SELECT rc.id,rc.occurred_utc, p.full_name, origin.code, dest.code,
     rc.relationship,rc.reason,rc.person_id,rc.from_household_id,rc.to_household_id
     FROM residency_changes rc
     JOIN people p ON p.id=rc.person_id AND p.area_id=rc.area_id
     JOIN households origin ON origin.id=rc.from_household_id
     JOIN households dest ON dest.id=rc.to_household_id
     WHERE rc.area_id=@area AND
      (p.search_name LIKE @q ESCAPE '\' OR p.full_name LIKE @q ESCAPE '\' OR origin.code LIKE @q ESCAPE '\'
       OR dest.code LIKE @q ESCAPE '\' OR rc.search_reason LIKE @q ESCAPE '\')
      ORDER BY rc.occurred_utc DESC,rc.id DESC LIMIT 200";
    using(var cmd=new SQLiteCommand(sql,c)) {
     cmd.Parameters.Add(P("@area",actor.AreaId));cmd.Parameters.Add(P("@q",SearchPattern(NormalizeSearch(term))));
     var result=new List<ResidencyChangeRow>();
     using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new ResidencyChangeRow {
      Id=r.GetString(0),Timestamp=r.GetString(1),PersonName=r.GetString(2),FromCode=r.GetString(3),ToCode=r.GetString(4),
      Relationship=r.GetString(5),Reason=r.GetString(6),PersonId=r.GetString(7),FromHouseholdId=r.GetString(8),ToHouseholdId=r.GetString(9)
     });
     return result;
    }
   }
  }
  public static void CheckTransfer(Session actor,string personId,string sourceId,string destinationId,
    string relationship,string reason,long personVersion) {
   if(String.IsNullOrWhiteSpace(personId)||String.IsNullOrWhiteSpace(sourceId)||String.IsNullOrWhiteSpace(destinationId)||sourceId==destinationId)
    throw new InvalidOperationException("Chọn người, hộ nguồn và hộ đích hợp lệ, khác nhau.");
   if(String.IsNullOrWhiteSpace(reason)||reason.Trim().Length<5||reason.Trim().Length>300||reason.Any(Char.IsControl))
    throw new InvalidOperationException("Lý do chuyển phải từ 5 đến 300 ký tự hợp lệ.");
   if(!new[]{"Con","Vợ/Chồng","Cha/Mẹ","Cháu","Người thân","Khác"}.Contains(relationship))
    throw new InvalidOperationException("Quan hệ hộ mới chưa hợp lệ.");
   using(var c=Open()) {
    Require(c,null,actor,"B03","create",actor==null?null:actor.AreaId);
    Require(c,null,actor,"B02","read",actor.AreaId);
    Require(c,null,actor,"B01","read",actor.AreaId);
    if(Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM households WHERE area_id=@a AND id IN (@from,@to)",
       P("@a",actor.AreaId),P("@from",sourceId),P("@to",destinationId)))!=2)
     throw new UnauthorizedAccessException("Hộ chuyển đến và chuyển đi không cùng phạm vi.");
    if(Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM people WHERE id=@p AND area_id=@a AND version=@v",
       P("@p",personId),P("@a",actor.AreaId),P("@v",personVersion)))!=1)
     throw new InvalidOperationException("Hồ sơ người đã thay đổi, cần tải lại.");
    string role=Convert.ToString(Scalar(c,null,@"SELECT relationship FROM household_memberships
        WHERE person_id=@p AND household_id=@h AND end_utc IS NULL",P("@p",personId),P("@h",sourceId)));
    if(String.IsNullOrEmpty(role))throw new InvalidOperationException("Người không còn ở hộ nguồn.");
    if(role=="Chủ hộ")throw new InvalidOperationException("Chủ hộ không được chuyển bằng thao tác nhanh.");
   }
  }
  public static string TransferMember(Session actor,string personId,string expectedSourceHouseholdId,
    string destinationHouseholdId,string relationship,string reason,long expectedPersonVersion) {
   if(String.IsNullOrWhiteSpace(personId)||String.IsNullOrWhiteSpace(expectedSourceHouseholdId)||
      String.IsNullOrWhiteSpace(destinationHouseholdId)||expectedSourceHouseholdId==destinationHouseholdId)
    throw new InvalidOperationException("Phải chọn người, hộ nguồn và hộ đích khác nhau theo ID.");
   if(String.IsNullOrWhiteSpace(reason)||reason.Trim().Length<5||reason.Trim().Length>300||reason.Any(Char.IsControl))
    throw new InvalidOperationException("Lý do chuyển phải từ 5 đến 300 ký tự hợp lệ.");
   var allowed=new HashSet<string>(StringComparer.Ordinal){"Con","Vợ/Chồng","Cha/Mẹ","Cháu","Người thân","Khác"};
   if(!allowed.Contains(relationship))throw new InvalidOperationException("Quan hệ với hộ đích không hợp lệ.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"B03","create",actor==null?null:actor.AreaId);
    Require(c,tx,actor,"B02","read",actor.AreaId);
    Require(c,tx,actor,"B01","read",actor.AreaId);
    if(Convert.ToInt32(Scalar(c,tx,@"SELECT COUNT(*) FROM households
       WHERE area_id=@area AND id IN (@src,@dst)",P("@area",actor.AreaId),P("@src",expectedSourceHouseholdId),P("@dst",destinationHouseholdId)))!=2)
     throw new UnauthorizedAccessException("Hộ nguồn và hộ đích phải cùng địa bàn được cấp quyền.");
    if(Convert.ToInt32(Scalar(c,tx,"SELECT COUNT(*) FROM people WHERE id=@p AND area_id=@area AND version=@ver",
      P("@p",personId),P("@area",actor.AreaId),P("@ver",expectedPersonVersion)))!=1)
     throw new InvalidOperationException("Nhân khẩu đã thay đổi hoặc nằm ngoài phạm vi; tải lại hồ sơ.");
    string currentId=null,currentRole=null;
    using(var cmd=new SQLiteCommand(@"SELECT id,relationship FROM household_memberships
       WHERE person_id=@p AND household_id=@source AND end_utc IS NULL LIMIT 1",c,tx)) {
     cmd.Parameters.Add(P("@p",personId));cmd.Parameters.Add(P("@source",expectedSourceHouseholdId));
     using(var r=cmd.ExecuteReader())if(r.Read()){currentId=r.GetString(0);currentRole=r.GetString(1);}
    }
    if(currentId==null)throw new InvalidOperationException("Nhân khẩu không còn thuộc hộ nguồn; không chuyển nhầm hồ sơ.");
    if(currentRole=="Chủ hộ")throw new InvalidOperationException("Không chuyển chủ hộ bằng B03; cần quy trình thay chủ hộ riêng.");
    string now=Now;
    using(var cmd=new SQLiteCommand("UPDATE household_memberships SET end_utc=@utc WHERE id=@id AND end_utc IS NULL",c,tx)) {
     cmd.Parameters.Add(P("@utc",now));cmd.Parameters.Add(P("@id",currentId));
     if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Dữ liệu đã thay đổi trong lúc chuyển hộ.");
    }
    string newMembership=Id(),eventId=Id();
    Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@i,@h,@p,@r,@t,@t)",
      P("@i",newMembership),P("@h",destinationHouseholdId),P("@p",personId),P("@r",relationship),P("@t",now));
    Exec(c,tx,"UPDATE people SET version=version+1,updated_utc=@utc WHERE id=@id",
      P("@utc",now),P("@id",personId));
    Exec(c,tx,"UPDATE households SET version=version+1,updated_utc=@utc WHERE id IN (@src,@dst)",
      P("@utc",now),P("@src",expectedSourceHouseholdId),P("@dst",destinationHouseholdId));
    Exec(c,tx,@"INSERT INTO residency_changes(id,area_id,person_id,from_household_id,to_household_id,
      old_membership_id,new_membership_id,relationship,reason,search_reason,actor_id,occurred_utc)
      VALUES(@i,@a,@p,@src,@dst,@old,@new,@r,@reason,@searchreason,@actor,@now)",
      P("@i",eventId),P("@a",actor.AreaId),P("@p",personId),P("@src",expectedSourceHouseholdId),
      P("@dst",destinationHouseholdId),P("@old",currentId),P("@new",newMembership),P("@r",relationship),
      P("@reason",reason.Trim()),P("@searchreason",NormalizeSearch(reason)),P("@actor",actor.Id),P("@now",now));
    Audit(c,tx,actor.Id,"MOVE_MEMBER_B03","residency_change",eventId);
    tx.Commit();return eventId;
   }
  }
  public static List<string[]> ParseBatch(string text) {
   var list=new List<string[]>();int lineNo=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string raw in (text??"").Split(new[]{"\r\n","\n"},StringSplitOptions.None)) {
    lineNo++;if(String.IsNullOrWhiteSpace(raw))continue;
    string[] cols=raw.Contains("\t")?raw.Split('\t'):raw.Split(';');
    if(cols.Length==3 && lineNo==1 && cols[0].Trim().Equals("Mã hộ",StringComparison.OrdinalIgnoreCase))continue;
    if(cols.Length!=3)throw new InvalidOperationException("Dòng "+lineNo+": cần đúng 3 cột Mã hộ / Địa chỉ / Chủ hộ.");
    for(int i=0;i<cols.Length;i++)cols[i]=cols[i].Trim();
    if(cols.Any(String.IsNullOrWhiteSpace))throw new InvalidOperationException("Dòng "+lineNo+": thiếu dữ liệu.");
    if(!seen.Add(cols[0]))throw new InvalidOperationException("Dòng "+lineNo+": trùng mã hộ trong lô.");
    list.Add(cols);
   }
   if(list.Count==0)throw new InvalidOperationException("Chưa có dòng dữ liệu.");
   if(list.Count>1000)throw new InvalidOperationException("Một lô thử nghiệm tối đa 1000 dòng.");
   return list;
  }
  public static int CheckBatch(Session s,string input) {
   var list=ParseBatch(input);
   using(var c=Open()){
    Require(c,null,s,"B01","create",s.AreaId);
    foreach(var line in list)if(Convert.ToInt32(Scalar(c,null,"SELECT COUNT(*) FROM households WHERE area_id=@a AND code=@c",P("@a",s.AreaId),P("@c",line[0])))!=0)throw new InvalidOperationException("Đã tồn tại mã hộ " + line[0]);
   }
   return list.Count;
  }
  public static int ImportBatch(Session s,string input) {
   var list=ParseBatch(input);
   using(var c=Open())using(var tx=c.BeginTransaction()){
    Require(c,tx,s,"B01","create",s.AreaId);
    foreach(var line in list)CreateHouseholdInTransaction(c,tx,s,line[0],line[1],line[2]);
    tx.Commit();return list.Count;
   }
  }
 }
}
