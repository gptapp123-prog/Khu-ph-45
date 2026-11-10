using System;
using System.Data.SQLite;
using System.Linq;

namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  // Allowed for explicit grants only. BusinessDefaultRights deliberately omits these.
  static readonly string[] PersonIdentityRights={"B02:identity_lookup","B02:identity_write"};

  static string CheckIdentityNumber(string value) {
   value=(value??"").Trim();
   if(value.Length!=12||value.Any(ch=>ch<'0'||ch>'9'))
    throw new InvalidOperationException("Nhập đủ 12 chữ số từ 0 đến 9.");
   return value;
  }

  static void RequirePersonIdentity(SQLiteConnection c,SQLiteTransaction tx,Session actor,string action) {
   Require(c,tx,actor,"B02","read",actor==null?null:actor.AreaId);
   Require(c,tx,actor,"B02",action,actor.AreaId);
  }

  public static string FindPersonIdByIdentity(Session actor,string input) {
   string number=CheckIdentityNumber(input);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequirePersonIdentity(c,tx,actor,"identity_lookup");
    string personId=Convert.ToString(Scalar(c,tx,@"SELECT i.person_id FROM person_identity_numbers i
      JOIN people p ON p.id=i.person_id AND p.area_id=i.area_id
      WHERE i.area_id=@area AND i.citizen_number=@number",P("@area",actor.AreaId),P("@number",number)));
    Audit(c,tx,actor.Id,personId.Length==0?"IDENTITY_LOOKUP_NO_MATCH":"IDENTITY_LOOKUP_MATCH","person",personId);
    tx.Commit();return personId;
   }
  }

  public static string GetMaskedPersonIdentity(Session actor,string personId) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequirePersonIdentity(c,tx,actor,"identity_write");
    if(Convert.ToInt32(Scalar(c,tx,"SELECT count(*) FROM people WHERE id=@id AND area_id=@area",
       P("@id",personId),P("@area",actor.AreaId)))!=1)
     throw new UnauthorizedAccessException("Hồ sơ nằm ngoài phạm vi được cấp.");
    string last=Convert.ToString(Scalar(c,tx,@"SELECT substr(citizen_number,-4)
      FROM person_identity_numbers WHERE person_id=@id AND area_id=@area",P("@id",personId),P("@area",actor.AreaId)));
    tx.Commit();return last.Length==0?"Chưa ghi CCCD":"••••••••"+last;
   }
  }

  public static void SavePersonIdentity(Session actor,string personId,long expectedVersion,string input) {
   string number=CheckIdentityNumber(input);
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequirePersonIdentity(c,tx,actor,"identity_write");
    Require(c,tx,actor,"B02","update",actor.AreaId);
    if(Convert.ToInt32(Scalar(c,tx,@"SELECT count(*) FROM people
       WHERE id=@id AND area_id=@area AND version=@v",P("@id",personId),P("@area",actor.AreaId),P("@v",expectedVersion)))!=1)
     throw new InvalidOperationException("Hồ sơ đã thay đổi hoặc ngoài phạm vi. Mở lại trước khi lưu.");
    if(Convert.ToInt32(Scalar(c,tx,@"SELECT count(*) FROM person_identity_numbers
       WHERE area_id=@area AND citizen_number=@number AND person_id<>@id",
       P("@area",actor.AreaId),P("@number",number),P("@id",personId)))!=0)
     throw new InvalidOperationException("Số này đã được gắn với hồ sơ khác trong khu phố.");
    Exec(c,tx,@"INSERT INTO person_identity_numbers(person_id,area_id,citizen_number,actor_id,updated_utc)
      VALUES(@id,@area,@number,@actor,@utc)
      ON CONFLICT(person_id) DO UPDATE SET citizen_number=excluded.citizen_number,
        actor_id=excluded.actor_id,updated_utc=excluded.updated_utc
      WHERE person_identity_numbers.area_id=excluded.area_id",
      P("@id",personId),P("@area",actor.AreaId),P("@number",number),P("@actor",actor.Id),P("@utc",Now));
    Exec(c,tx,"UPDATE people SET version=version+1,updated_utc=@utc WHERE id=@id AND area_id=@area",
      P("@id",personId),P("@area",actor.AreaId),P("@utc",Now));
    Audit(c,tx,actor.Id,"IDENTITY_NUMBER_SAVED","person",personId);
    tx.Commit();
   }
  }
 }
}
