using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  static string AccountIdentities() {
   using(var c=Open())return VaultStorage.Fingerprint(c,new[]{"accounts"},new Dictionary<string,List<string>>{{"accounts",new List<string>{"id","username","hash_b64","salt_b64","role","area_id","active","created_utc"}}});
  }
  internal static Session RunDefaultPolicyRegression(TestRunRecorder run,Session root,Session admin,string adminPassword) {
   run.BeginStep("P9_NEW_ADMIN_ALL_BUSINESS_RIGHTS");
   foreach(var right in BusinessDefaultRights()){var p=right.Split(':');if(!Can(admin,p[0],p[1])||!Can(root,p[0],p[1]))throw new InvalidDataException("Missing default right: "+right);}
   if(ListCatalog(admin,"",0).TotalCount!=13||GetWorkspaceSummary(admin).Households!=0)throw new InvalidDataException("Default catalog missing or business records seeded.");
   run.BeginStep("P9_NEW_ORDINARY_DEFAULTS_AND_PROTECTED_ADMINS");
   string password="T-"+Guid.NewGuid().ToString("N")+"!";CreateAccount(admin,"default_operator",password,null);var op=Login("default_operator",password);
   foreach(var right in BusinessDefaultRights()){var p=right.Split(':');if(!Can(op,p[0],p[1]))throw new InvalidDataException("Ordinary account missing default business right.");}
   string protectedHash=AccountIdentities();
   foreach(var actor in new[]{admin,op})foreach(var target in new[]{root.Id,admin.Id}) {
    bool denied=false;try{SetAccountStatus(actor,target,false);}catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new InvalidDataException("Default actor altered administrator status.");
    denied=false;try{SetAccountRights(actor,target,new[]{"B01:read"});}catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new InvalidDataException("Default actor altered administrator permissions.");
   }
   if(AccountIdentities()!=protectedHash||Can(op,"I04","manage")||Can(op,"I01","manage"))throw new InvalidDataException("Administrator identity/protected operation changed.");
   run.BeginStep("P9_DEFAULT_ORDINARY_WRITE_READBACK");
   AddHousehold(op,"P9-DEFAULT","[MẪU] Địa chỉ đầu","[MẪU] Chủ hộ");var h=FindHouseholds(op,"P9-DEFAULT").Single();UpdateHouseholdAddress(op,h.Id,h.Version,"[MẪU] Địa chỉ đã sửa");
   if(GetHousehold(op,h.Id).Address!="[MẪU] Địa chỉ đã sửa")throw new InvalidDataException("Default operator cannot edit household.");
   var person=HouseholdMembers(op,h.Id).Single();var original=GetPerson(op,person.Id);UpdatePersonProfile(op,original.Id,original.Version,"[MẪU] Hồ sơ đã sửa","2000-01-01","Nam");
   if(GetPerson(op,original.Id).FullName!="[MẪU] Hồ sơ đã sửa")throw new InvalidDataException("Default operator cannot edit person.");
   run.BeginStep("P9_SHARED_CATALOG_AREA_OVERRIDE");
   var global=ListCatalog(op,"",0).Items.First();string initialLabel=global.Label;
   string localId=SaveCatalog(op,new CatalogEntry{Id=global.Id,Version=global.Version,Category=global.Category,Code=global.Code,Label="[MẪU] Nhãn riêng",Active=true});
   if(localId==global.Id||ListCatalog(op,"",0).TotalCount!=13||ListCatalog(root,"",0).Items.Single(x=>x.Id==global.Id).Label!=initialLabel)throw new InvalidDataException("Catalog override changed shared definition or created visible duplicate.");
   run.BeginStep("P9_UPGRADE_EXISTING_ACCOUNTS_IDENTITY_PRESERVED");
   SetAccountRights(admin,op.Id,new[]{"B01:read"});SetAccountRights(root,admin.Id,new[]{"B01:read"});
   string identities=AccountIdentities();string householdId=h.Id;
   using(var c=Open())Exec(c,null,"DELETE FROM system_policy_migrations WHERE code=@c",P("@c",DefaultPolicyCode));
   Initialize();
   if(Can(admin,"B01","read"))throw new InvalidDataException("Policy migration did not invalidate old session.");
   admin=Login("test_admin",adminPassword);op=Login("default_operator",password);
   foreach(var actor in new[]{admin,op})foreach(var right in BusinessDefaultRights()){var p=right.Split(':');if(!Can(actor,p[0],p[1]))throw new InvalidDataException("Existing account not upgraded.");}
   if(AccountIdentities()!=identities||GetHousehold(op,householdId).Address!="[MẪU] Địa chỉ đã sửa"||!Directory.GetFiles(Path.Combine(DataFolder,"maintenance"),"before-policy-p9-*.qlkpvault").Any())throw new InvalidDataException("Policy upgrade changed identity/data or lacked recovery snapshot.");
   run.BeginStep("P9_RESTART_DOES_NOT_REGRANT_EXPLICIT_RESTRICTION");
   SetAccountRights(admin,op.Id,new[]{"B01:read"});op=Login("default_operator",password);Initialize();
   if(Can(op,"B01","create")||!Can(op,"B01","read"))throw new InvalidDataException("Restart undid explicit restriction.");
   run.BeginStep("P9_ROOT_WORK_AREA_SCOPE");
   string other=Id();using(var c=Open())Exec(c,null,"INSERT INTO areas(id,code,name,created_utc) VALUES(@id,'P9-OTHER','[MẪU] Khu phố khác',@utc)",P("@id",other),P("@utc",Now));
   var switched=SwitchWorkArea(root,other);AddHousehold(switched,"P9-OTHER-H","[MẪU] Khu phố khác","[MẪU] Chủ hộ khác");
   if(FindHouseholds(root,"P9-OTHER-H").Count!=0||FindHouseholds(switched,"P9-OTHER-H").Count!=1||ListWorkAreas(admin).Any(x=>x.Id==other))throw new InvalidDataException("Work-area scope leaked.");
   bool blocked=false;try{SwitchWorkArea(admin,other);}catch(UnauthorizedAccessException){blocked=true;}if(!blocked)throw new InvalidDataException("KP admin switched outside area.");
   return admin;
  }
 }
}
