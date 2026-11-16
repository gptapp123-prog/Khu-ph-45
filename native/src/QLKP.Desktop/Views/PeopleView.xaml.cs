using System.Linq;
using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class PeopleView : UserControl {
  readonly Session session;readonly Action<string> navigateHousehold;string preselectId;bool pendingPreselection;const int PageSize=50;int pageIndex;
  public PeopleView(Session s,Action<string> openHousehold=null,string personId=null){InitializeComponent();session=s;navigateHousehold=openHousehold;preselectId=personId;pendingPreselection=(personId!=null);Reload();InsuranceDeadlines.Visibility=DataStore.AllowsPersonalService(s,"E06",false)?Visibility.Visible:Visibility.Collapsed;LookupIdentity.Visibility=DataStore.Can(s,"B02","identity_lookup")?Visibility.Visible:Visibility.Collapsed;OpenHousehold.IsEnabled=DataStore.Can(s,"B01","read");OpenHistoryHousehold.IsEnabled=DataStore.Can(s,"B01","read") && DataStore.Can(s,"B03","read");}
  void PeopleLayout_SizeChanged(object sender,SizeChangedEventArgs e) {
   if(PeopleLayout!=null&&PeopleDetails!=null)
    WindowLayout.ArrangeMasterDetail(PeopleLayout,PeopleDetails,e.NewSize.Width<790,64,36);
  }
  internal void Reload(){try{
   var previous=People.SelectedItem as PersonRow;
   if(pendingPreselection)pageIndex=DataStore.GetPersonPageIndexById(session,preselectId,PageSize);
   var page=DataStore.GetPeoplePage(session,Search.Text,pageIndex,PageSize,(ResidencyFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"");
   pageIndex=page.PageIndex;
   PersonRow chosen=null;
   if(pendingPreselection){
    chosen=page.Items.Find(r=>r.Id==preselectId);
    if(chosen==null)throw new InvalidOperationException("Không định vị được nhân khẩu theo ID.");
    pendingPreselection=false;
   }else if(previous!=null)chosen=page.Items.Find(r=>r.Id==previous.Id);
   People.ItemsSource=page.Items;
   if(chosen!=null)People.SelectedItem=chosen;
   PageStatus.Text="Trang "+(page.TotalPages==0?0:pageIndex+1)+"/"+page.TotalPages+" · "+page.TotalCount+" nhân khẩu";
   PreviousPage.IsEnabled=pageIndex>0;NextPage.IsEnabled=pageIndex+1<page.TotalPages;
  }catch(Exception ex){People.ItemsSource=null;PersonHistory.ItemsSource=null;PreviousPage.IsEnabled=false;NextPage.IsEnabled=false;PageStatus.Text="Lỗi tải danh sách";MessageBox.Show(ex.Message);}}
  void Search_TextChanged(object sender,TextChangedEventArgs e){if(People!=null){pageIndex=0;Reload();}}
  void EditPerson_Click(object sender,RoutedEventArgs e){
   var selected=People.SelectedItem as PersonRow;if(selected==null){MessageBox.Show("Chọn nhân khẩu cần cập nhật.");return;}
   try{var editor=new PersonEditWindow(session,selected.Id){Owner=Window.GetWindow(this)};if(editor.ShowDialog()==true){
    Search.Clear();ResidencyFilter.SelectedIndex=0;preselectId=selected.Id;pendingPreselection=true;Reload();
   }}
   catch(Exception ex){MessageBox.Show("Không thể cập nhật: "+ex.Message);}
  }
  void ResidencyFilter_SelectionChanged(object sender,SelectionChangedEventArgs e){if(People!=null){pageIndex=0;Reload();}}
  void PreviousPage_Click(object sender,RoutedEventArgs e){if(pageIndex>0){pageIndex--;Reload();}}
  void NextPage_Click(object sender,RoutedEventArgs e){if(NextPage.IsEnabled && pageIndex<int.MaxValue){pageIndex++;Reload();}}
  void OpenHousehold_Click(object sender,RoutedEventArgs e){
   var p=People.SelectedItem as PersonRow;
   if(p==null||String.IsNullOrEmpty(p.HouseholdId)){MessageBox.Show("Chưa có hồ sơ hộ được liên kết.");return;}
   try { DataStore.GetHousehold(session,p.HouseholdId);if(navigateHousehold!=null)navigateHousehold(p.HouseholdId); }
   catch(Exception ex){MessageBox.Show("Không thể mở hồ sơ hộ: "+ex.Message);}
  }
  void LookupIdentity_Click(object sender,RoutedEventArgs e) {
   try {
    var dialog=new PersonIdentityWindow(session){Owner=Window.GetWindow(this)};
    if(dialog.ShowDialog()==true) {
     Search.Clear();ResidencyFilter.SelectedIndex=0;
     preselectId=dialog.MatchedPersonId;pendingPreselection=true;Reload();
    }
   }catch(Exception){MessageBox.Show("Không thể mở tra cứu CCCD.");}
  }
  void EditIdentity_Click(object sender,RoutedEventArgs e) {
   var selected=People.SelectedItem as PersonRow;if(selected==null)return;
   try {
    var dialog=new PersonIdentityWindow(session,selected.Id){Owner=Window.GetWindow(this)};
    if(dialog.ShowDialog()==true)Reload();
   }catch(Exception){MessageBox.Show("Không thể mở định danh của hồ sơ.");}
  }
  void OpenResidencePeriods_Click(object sender,RoutedEventArgs e) {
   var selected=People.SelectedItem as PersonRow;
   if(selected==null)return;
   try {new ResidencePeriodsWindow(session,selected.Id){Owner=Window.GetWindow(this)}.ShowDialog();}
   catch(Exception){MessageBox.Show("Không thể mở lịch sử địa chỉ cư trú.");}
  }
  void OpenHistoryHousehold_Click(object sender,RoutedEventArgs e) {
   var selected=People.SelectedItem as PersonRow;
   var history=PersonHistory.SelectedItem as ResidencyChangeRow;
   if(selected==null||history==null||history.PersonId!=selected.Id){HistoryNotice.Text="Chọn đúng nhân khẩu và dòng lịch sử cần mở hộ đích.";return;}
   try {
    DataStore.GetPerson(session,selected.Id);
    DataStore.GetHousehold(session,history.ToHouseholdId);
    if(navigateHousehold!=null)navigateHousehold(history.ToHouseholdId);
   }catch(Exception ex){MessageBox.Show("Không thể mở hộ đích: "+ex.Message);}
  }
  void ReviewResidentWorkbook_Click(object sender,RoutedEventArgs e) {
   var owner=Window.GetWindow(this);
   var picker=new Microsoft.Win32.OpenFileDialog {Filter="Tệp Excel (*.xlsx)|*.xlsx",Multiselect=false};
   if(picker.ShowDialog(owner)!=true)return;
   try {
    var rows=DataStore.ReviewResidentWorkbook(session,picker.FileName);
    var panel=new DockPanel {Margin=new Thickness(16)};
    var summary=new TextBlock {Text=rows.Count+" dòng. Xác định mã hộ và đối chiếu định danh trước khi chuyển dữ liệu sang mẫu nhập hộ–nhân khẩu của QLKP.",
     Margin=new Thickness(0,0,0,12),TextWrapping=TextWrapping.Wrap};
    DockPanel.SetDock(summary,Dock.Top);panel.Children.Add(summary);
    var close=new Button {Content="Đóng",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0),Padding=new Thickness(12,5,12,5)};
    DockPanel.SetDock(close,Dock.Bottom);panel.Children.Add(close);
    var grid=new DataGrid {ItemsSource=rows,CanUserAddRows=false,IsReadOnly=true,AutoGenerateColumns=false};
    grid.Columns.Add(new DataGridTextColumn {Header="Bản ghi",Binding=new System.Windows.Data.Binding("Record")});
    grid.Columns.Add(new DataGridTextColumn {Header="Họ và tên",Binding=new System.Windows.Data.Binding("Name")});
    grid.Columns.Add(new DataGridTextColumn {Header="Ngày sinh",Binding=new System.Windows.Data.Binding("BirthDate")});
    grid.Columns.Add(new DataGridTextColumn {Header="Dữ liệu cần đối chiếu",Binding=new System.Windows.Data.Binding("Issue"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
    panel.Children.Add(grid);
    new Window {Title="Đối chiếu danh sách dân cư",Content=panel,Owner=owner,Width=760,Height=470,
     WindowStartupLocation=WindowStartupLocation.CenterOwner}.ShowDialog();
   } catch(Exception ex) {MessageBox.Show("Không đọc được mẫu dân cư. "+ex.Message);}
  }
  void InsuranceDeadlines_Click(object sender,RoutedEventArgs e) {
   new InsuranceDeadlinesWindow(session){Owner=Window.GetWindow(this)}.ShowDialog();
  }
  void PersonalServices_Click(object sender,RoutedEventArgs e) {
   var selected=People.SelectedItem as PersonRow;
   if(selected==null)return;
   try {
    var person=DataStore.GetPerson(session,selected.Id);
    new PersonalServicesWindow(session,person){Owner=Window.GetWindow(this)}.ShowDialog();
   } catch(Exception ex) {MessageBox.Show("Không mở được hồ sơ cá nhân. "+ex.Message);}
  }
  void People_SelectionChanged(object sender,SelectionChangedEventArgs e){
   var p=People.SelectedItem as PersonRow;
   if(EditPerson!=null)EditPerson.IsEnabled=false;
   if(EditIdentity!=null)EditIdentity.Visibility=Visibility.Collapsed;
   if(PersonalServices!=null)PersonalServices.Visibility=Visibility.Collapsed;
   if(OpenResidencePeriods!=null)OpenResidencePeriods.Visibility=Visibility.Collapsed;
   PersonHistory.ItemsSource=null;
   if(p==null){Detail.Text="Chọn một nhân khẩu.";HistoryNotice.Text="";return;}
   try {
    // Revalidate selected ID at the service boundary; do not trust stale row data.
    var current=DataStore.GetPerson(session,p.Id);
    EditPerson.Visibility=DataStore.Can(session,"B02","update")?Visibility.Visible:Visibility.Collapsed;
    EditPerson.IsEnabled=EditPerson.Visibility==Visibility.Visible;
    PersonalServices.Visibility=DataStore.PersonalServices.Any(service=>DataStore.AllowsPersonalService(session,service.Feature,false))
     ?Visibility.Visible:Visibility.Collapsed;
    if(EditPerson.IsEnabled&&DataStore.Can(session,"B02","identity_write"))EditIdentity.Visibility=Visibility.Visible;
    Detail.Text="Mã hồ sơ: "+current.Id+"\nHọ và tên: "+current.FullName+"\nNgày sinh: "+current.BirthDateDisplay+"\nGiới tính: "+current.Gender+"\nMã hộ hiện tại: "+current.HouseholdCode+"\nQuan hệ: "+current.Relationship;
    if(DataStore.Can(session,"B03","read")){
     OpenResidencePeriods.Visibility=Visibility.Visible;
     var history=DataStore.ListPersonResidencyHistory(session,current.Id);
     PersonHistory.ItemsSource=history;
     HistoryNotice.Text=history.Count==0?"Chưa ghi nhận biến động B03 cho hồ sơ này.":history.Count+" sự kiện được ghi, chỉ chọn dòng khi cần mở hộ đích.";
    }else HistoryNotice.Text="Tài khoản chưa được cấp quyền xem lịch sử B03.";
   } catch(Exception ex){Detail.Text="Không thể mở chi tiết hồ sơ: "+ex.Message;HistoryNotice.Text="Không tải được lịch sử.";}
  }
 }
}
