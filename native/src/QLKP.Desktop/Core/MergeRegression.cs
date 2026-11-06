using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  // Test data is created only inside an explicit --self-test isolation directory.
  internal static string CreateMergeFixture(Session actor,string tag,string areaCode=null) {
   if(_dataFolder==DefaultDataFolder||!Path.GetFileName(DataFolder).StartsWith("QLKP-SELFTEST-",StringComparison.Ordinal))throw new InvalidOperationException("Merge fixtures require isolated storage.");
   string path=Path.Combine(DataFolder,"merge-"+tag+".db");
   using(var c=VaultStorage.OpenPlain(path,false,true)) {
    Exec(c,null,File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"schema.sql")));
    string area=Id(),root=Id(),sourceActor=Id(),h=Id(),p=Id(),m=Id(),a=Id(),b=Id();
    string code=areaCode;using(var current=Open())if(code==null)code=Convert.ToString(Scalar(current,null,"SELECT code FROM areas WHERE id=@id",P("@id",actor.AreaId)));
    using(var tx=c.BeginTransaction()) {
     Exec(c,tx,"INSERT INTO areas VALUES(@id,@code,'[MẪU] Nguồn hợp nhất',@utc)",P("@id",area),P("@code",code),P("@utc",Now));
     Exec(c,tx,"INSERT INTO accounts(id,username,hash_b64,salt_b64,role,created_utc) VALUES(@id,'source-root','fixture-only','fixture-only','ROOT_ADMIN',@utc)",P("@id",root),P("@utc",Now));
     Exec(c,tx,"INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES(@id,'source-only-account','fixture-only','fixture-only','KP_ADMIN',@area,@utc)",P("@id",sourceActor),P("@area",area),P("@utc",Now));
     Exec(c,tx,"INSERT INTO households(id,area_id,code,address,search_address,created_utc,updated_utc) VALUES(@id,@area,@code,'[MẪU] Địa chỉ nguồn','[mau] dia chi nguon',@utc,@utc)",P("@id",h),P("@area",area),P("@code","MERGE-"+tag),P("@utc",Now));
     Exec(c,tx,"INSERT INTO people(id,area_id,full_name,search_name,created_utc,updated_utc) VALUES(@id,@area,'[MẪU] Người nguồn','[mau] nguoi nguon',@utc,@utc)",P("@id",p),P("@area",area),P("@utc",Now));
     Exec(c,tx,"INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(@id,@h,@p,'Chủ hộ',@utc,@utc)",P("@id",m),P("@h",h),P("@p",p),P("@utc",Now));
     foreach(var f in new[]{Tuple.Create(a,"B04",h,"B01","street","Đường mẫu"),Tuple.Create(b,"B05",a,"B04","construction_kind","Nhà mẫu")}) {
      Exec(c,tx,"INSERT INTO module_records(id,area_id,feature,record_code,title,search_title,status,related_feature,related_id,created_by,updated_by,created_utc,updated_utc) VALUES(@id,@area,@f,@code,'[MẪU] Liên kết nguồn','[mau] lien ket nguon','Mới',@rf,@rid,@actor,@actor,@utc,@utc)",P("@id",f.Item1),P("@area",area),P("@f",f.Item2),P("@code","M-"+tag+"-"+f.Item2),P("@rf",f.Item4),P("@rid",f.Item3),P("@actor",sourceActor),P("@utc",Now));
      Exec(c,tx,"INSERT INTO module_field_values VALUES(@id,@key,@value)",P("@id",f.Item1),P("@key",f.Item5),P("@value",f.Item6));
     }
     Exec(c,tx,"UPDATE module_records SET version=2 WHERE id=@id",P("@id",a));
     Exec(c,tx,"INSERT INTO module_record_history(id,record_id,actor_id,old_version,new_version,old_status,new_status,occurred_utc) VALUES(@id,@record,@actor,1,2,'Mới','Mới',@utc)",P("@id",Id()),P("@record",a),P("@actor",sourceActor),P("@utc",Now));
     Exec(c,tx,"INSERT INTO module_relations(id,area_id,source_id,target_feature,target_id,relation_kind,created_by,created_utc) VALUES(@id,@area,@source,'B02',@p,'THAM_CHIEU',@actor,@utc)",P("@id",Id()),P("@area",area),P("@source",b),P("@p",p),P("@actor",sourceActor),P("@utc",Now));
     Exec(c,tx,"INSERT INTO shared_catalog_items(id,scope_area,category_code,item_code,item_label,changed_by,updated_utc) VALUES(@id,'*','SOURCE','ONLY','[MẪU] Danh mục toàn hệ thống nguồn',@actor,@utc)",P("@id",Id()),P("@actor",sourceActor),P("@utc",Now));
     Exec(c,tx,"PRAGMA user_version=12");tx.Commit();
    }VaultStorage.Check(c);
   }return path;
  }
  static string CurrentFingerprint(params string[] tables){using(var c=Open())return VaultStorage.Fingerprint(c,tables.Length==0?null:tables);}
  internal static void RunMergeRegression(TestRunRecorder run,Session root,Session admin,Session limited) {
   string path=CreateMergeFixture(admin,"MAIN");string policy=CurrentFingerprint("accounts","permissions","delegation_ceiling");
   run.BeginStep("MERGE_PERMISSION_AND_SCOPE_DENIED");
   foreach(var actor in new[]{new Session{Id=root.Id,Role=root.Role,AreaId=root.AreaId,PolicyVersion=root.PolicyVersion,ExpiresUtc=DateTime.UtcNow.AddMinutes(-1)},limited}){bool denied=false;try{using(var plan=PreviewMerge(actor,path,"")){} }catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new InvalidDataException("Unapproved merger accepted.");}
   string wrong=CreateMergeFixture(admin,"OTHER","OTHER-AREA");bool rejected=false;try{using(var plan=PreviewMerge(admin,wrong,"")){} }catch(InvalidOperationException){rejected=true;}if(!rejected)throw new InvalidDataException("Cross-area source accepted.");
   run.BeginStep("MERGE_PREVIEW_DOES_NOT_CHANGE_CURRENT_DATA");
   string before=CurrentFingerprint();
   using(var plan=PreviewMerge(admin,path,"")) {
    if(plan.Added!=9||plan.Identical!=0||CurrentFingerprint()!=before||FindHouseholds(admin,"MERGE-MAIN").Count!=0)throw new InvalidDataException("Preview changed data or miscounted.");
    run.BeginStep("MERGE_STALE_PREVIEW_REJECTED");
    AddHousehold(admin,"MERGE-CONCURRENT","[MẪU] Cập nhật đồng thời","[MẪU] Chủ hộ");
    string afterChange=CurrentFingerprint();rejected=false;try{ApplyMerge(admin,plan);}catch(InvalidOperationException){rejected=true;}
    if(!rejected||CurrentFingerprint()!=afterChange)throw new InvalidDataException("Stale plan overwrote current data.");
   }
   run.BeginStep("MERGE_APPLY_IDENTITIES_LINKS_AND_ACCOUNTS");string recovery;
   using(var plan=PreviewMerge(admin,path,"")){ApplyMerge(admin,plan);recovery=plan.RecoveryFile;if(!plan.Applied||!File.Exists(recovery)||VaultStorage.IsPlainSqlite(recovery))throw new InvalidDataException("Merge recovery missing or unencrypted.");}
   var found=FindHouseholds(admin,"MERGE-MAIN");if(found.Count!=1||found[0].MemberCount!=1||HouseholdMembers(admin,found[0].Id).Count!=1)throw new InvalidDataException("Merged household membership lost.");
   if(CurrentFingerprint("accounts","permissions","delegation_ceiling")!=policy)throw new InvalidDataException("Merge changed accounts or permissions.");
   using(var c=Open()) {
    if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM accounts WHERE username='source-only-account'"))!=0||Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM shared_catalog_items WHERE category_code='SOURCE' AND item_code='ONLY'"))!=0)throw new InvalidDataException("Source identity/global scope imported.");
    if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM module_records WHERE record_code LIKE 'M-MAIN-%' AND area_id=@area",P("@area",admin.AreaId)))!=2)throw new InvalidDataException("Module scope or typed link missing.");
    if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM data_merge_items WHERE source_created_by<>'' AND source_created_by<>@actor",P("@actor",admin.Id)))<2)throw new InvalidDataException("Original actor provenance lost.");VaultStorage.Check(c);
    string originId=Convert.ToString(Scalar(c,null,"SELECT id FROM module_records WHERE feature='B04' AND record_code='M-MAIN-B04'"));
    var history=GetModuleHistory(admin,"B04",originId);if(history.Count!=1||!history[0].Actor.StartsWith("Tiếp nhận từ kho nguồn:",StringComparison.Ordinal))throw new InvalidDataException("Imported history attributed as a local original action.");
   }
   run.BeginStep("MERGE_UNREADABLE_RELATION_TARGET_DENIED");
   string restrictedSecret="T-"+Guid.NewGuid().ToString("N")+"!";CreateAccount(root,"merge_restricted_admin",restrictedSecret,null);
   var restricted=Login("merge_restricted_admin",restrictedSecret);var allowed=GetAccountRights(root,restricted.Id);allowed.AddRange(ModuleCatalog.AdditionalRights());SetAccountRights(root,restricted.Id,allowed);restricted=Login("merge_restricted_admin",restrictedSecret);
   string hidden=SaveModuleRecord(restricted,"D02",new ModuleRecord{Code="MERGE-HIDDEN",Title="[MẪU] Hồ sơ không được đọc",Status="Hồ sơ nội bộ",Fields=new System.Collections.Generic.Dictionary<string,string>{{"full_name","[MẪU] Nguồn riêng"}}});
   SetAccountRights(root,restricted.Id,allowed.Where(x=>!x.StartsWith("D02:",StringComparison.Ordinal)));restricted=Login("merge_restricted_admin",restrictedSecret);
   string deniedTarget=CreateMergeFixture(restricted,"DENIED-TARGET");using(var c=VaultStorage.OpenPlain(deniedTarget,false)){Exec(c,null,"DROP TRIGGER module_relations_guard_update;UPDATE module_relations SET target_feature='D02',target_id=@id",P("@id",hidden));}
   before=CurrentFingerprint();rejected=false;try{using(var plan=PreviewMerge(restricted,deniedTarget,"")){} }catch(UnauthorizedAccessException){rejected=true;}
   if(!rejected||CurrentFingerprint()!=before)throw new InvalidDataException("Merge linked a target without read permission.");
   run.BeginStep("MERGE_REPEAT_SKIPS_IDENTICAL_ROWS");
   using(var plan=PreviewMerge(admin,path,""))if(plan.Added!=0||plan.Identical!=9)throw new InvalidDataException("Repeated merge duplicates data.");
   run.BeginStep("MERGE_CONFLICT_ROLLS_BACK_ENTIRE_BATCH");
   using(var c=VaultStorage.OpenPlain(path,false)){Exec(c,null,"UPDATE households SET address='[MẪU] Khác nội dung'");}
   before=CurrentFingerprint();rejected=false;try{using(var plan=PreviewMerge(admin,path,"")){} }catch(InvalidOperationException){rejected=true;}
   if(!rejected||CurrentFingerprint()!=before)throw new InvalidDataException("Conflicting source changed current database.");
   run.BeginStep("MERGE_BROKEN_LINK_REJECTED");
   string broken=CreateMergeFixture(admin,"BROKEN");
   using(var c=VaultStorage.OpenPlain(broken,false)){Exec(c,null,"DROP TRIGGER module_reference_update;UPDATE module_records SET related_id='missing-id' WHERE feature='B04';");}
   before=CurrentFingerprint();rejected=false;try{using(var plan=PreviewMerge(admin,broken,"")){} }catch(InvalidOperationException){rejected=true;}
   if(!rejected||CurrentFingerprint()!=before)throw new InvalidDataException("Broken reference merge changed data.");
   run.BeginStep("MERGE_LOCAL_RECOVERY_RESTORE");
   EncryptedBackup.Validate(root,recovery,"");EncryptedBackup.Restore(root,recovery,"");
   if(FindHouseholds(admin,"MERGE-MAIN").Count!=0||FindHouseholds(admin,"MERGE-CONCURRENT").Count!=1||Login("test_root",null)!=null)throw new InvalidDataException("Local pre-merge recovery incorrect.");
  }
 }
}
