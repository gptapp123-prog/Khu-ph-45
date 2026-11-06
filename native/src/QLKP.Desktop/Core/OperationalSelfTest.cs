using System;
using System.IO;
using System.Linq;
namespace QLKP.Desktop.Core {
 public static class OperationalSelfTest {
  // Runs against the actual C# repository and SQLite provider, inside SelfTest's isolated DB.
  // This is repository execution coverage; it does not certify UI, OCR or Android workflows.
  public static void Run(Session actor,Session forbidden,TestRunRecorder journal=null) {
   foreach(var spec in ModuleCatalog.Editable) {
    if(journal!=null)journal.BeginStep("MODULE_"+spec.Code);
    var draft=new ModuleRecord{Code="OPER-"+spec.Code,Title="[MẪU] kiểm thử "+spec.Code,Status=spec.Statuses[0]};
    foreach(var field in spec.Fields.Where(x=>x.Required)) {
     string value=field.Type=="date"?"2026-01-01":field.Type=="time"?"08:00":
      field.Type=="account"?actor.Id:field.Type.StartsWith("choice:",StringComparison.Ordinal)?field.Type.Substring(7).Split('|')[0]:
      new[]{"number","nonnegative","percentage"}.Contains(field.Type)?"1":"[MẪU]";
     draft.Fields[field.Key]=value;
    }
    string id=DataStore.SaveModuleRecord(actor,spec.Code,draft);
    var saved=DataStore.GetModuleRecord(actor,spec.Code,id);
    if(saved.Id!=id||saved.Version!=1)throw new InvalidDataException(spec.Code+": tạo/đọc sai ID hoặc phiên bản.");
    if(!DataStore.GetModulePage(actor,spec.Code,draft.Code,0).Items.Any(x=>x.Id==id))
     throw new InvalidDataException(spec.Code+": không tìm được hồ sơ vừa tạo.");
    if(!DataStore.GetModulePage(actor,spec.Code,"kiểm thử",0).Items.Any(x=>x.Id==id))
     throw new InvalidDataException(spec.Code+": tra cứu có dấu không khớp chỉ mục chuẩn hóa.");
    saved.Title+=" đã sửa";DataStore.SaveModuleRecord(actor,spec.Code,saved,saved.Version);
    var current=DataStore.GetModuleRecord(actor,spec.Code,id);
    if(current.Version!=2||current.Title!=saved.Title||DataStore.GetModuleHistory(actor,spec.Code,id).Count!=1)
     throw new InvalidDataException(spec.Code+": cập nhật/lịch sử sai.");
    bool stale=false,denied=false;
    try{DataStore.SaveModuleRecord(actor,spec.Code,saved,1);}catch(InvalidOperationException){stale=true;}
    try{DataStore.GetModuleRecord(forbidden,spec.Code,id);}catch(UnauthorizedAccessException){denied=true;}
    if(!stale||!denied)throw new InvalidDataException(spec.Code+": chưa chặn phiên bản cũ/không quyền.");
    Console.WriteLine("REPOSITORY_PASS "+spec.Code+" create/read/search/update/history/stale/deny");
   }
   if(journal!=null)journal.BeginStep("SYSTEM_PAGING");
   var audit=DataStore.GetAuditPage(actor,0);
   if(audit.TotalCount==0||audit.Items.Count==0)throw new InvalidDataException("I03: không có audit sau các phép ghi.");
   var quality=DataStore.GetDataQualityPage(actor,0);
   if(quality.PageSize!=50||quality.Items.Count>50)throw new InvalidDataException("I08: kích thước trang sai.");
   var changes=DataStore.GetResidencyChangePage(actor,"",0);
   if(changes.Items.Count>50)throw new InvalidDataException("B03: kích thước trang sai.");
   Console.WriteLine("REPOSITORY_PASS I03 I08 scoped/paged");
  }
 }
}
