using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class AccountsView : UserControl {
  readonly Session session;
  public AccountsView(Session s){InitializeComponent();session=s;
   UserRights.Visibility=s.Role=="ROOT_ADMIN"?Visibility.Collapsed:Visibility.Visible;
   AdditionalRightsSection.Visibility=s.Role=="ROOT_ADMIN"?Visibility.Collapsed:Visibility.Visible;
   Instruction.Text=s.Role=="ROOT_ADMIN"?"ADMIN gốc chỉ được tạo ADMIN khu phố; không mặc nhiên đọc hồ sơ dân cư.":"ADMIN khu phố chỉ tạo tài khoản được cấp trong địa bàn và dưới trần ủy quyền; không thể tự cấp quyền.";
   foreach(var right in ModuleCatalog.AdditionalRights()) {
    AdditionalRights.Children.Add(new CheckBox{Content=right,Tag=right,Margin=new Thickness(7,3,7,3)});
   }
   Reload();
  }
  void Reload(){try{Accounts.ItemsSource=DataStore.ListAccounts(session);}catch(Exception ex){Accounts.ItemsSource=null;MessageBox.Show(ex.Message);}}
  void Create_Click(object sender,RoutedEventArgs e){
   try {
    var rights=new List<string>();
    if(ReadHousehold.IsChecked==true)rights.Add("B01:read");if(CreateHousehold.IsChecked==true)rights.Add("B01:create");
    if(ReadPeople.IsChecked==true)rights.Add("B02:read");if(CreatePeople.IsChecked==true)rights.Add("B02:create");
    if(UpdatePeople.IsChecked==true)rights.Add("B02:update");
    if(ReadChanges.IsChecked==true)rights.Add("B03:read");if(CreateChanges.IsChecked==true)rights.Add("B03:create");if(ReadQuality.IsChecked==true)rights.Add("I08:read");
    foreach(CheckBox cb in AdditionalRights.Children)if(cb.IsChecked==true)rights.Add(Convert.ToString(cb.Tag));
    DataStore.CreateAccount(session,Username.Text,Password.Password,rights);
    Username.Clear();Password.Clear();Reload();MessageBox.Show("Đã tạo tài khoản. Cấp quyền được kiểm tra tại tầng dữ liệu.");
   }catch(Exception ex){MessageBox.Show("Không thể cấp tài khoản: "+ex.Message);}
  }
 }
}
