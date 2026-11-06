using System;
using System.Data.SQLite;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  // Household movement and residency classification retain their separate workflows.
  public static void UpdatePersonProfile(Session actor,string id,long expectedVersion,string name,string dob,string gender) {
   if(String.IsNullOrWhiteSpace(id)||expectedVersion<1)throw new InvalidOperationException("Thiếu ID hoặc phiên bản hồ sơ.");
   var draft=new MemberDraft{Name=(name??"").Trim(),Relationship="Khác",DateOfBirth=(dob??"").Trim(),Gender=(gender??"").Trim()};
   ValidateMemberDraft(draft,1);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"B02","read",actor==null?null:actor.AreaId);
    Require(c,tx,actor,"B02","update",actor.AreaId);
    using(var cmd=new SQLiteCommand(@"UPDATE people SET full_name=@name,search_name=@search,date_of_birth=@dob,gender=@gender,
      version=version+1,updated_utc=@utc WHERE id=@id AND area_id=@area AND version=@version",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@name",draft.Name),P("@search",NormalizeSearch(draft.Name)),
      P("@dob",draft.DateOfBirth),P("@gender",draft.Gender),P("@utc",Now),P("@id",id),P("@area",actor.AreaId),P("@version",expectedVersion)});
     if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Hồ sơ đã thay đổi hoặc không thuộc phạm vi; tải lại trước khi sửa.");
    }
    Audit(c,tx,actor.Id,"UPDATE_PERSON_PROFILE","person",id);
    tx.Commit();
   }
  }
 }
}
