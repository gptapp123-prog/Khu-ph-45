using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QLKP.Desktop.Core {
 internal sealed class WorkbookResidenceDraft {
  internal string Key,Kind,Address,Start,End;
  internal IntakePreviewRow Preview;
 }
 public static partial class DataStore {
  internal const string WorkbookResidenceSheet="Lịch sử cư trú";
  static readonly string[] WorkbookResidenceHeaders={"Mã nguồn v2.1","Loại cư trú","Địa chỉ","Từ ngày","Đến ngày"};

  static void RequireWorkbookResidence(SQLiteConnection c,SQLiteTransaction tx,Session session) {
   Require(c,tx,session,"B02","read",session.AreaId);
   Require(c,tx,session,"B03","read",session.AreaId);
   Require(c,tx,session,"B03","create",session.AreaId);
  }
  static string WorkbookResidenceKind(string input) {
   switch(NormalizeSearch(input).Replace('_',' ')) {
    case "thuong tru":return "thuong_tru";
    case "tam tru":return "tam_tru";
    default:throw new InvalidDataException("Chỉ hỗ trợ loại Thường trú hoặc Tạm trú.");
   }
  }
  static string WorkbookResidenceDate(string input) {
   if(input.Length==0)return "";
   DateTime date;
   if(!DateTime.TryParseExact(input,new[]{"yyyy-MM-dd","dd-MM-yyyy","dd/MM/yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out date))
    throw new InvalidDataException("Ngày cư trú không hợp lệ.");
   return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  }
  static WorkbookResidenceDraft[] ParseWorkbookResidence(string text) {
   if(text==null)return new WorkbookResidenceDraft[0];
   var table=ReadIntakeTable(text,true);
   if(!table[0].SequenceEqual(WorkbookResidenceHeaders,StringComparer.Ordinal))
    throw new InvalidDataException("Trang Lịch sử cư trú sai tiêu đề.");
   var periods=new List<WorkbookResidenceDraft>();
   for(int i=1;i<table.Count;i++) {
    var row=table[i];
    int line=i+1;
    try {
     if(String.IsNullOrWhiteSpace(row[0])||row[0].Any(Char.IsControl))
      throw new InvalidDataException("Thiếu mã nguồn v2.1 hợp lệ.");
     string kind=WorkbookResidenceKind(row[1]),start=WorkbookResidenceDate(row[3]),end=WorkbookResidenceDate(row[4]);
     string address=ValidateResidencePeriod(kind,row[2],start,end);
     periods.Add(new WorkbookResidenceDraft{Key=WorkbookHouseholdKey(row[0]),Kind=kind,Address=address,Start=start,End=end,
      Preview=new IntakePreviewRow{SheetName=WorkbookResidenceSheet,LineNumber=line,TargetKey=row[0],
       Operation=kind=="thuong_tru"?"Thêm kỳ thường trú":"Thêm kỳ tạm trú",Notice=start+" → "+(end.Length==0?"Chưa ghi ngày kết thúc":end)}});
    } catch(InvalidOperationException ex){throw new InvalidDataException("Lịch sử cư trú · dòng "+line+" · "+ex.Message,ex);}
    catch(InvalidDataException ex){throw new InvalidDataException("Lịch sử cư trú · dòng "+line+" · "+ex.Message,ex);}
   }
   var ordered=periods.OrderBy(row=>row.Key,StringComparer.Ordinal).ThenBy(row=>row.Kind,StringComparer.Ordinal)
    .ThenBy(row=>row.Start,StringComparer.Ordinal).ToArray();
   for(int i=1;i<ordered.Length;i++) {
    var old=ordered[i-1];var current=ordered[i];
    if(old.Key==current.Key&&old.Kind==current.Kind&&(old.Start==current.Start||String.CompareOrdinal(old.End,current.Start)>0))
     throw new InvalidDataException("Lịch sử cư trú · dòng "+current.Preview.LineNumber+" · Kỳ cùng loại trùng hoặc chồng lấn.");
   }
   return ordered;
  }
 }
}
