using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QLKP.Desktop.Core {
 internal static class NeighborhoodRegression {
  internal static void Run(Session root,Session admin,Session denied,string personId,string folder,TestRunRecorder journal) {
   journal.BeginStep("NEIGHBORHOOD_WORKBOOK_ATOMIC_IMPORT");
   string tasksPath=Path.Combine(folder,"neighborhood-tasks.xlsx");
   XlsxWorkbook.Save(tasksPath,new Dictionary<string,IEnumerable<object[]>> {
    {"Công việc",new object[][]{
      new object[]{"STT","Ngày giao","Công việc","Lĩnh vực","Người phụ trách","Địa bàn (xóm/thôn)","Hạn hoàn thành","Mức độ ưu tiên","Trạng thái","Kết quả thực hiện","Ghi chú"},
      new object[]{1,"01/11/2020","Việc kiểm thử đã quá hạn","Môi trường","Tổ trưởng","Tổ A","2020-11-07","Cao","Đang thực hiện","",""},
      new object[]{2,"02/11/2020","Việc kiểm thử hoàn thành","An sinh","","Tổ A","2020-11-08","Trung bình","Hoàn thành","Đã xác nhận",""},
      new object[]{3}
     }}
   });
   var tasks=DataStore.ParseNeighborhoodWorkbook("A02",tasksPath,null);
   if(tasks.Count!=2||tasks[0].Record.Fields["source_assignee"]!="Tổ trưởng"||tasks[1].Status!="Hoàn thành nội bộ")
    throw new InvalidDataException("Không đọc đúng dòng nghiệp vụ Excel.");
   Reject<UnauthorizedAccessException>(()=>DataStore.CheckOrCommitNeighborhoodWorkbook(denied,"A02",tasks,true));
   DataStore.CheckOrCommitNeighborhoodWorkbook(admin,"A02",tasks,false);
   if(DataStore.GetModulePage(admin,"A02",tasks[0].Code,0).TotalCount!=0)
    throw new InvalidDataException("Xem trước đã ghi dữ liệu.");
   tasks[1].Record.Fields["result"]="";
   Reject<InvalidOperationException>(()=>DataStore.CheckOrCommitNeighborhoodWorkbook(admin,"A02",tasks,true));
   if(DataStore.GetModulePage(admin,"A02",tasks[0].Code,0).TotalCount!=0)
    throw new InvalidDataException("Lô lỗi chưa hoàn tác toàn bộ.");
   tasks[1].Record.Fields["result"]="Đã xác nhận";
   DataStore.CheckOrCommitNeighborhoodWorkbook(admin,"A02",tasks,true);
   Reject<InvalidOperationException>(()=>DataStore.CheckOrCommitNeighborhoodWorkbook(admin,"A02",tasks,true));
   var task=DataStore.GetModulePage(admin,"A02",tasks[0].Code,0,50,true).Items.Single();
   if(task.TaskDueState!="Quá hạn")throw new InvalidDataException("A05 chưa nhận đúng quá hạn.");
   var report=DataStore.GetReportPage(admin,"H04","2020-12-01","2020-12-31","A02","",0,50);
   if(!report.Items.Any(row=>row.RecordId==task.Id)||report.Items.Any(row=>row.Code==tasks[1].Code))
    throw new InvalidDataException("H04 chưa xử lý đúng tồn đọng kỳ trước.");

   string projectsPath=Path.Combine(folder,"neighborhood-projects.xlsx");
   XlsxWorkbook.Save(projectsPath,new Dictionary<string,IEnumerable<object[]>> {
    {"Du an-cong trinh",new object[][]{
      new object[]{"Ma DA","Ten du an","Dia diem","Chu dau tu","Ngay khoi cong","Ngay hoan thanh (KH)","Tien do (%)","Ngan sach (VND)","Da chi (VND)","Trang thai"},
      new object[]{"TEST-DA-Excel","Công trình kiểm thử","Địa chỉ nguồn","Đơn vị mẫu","01/03/2026","01/12/2026",45,1000,100,"Dang thi cong"}
     }}
   });
   var projects=DataStore.ParseNeighborhoodWorkbook("B05",projectsPath,"Chưa phân loại");
   DataStore.CheckOrCommitNeighborhoodWorkbook(admin,"B05",projects,true);
   string projectId=DataStore.GetModulePage(admin,"B05","TEST-DA-Excel",0).Items.Single().Id;
   var project=DataStore.GetModuleRecord(admin,"B05",projectId);
   if(project.Fields["progress"]!="45"||project.DueDate!="2026-12-01"||project.Fields["site_address"]!="Địa chỉ nguồn")
    throw new InvalidDataException("B05 không lưu đúng tiến độ, mốc hoặc địa chỉ nguồn.");
   Reject<UnauthorizedAccessException>(()=>DataStore.TrackPublicProject(denied,projectId));
   var oldFollowup=new ModuleRecord {Code="LEGACY-F05",Title="Theo dõi đã có",Status="Ghi nhận",RelatedFeature="B05",RelatedId=projectId};
   oldFollowup.Fields["asset_kind"]="Công trình công cộng";
   string previousId=DataStore.SaveModuleRecord(admin,"F05",oldFollowup);
   string monitoringId=DataStore.TrackPublicProject(admin,projectId);
   if(monitoringId!=previousId)throw new InvalidDataException("F05 bỏ qua hồ sơ theo dõi đã liên kết.");
   if(DataStore.TrackPublicProject(admin,projectId)!=monitoringId||
    DataStore.GetModuleRecord(admin,"F05",monitoringId).RelatedId!=projectId)
    throw new InvalidDataException("F05 không giữ đúng hồ sơ B05 hoặc đã tạo trùng theo dõi.");
   Console.WriteLine("REPOSITORY_PASS NEIGHBORHOOD_WORKBOOK_ATOMIC_IMPORT");

   journal.BeginStep("PERSONAL_SERVICE_PERMISSIONS_AND_VERSION");
   var card=new PersonalServiceRecord();
   card.Values.Add("expires_on","25-11-2026");
   Reject<UnauthorizedAccessException>(()=>DataStore.ReadPersonalService(admin,personId,"E06"));
   Reject<UnauthorizedAccessException>(()=>DataStore.WritePersonalService(admin,personId,"E06",card));
   Reject<UnauthorizedAccessException>(()=>DataStore.ReadPersonalService(root,"wrong-person","E06"));
   DataStore.WritePersonalService(root,personId,"E06",card);
   Reject<UnauthorizedAccessException>(()=>DataStore.GetInsuranceDeadlines(admin,new DateTime(2026,11,16),false,0));
   if(DataStore.GetInsuranceDeadlines(root,new DateTime(2026,10,26),false,0).Items.Single().PersonId!=personId||
    DataStore.GetInsuranceDeadlines(root,new DateTime(2026,10,25),false,0).TotalCount!=0||
    DataStore.GetInsuranceDeadlines(root,new DateTime(2026,11,25),true,0).TotalCount!=0||
    DataStore.GetInsuranceDeadlines(root,new DateTime(2026,11,26),true,0).Items.Single().PersonId!=personId)
    throw new InvalidDataException("Danh sách BHYT phân loại sai biên 30 ngày hoặc ngày hết hạn.");
   var stored=DataStore.ReadPersonalService(root,personId,"E06");
   if(stored.Version!=1||DataStore.PersonalInsuranceState(stored,new DateTime(2026,11,16))!="Sắp hết hạn (9 ngày)"||
    DataStore.PersonalInsuranceState(stored,new DateTime(2026,11,26))!="Hết hạn")
    throw new InvalidDataException("Ngày hết hạn hoặc phiên bản BHYT không khớp.");
   Reject<InvalidOperationException>(()=>DataStore.WritePersonalService(root,personId,"E06",card));
   stored.Values["expires_on"]="2027-11-25";
   DataStore.WritePersonalService(root,personId,"E06",stored);
   Reject<InvalidOperationException>(()=>DataStore.WritePersonalService(root,personId,"E06",stored));
   if(DataStore.GetInsuranceDeadlines(root,new DateTime(2026,11,26),true,0).TotalCount!=0)
    throw new InvalidDataException("Danh sách BHYT vẫn giữ ngày cũ sau khi gia hạn.");
   Console.WriteLine("REPOSITORY_PASS PERSONAL_SERVICE_PERMISSIONS_AND_VERSION");
  }

  static void Reject<T>(Action action) where T:Exception {
   try {action();}
   catch(Exception ex) when(ex is T) {return;}
   throw new InvalidDataException("Thao tác kiểm thử cần bị từ chối.");
  }
 }
}
