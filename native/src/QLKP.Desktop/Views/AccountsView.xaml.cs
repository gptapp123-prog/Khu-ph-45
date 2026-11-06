using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class AccountsView : UserControl {
  readonly Session session;
  public AccountsView(Session s){InitializeComponent();session=s;
   CreateSection.Visibility=DataStore.Can(s,"I01","manage")?Visibility.Visible:Visibility.Collapsed;
   CustomizeRights.Visibility=s.Role=="ROOT_ADMIN"?Visibility.Collapsed:Visibility.Visible;
   UserRights.Visibility=Visibility.Collapsed;
   AdditionalRightsSection.Visibility=Visibility.Collapsed;
   Instruction.Text="Tài khoản mới có sẵn quyền xem, thêm và chỉnh sửa toàn bộ nghiệp vụ trong khu phố. Tài khoản quản trị được bảo vệ riêng.";
   foreach(var cb in new[]{ReadHousehold,CreateHousehold,ReadPeople,CreatePeople,UpdatePeople,ReadChanges,CreateChanges,ReadQuality})cb.IsChecked=true;
   foreach(var right in ModuleCatalog.AdditionalRights()) {
    AdditionalRights.Children.Add(new CheckBox{Content=right,Tag=right,IsChecked=true,Margin=new Thickness(7,3,7,3)});
   }
   Reload();
  }
  void Customize_Changed(object sender,RoutedEventArgs e){if(UserRights==null||AdditionalRightsSection==null)return;UserRights.Visibility=AdditionalRightsSection.Visibility=CustomizeRights.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
  void Reload(){try{Accounts.ItemsSource=DataStore.ListAccounts(session);}catch(Exception ex){Accounts.ItemsSource=null;AccountsFeedback.Text="Không tải được danh sách tài khoản: "+ex.Message;}}
  void Create_Click(object sender,RoutedEventArgs e){
   try {
    var rights=new List<string>();
    if(ReadHousehold.IsChecked==true)rights.Add("B01:read");if(CreateHousehold.IsChecked==true)rights.Add("B01:create");
    if(ReadPeople.IsChecked==true)rights.Add("B02:read");if(CreatePeople.IsChecked==true)rights.Add("B02:create");
    if(UpdatePeople.IsChecked==true)rights.Add("B02:update");
    if(ReadChanges.IsChecked==true)rights.Add("B03:read");if(CreateChanges.IsChecked==true)rights.Add("B03:create");if(ReadQuality.IsChecked==true)rights.Add("I08:read");
    foreach(CheckBox cb in AdditionalRights.Children)if(cb.IsChecked==true)rights.Add(Convert.ToString(cb.Tag));
    DataStore.CreateAccount(session,Username.Text,Password.Password,CustomizeRights.IsChecked==true?rights:null);
    Username.Clear();Password.Clear();Reload();AccountsFeedback.Text="Đã tạo tài khoản theo quyền được cấp.";
   }catch(Exception ex){AccountsFeedback.Text="Không thể cấp tài khoản: "+ex.Message;}
  }
 }
}
