using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
namespace QLKP.Desktop.Core {
 public sealed class WorkArea {public string Id {get;set;} public string Name {get;set;} public string Code {get;set;} public string Display {get{return Code+" · "+Name;}}}
 public sealed class WorkspaceSummary {
  public long Households,People,Records,Catalog;
  public string Display {get{return Households+" hộ · "+People+" nhân khẩu · "+Records+" hồ sơ nghiệp vụ · "+Catalog+" danh mục";}}
 }
 public static partial class DataStore {
  const string DefaultPolicyCode="BUSINESS_DEFAULTS_P9_V1";
  public static List<string> BusinessDefaultRights() {
   var rights=new HashSet<string>(ModuleCatalog.AdditionalRights(),StringComparer.Ordinal);
   foreach(var f in FeatureCatalog.All.Where(x=>!new[]{"I01","I02","I04"}.Contains(x.Code)))rights.Add(f.Code+":read");
   foreach(var p in new[]{"B01:read","B01:create","B01:update","B02:read","B02:create","B02:update","B03:read","B03:create","I08:read"})rights.Add(p);
   return rights.OrderBy(x=>x,StringComparer.Ordinal).ToList();
  }
  static void GrantBusinessDefaults(SQLiteConnection c,SQLiteTransaction tx,string id,string area,string root,bool ceiling) {
   foreach(string right in BusinessDefaultRights()) {
    var p=right.Split(':');Grant(c,tx,id,p[0],p[1],area,root);
    if(ceiling)Exec(c,tx,"INSERT OR IGNORE INTO delegation_ceiling(admin_id,feature,action,area_id) VALUES(@id,@f,@action,@area)",P("@id",id),P("@f",p[0]),P("@action",p[1]),P("@area",area));
   }
  }
  // This one-time policy upgrade is separate from recurring permission changes.
  // A future explicit restriction stays restricted after a restart.
  static void UpgradeBusinessDefaults() {
   using(var c=Open())if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM system_policy_migrations WHERE code=@c",P("@c",DefaultPolicyCode)))!=0)return;
   using(var c=Open())if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM accounts"))>0) {
    string folder=Path.Combine(DataFolder,"maintenance");Directory.CreateDirectory(folder);var key=VaultStorage.CurrentKey();
    try{VaultStorage.Snapshot(Path.Combine(folder,"before-policy-p9-"+Guid.NewGuid().ToString("N")+".qlkpvault"),key);}finally{Array.Clear(key,0,key.Length);}
   }
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    if(Convert.ToInt32(Scalar(c,tx,"SELECT count(*) FROM system_policy_migrations WHERE code=@c",P("@c",DefaultPolicyCode)))!=0){tx.Commit();return;}
    string root=Convert.ToString(Scalar(c,tx,"SELECT id FROM accounts WHERE role='ROOT_ADMIN'"));
    var accounts=new List<Tuple<string,string,string>>();
    using(var q=new SQLiteCommand("SELECT id,area_id,role FROM accounts WHERE role<>'ROOT_ADMIN'",c,tx))using(var r=q.ExecuteReader())while(r.Read())accounts.Add(Tuple.Create(r.GetString(0),r.GetString(1),r.GetString(2)));
    if(accounts.Count>0&&String.IsNullOrEmpty(root))throw new InvalidDataException("Kho thiếu quản trị hệ thống; cần phục hồi đúng tài khoản quản trị.");
    foreach(var a in accounts){GrantBusinessDefaults(c,tx,a.Item1,a.Item2,root,a.Item3=="KP_ADMIN");Exec(c,tx,"UPDATE accounts SET policy_version=policy_version+1 WHERE id=@id",P("@id",a.Item1));}
    if(!String.IsNullOrEmpty(root)){EnsureReferenceRows(c,tx,root,"*");Audit(c,tx,root,"BUSINESS_DEFAULTS_P9_APPLIED","policy",DefaultPolicyCode);}
    Exec(c,tx,"INSERT INTO system_policy_migrations(code,applied_utc) VALUES(@code,@utc)",P("@code",DefaultPolicyCode),P("@utc",Now));tx.Commit();
   }
  }
  public static List<WorkArea> ListWorkAreas(Session actor) {
   using(var c=Open()) {
    Require(c,null,actor,"A01","read",actor==null?null:actor.AreaId);
    var list=new List<WorkArea>();using(var q=new SQLiteCommand("SELECT id,code,name FROM areas WHERE @root=1 OR id=@area ORDER BY created_utc,id",c)) {
     q.Parameters.AddRange(new[]{P("@root",actor.Role=="ROOT_ADMIN"?1:0),P("@area",actor.AreaId)});using(var r=q.ExecuteReader())while(r.Read())list.Add(new WorkArea{Id=r.GetString(0),Code=r.GetString(1),Name=r.GetString(2)});
    }return list;
   }
  }
  public static Session SwitchWorkArea(Session actor,string area) {
   using(var c=Open()) {
    Require(c,null,actor,"I01","manage",actor==null?null:actor.AreaId);
    if(actor.Role!="ROOT_ADMIN"||Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM areas WHERE id=@id",P("@id",area)))!=1)throw new UnauthorizedAccessException("Chỉ quản trị hệ thống chọn khu phố làm việc.");
   }
   return new Session{Id=actor.Id,Username=actor.Username,Role=actor.Role,AreaId=area,PolicyVersion=actor.PolicyVersion,ExpiresUtc=actor.ExpiresUtc};
  }
  public static WorkspaceSummary GetWorkspaceSummary(Session s) {
   using(var c=Open())using(var tx=c.BeginTransaction(System.Data.IsolationLevel.ReadCommitted)) {
    var result=new WorkspaceSummary();
    if(HasPermission(c,tx,s,"B01","read",s.AreaId))result.Households=Convert.ToInt64(Scalar(c,tx,"SELECT count(*) FROM households WHERE area_id=@a",P("@a",s.AreaId)));
    if(HasPermission(c,tx,s,"B02","read",s.AreaId))result.People=Convert.ToInt64(Scalar(c,tx,"SELECT count(*) FROM people WHERE area_id=@a",P("@a",s.AreaId)));
    foreach(var m in ModuleCatalog.Editable)if(HasPermission(c,tx,s,m.Code,"read",s.AreaId))result.Records+=Convert.ToInt64(Scalar(c,tx,"SELECT count(*) FROM module_records WHERE area_id=@a AND feature=@f",P("@a",s.AreaId),P("@f",m.Code)));
    if(HasPermission(c,tx,s,"I07","read",s.AreaId))result.Catalog=Convert.ToInt64(Scalar(c,tx,"SELECT count(*) FROM shared_catalog_items g WHERE g.scope_area=@a OR (g.scope_area='*' AND NOT EXISTS(SELECT 1 FROM shared_catalog_items l WHERE l.scope_area=@a AND l.category_code=g.category_code AND l.item_code=g.item_code))",P("@a",s.AreaId)));
    tx.Commit();return result;
   }
  }
 }
}
