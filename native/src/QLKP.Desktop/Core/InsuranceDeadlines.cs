using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Xml.Linq;

namespace QLKP.Desktop.Core {
 internal sealed class InsuranceDeadlineRow {
  public string PersonId {get;set;}
  public string Name {get;set;}
  public string BirthDate {get;set;}
  public string ExpiryDate {get;set;}
  public string State {get;set;}
 }

 public static partial class DataStore {
  static string InsuranceExpiry(XElement data) {
   foreach(var field in data.Elements("field")) {
    if((string)field.Attribute("key")=="expires_on"&&
       DateTime.TryParseExact(field.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date))
     return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
   }
   return "";
  }

  static void BackfillInsuranceExpiry(SQLiteConnection c,SQLiteTransaction tx) {
   var dates=new List<KeyValuePair<string,string>>();
   using(var q=new SQLiteCommand("SELECT person_id,payload FROM personal_service_records WHERE feature='E06'",c,tx))
   using(var r=q.ExecuteReader())while(r.Read()) {
    var expiry=InsuranceExpiry(XElement.Parse(r.GetString(1)));
    if(expiry.Length>0)dates.Add(new KeyValuePair<string,string>(r.GetString(0),expiry));
   }
   foreach(var pair in dates)
    Exec(c,tx,"UPDATE personal_service_records SET expiry_date=@date WHERE person_id=@person AND feature='E06'",
     P("@date",pair.Value),P("@person",pair.Key));
  }

  internal static RecordPage<InsuranceDeadlineRow> GetInsuranceDeadlines(Session session,DateTime today,bool expired,int page) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    var area=session?.AreaId;
    Require(c,tx,session,"B02","read",area);
    Require(c,tx,session,"E06","personal_read",area);
    const string listSql=@"SELECT p.id,p.full_name,p.date_of_birth,s.expiry_date
      FROM personal_service_records s JOIN people p ON p.id=s.person_id AND p.area_id=s.area_id
      WHERE s.area_id=@area AND s.feature='E06' AND s.expiry_date<>''";
    string filter=expired?" AND s.expiry_date<@today":" AND s.expiry_date>=@today AND s.expiry_date<=@last";
    var parameters=new List<SQLiteParameter>{P("@area",area),P("@today",today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))};
    if(!expired)parameters.Add(P("@last",today.AddDays(30).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)));
    var result=ReadOperationalPage(c,tx,listSql+filter,"4,1",page,50,
     r=>new InsuranceDeadlineRow {
      PersonId=r.GetString(0),Name=r.GetString(1),BirthDate=DisplayLabels.Date(r.IsDBNull(2)?null:r.GetString(2)),
      ExpiryDate=DisplayLabels.Date(r.GetString(3)),State=expired?"Hết hạn":"Sắp hết hạn"
     },parameters.ToArray());
    Audit(c,tx,session.Id,"PERSONAL_SERVICE_EXPIRY_LIST_E06","area",area);
    tx.Commit();return result;
   }
  }
 }
}
