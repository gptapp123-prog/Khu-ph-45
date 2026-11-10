using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace QLKP.Desktop.Core {
 internal sealed class WorkbookIntakePreview {
  internal WorkbookIntakePreview(IntakePreview[] batches,WorkbookResidenceDraft[] residence) {Batches=batches;Residence=residence;}
  internal IntakePreview[] Batches {get;}
  internal WorkbookResidenceDraft[] Residence {get;}
  internal IEnumerable<IntakePreviewRow> Rows {get {return Batches.SelectMany(batch=>batch.Rows).Concat(Residence.Select(row=>row.Preview));}}
  internal int Count {get {return Batches.Sum(batch=>batch.Rows.Count)+Residence.Length;}}
  internal bool HasCandidateWarnings {get {return Batches.Any(batch=>batch.HasCandidateWarnings);}}
 }
 public static partial class DataStore {
  static readonly string[] WorkbookSheets={"Hộ và chủ hộ","Thêm nhân khẩu","Bổ sung nhân khẩu"};
  static readonly string[] WorkbookModes={"CREATE_HOUSEHOLD","ADD_MEMBER","ENRICH_PERSON"};
  const string WorkbookSource="XLSX_LOCAL";

  internal static void SaveIntakeTemplate(string path) {
   XlsxWorkbook.Save(path,WorkbookSheets.Select((name,index)=>new {name,index})
    .ToDictionary(sheet=>sheet.name,sheet=>(IEnumerable<object[]>)new object[][]{IntakeHeaders(WorkbookModes[sheet.index])}));
  }

  static string WorkbookHouseholdKey(string code) {
   // Match the ASCII-only case folding of SQLite COLLATE NOCASE.
   return new string(code.Select(ch=>ch>='a'&&ch<='z'?(char)(ch-'a'+'A'):ch).ToArray());
  }
  static string WorkbookRequestId(string area,string digest,string mode) {
   return IntakeDigest("RESIDENT_WORKBOOK_V1",area,digest+mode).Substring(0,32).ToLowerInvariant();
  }
  static bool WorkbookBatchExists(SQLiteConnection c,SQLiteTransaction tx,string id) {
   return Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM intake_batches WHERE request_id=@id",P("@id",id)))!=0;
  }
  static IntakePreview ValidateWorkbookSheet(SQLiteConnection c,SQLiteTransaction tx,Session session,
   int index,string label,string text,HashSet<string> planned,bool allowSourceKeys) {
   try {
    var batch=PreviewIntake(c,tx,session,WorkbookModes[index],WorkbookSource,label,text,planned,allowSourceKeys);
    foreach(var row in batch.Rows)row.SheetName=WorkbookSheets[index];
    return batch;
   } catch(InvalidDataException ex){throw new InvalidDataException(WorkbookSheets[index]+" · "+ex.Message,ex);}
   catch(InvalidOperationException ex){throw new InvalidOperationException(WorkbookSheets[index]+" · "+ex.Message,ex);}
  }
  internal static WorkbookIntakePreview PreviewWorkbookIntake(Session session,string path) {
   if(session==null||String.IsNullOrEmpty(session.AreaId))throw new UnauthorizedAccessException("Cần tài khoản thuộc khu phố.");
   string label=IntakeSourceLabel(path);
   var texts=XlsxIntakeReader.ReadWorkbook(path,WorkbookSheets,WorkbookResidenceSheet);
   var residence=ParseWorkbookResidence(texts[WorkbookSheets.Length]);
   var populated=new List<int>();
   int count=0;
   for(int i=0;i<WorkbookSheets.Length;i++) {
    var table=ReadIntakeTable(texts[i],true);
    count+=table.Count-1;
    if(table.Count>1)populated.Add(i);
   }
   if(count<1||count+residence.Length>IntakeMaxRows)throw new InvalidDataException("Sổ cần từ 1 đến 5.000 dòng dữ liệu tính chung các trang.");
   string digest=String.Concat(populated.Select(i=>IntakeDigest(WorkbookModes[i],WorkbookSource,texts[i])));
   if(residence.Length>0) {
    var sourceKeys=new HashSet<string>(StringComparer.Ordinal);
    foreach(int i in populated.Where(index=>index<2))foreach(var row in ParseIntakeRows(WorkbookModes[i],texts[i],true)) {
     string key=GetIntake(row,"legacy_person_key");
     if(key.Length>0&&!sourceKeys.Add(WorkbookHouseholdKey(key)))
      throw new InvalidDataException("Mã nguồn v2.1 bị trùng trong sổ nhập.");
    }
    if(residence.Any(row=>!sourceKeys.Contains(row.Key)))throw new InvalidDataException("Có kỳ cư trú thiếu người tương ứng trong các trang tạo hồ sơ.");
    digest+=IntakeDigest(WorkbookResidenceSheet,WorkbookSource,texts[WorkbookSheets.Length]);
   }
   var planned=new HashSet<string>(StringComparer.Ordinal);
   var batches=new List<IntakePreview>();
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    if(residence.Length>0)RequireWorkbookResidence(c,tx,session);
    foreach(int i in populated)RequireIntakeMode(c,tx,session,WorkbookModes[i]);
    foreach(int i in populated) {
     string id=WorkbookRequestId(session.AreaId,digest,WorkbookModes[i]);
     if(WorkbookBatchExists(c,tx,id))throw new InvalidOperationException("Sổ với dữ liệu này đã được nhập vào khu phố.");
     var batch=ValidateWorkbookSheet(c,tx,session,i,label,texts[i],planned,residence.Length>0);
     batch.RequestId=id;
     batches.Add(batch);
     if(i==0)planned.UnionWith(batch.Rows.Select(row=>WorkbookHouseholdKey(row.TargetKey)));
    }
    tx.Commit();
   }
   return new WorkbookIntakePreview(batches.ToArray(),residence);
  }
  internal static int CommitWorkbookIntake(Session session,WorkbookIntakePreview preview,bool allowDuplicates) {
   if(session==null||preview==null||preview.Batches.Length==0||preview.Batches.Any(batch=>batch.ActorId!=session.Id||batch.AreaId!=session.AreaId))
    throw new UnauthorizedAccessException("Bản xem trước sổ không thuộc tài khoản và khu phố hiện tại.");
   if(preview.HasCandidateWarnings&&!allowDuplicates)
    throw new InvalidOperationException("Cần xác nhận đã đối chiếu các trường hợp nghi trùng.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    bool includesResidence=preview.Residence.Length>0;
    if(includesResidence)RequireWorkbookResidence(c,tx,session);
    foreach(var batch in preview.Batches)RequireIntakeMode(c,tx,session,batch.Mode);
    int recorded=preview.Batches.Count(batch=>WorkbookBatchExists(c,tx,batch.RequestId));
    if(recorded!=0&&recorded!=preview.Batches.Length)
     throw new InvalidOperationException("Một phần lô đã có trong kho. Kiểm tra lịch sử trước khi tiếp tục.");
    if(recorded==0) {
     var planned=new HashSet<string>(StringComparer.Ordinal);
     foreach(var batch in preview.Batches) {
      int i=Array.IndexOf(WorkbookModes,batch.Mode);
      var current=ValidateWorkbookSheet(c,tx,session,i,batch.SourceLabel,batch.Payload,planned,includesResidence);
      if(current.HasCandidateWarnings&&!allowDuplicates)
       throw new InvalidOperationException("Có trường hợp nghi trùng mới. Hãy xem trước lại.");
      if(i==0)planned.UnionWith(current.Rows.Select(row=>WorkbookHouseholdKey(row.TargetKey)));
     }
    }
    var newPeople=includesResidence?new Dictionary<string,string>(StringComparer.Ordinal):null;
    int count=preview.Batches.Sum(batch=>CommitIntake(c,tx,session,batch,allowDuplicates,newPeople));
    if(recorded==0)foreach(var residence in preview.Residence)
     AddResidencePeriod(c,tx,session,newPeople[residence.Key],residence.Kind,residence.Address,residence.Start,residence.End);
    tx.Commit();return count+preview.Residence.Length;
   }
  }
 }
}
