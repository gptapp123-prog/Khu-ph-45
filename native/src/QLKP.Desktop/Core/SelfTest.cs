using System;
using System.IO;
namespace QLKP.Desktop.Core {
 public static class SelfTest {
  public static void Run() {
   using(var journal=new TestRunRecorder("windows_self_test","repository_runtime")) {
    journal.BeginStep("ISOLATE_STORAGE");
    try{RunIsolated(journal);journal.Complete("PASS");Console.WriteLine("SELF_TEST_PASS");Console.WriteLine("TEST_HISTORY_ID "+journal.Report.Id);}
    catch(Exception ex){journal.Complete("FAIL",ex.GetType().Name);throw;}
   }
  }
  static void TestOfflineTaskExchange(Session root,Session admin,Session denied,string adminSecret,string folder) {
   DataStore.SetAccountRights(root,admin.Id,new[]{"A02:read","A02:create","A02:update","I06:read","I06:export"});
   admin=DataStore.Login("test_admin",adminSecret);
   const string password="TaskFixture_2026!";
   var utf8=System.Text.Encoding.UTF8;
   var resultMagic=utf8.GetBytes("QLKPTR01");
   var fixture=Convert.FromBase64String("UUxLUFRSMDEAAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobAAS68Gikn9CL+bSFKwQvg1jgHnX3eK91woBVNqyiA4Dupaz/rO327gaGCSFgBzv4cV89WYQXgt9zsHw7ja62bnb3RkmRDbYmdDlhAUs=");
   string expectedResult="Đã kiểm tra.";
   string packed=Convert.ToBase64String(utf8.GetBytes(expectedResult));
   var fixturePlain=OfflineCatalogTransfer.Unseal(resultMagic,fixture,password,16384);
   if(utf8.GetString(fixturePlain)!="1234567890124abc8abc123456789012\n"+packed)
    throw new InvalidDataException("A02 đọc sai định dạng kết quả Android.");
   var draft=new ModuleRecord{Code="TASK-EXCHANGE",Title="Nhiệm vụ giả lập",Status="Mới",Notes="Nội dung chỉ ở PC"};
   draft.Fields["assignee_id"]=admin.Id;
   draft.Fields["deadline"]="2026-11-10";
   string id=DataStore.SaveModuleRecord(admin,"A02",draft);
   var task=DataStore.GetModuleRecord(admin,"A02",id);
   string dispatch=Path.Combine(folder,"test.qlkptask");
   string resultPath=Path.Combine(folder,"test.qlkpresult");
   DataStore.ExportOfflineTask(admin,id,task.Version,"Kiểm tra đèn đường.",dispatch,password);
   var packet=File.ReadAllBytes(dispatch);
   string[] fields=utf8.GetString(OfflineCatalogTransfer.Unseal(utf8.GetBytes("QLKPTD01"),packet,password,16384)).Split('\n');
   if(fields.Length!=4||utf8.GetString(Convert.FromBase64String(fields[1]))!=draft.Code||
      utf8.GetString(Convert.FromBase64String(fields[2]))!="10-11-2026"||
      utf8.GetString(Convert.FromBase64String(fields[3]))!="Kiểm tra đèn đường.")
    throw new InvalidDataException("A02 xuất thừa hoặc thiếu dữ liệu giao việc.");
   var result=OfflineCatalogTransfer.Seal(resultMagic,utf8.GetBytes(fields[0]+"\n"+packed),password);
   File.WriteAllBytes(resultPath,result);
   RequireTaskRejection<UnauthorizedAccessException>(()=>DataStore.PreviewOfflineTaskResult(denied,resultPath,password));
   RequireTaskRejection<InvalidDataException>(()=>DataStore.PreviewOfflineTaskResult(admin,resultPath,"wrong_password_123"));
   var changed=(byte[])result.Clone();changed[changed.Length-1]^=1;
   File.WriteAllBytes(resultPath,changed);
   RequireTaskRejection<InvalidDataException>(()=>DataStore.PreviewOfflineTaskResult(admin,resultPath,password));
   File.WriteAllBytes(resultPath,result);
   var preview=DataStore.PreviewOfflineTaskResult(admin,resultPath,password);
   if(preview.Result!=expectedResult||DataStore.GetModuleRecord(admin,"A02",id).Status!="Mới")
    throw new InvalidDataException("A02 chưa đối chiếu đúng nội dung hoặc xem trước đã ghi dữ liệu.");
   task.Title="Nhiệm vụ đã chỉnh";
   DataStore.SaveModuleRecord(admin,"A02",task,task.Version);
   RequireTaskRejection<InvalidOperationException>(()=>DataStore.ConfirmOfflineTaskResult(admin,preview));

   task=DataStore.GetModuleRecord(admin,"A02",id);
   File.Delete(dispatch);
   DataStore.ExportOfflineTask(admin,id,task.Version,"Kiểm tra đèn đường.",dispatch,password);
   string token=utf8.GetString(OfflineCatalogTransfer.Unseal(utf8.GetBytes("QLKPTD01"),File.ReadAllBytes(dispatch),password,16384)).Split('\n')[0];
   File.WriteAllBytes(resultPath,OfflineCatalogTransfer.Seal(resultMagic,utf8.GetBytes(token+"\n"+packed),password));
   preview=DataStore.PreviewOfflineTaskResult(admin,resultPath,password);
   DataStore.ConfirmOfflineTaskResult(admin,preview);
   RequireTaskRejection<InvalidOperationException>(()=>DataStore.ConfirmOfflineTaskResult(admin,preview));
   var completed=DataStore.GetModuleRecord(admin,"A02",id);
   if(completed.Status!="Hoàn thành nội bộ"||completed.Fields["result"]!=expectedResult||
      completed.Fields["progress"]!="100"||completed.Notes!=draft.Notes||completed.Version!=task.Version+1||
      DataStore.GetModuleHistory(admin,"A02",id).Count!=2)
    throw new InvalidDataException("A02 không ghi đúng kết quả, trạng thái hoặc lịch sử.");
   Console.WriteLine("REPOSITORY_PASS A02 offline dispatch/result/preview/stale/replay");
  }
  static void RequireTaskRejection<T>(Action action) where T:Exception {
   try {action();}catch(T){return;}
   throw new InvalidDataException("A02 chấp nhận gói đáng lẽ phải từ chối.");
  }
  static void RunIsolated(TestRunRecorder journal) {
   string sandbox=DataStore.BeginIsolatedSelfTest();
   try {
    journal.BeginStep("STARTUP_RESOURCES");
    StartupDiagnostics.CheckApplicationFiles();
    if(DisplayLabels.Date("2026-11-06")!="06-11-2026"||DisplayLabels.IsoDate("06-11-2026")!="2026-11-06")
     throw new InvalidDataException("Định dạng ngày giao diện không chuyển đổi đúng ISO.");
    bool badDisplayDate=false;
    try{DisplayLabels.IsoDate("31-02-2026");}catch(InvalidOperationException){badDisplayDate=true;}
    if(!badDisplayDate)throw new InvalidDataException("Giao diện nhận ngày không tồn tại.");
    foreach(var asset in new[]{"qlkp.ico","qlkp-icon.png"}) {
     var resource=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/"+asset));
     if(resource==null)throw new InvalidDataException("Thiếu icon đóng gói: "+asset);
     using(var image=resource.Stream) {
      byte[] bytes;using(var copy=new MemoryStream()){image.CopyTo(copy);bytes=copy.ToArray();}
      using(var hash=System.Security.Cryptography.SHA256.Create()) {
       string actual=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
       if(asset=="qlkp-icon.png"&&actual!="8b83e11f800b43cb39c0cd3fc473330d35bb5cc979fd8577b3b8d531512ebd96")throw new InvalidDataException("Ảnh logo đóng gói bị thiếu hoặc thay đổi.");
      }
      using(var complete=new MemoryStream(bytes,false)) {
      var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(complete,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
      if(decoder.Frames.Count<1)throw new InvalidDataException("Không giải mã được icon: "+asset);
      var frame=decoder.Frames[decoder.Frames.Count-1];int stride=(frame.PixelWidth*frame.Format.BitsPerPixel+7)/8;frame.CopyPixels(new byte[stride*frame.PixelHeight],stride,0);
      }
     }
    }
    journal.BeginStep("INITIALIZE_SANDBOX");
    DataStore.Initialize();
    StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);
    if(!DataStore.IsFirstRun())throw new InvalidDataException("Kho kiểm thử không rỗng.");
    string ownerSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    string adminSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    string userSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    journal.BeginStep("ACCOUNTS_AND_RIGHTS");
    DataStore.SetupOwner("test_root", ownerSecret, "KP-TEST", "Khu phố mẫu");
    var root=DataStore.Login("test_root",ownerSecret);
    if(root==null||root.Role!="ROOT_ADMIN"||!DataStore.Can(root,"B01","read"))throw new InvalidDataException("Không bảo đảm quyền ADMIN gốc.");
    DataStore.CreateAccount(root,"test_admin",adminSecret,null);
    var admin=DataStore.Login("test_admin",adminSecret);
    if(admin==null||admin.Role!="KP_ADMIN"||!DataStore.Can(admin,"I08","read"))throw new InvalidDataException("Không tạo được ADMIN khu phố hoặc thiếu quyền I08 được cấp rõ ràng.");
    admin=DataStore.RunDefaultPolicyRegression(journal,root,admin,adminSecret);
    var expiredRoot=new Session{Id=root.Id,Username=root.Username,Role=root.Role,AreaId=root.AreaId,PolicyVersion=root.PolicyVersion,ExpiresUtc=DateTime.UtcNow.AddMinutes(-1)};
    // Explicit restrictions remain supported after the one-time default upgrade.
    var explicitRights=DataStore.GetAccountRights(root,admin.Id);
    explicitRights.AddRange(ModuleCatalog.AdditionalRights());
    DataStore.SetAccountRights(root,admin.Id,explicitRights);
    admin=DataStore.Login("test_admin",adminSecret);
    if(admin==null || !DataStore.Can(admin,"A02","create"))
     throw new InvalidDataException("Cấp trần ủy quyền nhóm A/B bằng ADMIN gốc thất bại.");
    DataStore.CreateAccount(admin,"test_user",userSecret,new []{"B01:read","B01:create","B02:read","B02:create","B02:update","B03:read","B03:create"});
    var user=DataStore.Login("test_user",userSecret);
    if(user==null||!DataStore.Can(user,"B01","create")||DataStore.Can(user,"I01","manage"))throw new InvalidDataException("Phân quyền được cấp không chính xác.");
    journal.BeginStep("HOUSEHOLDS_AND_PEOPLE");
    DataStore.AddHousehold(user,"TEST001","Địa chỉ mẫu","Người mẫu A");
    var households=DataStore.FindHouseholds(user,"TEST001");
    if(households.Count!=1||households[0].MemberCount!=1)throw new InvalidDataException("Quan hệ hộ–người không toàn vẹn.");
    // V0.8 runtime checks run on Windows against the real SQLite provider, not Python mirrors.
    DataStore.AddHousehold(user,"ACCENT01","Đường Nguyễn Huệ","Đặng Văn Minh");
    if(DataStore.FindHouseholds(user,"duong nguyen hue").Count!=1 ||
       DataStore.FindPeople(user,"dang van minh").Count!=1)
     throw new InvalidDataException("Tra cứu tiếng Việt không dấu chưa chính xác.");
    DataStore.AddMember(user,households[0].Id,"Người mẫu B","Con",null,null);
    var members=DataStore.HouseholdMembers(user,households[0].Id);
    if(members.Count!=2)throw new InvalidDataException("Không bổ sung được nhân khẩu.");
    // B02 fast-entry: preview -> atomic write to selected household ID.
    string goodMembers="Người mẫu C\t2008-08-12\tNữ\tCon\nNgười mẫu D\t\tNam\tCha/Mẹ";
    if(DataStore.CheckMembersBatch(user,households[0].Id,goodMembers)!=2)
     throw new InvalidDataException("Dự kiểm tra nhân khẩu không đúng.");
    if(DataStore.ImportMembersBatch(user,households[0].Id,goodMembers,"00000000000040008000000000000001")!=2 ||
       DataStore.HouseholdMembers(user,households[0].Id).Count!=4)
     throw new InvalidDataException("Nhập lô nhân khẩu không liên kết đúng hộ.");
    // Retransmitting an approved request with the same idempotency ID changes nothing.
    if(DataStore.ImportMembersBatch(user,households[0].Id,goodMembers,"00000000000040008000000000000001")!=2 ||
       DataStore.HouseholdMembers(user,households[0].Id).Count!=4)
     throw new InvalidDataException("Gửi lặp tạo thêm nhân khẩu.");
    bool reusedKeyDenied=false;
    try {DataStore.ImportMembersBatch(user,households[0].Id,"Người khác\t\tNam\tCon","00000000000040008000000000000001");}
    catch(InvalidOperationException){reusedKeyDenied=true;}
    if(!reusedKeyDenied)throw new InvalidDataException("Cho sử dụng mã lần gửi cũ cho nội dung mới.");
    bool badMemberBatchBlocked=false;
    try {DataStore.ImportMembersBatch(user,households[0].Id,
      "Người lô lỗi\t\tNam\tCon\nNgười giả chủ hộ\t\tNữ\tChủ hộ","00000000000040008000000000000002");}
    catch(InvalidOperationException){badMemberBatchBlocked=true;}
    if(!badMemberBatchBlocked || DataStore.HouseholdMembers(user,households[0].Id).Count!=4)
     throw new InvalidDataException("Nhập lô nhân khẩu lỗi chưa được rollback.");
    if(DataStore.FindPeople(user,"Người lô lỗi").Count!=0)
     throw new InvalidDataException("Lô lỗi để lại người mồ côi.");
    if(DataStore.GetPerson(user,members[0].Id).Id!=members[0].Id)throw new InvalidDataException("Định danh hồ sơ sai.");
    if(DataStore.GetHousehold(user,households[0].Id).Id!=households[0].Id)throw new InvalidDataException("Mở nhầm hồ sơ hộ.");
    try { DataStore.ImportBatch(user,"TEST002\tĐịa chỉ mới\tNgười mới\nTEST001\tTrùng mã\tHộ lỗi"); throw new InvalidDataException("Không chặn lô trùng mã."); }
    catch(System.Data.SQLite.SQLiteException) { }
    if(DataStore.FindHouseholds(user,"TEST002").Count!=0)throw new InvalidDataException("Import lô bị ghi một phần.");
    // V0.9 Windows runtime gate: the selected row must belong to the returned authorized page.
    var firstPage=DataStore.GetHouseholdsPage(user,"",0,10);
    if(firstPage.TotalCount<2 || firstPage.Items.Count==0 || firstPage.PageSize!=10)
     throw new InvalidDataException("B01 phân trang trả tổng số hoặc giới hạn sai.");
    if(DataStore.GetHouseholdsPage(user,"TEST001",0,10).Items.Count!=1)
     throw new InvalidDataException("B01 tra cứu theo mã hộ không chính xác.");
    if(DataStore.GetHouseholdsPage(user,"TEST001",12,10).PageIndex!=0)
     throw new InvalidDataException("B01 không điều chỉnh trang vượt giới hạn.");
    var peoplePage=DataStore.GetPeoplePage(user,"Nguoi mau",0,10);
    if(peoplePage.TotalCount<2 || peoplePage.Items.Count==0)
     throw new InvalidDataException("B02 phân trang/tìm kiếm tiếng Việt có vấn đề.");
    // V0.12 operational runtime gate: A01–A06 and B04–B07 are real, scoped CRUD/derived views.
    journal.BeginStep("OPERATIONS_AND_LINKS");
    string operatorSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_operations",operatorSecret,new [] {
     "A01:read","A02:read","A02:create","A02:update","A03:read","A03:create",
     "A04:read","A04:create","A05:read","A06:read","A06:create",
     "B01:read","B04:read","B04:create","B04:update","B05:read","B05:create",
     "B06:read","B06:create","B07:read","B07:create","I05:read"});
    var operatorUser=DataStore.Login("test_operations",operatorSecret);
    if(operatorUser==null||!DataStore.Can(operatorUser,"A02","create")||!DataStore.Can(root,"A02","read"))
     throw new InvalidDataException("Không tách đúng ADMIN gốc và nghiệp vụ điều hành.");
    var qrResult=DataStore.ParseLocalQrReference(operatorUser,"QLKP1|B01|"+households[0].Id);
    if(qrResult.Item2!=households[0].Id)throw new InvalidDataException("I05 QR nội bộ không mở đúng mã hộ.");
    var taskDraft=new ModuleRecord{Code="NV-01",Title="Rà soát Đường Nguyễn Huệ",Status="Mới",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"deadline","2020-02-02"}}};
    string taskId=DataStore.SaveModuleRecord(operatorUser,"A02",taskDraft);
    var task=DataStore.GetModuleRecord(operatorUser,"A02",taskId);
    if(task.Code!="NV-01"||DataStore.GetModulePage(operatorUser,"A02","ra soat duong",0,50).TotalCount!=1 ||
       DataStore.GetModulePage(operatorUser,"A02","",0,50,true).TotalCount!=1)
     throw new InvalidDataException("Danh sách A02, lọc không dấu hoặc A05 tồn đọng không đúng.");
    task.Status="Đang xử lý";
    DataStore.SaveModuleRecord(operatorUser,"A02",task,task.Version);
    if(DataStore.GetModuleHistory(operatorUser,"A02",taskId).Count!=1)
     throw new InvalidDataException("A02 chưa có nhật ký đổi trạng thái.");
    bool staleTaskBlocked=false;
    try{DataStore.SaveModuleRecord(operatorUser,"A02",task,task.Version);}
    catch(InvalidOperationException){staleTaskBlocked=true;}
    if(!staleTaskBlocked)throw new InvalidDataException("Không khóa phiên bản hồ sơ A02 cũ.");
    var address=new ModuleRecord{Code="DC-01",Title="Địa chỉ đã kiểm tra nội bộ",Status="Mới",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"street","Đường Mẫu"}}};
    string addressId=DataStore.SaveModuleRecord(operatorUser,"B04",address);
    var house=new ModuleRecord{Code="CT-01",Title="Công trình mẫu",Status="Mới",RelatedFeature="B04",RelatedId=addressId,
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"construction_kind","Nhà ở"}}};
    string structureId=DataStore.SaveModuleRecord(operatorUser,"B05",house);
    if(DataStore.GetModuleRecord(operatorUser,"B05",structureId).RelatedId!=addressId ||
       DataStore.FindLinkCandidates(operatorUser,"B04","DC-01").Count!=1)
     throw new InvalidDataException("B04–B05 không liên kết đúng ID.");
    if(DataStore.GetModuleOverview(operatorUser).Find(x=>x.Feature=="A02").Count!=1)
     throw new InvalidDataException("A01 không tổng hợp đúng phạm vi được cấp.");
    bool wrongAreaDenied=false;
    try{DataStore.SaveModuleRecord(operatorUser,"B05",new ModuleRecord{Code="CT-02",Title="Liên kết sai",Status="Mới",RelatedFeature="B04",RelatedId="invalid-id",
       Fields=new System.Collections.Generic.Dictionary<string,string>{{"construction_kind","Nhà ở"}}});}
    catch(InvalidOperationException){wrongAreaDenied=true;}
    if(!wrongAreaDenied)throw new InvalidDataException("Có thể tạo liên kết B05 đến ID không tồn tại.");
    if(DataStore.Can(user,"A02","read"))throw new InvalidDataException("Tài khoản dân cư tự có quyền A02.");
    // V0.13 runtime gate: scoped document/personnel modules, explicit permissions,
    // handover workflow and multi-relation integrity. All inputs are synthetic.
    journal.BeginStep("DOCUMENTS_AND_PERSONNEL");
    string clericalSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_clerical",clericalSecret,new []{
     "C01:read","C01:create","C01:update","C02:read","C02:create","C03:read","C03:create",
     "C04:read","C04:create","C05:read","C05:create","C06:read","C06:create",
     "D01:read","D01:create","D02:read","D02:create","D03:read","D03:create",
     "D04:read","D04:create","D05:read","D05:create","A02:read","A02:create"});
    var clerk=DataStore.Login("test_clerical",clericalSecret);
    if(clerk==null||!DataStore.Can(clerk,"C01","create")||DataStore.Can(user,"D02","read"))
     throw new InvalidDataException("Phân quyền C/D sai hoặc tài khoản khác tự có quyền D02.");
    var incoming=new ModuleRecord{Code="VBM-001",Title="Văn bản đến giả lập",Status="Tiếp nhận nội bộ",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"doc_no","VBM-001"},{"doc_date","2026-01-01"},{"sender","Đơn vị mẫu"}}};
    string incomingId=DataStore.SaveModuleRecord(clerk,"C01",incoming);
    if(DataStore.GetModulePage(clerk,"C01","van ban den gia lap",0,50).TotalCount!=1)
     throw new InvalidDataException("C01 không lưu hoặc tìm theo tiêu đề.");
    bool falseDispatchDenied=false;
    try{DataStore.SaveModuleRecord(clerk,"C02",new ModuleRecord{Code="VB-DI-MAU",Title="Văn bản đi mẫu",Status="Đã ghi nhận gửi"});}
    catch(InvalidOperationException){falseDispatchDenied=true;}
    if(!falseDispatchDenied)throw new InvalidDataException("C02 cho ghi đã gửi khi chưa có căn cứ.");
    var meeting=new ModuleRecord{Code="HOP-01",Title="Cuộc họp giả lập",Status="Dự kiến",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"meeting_date","2026-01-02"},{"venue","Nhà sinh hoạt mẫu"}}};
    string meetingId=DataStore.SaveModuleRecord(clerk,"C04",meeting);
    var minutes=new ModuleRecord{Code="BB-01",Title="Biên bản mẫu",Status="Đã xác nhận nội bộ",
     RelatedFeature="C04",RelatedId=meetingId,
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"minutes_date","2026-01-02"},{"secretary","Người ghi mẫu"},{"reviewer_note","Căn cứ nội bộ mẫu"}}};
    string minutesId=DataStore.SaveModuleRecord(clerk,"C05",minutes);
    if(DataStore.GetModuleRecord(clerk,"C05",minutesId).RelatedId!=meetingId)
     throw new InvalidDataException("C05 không liên kết đúng ID buổi họp.");
    bool unverifiedMemberDenied=false;
    try{DataStore.SaveModuleRecord(clerk,"D02",new ModuleRecord{Code="D02-01",Title="Hồ sơ giả",Status="Đã đối chiếu nguồn",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"full_name","Người giả lập"}}});}
    catch(InvalidOperationException){unverifiedMemberDenied=true;}
    if(!unverifiedMemberDenied)throw new InvalidDataException("D02 tự xác nhận khi thiếu nguồn đối chiếu.");
    var source=DataStore.GetModuleRecord(clerk,"C01",incomingId);
    DataStore.AddModuleRelation(clerk,"C01",incomingId,"BIEN_BAN","C05",minutesId,source.Version);
    if(!DataStore.GetModuleRelations(clerk,"C01",incomingId).Exists(x=>x.TargetId==minutesId))
     throw new InvalidDataException("Không đọc được liên kết nhiều hồ sơ theo ID.");
    bool staleLinkDenied=false;
    try{DataStore.AddModuleRelation(clerk,"C01",incomingId,"THAM_CHIEU","C04",meetingId,source.Version);}
    catch(InvalidOperationException){staleLinkDenied=true;}
    if(!staleLinkDenied)throw new InvalidDataException("Liên kết không chặn phiên bản hồ sơ cũ.");
    bool unauthorizedReadDenied=false;
    try{DataStore.GetModuleRecord(user,"D02","fake-id");}
    catch(UnauthorizedAccessException){unauthorizedReadDenied=true;}
    if(!unauthorizedReadDenied)throw new InvalidDataException("Tài khoản dân cư được truy cập hồ sơ D02.");
    // V0.14 runtime gate: E01–E07 are scoped internal coordination registers;
    // this self-test uses only synthetic records and the temporary isolated database.
    journal.BeginStep("SOCIAL_WELFARE");
    string welfareSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_welfare",welfareSecret,new []{
     "E01:read","E01:create","E01:update","E02:read","E02:create",
     "E03:read","E03:create","E04:read","E04:create",
     "E05:read","E05:create","E06:read","E06:create","E07:read","E07:create",
     "B02:read"});
    var welfare=DataStore.Login("test_welfare",welfareSecret);
    if(welfare==null || !DataStore.Can(welfare,"E01","create") || DataStore.Can(user,"E01","read") ||
       !DataStore.Can(root,"E01","read"))
     throw new InvalidDataException("Phân quyền E01–E07 không được cấp tách biệt.");
    var support=new ModuleRecord{Code="AS-MAU-01",Title="[MẪU] Hỗ trợ cộng đồng",Status="Bản nháp",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"program_kind","Hỗ trợ sinh kế"},{"received_on","2026-01-08"}}};
    string supportId=DataStore.SaveModuleRecord(welfare,"E01",support);
    if(DataStore.GetModulePage(welfare,"E01","ho tro cong dong",0,50).TotalCount!=1)
     throw new InvalidDataException("Danh sách E01 không lưu hoặc tìm kiếm không dấu được.");
    bool unsupportedTransferDenied=false;
    try{DataStore.SaveModuleRecord(welfare,"E01",new ModuleRecord{Code="AS-SAI",Title="[MẪU] Chuyển không căn cứ",Status="Đã chuyển cơ quan có thẩm quyền",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"program_kind","Khác"},{"received_on","2026-01-08"}}});}
    catch(InvalidOperationException){unsupportedTransferDenied=true;}
    if(!unsupportedTransferDenied)throw new InvalidDataException("E01 tự ghi trạng thái chuyển thiếu căn cứ.");
    var aggregate=new ModuleRecord{Code="YTE-MAU-01",Title="[MẪU] Hoạt động dân số",Status="Dự kiến",
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"activity_kind","Hoạt động dân số"},{"aggregate_count","120"}}};
    string aggregateId=DataStore.SaveModuleRecord(welfare,"E06",aggregate);
    if(DataStore.GetModuleRecord(welfare,"E06",aggregateId).Id!=aggregateId)
     throw new InvalidDataException("E06 không mở được bản ghi tổng hợp theo ID.");
    aggregate.Code="YTE-MAU-02";aggregate.RelatedFeature="B02";aggregate.RelatedId=members[0].Id;
    bool individualLinkBlocked=false;
    try{DataStore.SaveModuleRecord(welfare,"E06",aggregate);}catch(InvalidOperationException){individualLinkBlocked=true;}
    if(!individualLinkBlocked)throw new InvalidDataException("E06 nhận liên kết sức khỏe cá nhân.");
    bool readByUnassignedDenied=false;
    try{DataStore.GetModuleRecord(user,"E01",supportId);}catch(UnauthorizedAccessException){readByUnassignedDenied=true;}
    if(!readByUnassignedDenied)throw new InvalidDataException("B02 có thể đọc hồ sơ an sinh E01 khi không có quyền.");
    // V0.15 runtime gate: F01-F07 scoped coordination, typed links, and F06 aggregates.
    journal.BeginStep("SAFETY_AND_URBAN");
    string urbanSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_urban",urbanSecret,new []{
     "F01:read","F01:create","F01:update","F02:read","F02:create",
     "F03:read","F03:create","F04:read","F04:create","F05:read","F05:create",
     "F06:read","F06:create","F07:read","F07:create","B04:read","C04:read"});
    var urban=DataStore.Login("test_urban",urbanSecret);
    if(urban==null || !DataStore.Can(urban,"F01","create") || DataStore.Can(user,"F01","read") ||
       !DataStore.Can(root,"F01","read"))
     throw new InvalidDataException("F01-F07 bị cấp quyền ngoài trần ủy quyền.");
    var notice=new ModuleRecord{Code="F01-MAU-01",Title="[MẪU] phối hợp an toàn",Status="Ghi nhận nội bộ",
     RelatedFeature="B04",RelatedId=addressId,
     Fields=new System.Collections.Generic.Dictionary<string,string>{{"incident_kind","Tuyên truyền an toàn"}}};
    string noticeId=DataStore.SaveModuleRecord(urban,"F01",notice);
    if(DataStore.GetModulePage(urban,"F01","phoi hop an toan",0,50).TotalCount!=1 ||
       DataStore.GetModuleRecord(urban,"F01",noticeId).RelatedId!=addressId)
     throw new InvalidDataException("F01 không lưu hoặc tìm đúng hồ sơ liên kết theo ID.");
    bool arbitraryLinkDenied=false;
    try{DataStore.SaveModuleRecord(urban,"F01",new ModuleRecord{Code="F01-SAI",Title="[MẪU] liên kết sai",Status="Ghi nhận nội bộ",
      RelatedFeature="C04",RelatedId=meetingId,Fields=new System.Collections.Generic.Dictionary<string,string>{{"incident_kind","Khác"}}});}
    catch(InvalidOperationException){arbitraryLinkDenied=true;}
    if(!arbitraryLinkDenied)throw new InvalidDataException("F01 mở liên kết tùy tiện ngoài danh mục cho phép.");
    bool pcccReferenceRequired=false;
    try{DataStore.SaveModuleRecord(urban,"F02",new ModuleRecord{Code="F02-SAI",Title="[MẪU] chuyển không nguồn",Status="Đã chuyển đầu mối PCCC",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"activity_kind","Tuyên truyền PCCC"}}});}
    catch(InvalidOperationException){pcccReferenceRequired=true;}
    if(!pcccReferenceRequired)throw new InvalidDataException("F02 tự xác nhận chuyển PCCC khi thiếu căn cứ.");
    var aggregateF06=new ModuleRecord{Code="F06-MAU-01",Title="[MẪU] hoạt động tổng hợp",Status="Kế hoạch phối hợp",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"activity_kind","Tuyên truyền"},{"aggregate_count","20"}}};
    string aggregateF06Id=DataStore.SaveModuleRecord(urban,"F06",aggregateF06);
    if(DataStore.GetModuleRecord(urban,"F06",aggregateF06Id).Id!=aggregateF06Id)
     throw new InvalidDataException("F06 không lưu bản tổng hợp theo ID.");
    bool personalF06Blocked=false;
    aggregateF06.Code="F06-LOI"; aggregateF06.RelatedFeature="B02";aggregateF06.RelatedId=members[0].Id;
    try{DataStore.SaveModuleRecord(urban,"F06",aggregateF06);}
    catch(InvalidOperationException){personalF06Blocked=true;}
    if(!personalF06Blocked)throw new InvalidDataException("F06 cho phép liên kết nhân khẩu trên kho chưa mã hóa.");
    // V0.16 runtime gate: four G workflows, exact links, evidence and task delegation.
    journal.BeginStep("FEEDBACK_AND_TASKS");
    string communitySecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_community",communitySecret,new []{
      "G01:read","G01:create","G01:update","G02:read","G02:create",
      "G03:read","G03:create","G04:read","G04:create",
      "B04:read","C05:read","A02:read","A02:create"});
    var community=DataStore.Login("test_community",communitySecret);
    if(community==null || !DataStore.Can(community,"G01","create") || DataStore.Can(user,"G01","read") ||
       !DataStore.Can(root,"G01","read"))throw new InvalidDataException("Quyền nhóm G vượt trần ủy quyền.");
    var feedback=new ModuleRecord{Code="G01-MAU-01",Title="[MẪU] phản ánh cộng đồng không định danh",
      Status="Tiếp nhận nội bộ",RelatedFeature="B04",RelatedId=addressId,
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"issue_kind","Môi trường"},
       {"intake_channel","Trực tiếp"},{"received_on","2026-11-05"}}};
    string feedbackId=DataStore.SaveModuleRecord(community,"G01",feedback);
    if(DataStore.GetModulePage(community,"G01","phan anh cong dong",0,50).TotalCount!=1 ||
       DataStore.GetModuleRecord(community,"G01",feedbackId).RelatedId!=addressId)
     throw new InvalidDataException("G01 không tìm đúng hồ sơ và liên kết B04.");
    bool unverifiedForwardDenied=false;
    try{DataStore.SaveModuleRecord(community,"G01",new ModuleRecord{Code="G01-INVALID",
      Title="[MẪU] chưa có căn cứ",Status="Đã chuyển đầu mối",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"issue_kind","Hạ tầng"},
       {"intake_channel","Điện thoại"},{"received_on","2026-11-05"}}});}
    catch(InvalidOperationException){unverifiedForwardDenied=true;}
    if(!unverifiedForwardDenied)throw new InvalidDataException("G01 cho chuyển không căn cứ.");
    var mediation=new ModuleRecord{Code="G02-MAU-01",Title="[MẪU] theo dõi hòa giải nội bộ",
      Status="Đã lập biên bản nội bộ",RelatedFeature="C05",RelatedId=minutesId,
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"case_kind","Quan hệ láng giềng"},
       {"opened_on","2026-11-05"},{"minutes_reference","BB-01"}}};
    string mediationId=DataStore.SaveModuleRecord(community,"G02",mediation);
    if(DataStore.GetModuleRecord(community,"G02",mediationId).RelatedId!=minutesId)
     throw new InvalidDataException("G02 không liên kết đúng biên bản C05.");
    bool earlyNoticeDenied=false;
    try{DataStore.SaveModuleRecord(community,"G03",new ModuleRecord{Code="G03-INVALID",
      Title="[MẪU] truyền thông",Status="Đã ghi nhận thông báo",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"campaign_kind","Văn hóa"}}});}
    catch(InvalidOperationException){earlyNoticeDenied=true;}
    if(!earlyNoticeDenied)throw new InvalidDataException("G03 ghi đã thông báo khi chưa có nguồn.");
    bool inventedAgencyReplyDenied=false;
    try{DataStore.SaveModuleRecord(community,"G04",new ModuleRecord{Code="G04-INVALID",
      Title="[MẪU] cơ quan",Status="Đã có phản hồi",
      Fields=new System.Collections.Generic.Dictionary<string,string>{{"agency_kind","Phường"},
       {"recipient","Bộ phận giả lập"}}});}
    catch(InvalidOperationException){inventedAgencyReplyDenied=true;}
    if(!inventedAgencyReplyDenied)throw new InvalidDataException("G04 không yêu cầu căn cứ phản hồi.");
    string linkedTaskId=DataStore.CreateTaskFromFeedback(community,feedbackId);
    if(DataStore.CreateTaskFromFeedback(community,feedbackId)!=linkedTaskId)
     throw new InvalidDataException("G01: bấm lại tạo nhiệm vụ không trả đúng ID đã tạo.");
    var linkedTask=DataStore.GetModuleRecord(community,"A02",linkedTaskId);
    if(linkedTask.RelatedFeature!="G01" || linkedTask.RelatedId!=feedbackId ||
       linkedTask.Title.Contains("người phản ánh"))
     throw new InvalidDataException("Không tạo được nhiệm vụ theo ID nguồn G01 an toàn.");
    bool unauthorizedGDenied=false;
    try{DataStore.GetModuleRecord(user,"G01",feedbackId);}
    catch(UnauthorizedAccessException){unauthorizedGDenied=true;}
    if(!unauthorizedGDenied)throw new InvalidDataException("Tài khoản B01/B02 đọc được phản ánh G01 không được cấp quyền.");
    // A B01-only account must not receive B02 member fields even via a direct service call.
    string viewerSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_household_only",viewerSecret,new []{"B01:read"});
    var viewer=DataStore.Login("test_household_only",viewerSecret);
    if(viewer==null || !DataStore.Can(viewer,"B01","read") || DataStore.Can(viewer,"B02","read"))
     throw new InvalidDataException("Không thiết lập được quyền chỉ xem hộ.");
    if(DataStore.GetHouseholdsPage(viewer,"",0,10).TotalCount<2)
     throw new InvalidDataException("Tài khoản chỉ xem hộ bị chặn sai.");
    bool membersDenied=false;
    try {DataStore.HouseholdMembers(viewer,households[0].Id);}
    catch(UnauthorizedAccessException){membersDenied=true;}
    if(!membersDenied)throw new InvalidDataException("B01-only truy xuất được danh sách B02 qua service.");
    journal.BeginStep("RESIDENCY_TRANSFER");
    // B03 runtime: move an exact person between two different households in one area.
    DataStore.AddHousehold(user,"TEST090","Địa chỉ khác","Chủ hộ mới");
    var dest=DataStore.FindHouseholds(user,"TEST090");
    if(dest.Count!=1)throw new InvalidDataException("Thiếu hộ đích B03.");
    var movable=DataStore.FindPeople(user,"Người mẫu B");
    if(movable.Count!=1||movable[0].HouseholdId!=households[0].Id)
     throw new InvalidDataException("Lấy ID người cần chuyển không chính xác.");
    var verified=DataStore.GetPerson(user,movable[0].Id);
    DataStore.CheckTransfer(user,verified.Id,households[0].Id,dest[0].Id,"Con","Chuyển sang hộ khác",verified.Version);
    DataStore.TransferMember(user,verified.Id,households[0].Id,dest[0].Id,"Con","Chuyển sang hộ khác",verified.Version);
    if(DataStore.GetPerson(user,verified.Id).HouseholdId!=dest[0].Id)
     throw new InvalidDataException("Chuyển B03 sai hộ đích.");
    if(DataStore.ListResidencyChanges(user,"chuyen sang ho khac").Count!=1)
     throw new InvalidDataException("Tra cứu biến động không dấu chưa đạt.");
    if(DataStore.ListPersonResidencyHistory(user,verified.Id).Count!=1 || DataStore.ListResidencyChanges(user).Count!=1)
     throw new InvalidDataException("Chưa ghi biến động có thể truy xuất.");
    if(DataStore.HouseholdMembers(user,households[0].Id).Exists(x=>x.Id==verified.Id))
     throw new InvalidDataException("Hộ cũ vẫn có thành viên đã chuyển.");
    bool staleMoveBlocked=false;
    try {DataStore.TransferMember(user,verified.Id,households[0].Id,dest[0].Id,"Con","Yêu cầu cũ đã hết hạn",verified.Version);}
    catch(InvalidOperationException){staleMoveBlocked=true;}
    if(!staleMoveBlocked)throw new InvalidDataException("Cho phép gửi lặp biến động theo bản cũ.");
    var owner=DataStore.FindPeople(user,"Người mẫu A");
    bool headMoveBlocked=false;
    try {DataStore.TransferMember(user,owner[0].Id,households[0].Id,dest[0].Id,"Khác","Chuyển chủ hộ bằng lối tắt",DataStore.GetPerson(user,owner[0].Id).Version);}
    catch(InvalidOperationException){headMoveBlocked=true;}
    if(!headMoveBlocked)throw new InvalidDataException("B03 cho chuyển chủ hộ trái quy trình.");
    // I08 runtime: same name is an advisory quality finding only; never merge automatically.
    DataStore.AddMember(user,dest[0].Id,"Người mẫu C","Con",null,null);
    if(DataStore.ListDataQualityIssues(admin).Count<2)
     throw new InvalidDataException("I08 chưa phát hiện tên trùng cần đối chiếu.");
    journal.BeginStep("DATA_INTAKE");
    // V0.11 C# runtime gate: collect -> preview -> exact-ID enrichment -> atomic batch commit.
    if(!DataStore.Can(user,"B02","update"))throw new InvalidDataException("Thiếu quyền bổ sung trường B02.");
    var intakeH=DataStore.PreviewIntake(user,"CREATE_HOUSEHOLD","MANUAL","selftest-household",
      "Mã hộ\tĐịa chỉ\tChủ hộ\nNEW-901\tĐịa chỉ thử 901\tChủ hộ 901");
    if(intakeH.Rows.Count!=1 || DataStore.CommitIntake(user,intakeH,false)!=1 ||
       DataStore.CommitIntake(user,intakeH,false)!=1 || DataStore.FindHouseholds(user,"NEW-901").Count!=1)
     throw new InvalidDataException("Nhập hộ tạo bản ghi trùng khi gửi lại lô.");
    var intakeP=DataStore.PreviewIntake(user,"ADD_MEMBER","CLIPBOARD","selftest-clipboard",
      "Mã hộ\tHọ tên\tNgày sinh\tGiới tính\tQuan hệ\nNEW-901\tThành viên cần bổ sung\t\t\tCon");
    if(intakeP.Rows.Count!=1 || DataStore.CommitIntake(user,intakeP,false)!=1)
     throw new InvalidDataException("Nhập người theo mã hộ thất bại.");
    var added=DataStore.FindPeople(user,"Thành viên cần bổ sung");
    if(added.Count!=1 || added[0].HouseholdCode!="NEW-901")
     throw new InvalidDataException("Liên kết nhân khẩu mới không đúng mã hộ.");
    var detail=DataStore.GetPerson(user,added[0].Id);
    var enrich=DataStore.PreviewIntake(user,"ENRICH_PERSON","MANUAL","selftest-enrich",
      "Mã hồ sơ\tPhiên bản\tNgày sinh\tGiới tính\n"+detail.Id+"\t"+detail.Version+"\t2001-01-02\tNam");
    if(enrich.Rows.Count!=1 || DataStore.CommitIntake(user,enrich,false)!=1)
     throw new InvalidDataException("Bổ sung trường nhân khẩu theo ID thất bại.");
    bool staleEnrichRejected=false;
    try {DataStore.CommitIntake(user,DataStore.PreviewIntake(user,"ENRICH_PERSON","MANUAL","selftest-stale",
       "Mã hồ sơ\tPhiên bản\tNgày sinh\tGiới tính\n"+detail.Id+"\t"+detail.Version+"\t2001-01-02\tNam"),false);}
    catch(InvalidOperationException){staleEnrichRejected=true;}
    if(!staleEnrichRejected)throw new InvalidDataException("Không chặn bổ sung phiên bản cũ.");
    // Preview succeeds, concurrent write creates a duplicate, then the whole commit must rollback.
    var atomic=DataStore.PreviewIntake(user,"CREATE_HOUSEHOLD","MANUAL","selftest-atomic",
      "Mã hộ\tĐịa chỉ\tChủ hộ\nNEW-902\tĐịa chỉ mới\tNgười A\nNEW-903\tĐịa chỉ khác\tNgười B");
    DataStore.AddHousehold(user,"NEW-903","Dữ liệu cạnh tranh","Chủ hộ cạnh tranh");
    bool atomicRejected=false;
    try {DataStore.CommitIntake(user,atomic,false);}catch(System.Data.SQLite.SQLiteException){atomicRejected=true;}
    if(!atomicRejected || DataStore.FindHouseholds(user,"NEW-902").Count!=0)
     throw new InvalidDataException("Lô nhập nhanh không rollback toàn bộ khi gặp trùng đích.");
    journal.BeginStep("EXCEL_INTAKE");
    string template=Path.Combine(sandbox,"template.xlsx");
    DataStore.SaveIntakeTemplate(template);
    var sheets=XlsxIntakeReader.SheetNames(template);
    if(sheets.Length!=3||sheets[1]!="Thêm nhân khẩu"||
       XlsxIntakeReader.ReadSheet(template,1)!="Mã hộ\tHọ tên\tNgày sinh\tGiới tính\tQuan hệ")
     throw new InvalidDataException("Mẫu XLSX không đúng trang hoặc cột.");
    string workbook=Path.Combine(sandbox,"multi-sheet.xlsx");
    XlsxWorkbook.Save(workbook,new System.Collections.Generic.Dictionary<string,System.Collections.Generic.IEnumerable<object[]>>{
     {"Hướng dẫn",new[]{new object[]{"Bỏ qua trang này"}}},
     {"Hộ gia đình",new object[][]{DataStore.IntakeHeaders("CREATE_HOUSEHOLD"),new object[]{"EXCEL-01","Địa chỉ Excel","Người thử Excel"}}}
    });
    var excel=DataStore.PreviewIntake(user,"CREATE_HOUSEHOLD","XLSX_LOCAL","multi-sheet.xlsx",XlsxIntakeReader.ReadSheet(workbook,1));
    if(DataStore.FindHouseholds(user,"EXCEL-01").Count!=0||DataStore.CommitIntake(user,excel,false)!=1||
       DataStore.FindHouseholds(user,"EXCEL-01").Count!=1)
     throw new InvalidDataException("Đọc trang XLSX được chọn hoặc nhập lô vào kho thất bại.");
    Console.WriteLine("REPOSITORY_PASS XLSX template/selected sheet/preview/commit");
    // V0.17 Windows runtime gate: reports H01–H05 from authorized source records only.
    journal.BeginStep("REPORTS");
    string reportSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(admin,"test_reporter",reportSecret,new[]{
      "H01:read","H02:read","H03:read","H04:read","H05:read","H05:export",
      "A02:read","B01:read","B02:read"});
    var reporter=DataStore.Login("test_reporter",reportSecret);
    if(reporter==null || !DataStore.Can(reporter,"H05","export") || !DataStore.Can(root,"H01","read"))
     throw new InvalidDataException("Quyền báo cáo riêng không đúng.");
    var date=DateTime.UtcNow.Date;
    var beginning=new DateTime(date.Year,date.Month,1).ToString("yyyy-MM-dd");
    var ending=date.ToString("yyyy-MM-dd");
    var periodic=DataStore.GetReportPage(reporter,"H01",beginning,ending,"A02","",0,50);
    long reportTaskCount=DataStore.GetModulePage(reporter,"A02","",0,50).TotalCount;
    if(periodic.TotalCount!=1||periodic.Items[0].Count!=reportTaskCount)
     throw new InvalidDataException("H01 chưa tổng hợp số hồ sơ A02 theo kỳ.");
    var urgent=DataStore.GetReportPage(reporter,"H02",beginning,ending,"A02","",0,50);
    if(urgent.TotalCount!=reportTaskCount||!urgent.Items.Exists(x=>x.RecordId==taskId))
     throw new InvalidDataException("H02 không liên kết theo đúng ID hồ sơ nguồn.");
    var overdue=DataStore.GetReportPage(reporter,"H04",beginning,ending,"A02","",0,50);
    if(overdue.TotalCount!=1||overdue.Items[0].RecordId!=taskId)
     throw new InvalidDataException("H04 bỏ sót công việc tồn đọng từ kỳ trước.");
    bool deniedSource=false;
    try{DataStore.GetReportPage(reporter,"H03",beginning,ending,"G01","",0,50);}
    catch(UnauthorizedAccessException){deniedSource=true;}
    if(!deniedSource)throw new InvalidDataException("H03 cho báo cáo nguồn G01 chưa được cấp quyền.");
    string reportXlsx=Path.Combine(sandbox,"summary.xlsx");
    DataStore.ExportReportAggregateXlsx(reporter,beginning,ending,"A02","",reportXlsx);
    string excelReport=XlsxIntakeReader.ReadFirstSheet(reportXlsx);
    if(!excelReport.StartsWith("Mã khu phố\tPhân hệ",StringComparison.Ordinal)||!excelReport.Contains("\tA02\t")||
       !excelReport.Contains("Ẩn (1–4)")||excelReport.Contains(taskId))
     throw new InvalidDataException("Báo cáo XLSX sai nguồn, lộ ID hoặc bỏ ngưỡng ẩn nhóm nhỏ.");
    Console.WriteLine("REPOSITORY_PASS XLSX H05 aggregate/threshold");
    var csv=DataStore.ExportReportAggregateCsv(reporter,beginning,ending,"A02","");
    if(!csv.Contains("A02")||csv.Contains(taskId)||csv.Contains("Rà soát Đường"))
     throw new InvalidDataException("H05 không tạo CSV tổng hợp an toàn.");
    DataStore.LogReportExport(reporter);
    journal.BeginStep("B03_RESIDENCE_PERIODS");
    string firstPeriod=DataStore.AddResidencePeriod(admin,verified.Id,"thuong_tru","[MẪU] Địa chỉ cũ","01-01-2020","");
    DataStore.AddResidencePeriod(admin,verified.Id,"thuong_tru","[MẪU] Địa chỉ mới","01-01-2021","");
    DataStore.AddResidencePeriod(admin,verified.Id,"tam_tru","[MẪU] Địa chỉ tạm trú","01-01-2021","");
    var residence=DataStore.GetResidencePeriods(admin,verified.Id,0);
    var firstRow=residence.Items.Find(r=>r.Id==firstPeriod);
    if(residence.TotalCount!=3||firstRow==null||firstRow.ValidUntil!="2021-01-01"||firstRow.Address!="[MẪU] Địa chỉ cũ")
     throw new InvalidDataException("B03: không giữ đúng lịch sử địa chỉ hoặc mốc hiệu lực.");
    bool residenceDenied=false,residenceOverlap=false;
    try{DataStore.GetResidencePeriods(expiredRoot,verified.Id,0);}catch(UnauthorizedAccessException){residenceDenied=true;}
    try{DataStore.AddResidencePeriod(admin,verified.Id,"thuong_tru","[MẪU] Trùng","01-01-2021","");}catch(InvalidOperationException){residenceOverlap=true;}
    if(!residenceDenied||!residenceOverlap||DataStore.GetResidencePeriods(admin,verified.Id,0).TotalCount!=3)
     throw new InvalidDataException("B03: kiểm tra phiên hoặc kỳ trùng thất bại.");
    journal.BeginStep("B02_IDENTITY_SCOPED_LOOKUP");
    string identityTestSecret="T-"+Guid.NewGuid().ToString("N")+"!";
    DataStore.CreateAccount(root,"identity_test_admin",identityTestSecret,null);
    var identityTest=DataStore.Login("identity_test_admin",identityTestSecret);
    bool identityDenied=false;
    try{DataStore.FindPersonIdByIdentity(identityTest,"000000123456");}
    catch(UnauthorizedAccessException){identityDenied=true;}
    if(!identityDenied)throw new InvalidDataException("B02: identity lookup granted by default.");
    var identityRights=DataStore.GetAccountRights(root,identityTest.Id);
    identityRights.AddRange(new[]{"B02:identity_lookup","B02:identity_write"});
    DataStore.SetAccountRights(root,identityTest.Id,identityRights);
    identityTest=DataStore.Login("identity_test_admin",identityTestSecret);
    var identityPerson=DataStore.GetPerson(identityTest,verified.Id);
    DataStore.SavePersonIdentity(identityTest,identityPerson.Id,identityPerson.Version,"000000123456");
    if(DataStore.FindPersonIdByIdentity(identityTest,"000000123456")!=identityPerson.Id||
       DataStore.GetMaskedPersonIdentity(identityTest,identityPerson.Id)!="••••••••3456"||
       DataStore.FindPersonIdByIdentity(identityTest,"000000123457")!="")
     throw new InvalidDataException("B02: exact lookup or mask incorrect.");
    DataStore.SetAccountRights(root,identityTest.Id,identityRights.FindAll(r=>r!="B02:identity_lookup"));
    bool staleIdentityDenied=false;
    try{DataStore.FindPersonIdByIdentity(identityTest,"000000123456");}
    catch(UnauthorizedAccessException){staleIdentityDenied=true;}
    if(!staleIdentityDenied)throw new InvalidDataException("B02: stale session retained identity lookup.");
    journal.BeginStep("F08_VEHICLE_REGISTER");
    string vehicleId=DataStore.SaveVehicle(admin,null,"00-TH-01234","Hãng thử","truck","1500",verified.Id,true);
    var vehicle=DataStore.GetVehicles(admin,"00.TH.01234",0).Items.Find(x=>x.Id==vehicleId);
    if(vehicle==null||vehicle.OwnerId!=verified.Id||vehicle.PayloadKg!=1500||vehicle.Seats.HasValue)
     throw new InvalidDataException("F08: xe tải hoặc liên kết chủ xe sai.");
    bool duplicateVehicle=false,deniedVehicle=false,staleVehicle=false;
    try{DataStore.SaveVehicle(admin,null,"00TH01234","Trùng","motorcycle","",verified.Id,true);}
    catch(InvalidOperationException){duplicateVehicle=true;}
    try{DataStore.GetVehicles(expiredRoot,"",0);}catch(UnauthorizedAccessException){deniedVehicle=true;}
    DataStore.SaveVehicle(admin,vehicle,vehicle.Plate,vehicle.Brand,"passenger","7",verified.Id,false);
    try{DataStore.SaveVehicle(admin,vehicle,vehicle.Plate,vehicle.Brand,"truck","1500",verified.Id,true);}
    catch(InvalidOperationException){staleVehicle=true;}
    var editedVehicle=DataStore.GetVehicles(admin,"00TH01234",0).Items.Find(x=>x.Id==vehicleId);
    if(!duplicateVehicle||!deniedVehicle||!staleVehicle||editedVehicle==null||editedVehicle.Seats!=7||
       editedVehicle.PayloadKg.HasValue||editedVehicle.Active)
     throw new InvalidDataException("F08: kiểm tra trùng, quyền, phiên bản hoặc chuyển loại thất bại.");
    OperationalSelfTest.Run(admin,expiredRoot,journal);
    journal.BeginStep("PROFILE_WRITES");
    string incomingTask=DataStore.CreateTaskFromIncoming(clerk,incomingId);
    if(DataStore.CreateTaskFromIncoming(clerk,incomingId)!=incomingTask||DataStore.GetModuleRecord(clerk,"A02",incomingTask).RelatedId!=incomingId)
     throw new InvalidDataException("C01: bấm lại tạo nhiệm vụ không giữ liên kết nguồn đúng ID.");
    var addressBefore=DataStore.GetHousehold(admin,households[0].Id);
    DataStore.UpdateHouseholdAddress(admin,addressBefore.Id,addressBefore.Version,"Địa chỉ kiểm thử cập nhật");
    var addressAfter=DataStore.GetHousehold(admin,addressBefore.Id);
    if(addressAfter.Version!=addressBefore.Version+1||addressAfter.Code!=addressBefore.Code||addressAfter.Address!="Địa chỉ kiểm thử cập nhật")
     throw new InvalidDataException("B01: cập nhật địa chỉ không giữ đúng ID/mã/phiên bản.");
    bool staleAddress=false,deniedAddress=false;
    try{DataStore.UpdateHouseholdAddress(admin,addressBefore.Id,addressBefore.Version,"Sai");}catch(InvalidOperationException){staleAddress=true;}
    try{DataStore.UpdateHouseholdAddress(user,addressBefore.Id,addressAfter.Version,"Sai");}catch(UnauthorizedAccessException){deniedAddress=true;}
    if(!staleAddress||!deniedAddress)throw new InvalidDataException("B01: chưa chặn phiên bản cũ hoặc thiếu quyền sửa.");
    Console.WriteLine("REPOSITORY_PASS B01 address/update/readback/stale/deny");
    var profile=DataStore.GetPerson(user,members[0].Id);
    DataStore.UpdatePersonProfile(user,profile.Id,profile.Version,"[MẪU] Đặng Văn Kiểm","2000-01-01","Nam");
    var editedProfile=DataStore.GetPerson(user,profile.Id);
    if(editedProfile.FullName!="[MẪU] Đặng Văn Kiểm"||editedProfile.Version!=profile.Version+1)
     throw new InvalidDataException("B02: sửa hồ sơ không cập nhật đúng ID/phiên bản.");
    bool staleProfile=false,deniedProfile=false;
    try{DataStore.UpdatePersonProfile(user,profile.Id,profile.Version,"Sai","2000-01-01","Nam");}
    catch(InvalidOperationException){staleProfile=true;}
    try{DataStore.UpdatePersonProfile(expiredRoot,profile.Id,editedProfile.Version,"Sai","2000-01-01","Nam");}
    catch(UnauthorizedAccessException){deniedProfile=true;}
    if(!staleProfile||!deniedProfile)throw new InvalidDataException("B02: chưa chặn cập nhật sai quyền hoặc phiên bản cũ.");
    Console.WriteLine("REPOSITORY_PASS B02 profile/update/stale/deny");
    DataStore.RunMergeRegression(journal,root,admin,user);
    journal.BeginStep("ACCOUNT_REVOCATION");
    // I02 + I03 runtime checks: changing a ceiling must revoke now-illegal child grants.
    var oldRights=DataStore.GetAccountRights(root,admin.Id);
    if(!oldRights.Contains("B01:read")||!oldRights.Contains("A02:read")||!oldRights.Contains("B04:create"))throw new InvalidDataException("ADMIN khu phố chưa có bộ quyền ban đầu.");
    DataStore.SetAccountRights(root,admin.Id,new [] {"B01:read","B02:read","B03:read"});
    if(DataStore.Can(user,"B01","read") || DataStore.Can(admin,"I02","manage"))
      throw new InvalidDataException("Phiên cũ chưa bị vô hiệu sau thay đổi chính sách.");
    admin=DataStore.Login("test_admin",adminSecret);
    user=DataStore.Login("test_user",userSecret);
    if(admin==null||user==null||DataStore.Can(user,"B01","create")||!DataStore.Can(user,"B01","read"))
      throw new InvalidDataException("Thu hồi quyền dây chuyền không đúng.");
    bool selfGrantBlocked=false;
    try {DataStore.SetAccountRights(admin,admin.Id,new []{"B01:create"});}
    catch(UnauthorizedAccessException){selfGrantBlocked=true;}
    if(!selfGrantBlocked)throw new InvalidDataException("Cho phép ADMIN tự cấp quyền.");
    DataStore.SetAccountStatus(admin,user.Id,false);
    if(DataStore.Can(user,"B01","read")||DataStore.Login("test_user",userSecret)!=null)
      throw new InvalidDataException("Khóa tài khoản không làm mất hiệu lực quyền.");
    DataStore.SetAccountStatus(admin,user.Id,true);
    if(DataStore.Login("test_user",userSecret)==null)throw new InvalidDataException("Mở khóa thất bại.");
    if(DataStore.ListAudit(root).Count==0)throw new InvalidDataException("Không có nhật ký quản trị.");
    // I04 real Windows self-test: authenticated backup, tamper refusal and restore of original rows.
    journal.BeginStep("ENCRYPTED_BACKUP");
    string backup=Path.Combine(sandbox,"recovery-test.qlkpbak");
    string badCopy=Path.Combine(sandbox,"tampered-test.qlkpbak");
    string recoveryPassword="Recovery!"+Guid.NewGuid().ToString("N")+"_2026";
    if(!DataStore.Can(root,"I04","manage") || DataStore.Can(admin,"I04","manage"))
     throw new InvalidDataException("Quyền bảo trì kho không bị giới hạn ADMIN gốc.");
    VaultRegression.Run(journal,root);
    journal.BeginStep("BACKUP_AFTER_VAULT_REGRESSIONS");
    EncryptedBackup.Create(root,backup,recoveryPassword);
    EncryptedBackup.Validate(root,backup,recoveryPassword);
    bool rejected=false;
    try {EncryptedBackup.Validate(root,backup,recoveryPassword+"WRONG");}
    catch(UnauthorizedAccessException){rejected=true;}
    if(!rejected)throw new InvalidDataException("Sai mật khẩu vẫn giải mã được bản sao.");
    File.Copy(backup,badCopy);
    using(var f=new FileStream(badCopy,FileMode.Open,FileAccess.ReadWrite)) {
     f.Position=f.Length-7; int before=f.ReadByte();f.Position--;
     f.WriteByte((byte)(before^0x04));
    }
    rejected=false;
    try {EncryptedBackup.Validate(root,badCopy,recoveryPassword);}
    catch(UnauthorizedAccessException){rejected=true;}
    if(!rejected)throw new InvalidDataException("Bản sao bị chỉnh sửa không bị từ chối.");
    DataStore.CreateAccount(root,"test_temp_admin", "Temp!"+Guid.NewGuid().ToString("N"),null);
    if(!DataStore.ListAccounts(root).Exists(x=>x.Username=="test_temp_admin"))
     throw new InvalidDataException("Không tạo được dữ liệu thử trước phục hồi.");
    EncryptedBackup.Restore(root,backup,recoveryPassword);
    if(DataStore.ListAccounts(root).Exists(x=>x.Username=="test_temp_admin"))
     throw new InvalidDataException("Không phục hồi đúng ảnh chụp dữ liệu.");
    journal.BeginStep("CATALOG_TRANSFER");
    // V0.18 I07/I09 local operations: catalog edits must be scoped, versioned and audited.
    // Earlier revocation tests removed I05/I06 rights. Explicitly restore only this test's rights.
    DataStore.SetAccountRights(root,admin.Id,new[]{"B01:read","B02:read","B03:read","I05:read","I06:read","I06:export","I07:read"});
    admin=DataStore.Login("test_admin",adminSecret);
    DataStore.SetAccountRights(admin,operatorUser.Id,new[]{"I05:read","B01:read","B02:read"});
    operatorUser=DataStore.Login("test_operations",operatorSecret);
    user=DataStore.Login("test_user",userSecret);
    var cat=new CatalogEntry{Category="LOAI_NHA",Code="NHA_TRO",Label="[MẪU] Nhà trọ",Active=true};
    string categoryId=DataStore.SaveCatalog(root,cat);
    var page=DataStore.ListCatalog(root,"NHA_TRO",0,50);
    if(page.TotalCount!=1||page.Items[0].Id!=categoryId)
     throw new InvalidDataException("I07 không lưu hoặc phân trang danh mục toàn cục.");
    cat.Id=categoryId;cat.Version=page.Items[0].Version;cat.Label="[MẪU] Nhà trọ cập nhật";
    DataStore.SaveCatalog(root,cat);
    bool staleCatalog=false;
    try{DataStore.SaveCatalog(root,cat);}catch(InvalidOperationException){staleCatalog=true;}
    if(!staleCatalog)throw new InvalidDataException("I07 không chặn cập nhật phiên bản cũ.");
    bool wrongScope=false;
    try{DataStore.SaveCatalog(admin,new CatalogEntry{Category="LOAI_NHA",Code="MA_TRUY_CAP",Label="Cấp quyền sai",Active=true});}
    catch(UnauthorizedAccessException){wrongScope=true;}
    if(!wrongScope)throw new InvalidDataException("I07 cho tài khoản không quyền chỉnh danh mục.");
    // I06 PILOT: encrypted, one-way, fictional catalog transfer; never citizen records or network.
    var transferPath=Path.Combine(sandbox,"catalog-demo.qlkpsync");
    var transferOutcome=OfflineCatalogTransfer.Export(admin,transferPath,"DemoOnly_Qlkp_2026!");
    if(!File.Exists(transferPath) || new FileInfo(transferPath).Length<56 ||
       !transferOutcome.Contains("Đã xuất"))
     throw new InvalidDataException("I06 không tạo được gói mã hóa danh mục giả lập.");
    byte[] transferHeader=new byte[8];
    using(var f=File.OpenRead(transferPath))if(f.Read(transferHeader,0,8)!=8 ||
       System.Text.Encoding.ASCII.GetString(transferHeader)!="QLKPSY01")
     throw new InvalidDataException("I06 xuất sai định dạng gói mã hóa.");
    bool unauthorizedExport=false;
    try{OfflineCatalogTransfer.Export(user,Path.Combine(sandbox,"denied.qlkpsync"),"DemoOnly_Qlkp_2026!");}
    catch(UnauthorizedAccessException){unauthorizedExport=true;}
    if(!unauthorizedExport || File.Exists(Path.Combine(sandbox,"denied.qlkpsync")))
     throw new InvalidDataException("I06 cho tài khoản thiếu quyền xuất gói.");
    var oldCatalog=DataStore.ListCatalog(root,"NHA_TRO",0,50).Items[0];oldCatalog.Active=false;DataStore.SaveCatalog(root,oldCatalog);
    if(DataStore.EnsureReferenceCatalog(root)!=0||DataStore.EnsureReferenceCatalog(root)!=0)
     throw new InvalidDataException("I07: nạp danh mục chuẩn không nguyên tử hoặc không idempotent.");
    foreach(var entry in OfflineCatalogTransfer.ReferenceEntries())
     if(!OfflineCatalogTransfer.IsReferenceEntry(entry.Category,entry.Code,entry.Label))throw new InvalidDataException("I06: mã chuẩn không khớp nhãn.");
    if(OfflineCatalogTransfer.IsReferenceEntry("LOAI_NHA","NHA_RIENG","Nhãn tùy ý"))throw new InvalidDataException("I06: chấp nhận nhãn ngoài danh mục chuẩn.");
    string referencePath=Path.Combine(sandbox,"catalog-reference.qlkpsync");
    OfflineCatalogTransfer.ExportReferenceCatalog(admin,referencePath,recoveryPassword);
    if(!File.Exists(referencePath))throw new InvalidDataException("I06: không xuất được danh mục sử dụng.");
    Console.WriteLine("REPOSITORY_PASS I07 I06 reference/catalog/create/idempotent/export");
    if(!DataStore.VersionDiagnostics(root).Contains("Lược đồ SQLite: "+DataStore.SchemaVersion))
     throw new InvalidDataException("I09 không hiển thị đúng phiên bản schema.");
    journal.BeginStep("QR_REFERENCE");
    // I05 uses a fake QR payload with the household's exact GUID; no external URL.
    var householdForQr=DataStore.GetHouseholdsPage(user,"",0,50);
    if(householdForQr.Items.Count==0)throw new InvalidDataException("Thiếu hộ thử I05.");
    bool missingQrRightDenied=false;
    try{DataStore.ParseLocalQrReference(user,"QLKP1|B01|"+householdForQr.Items[0].Id);}
    catch(UnauthorizedAccessException){missingQrRightDenied=true;}
    if(!missingQrRightDenied)throw new InvalidDataException("I05 không yêu cầu quyền riêng.");
    bool externalQrDenied=false;
    try{DataStore.ParseLocalQrReference(operatorUser,"https://invalid.example/redirect");}
    catch(InvalidDataException){externalQrDenied=true;}
    if(!externalQrDenied)throw new InvalidDataException("I05 chấp nhận URL ngoài danh mục.");
    journal.BeginStep("PRIVACY_AND_ARTIFACT_GUARDS");
    // V0.19: small aggregate counts must never leak via H01/H03 UI bindings.
    if(new ReportLine{Feature="H03",RecordId="",Count=2}.CountDisplay!="Ẩn (1–4)" ||
       new ReportLine{Feature="H03",RecordId="",Count=5}.CountDisplay!="5" ||
       new ReportLine{Feature="H02",RecordId="valid-id",Count=1}.CountDisplay!="1")
      throw new InvalidDataException("Báo cáo không che đúng nhóm dữ liệu nhỏ.");
    // Confirm fail-closed behavior if an encrypted-backup process leaves a plaintext stage.
    string repairDir=Path.Combine(sandbox,"maintenance");
    Directory.CreateDirectory(repairDir);
    string leftover=Path.Combine(repairDir,"qlkp-plaintext-check.db");
    File.WriteAllText(leftover,"FAKE SELFTEST; NOT PERSONAL DATA");
    bool leftoversBlocked=false;
    try{StartupDiagnostics.CheckPlaintextMaintenanceArtifacts();}
    catch(InvalidDataException){leftoversBlocked=true;}
    finally{File.Delete(leftover);}
    if(!leftoversBlocked)throw new InvalidDataException("Khởi chạy không phát hiện tệp bảo trì chưa mã hóa.");
    StartupDiagnostics.CheckPlaintextMaintenanceArtifacts();
    journal.BeginStep("A02_OFFLINE_TASK_EXCHANGE");
    TestOfflineTaskExchange(root,admin,user,adminSecret,sandbox);
    StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);
   } catch(Exception ex){journal.EndStep("FAIL",ex.GetType().Name);throw;}
   finally {
    // Storage cleanup must still execute even if the journal itself cannot be written.
    try{journal.BeginStep("SANDBOX_CLEANUP");}
    finally{DataStore.EndIsolatedSelfTest(sandbox);}
    journal.EndStep("PASS");
   }
  }
 }
}
