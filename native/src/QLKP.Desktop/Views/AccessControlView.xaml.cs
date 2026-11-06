using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class AccessControlView : UserControl {
  readonly Session session;
  public AccessControlView(Session s) { InitializeComponent();session=s;
   foreach(var right in ModuleCatalog.AdditionalRights())AdditionalRights.Children.Add(new CheckBox{Content=right,Tag=right,Margin=new Thickness(4,3,4,3)});
   Reload(); }
  AccountRow Selected { get { return Accounts.SelectedItem as AccountRow; } }
  void Reload() {try{ Accounts.ItemsSource=DataStore.ListAccounts(session);Info.Text=""; }
   catch(Exception e){Accounts.ItemsSource=null;Info.Text="Không thể tải danh sách: "+e.GetType().Name;} }
  void Reload_Click(object sender,RoutedEventArgs e){Reload();}
  void Accounts_SelectionChanged(object sender,SelectionChangedEventArgs e) {
   var row=Selected;SaveRights.IsEnabled=row!=null&&row.Id!=session.Id&&(session.Role=="ROOT_ADMIN"?row.Role=="KP_ADMIN":row.Role=="ASSIGNED_USER");BlockAccount.IsEnabled=UnblockAccount.IsEnabled=row!=null&&row.Role=="ASSIGNED_USER"&&session.Role=="KP_ADMIN"; SelectedAccount.Text=row==null?"Chưa chọn tài khoản":row.Username+" · "+DisplayLabels.Role(row.Role)+" · "+row.Status;
   RightB01Read.IsChecked=false;RightB01Create.IsChecked=false;RightB02Read.IsChecked=false;RightB02Create.IsChecked=false;RightB02Update.IsChecked=false;RightB03Read.IsChecked=false;RightB03Create.IsChecked=false;RightI08Read.IsChecked=false;
   foreach(CheckBox cb in AdditionalRights.Children)cb.IsChecked=false;
   if(row==null)return;
   if(row.Id==session.Id||row.Role=="ROOT_ADMIN"||(session.Role!="ROOT_ADMIN"&&row.Role!="ASSIGNED_USER")){Info.Text="Tài khoản quản trị được bảo vệ; không thay đổi từ thao tác này.";return;}
   try {
    var rights=new HashSet<string>(DataStore.GetAccountRights(session,row.Id));
    RightB01Read.IsChecked=rights.Contains("B01:read");RightB01Create.IsChecked=rights.Contains("B01:create");
    RightB02Read.IsChecked=rights.Contains("B02:read");RightB02Create.IsChecked=rights.Contains("B02:create");RightB02Update.IsChecked=rights.Contains("B02:update");
    RightB03Read.IsChecked=rights.Contains("B03:read");RightB03Create.IsChecked=rights.Contains("B03:create");RightI08Read.IsChecked=rights.Contains("I08:read");
    foreach(CheckBox cb in AdditionalRights.Children)cb.IsChecked=rights.Contains(Convert.ToString(cb.Tag));Info.Text="";
   } catch(Exception ex){Info.Text="Không có quyền đọc cấu hình: "+ex.GetType().Name;}
  }
  List<string> SelectedRights(){var v=new List<string>();
   if(RightB01Read.IsChecked==true)v.Add("B01:read");if(RightB01Create.IsChecked==true)v.Add("B01:create");
   if(RightB02Read.IsChecked==true)v.Add("B02:read");if(RightB02Create.IsChecked==true)v.Add("B02:create");
   if(RightB02Update.IsChecked==true)v.Add("B02:update");
   if(RightB03Read.IsChecked==true)v.Add("B03:read");if(RightB03Create.IsChecked==true)v.Add("B03:create");if(RightI08Read.IsChecked==true)v.Add("I08:read");
   foreach(CheckBox cb in AdditionalRights.Children)if(cb.IsChecked==true)v.Add(Convert.ToString(cb.Tag));
   return v;}
  void Save_Click(object sender,RoutedEventArgs e){var row=Selected;if(row==null){Info.Text="Chọn tài khoản cần cập nhật quyền.";return;}
   if(MessageBox.Show("Thay đổi quyền của "+row.Username+"?", "Xác nhận",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try{DataStore.SetAccountRights(session,row.Id,SelectedRights());Info.Text="Đã cập nhật quyền; phiên cũ không còn hợp lệ.";}
   catch(Exception ex){Info.Text="Không thể đổi quyền: "+ex.GetType().Name;}}
  void ChangeStatus(bool active) { var row=Selected;if(row==null){Info.Text="Chọn tài khoản cần khóa hoặc mở khóa.";return;}
   if(MessageBox.Show((active?"Mở khóa ":"Khóa ")+row.Username+"?","Xác nhận",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try{DataStore.SetAccountStatus(session,row.Id,active);Reload();Info.Text="Đã thay đổi trạng thái tài khoản.";}
   catch(Exception ex){Info.Text="Không thể đổi trạng thái: "+ex.GetType().Name;}}
  void Block_Click(object sender,RoutedEventArgs e){ChangeStatus(false);}
  void Unblock_Click(object sender,RoutedEventArgs e){ChangeStatus(true);}
 }
}
