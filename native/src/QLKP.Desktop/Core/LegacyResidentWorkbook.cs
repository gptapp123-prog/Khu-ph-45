using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QLKP.Desktop.Core {
 internal sealed class LegacyResidentConversion {
  readonly Dictionary<string,IEnumerable<object[]>> sheets;
  internal LegacyResidentConversion(Dictionary<string,IEnumerable<object[]>> sheets,string summary) {
   this.sheets=sheets;Summary=summary;
  }
  internal string Summary {get;}
  internal void Save(string path) {XlsxWorkbook.Save(path,sheets);}
 }

 public static partial class DataStore {
  static readonly string[] LegacySheets={"HoGiaDinh","NhanKhau","ThanhVienHo"};
  sealed class LegacyRow {
   internal string Sheet;
   internal int Line;
   internal Dictionary<string,string> Fields;
   internal string Value(string column) {return GetIntake(Fields,column);}
   internal string Required(string column) {
    string value=Value(column);
    if(value.Length==0)throw Error("Thiếu giá trị ở cột "+column+".");
    return value;
   }
   internal InvalidDataException Error(string message) {
    return new InvalidDataException(Sheet+" · dòng dữ liệu "+Line+" · "+message);
   }
  }
  static List<LegacyRow> LegacyRows(string sheet,string raw,params string[] required) {
   var table=ReadIntakeTable(raw,true,64);
   var headers=table[0];
   if(headers.Any(String.IsNullOrWhiteSpace)||headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=headers.Length||
      required.Any(key=>!headers.Contains(key,StringComparer.OrdinalIgnoreCase)))
    throw new InvalidDataException(sheet+" · thiếu cột bắt buộc hoặc có tiêu đề trùng. Cần "+String.Join(", ",required)+".");
   return table.Skip(1).Select((values,index)=>new LegacyRow {
    Sheet=sheet,Line=index+1,Fields=headers.Select((name,column)=>new {name,column})
     .ToDictionary(item=>item.name,item=>values[item.column],StringComparer.OrdinalIgnoreCase)
   }).ToList();
  }
  static Dictionary<string,LegacyRow> LegacyIndex(IEnumerable<LegacyRow> rows,string column) {
   var index=new Dictionary<string,LegacyRow>(StringComparer.Ordinal);
   foreach(var row in rows) {
    string key=WorkbookHouseholdKey(row.Required(column));
    if(index.ContainsKey(key))throw row.Error("Mã lặp ở cột "+column+".");
    index.Add(key,row);
   }
   return index;
  }
  static int CountOmitted(IEnumerable<LegacyRow> rows,params string[] used) {
   return rows.Sum(row=>row.Fields.Count(cell=>!String.IsNullOrWhiteSpace(cell.Value)&&
    !used.Contains(cell.Key,StringComparer.OrdinalIgnoreCase)));
  }
  internal static LegacyResidentConversion ConvertLegacyResidentWorkbook(string path) {
   string[] excluded, populatedExcluded;
   var sheets=XlsxIntakeReader.ReadLegacySheets(path,LegacySheets,out excluded,out populatedExcluded);
   var households=LegacyRows(LegacySheets[0],sheets[0],"MaHo","MaChuHo","DiaChiDayDu");
   var residents=LegacyRows(LegacySheets[1],sheets[1],"MaNhanKhau","HoTen","NgaySinh","GioiTinh");
   var memberships=LegacyRows(LegacySheets[2],sheets[2],"MaHo","MaNhanKhau","QuanHeVoiChuHo","DenNgay","TrangThaiQuanHe");
   if(households.Count==0||residents.Count==0||memberships.Count==0||residents.Count>IntakeMaxRows)
    throw new InvalidDataException("Cần dữ liệu ở cả ba trang, với tối đa 5.000 nhân khẩu.");
   var housesByKey=LegacyIndex(households,"MaHo");
   var peopleByKey=LegacyIndex(residents,"MaNhanKhau");
   var activeByPerson=new Dictionary<string,LegacyRow>(StringComparer.Ordinal);
   var active=new List<LegacyRow>();
   foreach(var membership in memberships) {
    string status=NormalizeSearch(membership.Value("TrangThaiQuanHe"));
    bool ended=membership.Value("DenNgay").Length!=0||status=="da ket thuc";
    if(status=="hieu luc"&&ended)throw membership.Error("Quan hệ Hiệu lực có ngày kết thúc.");
    if(ended||status=="cho xac nhan")continue;
    if(status.Length!=0&&status!="hieu luc")throw membership.Error("Trạng thái quan hệ chưa được hỗ trợ.");
    string household=WorkbookHouseholdKey(membership.Required("MaHo"));
    string person=WorkbookHouseholdKey(membership.Required("MaNhanKhau"));
    LegacyRow resident;
    if(!housesByKey.ContainsKey(household)||!peopleByKey.TryGetValue(person,out resident))
     throw membership.Error("Không tìm thấy mã hộ hoặc mã nhân khẩu tương ứng.");
    string currentHousehold=resident.Value("MaHoHienTai");
    if(currentHousehold.Length!=0&&WorkbookHouseholdKey(currentHousehold)!=household)
     throw membership.Error("Mã hộ hiện tại ở NhanKhau khác mã hộ trong ThanhVienHo.");
    if(activeByPerson.ContainsKey(person))throw membership.Error("Nhân khẩu có nhiều quan hệ hộ đang hiệu lực.");
    activeByPerson.Add(person,membership);
    active.Add(membership);
   }

   var headKeys=new HashSet<string>(StringComparer.Ordinal);
   var householdRows=new List<object[]>{IntakeHeaders("CREATE_HOUSEHOLD").Concat(new[]{"Ngày sinh","Giới tính"}).ToArray()};
   foreach(var household in households) {
    string householdCode=household.Required("MaHo");
    string headKey=WorkbookHouseholdKey(household.Required("MaChuHo"));
    LegacyRow head, membership;
    if(!peopleByKey.TryGetValue(headKey,out head)||!activeByPerson.TryGetValue(headKey,out membership)||
       WorkbookHouseholdKey(membership.Required("MaHo"))!=WorkbookHouseholdKey(householdCode)||
       NormalizeSearch(membership.Required("QuanHeVoiChuHo"))!="chu ho"||!headKeys.Add(headKey))
     throw household.Error("Mã chủ hộ phải chỉ tới nhân khẩu có quan hệ Chủ hộ đang hiệu lực trong chính hộ này.");
    householdRows.Add(new object[]{householdCode,household.Required("DiaChiDayDu"),head.Required("HoTen"),head.Value("NgaySinh"),head.Value("GioiTinh")});
   }

   var memberRows=new List<object[]>{IntakeHeaders("ADD_MEMBER")};
   foreach(var resident in residents) {
    string person=WorkbookHouseholdKey(resident.Required("MaNhanKhau"));
    LegacyRow membership;
    if(!activeByPerson.TryGetValue(person,out membership))
     throw resident.Error("Thiếu quan hệ hộ hiện hành đã xác nhận.");
    if(headKeys.Contains(person))continue;
    string relation=membership.Required("QuanHeVoiChuHo");
    if(NormalizeSearch(relation)=="chu ho")throw membership.Error("Quan hệ Chủ hộ không khớp với MaChuHo của hộ.");
    var household=housesByKey[WorkbookHouseholdKey(membership.Required("MaHo"))];
    memberRows.Add(new object[]{household.Required("MaHo"),resident.Required("HoTen"),resident.Value("NgaySinh"),resident.Value("GioiTinh"),relation});
   }

   // Apply the current intake rules before exposing an output that can be imported.
   ValidateLegacyOutput("CREATE_HOUSEHOLD",householdRows);
   if(memberRows.Count>1)ValidateLegacyOutput("ADD_MEMBER",memberRows);
   int omitted=CountOmitted(households,"MaHo","MaChuHo","DiaChiDayDu")+
    CountOmitted(residents,"MaNhanKhau","HoTen","NgaySinh","GioiTinh","MaHoHienTai")+
    CountOmitted(active,"MaHo","MaNhanKhau","QuanHeVoiChuHo","DenNgay","TrangThaiQuanHe");
   var output=new Dictionary<string,IEnumerable<object[]>>{
    {WorkbookSheets[0],householdRows},{WorkbookSheets[1],memberRows},{WorkbookSheets[2],new object[][]{IntakeHeaders("ENRICH_PERSON")}}
   };
   string summary="Sẽ tạo mẫu nhập cho "+households.Count+" hộ và "+residents.Count+" người, gồm "+headKeys.Count+" chủ hộ.\n"+
    "Bỏ qua "+(memberships.Count-active.Count)+" quan hệ đã kết thúc hoặc chờ xác nhận và "+omitted+" ô có dữ liệu ngoài phạm vi ánh xạ trong ba trang nguồn.\n"+
    "Các trang ngoài phạm vi ("+excluded.Length+"): "+(excluded.Length==0?"không có":String.Join(", ",excluded))+".\n"+
    "Trang nghiệp vụ ngoài phạm vi có dữ liệu: "+(populatedExcluded.Length==0?"không có":String.Join(", ",populatedExcluded))+".\n"+
    "Chỉ chuyển mã hộ, địa chỉ, họ tên, ngày sinh, giới tính và quan hệ hiện hành. Các mã nhân khẩu v2.1 dùng để đối chiếu. Khi nhập, kho sẽ tạo ID mới.";
   return new LegacyResidentConversion(output,summary);
  }

  static void ValidateLegacyOutput(string mode,List<object[]> rows) {
   string text=String.Join("\n",rows.Select(row=>String.Join("\t",row.Select(cell=>
    "\""+Convert.ToString(cell).Replace("\"","\"\"")+"\""))));
   var parsed=ParseIntakeRows(mode,text);
   foreach(var row in parsed) {
    if(row["household_code"].Length>40||GetIntake(row,"head").Length>200||GetIntake(row,"full_name").Length>200)
     throw new InvalidDataException("Mã hộ hoặc họ tên vượt giới hạn nhập của QLKP.");
   }
  }
 }
}
