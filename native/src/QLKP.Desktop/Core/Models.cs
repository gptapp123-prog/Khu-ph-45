using System;
using System.Collections.Generic;
using System.Globalization;
namespace QLKP.Desktop.Core {
 public sealed class Session {
  public string Id {get;set;} public string Username {get;set;}
  public string Role {get;set;} public string AreaId {get;set;}
  public long PolicyVersion {get;set;} public DateTime ExpiresUtc {get;set;}
 }
 // A bounded, server-side page. The count is calculated from the same authorized query.
 public sealed class RecordPage<T> {
  public List<T> Items {get;set;} = new List<T>();
  public int PageIndex {get;set;}
  public int PageSize {get;set;}
  public long TotalCount {get;set;}
  public long TotalPages {get {return TotalCount==0?0:((TotalCount-1)/PageSize)+1;} }
 }
 public sealed class HouseholdRow {
  public string Id {get;set;} public string Code {get;set;}
  public string Head {get;set;} public string Address {get;set;}
  public int MemberCount {get;set;} public string Status {get;set;}
  public string AreaId {get;set;} public long Version {get;set;}
  public string HousingTypeCode {get;set;} public string ReviewStatusCode {get;set;}
  public string HousingType {get {return ClassificationLabels.Housing(HousingTypeCode)+(ReviewStatusCode=="DEMO_ONLY"?" [MẪU]":ReviewStatusCode=="VERIFIED"?" [ĐÃ ĐỐI CHIẾU]":" [CHỜ DUYỆT]");} }
  // Household-size grouping is DERIVED from active memberships, never hand-entered.
  public string SizeGroup {get {return MemberCount<=0 ? "Chưa có thành viên" : MemberCount==1 ? "Một người" : MemberCount==2 ? "Hai người" : MemberCount<=4 ? "3–4 người" : "Từ 5 người";} }
 }
 public sealed class PersonRow {
  public string Id {get;set;} public string FullName {get;set;}
  public string Gender {get;set;} public string BirthDate {get;set;}
  public string BirthDateDisplay {get{return DisplayLabels.Date(BirthDate);}}
  public string Relationship {get;set;} public string HouseholdCode {get;set;} public string HouseholdId {get;set;}
  public long Version {get;set;} public string ResidencyGroupCode {get;set;} public string ReviewStatusCode {get;set;}
  public string ResidencyGroup {get {return ClassificationLabels.Residency(ResidencyGroupCode)+(ReviewStatusCode=="DEMO_ONLY"?" [MẪU]":ReviewStatusCode=="VERIFIED"?" [ĐÃ ĐỐI CHIẾU]":" [CHỜ DUYỆT]");} }
  // Age is a calculated bracket for display, not a legal determination or school status.
  public string AgeGroup {get {
   DateTime dob;
   if(!DateTime.TryParseExact(BirthDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out dob))return "Chưa đủ dữ liệu";
   DateTime today=DateTime.Today;
   if(dob>today)return "Ngày sinh không hợp lệ";
   int years=today.Year-dob.Year;
   if(dob>DateTime.Today.AddYears(-years))years--;
   return years<6?"0–5 tuổi":years<18?"6–17 tuổi":years<36?"18–35 tuổi":years<60?"36–59 tuổi":"Từ 60 tuổi";
  } }
  public string Display {get { return FullName + " · " + BirthDate + " · ID: " + (Id==null?"":Id.Substring(Math.Max(0,Id.Length-8))); } }
 }
 public sealed class AccountRow {
  public string Id {get;set;} public string Username {get;set;}
  public string Role {get;set;} public string AreaId {get;set;}
  public string Status {get;set;}
  public string RoleDisplay {get{return DisplayLabels.Role(Role);}}
 }
 public sealed class AuditRow {
  public string Timestamp {get;set;} public string Actor {get;set;}
  public string EventCode {get;set;} public string EntityType {get;set;} public string Result {get;set;}
  public string TimestampDisplay {get{return DisplayLabels.UtcStamp(Timestamp);}}
 }
 public sealed class ResidencyChangeRow {
  public string Id {get;set;} public string Timestamp {get;set;}
  public string TimestampDisplay {get{return DisplayLabels.UtcStamp(Timestamp);}}
  public string PersonName {get;set;} public string FromCode {get;set;} public string ToCode {get;set;}
  public string Relationship {get;set;} public string Reason {get;set;}
  public string PersonId {get;set;} public string FromHouseholdId {get;set;} public string ToHouseholdId {get;set;}
 }
 public sealed class DataQualityIssue {
  public string Kind {get;set;} public string Subject {get;set;}
  public string TargetId {get;set;} public string TargetFeature {get;set;}
  public string Severity {get;set;} public string Finding {get;set;}
 }
 public sealed class Feature {
  public string Code {get;set;} public string Name {get;set;}
  public override string ToString() { return Code+" · "+Name; }
 }
}
