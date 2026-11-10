using System;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace QLKP.Desktop.Core {
 public sealed class VehicleRow {
  public string Id {get;set;}
  public string Plate {get;set;}
  public string Brand {get;set;}
  public string OwnerId {get;set;}
  public string OwnerName {get;set;}
  public string Kind {get;set;}
  public int? Seats {get;set;}
  public int? PayloadKg {get;set;}
  public bool Active {get;set;}
  public long Version {get;set;}
  public string Status {get{return Active?"Đang theo dõi":"Ngừng theo dõi";}}
  public string KindLabel {get{return Kind=="motorcycle"?"Xe máy":Kind=="passenger"?"Xe chở người":Kind=="truck"?"Xe tải":"Loại khác";}}
 }

 public static partial class DataStore {
  static void UpgradeVehicleRights(SQLiteConnection c,SQLiteTransaction tx) {
   // Existing accounts inherit only corresponding grants they already hold.
   foreach(var pair in new[]{Tuple.Create("read","read"),Tuple.Create("update","create"),Tuple.Create("update","update")}) {
    Exec(c,tx,@"INSERT OR IGNORE INTO permissions(account_id,feature,action,area_id,granted_by)
       SELECT account_id,'F08',@new,area_id,granted_by FROM permissions WHERE feature='B02' AND action=@old",
       P("@old",pair.Item1),P("@new",pair.Item2));
    Exec(c,tx,@"INSERT OR IGNORE INTO delegation_ceiling(admin_id,feature,action,area_id)
       SELECT admin_id,'F08',@new,area_id FROM delegation_ceiling WHERE feature='B02' AND action=@old",
       P("@old",pair.Item1),P("@new",pair.Item2));
   }
   Exec(c,tx,@"UPDATE accounts SET policy_version=policy_version+1
      WHERE EXISTS(SELECT 1 FROM permissions p WHERE p.account_id=accounts.id AND p.feature='F08')");
  }

  const string VehicleSql=@"SELECT v.id,v.plate,v.brand,v.owner_id,p.full_name,v.kind,v.seats,v.payload_kg,v.active,v.version
      FROM vehicles v JOIN people p ON p.id=v.owner_id AND p.area_id=v.area_id WHERE v.area_id=@area";

  static VehicleRow ReadVehicle(SQLiteDataReader r) {
   return new VehicleRow{Id=r.GetString(0),Plate=r.GetString(1),Brand=r.GetString(2),OwnerId=r.GetString(3),
    OwnerName=r.GetString(4),Kind=r.GetString(5),Seats=r.IsDBNull(6)?(int?)null:r.GetInt32(6),
    PayloadKg=r.IsDBNull(7)?(int?)null:r.GetInt32(7),Active=r.GetInt32(8)==1,Version=r.GetInt64(9)};
  }

  static string NormalizePlate(string value) {
   return new string((value??"").ToUpperInvariant().Where(ch=>ch!=' '&&ch!='-'&&ch!='.').ToArray());
  }

  static void RequireVehicles(SQLiteConnection c,SQLiteTransaction tx,Session actor,string action) {
   Require(c,tx,actor,"B02","read",actor==null?null:actor.AreaId);
   Require(c,tx,actor,"F08","read",actor.AreaId);
   if(action!="read")Require(c,tx,actor,"F08",action,actor.AreaId);
  }

  public static RecordPage<VehicleRow> GetVehicles(Session actor,string term,int page) {
   term=(term??"").Trim();
   if(term.Length>120)throw new InvalidOperationException("Từ khóa quá dài.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequireVehicles(c,tx,actor,"read");
    var rows=ReadOperationalPage(c,tx,VehicleSql+@" AND (v.plate LIKE @plate ESCAPE '\'
       OR p.search_name LIKE @owner ESCAPE '\' OR v.brand LIKE @brand ESCAPE '\')","2,1",page,25,ReadVehicle,
       P("@area",actor.AreaId),P("@plate",NormalizePlate(term).Length==0&&term.Length>0?"":SearchPattern(NormalizePlate(term))),
       P("@owner",SearchPattern(NormalizeSearch(term))),P("@brand",SearchPattern(term)));
    tx.Commit();return rows;
   }
  }

  static int? VehicleMeasure(string input,int max,string label) {
   int value;
   if(!Int32.TryParse((input??"").Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<1||value>max)
    throw new InvalidOperationException(label+" phải là số nguyên từ 1 đến "+max+".");
   return value;
  }

  public static string SaveVehicle(Session actor,VehicleRow selected,string plate,string brand,string kind,string measure,string ownerId,bool active) {
   plate=NormalizePlate(plate);brand=(brand??"").Trim();
   if(plate.Length<3||plate.Length>15||plate.Any(ch=>!(ch>='A'&&ch<='Z')&&!(ch>='0'&&ch<='9')))
    throw new InvalidOperationException("Biển số cần từ 3 đến 15 chữ hoặc số sau khi bỏ dấu phân cách.");
   if(brand.Length<1||brand.Length>80||brand.Any(Char.IsControl))throw new InvalidOperationException("Nhập hãng xe hợp lệ, tối đa 80 ký tự.");
   if(!new[]{"motorcycle","passenger","truck","other"}.Contains(kind))throw new InvalidOperationException("Chọn loại phương tiện.");
   int? seats=kind=="passenger"?VehicleMeasure(measure,999,"Số chỗ"):null;
   int? payload=kind=="truck"?VehicleMeasure(measure,1000000,"Tải trọng (kg)"):null;
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    RequireVehicles(c,tx,actor,selected==null?"create":"update");
    if(Convert.ToInt32(Scalar(c,tx,"SELECT count(*) FROM people WHERE id=@id AND area_id=@area",
       P("@id",ownerId),P("@area",actor.AreaId)))!=1)
     throw new UnauthorizedAccessException("Chọn chủ xe trong phạm vi đang quản lý.");
    string id=selected==null?Id():selected.Id;
    if(Convert.ToInt32(Scalar(c,tx,"SELECT count(*) FROM vehicles WHERE area_id=@area AND plate=@plate AND id<>@id",
       P("@id",id),P("@area",actor.AreaId),P("@plate",plate)))!=0)
     throw new InvalidOperationException("Biển số đã được ghi trong khu phố.");
    string sql=selected==null?@"INSERT INTO vehicles(id,area_id,owner_id,plate,brand,kind,seats,payload_kg,active,version,actor_id,updated_utc)
       VALUES(@id,@area,@owner,@plate,@brand,@kind,@seats,@kg,@active,1,@actor,@utc)":
       @"UPDATE vehicles SET owner_id=@owner,plate=@plate,brand=@brand,kind=@kind,seats=@seats,payload_kg=@kg,
         active=@active,version=version+1,actor_id=@actor,updated_utc=@utc WHERE id=@id AND area_id=@area AND version=@version";
    using(var cmd=new SQLiteCommand(sql,c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@id",id),P("@area",actor.AreaId),P("@owner",ownerId),P("@plate",plate),P("@brand",brand),
      P("@kind",kind),P("@seats",seats),P("@kg",payload),P("@active",active?1:0),P("@actor",actor.Id),P("@utc",Now)});
     if(selected!=null)cmd.Parameters.Add(P("@version",selected.Version));
     if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Hồ sơ xe đã thay đổi hoặc ngoài phạm vi. Tải lại trước khi sửa.");
    }
    Audit(c,tx,actor.Id,selected==null?"VEHICLE_CREATED":"VEHICLE_UPDATED","vehicle",id);
    tx.Commit();return id;
   }
  }
 }
}
