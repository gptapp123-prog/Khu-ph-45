using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace QLKP.Desktop.Core {
 public sealed class MergeSummary {
  public string Table {get;internal set;} public int Added {get;internal set;} public int Identical {get;internal set;}
 }
 public sealed class MergePlan : IDisposable {
  internal string Candidate,Before,BaseHash,CandidateHash,ActorId,AreaId,RunId;
  public string SourceHash {get;internal set;} public string AreaCode {get;internal set;}
  public List<MergeSummary> Summary {get;internal set;}=new List<MergeSummary>();
  public int Added {get{return Summary.Sum(x=>x.Added);}} public int Identical {get{return Summary.Sum(x=>x.Identical);}}
  public string RecoveryFile {get;internal set;}
  public bool Applied {get;internal set;}
  public void Dispose(){foreach(string f in new[]{Candidate,Before})if(!String.IsNullOrEmpty(f)&&File.Exists(f))File.Delete(f);Candidate=Before=null;}
 }
 public static partial class DataStore {
  static readonly string[] MergeTables={"households","people","household_profiles","person_profiles","household_memberships","member_import_batches","residency_changes","intake_batches","module_records","module_field_values","module_record_history","module_relations","shared_catalog_items"};
  static readonly string[] ActorColumns={"actor_id","created_by","updated_by","changed_by"};
  static string MergeStage(string prefix){string dir=Path.Combine(DataFolder,"maintenance");Directory.CreateDirectory(dir);return Path.Combine(dir,prefix+"-"+Guid.NewGuid().ToString("N")+".qlkpvault");}
  static void RequireMerge(SQLiteConnection c,SQLiteTransaction tx,Session s) {
   Require(c,tx,s,"I08","read",s==null?null:s.AreaId);Require(c,tx,s,"I08","merge",s.AreaId);
   if(s.Role!="KP_ADMIN")throw new UnauthorizedAccessException("Hợp nhất chỉ dành cho quản trị khu phố đã được cấp quyền.");
  }
  static List<Dictionary<string,object>> MergeRows(SQLiteConnection c,string table,string where="",SQLiteTransaction tx=null,params SQLiteParameter[] parameters) {
   var rows=new List<Dictionary<string,object>>();using(var cmd=new SQLiteCommand("SELECT * FROM "+VaultStorage.Quote(table)+(where.Length>0?" WHERE "+where:""),c,tx)) {
    cmd.Parameters.AddRange(parameters);using(var r=cmd.ExecuteReader())while(r.Read()) {if(rows.Count>=200000)throw new InvalidDataException("Lô hợp nhất vượt 200.000 dòng trong một bảng.");var row=new Dictionary<string,object>(StringComparer.Ordinal);for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.GetValue(i);rows.Add(row);}
   }return rows;
  }
  static string Text(Dictionary<string,object> r,string k){object v;return r.TryGetValue(k,out v)&&v!=DBNull.Value?Convert.ToString(v):"";}
  static bool SameValue(object a,object b){if(a==DBNull.Value||b==DBNull.Value)return a==DBNull.Value&&b==DBNull.Value;if(a is byte[]&&b is byte[])return ((byte[])a).SequenceEqual((byte[])b);return Equals(a,b);}
  static string[] MergeKey(string table){return table=="module_field_values"?new[]{"record_id","field_key"}:table=="household_profiles"?new[]{"household_id"}:table=="person_profiles"?new[]{"person_id"}:table=="member_import_batches"?new[]{"idempotency_key"}:table=="intake_batches"?new[]{"request_id"}:new[]{"id"};}
  static string ScopedSource(string table) {
   if(new[]{"households","people","residency_changes","intake_batches","module_records","module_relations"}.Contains(table))return "area_id=@area";
   if(table=="shared_catalog_items")return "scope_area=@area";
   if(table=="household_profiles")return "household_id IN (SELECT id FROM households WHERE area_id=@area)";
   if(table=="person_profiles")return "person_id IN (SELECT id FROM people WHERE area_id=@area)";
   if(new[]{"household_memberships","member_import_batches"}.Contains(table))return "household_id IN (SELECT id FROM households WHERE area_id=@area)";
   return "record_id IN (SELECT id FROM module_records WHERE area_id=@area)";
  }
  static string MergeFeature(SQLiteConnection source,string table,Dictionary<string,object> row) {
   if(table=="shared_catalog_items")return "I07";if(table=="residency_changes")return "B03";
   if(table.StartsWith("module_",StringComparison.Ordinal))return table=="module_records"?Text(row,"feature"):Convert.ToString(Scalar(source,null,"SELECT feature FROM module_records WHERE id=@id",P("@id",Text(row,table=="module_relations"?"source_id":"record_id"))));
   return table=="households"||table=="household_profiles"?"B01":"B02";
  }
  static void MergeInsert(SQLiteConnection c,SQLiteTransaction tx,string table,Dictionary<string,object> row) {
   var names=row.Keys.ToList();using(var cmd=new SQLiteCommand("INSERT INTO "+VaultStorage.Quote(table)+"("+String.Join(",",names.Select(VaultStorage.Quote))+") VALUES("+String.Join(",",names.Select((x,i)=>"@m"+i))+")",c,tx)) {for(int i=0;i<names.Count;i++)cmd.Parameters.Add(P("@m"+i,row[names[i]]));cmd.ExecuteNonQuery();}
  }
  static void BuildMerge(SQLiteConnection source,SQLiteConnection target,Session actor,string sourceArea,MergePlan plan) {
   using(var tx=target.BeginTransaction()) {
    RequireMerge(target,tx,actor);
    Exec(target,tx,"INSERT INTO data_merge_runs(id,area_id,actor_id,source_sha256,row_count,applied_utc) VALUES(@id,@area,@actor,@hash,0,@utc)",P("@id",plan.RunId),P("@area",actor.AreaId),P("@actor",actor.Id),P("@hash",plan.SourceHash),P("@utc",Now));
    var modules=new List<Dictionary<string,object>>();var newModules=new HashSet<string>();
    foreach(string table in MergeTables) {
     var summary=new MergeSummary{Table=table};plan.Summary.Add(summary);
     foreach(var original in MergeRows(source,table,ScopedSource(table),null,P("@area",sourceArea))) {
      string feature=MergeFeature(source,table,original);Require(target,tx,actor,feature,"read",actor.AreaId);Require(target,tx,actor,feature,feature=="I07"?"manage":"create",actor.AreaId);
      var row=new Dictionary<string,object>(original,StringComparer.Ordinal);
      if(row.ContainsKey("area_id"))row["area_id"]=actor.AreaId;
      if(table=="shared_catalog_items")row["scope_area"]=actor.AreaId;
      foreach(var col in ActorColumns)if(row.ContainsKey(col))row[col]=actor.Id;
      var keys=MergeKey(table);string clause=String.Join(" AND ",keys.Select((k,i)=>VaultStorage.Quote(k)+"=@k"+i));
      var existing=MergeRows(target,table,clause,tx,keys.Select((k,i)=>P("@k"+i,row[k])).ToArray());
      if(existing.Count>0) {
       if(row.Any(pair=>!ActorColumns.Contains(pair.Key)&&(!existing[0].ContainsKey(pair.Key)||!SameValue(pair.Value,existing[0][pair.Key]))))throw new InvalidOperationException("Xung đột cùng định danh trong "+table+". Đã hủy toàn bộ lô; đối chiếu hai kho trước khi thử lại.");
       // A duplicate record must also have exactly the same typed field set.
       if(table=="module_records") {
        var a=MergeRows(source,"module_field_values","record_id=@id",null,P("@id",row["id"]));var b=MergeRows(target,"module_field_values","record_id=@id",tx,P("@id",row["id"]));
        if(a.Count!=b.Count||a.Any(x=>!b.Any(y=>x.All(p=>SameValue(p.Value,y[p.Key])))))throw new InvalidOperationException("Trường dữ liệu của hồ sơ trùng ID chưa đồng nhất. Đã hủy toàn bộ lô.");
       }
       summary.Identical++;continue;
      }
      if(table=="module_records") {modules.Add(original);newModules.Add(Text(original,"id"));row["related_feature"]="";row["related_id"]="";}
      MergeInsert(target,tx,table,row);summary.Added++;
      string identity=String.Join("|",keys.Select(k=>Text(original,k)));
      Exec(target,tx,"INSERT INTO data_merge_items(run_id,source_table,source_id,source_actor_id,source_created_by,source_updated_by) VALUES(@r,@t,@id,@a,@c,@u)",P("@r",plan.RunId),P("@t",table),P("@id",identity),P("@a",Text(original,"actor_id")),P("@c",Text(original,"created_by").Length>0?Text(original,"created_by"):Text(original,"changed_by")),P("@u",Text(original,"updated_by")));
     }
    }
    // All referenced IDs now exist. Validate final links and the same business rules as the normal editor.
    foreach(var r in modules) {
     string id=Text(r,"id"),feature=Text(r,"feature");
     var fields=MergeRows(source,"module_field_values","record_id=@id",null,P("@id",id)).ToDictionary(x=>Text(x,"field_key"),x=>Text(x,"field_value"));
     var draft=new ModuleRecord{Id=id,Feature=feature,Code=Text(r,"record_code"),Title=Text(r,"title"),Notes=Text(r,"notes"),Status=Text(r,"status"),RelatedFeature=Text(r,"related_feature"),RelatedId=Text(r,"related_id"),Fields=fields};
     CheckModuleDraft(target,tx,actor,feature,draft);
     Exec(target,tx,"UPDATE module_records SET related_feature=@f,related_id=@r WHERE id=@id",P("@f",draft.RelatedFeature),P("@r",draft.RelatedId),P("@id",id));
    }
    using(var q=new SQLiteCommand("PRAGMA foreign_key_check",target,tx))using(var r=q.ExecuteReader())if(r.Read())throw new InvalidDataException("Liên kết sau hợp nhất không toàn vẹn; đã hủy lô.");
    Exec(target,tx,"UPDATE data_merge_runs SET row_count=@count WHERE id=@id",P("@count",plan.Added),P("@id",plan.RunId));
    Audit(target,tx,actor.Id,"DATA_MERGE_APPLIED","data_merge",plan.RunId);tx.Commit();
   }VaultStorage.Check(target);
  }
  public static MergePlan PreviewMerge(Session actor,string sourcePath,string phrase) {
   using(var c=Open())RequireMerge(c,null,actor);
   string path=Path.GetFullPath(sourcePath??"");VaultStorage.GuardFile(path);
   if(!File.Exists(path)||new FileInfo(path).Length>128L*1024*1024)throw new InvalidDataException("Chọn kho hoặc bản sao không quá 128 MB.");
   if(String.Equals(path,Path.GetFullPath(DatabasePath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chọn kho nguồn khác kho đang sử dụng.");
   if(File.Exists(path+"-wal")||File.Exists(path+"-journal"))throw new IOException("Kho nguồn còn giao dịch. Đóng ứng dụng nguồn đúng cách trước khi hợp nhất.");
   var plan=new MergePlan{ActorId=actor.Id,AreaId=actor.AreaId,RunId=Id(),Candidate=MergeStage("merge-candidate"),Before=MergeStage("merge-base")};
   string frozen=null;var key=VaultStorage.CurrentKey();
   try {
    using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
     using(var hash=SHA256.Create())plan.SourceHash=BitConverter.ToString(hash.ComputeHash(held)).Replace("-","").ToLowerInvariant();
     if(String.Equals(Path.GetExtension(path),".qlkpbak",StringComparison.OrdinalIgnoreCase))frozen=EncryptedBackup.Materialize(path,phrase);
     else {
      frozen=MergeStage("merge-source");byte[] sourceKey=null;
      try {
       if(!VaultStorage.IsPlainSqlite(path))sourceKey=VaultStorage.ReadKey(Path.Combine(Path.GetDirectoryName(path),"qlkp-vault.key.dpapi"));
       using(var source=sourceKey==null?VaultStorage.OpenPlain(path):VaultStorage.OpenEncrypted(path,sourceKey,true))using(var target=VaultStorage.OpenEncrypted(frozen,key,false,true))VaultStorage.CopyTrusted(source,target);
      }finally{if(sourceKey!=null)Array.Clear(sourceKey,0,sourceKey.Length);}
     }
    }
    using(var held=new FileStream(DatabasePath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)) {
     plan.BaseHash=VaultStorage.Snapshot(plan.Candidate,key);File.Copy(plan.Candidate,plan.Before,false);
    }
    using(var source=VaultStorage.OpenEncrypted(frozen,key,true))using(var target=VaultStorage.OpenEncrypted(plan.Candidate,key)) {
     plan.AreaCode=Convert.ToString(Scalar(target,null,"SELECT code FROM areas WHERE id=@area",P("@area",actor.AreaId)));
     string sourceArea=Convert.ToString(Scalar(source,null,"SELECT id FROM areas WHERE code=@code COLLATE NOCASE",P("@code",plan.AreaCode)));
     if(String.IsNullOrEmpty(sourceArea))throw new InvalidOperationException("Mã khu phố của nguồn không trùng khu phố đang quản lý. Không hợp nhất khác phạm vi.");
     BuildMerge(source,target,actor,sourceArea,plan);plan.CandidateHash=VaultStorage.Fingerprint(target);
    }
    return plan;
   }catch{plan.Dispose();throw;}
   finally{Array.Clear(key,0,key.Length);if(frozen!=null&&File.Exists(frozen))File.Delete(frozen);}
  }
  public static void ApplyMerge(Session actor,MergePlan plan) {
   if(plan==null||plan.Applied||String.IsNullOrEmpty(plan.Candidate)||plan.ActorId!=actor.Id||plan.AreaId!=actor.AreaId)throw new InvalidOperationException("Bản đối chiếu đã hết hiệu lực; kiểm tra lại nguồn.");
   using(var c=Open())RequireMerge(c,null,actor);
   var key=VaultStorage.CurrentKey();bool replaced=false;string rollback=MergeStage("before-merge");
   try {
    using(var held=new FileStream(DatabasePath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)) {
     using(var c=VaultStorage.OpenEncrypted(DatabasePath,key,true))if(VaultStorage.Fingerprint(c)!=plan.BaseHash)throw new InvalidOperationException("Kho đã thay đổi sau đối chiếu. Kiểm tra lại trước khi hợp nhất.");
     if(File.Exists(DatabasePath+"-wal")||File.Exists(DatabasePath+"-journal"))throw new IOException("Kho đang ghi dữ liệu; thử lại sau.");
     using(var c=VaultStorage.OpenEncrypted(plan.Candidate,key)) {
      VaultStorage.Check(c);if(VaultStorage.Fingerprint(c)!=plan.CandidateHash)throw new InvalidDataException("Bản đối chiếu đã thay đổi; từ chối áp dụng.");
      using(var q=new SQLiteCommand("UPDATE data_merge_runs SET applied_utc=@utc WHERE id=@id",c)){q.Parameters.Add(P("@utc",Now));q.Parameters.Add(P("@id",plan.RunId));q.ExecuteNonQuery();}
     }
     File.Copy(plan.Before,rollback,false);File.Replace(plan.Candidate,DatabasePath,null);plan.Candidate=null;replaced=true;
     using(var c=VaultStorage.OpenEncrypted(DatabasePath,key,true))VaultStorage.Check(c);
     plan.RecoveryFile=rollback;plan.Applied=true;
    }
    plan.Dispose();StartupDiagnostics.WriteEvent("DATA_MERGE_COMPLETED");
   }catch {
    if(replaced){string repair=MergeStage("merge-repair");try{File.Copy(rollback,repair,false);File.Replace(repair,DatabasePath,null);}finally{if(File.Exists(repair))File.Delete(repair);}}
    throw;
   }finally{Array.Clear(key,0,key.Length);}
  }
 }
}
