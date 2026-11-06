using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class MainWindow : Window {
  readonly Session session;
  readonly List<Feature> authorized;
  readonly List<string> visibleGroups;
  public MainWindow(Session s){
   InitializeComponent();WindowLayout.FitInitialSize(this);session=s;
   authorized=FeatureCatalog.All.Where(f =>
    ((ModuleCatalog.IsEditable(f.Code) || f.Code=="A01" || (f.Code=="A05" && DataStore.Can(s,"A02","read"))) && DataStore.Can(s,f.Code,"read")) ||
    (f.Code.Length==3 && f.Code[0]=='H' && DataStore.Can(s,f.Code,"read")) ||
    (f.Code=="B01" && DataStore.Can(s,"B01","read")) ||
    (f.Code=="B02" && DataStore.Can(s,"B02","read")) ||
    (f.Code=="B03" && DataStore.Can(s,"B03","read")) ||
    (f.Code=="I01" && DataStore.Can(s,"I01","manage")) ||
    (f.Code=="I02" && DataStore.Can(s,"I02","manage")) ||
    (f.Code=="I03" && DataStore.Can(s,"I03","read")) ||
    (f.Code=="I04" && DataStore.Can(s,"I04","manage")) ||
    (f.Code=="I08" && DataStore.Can(s,"I08","read")) ||
    (f.Code=="I05" && DataStore.Can(s,"I05","read")) ||
    (f.Code=="I06" && DataStore.Can(s,"I06","read")) ||
    (f.Code=="I07" && DataStore.Can(s,"I07","read")) ||
    (f.Code=="I09" && DataStore.Can(s,"I09","read"))
   ).ToList();
   visibleGroups=FeatureCatalog.GroupNames.Where(g=>authorized.Any(f=>f.Code[0]==g[0])).ToList();
   CurrentUser.Text=s.Username+" · "+DisplayLabels.Role(s.Role)+" · "+DataStore.AreaName(s);
   AccountLabel.Text=CurrentUser.Text;
   bool accountAccess=DataStore.Can(s,"I01","manage");
   AccountButton.Visibility=accountAccess?Visibility.Visible:Visibility.Collapsed;
   CurrentUser.Visibility=accountAccess?Visibility.Collapsed:Visibility.Visible;
   Status.Text="Phiên bản "+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
   if(s.Role!="ASSIGNED_USER")Status.ToolTip=DataStore.DatabasePath;
   Groups.ItemsSource=visibleGroups;
   if(visibleGroups.Count>0)Groups.SelectedIndex=0;
   else FeatureContent.Content=new NoticeView("Tài khoản chưa được cấp quyền sử dụng chức năng. Liên hệ quản trị khu phố.");
  }
  void Account_Click(object sender,RoutedEventArgs e){
   if(!DataStore.Can(session,"I01","manage")){FeatureContent.Content=new NoticeView("Phiên không có quyền quản trị tài khoản.");return;}
   Groups.SelectedItem=visibleGroups.First(g=>g[0]=='I');
   Features.SelectedItem=authorized.First(f=>f.Code=="I01");
  }
  void Logout_Click(object sender,RoutedEventArgs e){
   session.ExpiresUtc=DateTime.UtcNow;
   FeatureContent.Content=null;
   var login=new LoginWindow();Application.Current.MainWindow=login;login.Show();Close();
  }
  void Groups_SelectionChanged(object sender,SelectionChangedEventArgs e){
   if(Features==null || Groups.SelectedIndex<0)return;
   char c=visibleGroups[Groups.SelectedIndex][0];
   GroupTitle.Text=visibleGroups[Groups.SelectedIndex];
   Features.ItemsSource=authorized.Where(x=>x.Code[0]==c).ToList();
   Features.SelectedIndex=0;
  }
  void NavigateToHousehold(string id){
   DataStore.GetHousehold(session,id);
   Groups.SelectedItem=visibleGroups.First(g=>g.StartsWith("B"));
   Features.SelectedItem=authorized.First(f=>f.Code=="B01");
   FeatureContent.Content=new HouseholdView(session,NavigateToPerson,id);
  }
  void NavigateToPerson(string id){
   DataStore.GetPerson(session,id);
   Groups.SelectedItem=visibleGroups.First(g=>g.StartsWith("B"));
   Features.SelectedItem=authorized.First(f=>f.Code=="B02");
   FeatureContent.Content=new PeopleView(session,NavigateToHousehold,id);
  }
  // Typed deep-link: never route based on display text or row position.
  void NavigateToLinked(string feature,string id){
   if(feature=="B01"){NavigateToHousehold(id);return;}
   if(feature=="B02"){NavigateToPerson(id);return;}
   if(!ModuleCatalog.IsEditable(feature))throw new UnauthorizedAccessException("Đích điều hướng không thuộc kiểu hồ sơ đã được hỗ trợ.");
   DataStore.GetModuleRecord(session,feature,id);
   Groups.SelectedItem=visibleGroups.First(g=>g[0]==feature[0]);
   Features.SelectedItem=authorized.First(f=>f.Code==feature);
   FeatureContent.Content=new ModuleRecordsView(session,feature,NavigateToLinked,id);
  }
  void Features_SelectionChanged(object sender,SelectionChangedEventArgs e){
   if(FeatureContent==null)return;var f=Features.SelectedItem as Feature;if(f==null)return;
   try {
   if(f.Code.Length==3 && f.Code[0]=='H')FeatureContent.Content=DataStore.Can(session,f.Code,"read")?new ReportsView(session,f.Code,NavigateToLinked):(UserControl)new NoticeView("Không có quyền báo cáo.");
   else if(f.Code=="A01")FeatureContent.Content=DataStore.Can(session,"A01","read")?new OperationsDashboardView(session):(UserControl)new NoticeView("Không có quyền xem bàn điều hành.");
   else if(f.Code=="A05")FeatureContent.Content=DataStore.Can(session,"A05","read")&&DataStore.Can(session,"A02","read")?new ModuleRecordsView(session,"A05",NavigateToLinked):(UserControl)new NoticeView("A05 cần cả quyền A05:read và A02:read.");
   else if(ModuleCatalog.IsEditable(f.Code))FeatureContent.Content=DataStore.Can(session,f.Code,"read")?new ModuleRecordsView(session,f.Code,NavigateToLinked):(UserControl)new NoticeView("Không có quyền mở hồ sơ "+f.Code);
   else if(f.Code=="B01")FeatureContent.Content=DataStore.Can(session,"B01","read")?new HouseholdView(session,NavigateToPerson):(UserControl)new NoticeView("Không được phép xem hồ sơ hộ.");
   else if(f.Code=="B02")FeatureContent.Content=DataStore.Can(session,"B02","read")?new PeopleView(session,NavigateToHousehold):(UserControl)new NoticeView("Không được phép xem nhân khẩu.");
   else if(f.Code=="B03")FeatureContent.Content=DataStore.Can(session,"B03","read")?new ResidencyChangeView(session,NavigateToPerson,NavigateToHousehold):(UserControl)new NoticeView("Không được phép xem biến động.");
   else if(f.Code=="I01")FeatureContent.Content=DataStore.Can(session,"I01","manage")?new AccountsView(session):(UserControl)new NoticeView("Không được phép quản trị tài khoản.");
   else if(f.Code=="I02")FeatureContent.Content=DataStore.Can(session,"I02","manage")?new AccessControlView(session):(UserControl)new NoticeView("Không được phép quản lý quyền.");
   else if(f.Code=="I03")FeatureContent.Content=DataStore.Can(session,"I03","read")?new AuditTrailView(session):(UserControl)new NoticeView("Không được phép xem nhật ký.");
   else if(f.Code=="I04")FeatureContent.Content=DataStore.Can(session,"I04","manage")?new BackupView(session):(UserControl)new NoticeView("Không được phép sao lưu và khôi phục.");
   else if(f.Code=="I08")FeatureContent.Content=DataStore.Can(session,"I08","read")?new DataQualityView(session,NavigateToHousehold,NavigateToPerson):(UserControl)new NoticeView("Không được phép kiểm tra chất lượng dữ liệu.");
   else if(f.Code=="I05")FeatureContent.Content=DataStore.Can(session,"I05","read")?new LocalQrIntakeView(session,NavigateToLinked):(UserControl)new NoticeView("Không có quyền I05:read.");
   else if(f.Code=="I06")FeatureContent.Content=DataStore.Can(session,"I06","read")?new OfflineCatalogTransferView(session):(UserControl)new NoticeView("Không có quyền I06:read.");
   else if(f.Code=="I07")FeatureContent.Content=DataStore.Can(session,"I07","read")?new SystemCatalogView(session):(UserControl)new NoticeView("Không có quyền I07:read.");
   else if(f.Code=="I09")FeatureContent.Content=DataStore.Can(session,"I09","read")?new VersionDiagnosticsView(session):(UserControl)new NoticeView("Không có quyền I09:read.");
   }catch(Exception ex){
    StartupDiagnostics.WriteException("NAVIGATION_FAIL",ex);
    FeatureContent.Content=new NoticeView("Không mở được chức năng "+f.Code+". Kiểm tra quyền truy cập hoặc liên hệ quản trị viên.");
   }
  }
 }
}
