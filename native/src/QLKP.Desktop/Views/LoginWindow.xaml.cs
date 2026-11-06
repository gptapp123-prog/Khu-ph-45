using System;
using System.Windows;
using System.Windows.Input;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class LoginWindow : Window {
  public LoginWindow(){InitializeComponent();WindowLayout.FitInitialSize(this);OwnerSetup.Visibility=DataStore.IsFirstRun()?Visibility.Visible:Visibility.Collapsed;}
  string CurrentPassword {get{return RevealPassword.IsChecked==true?VisibleLoginPassword.Text:LoginPassword.Password;}}
  void RevealPassword_Changed(object sender,RoutedEventArgs e){
   if(LoginPassword==null||VisibleLoginPassword==null||RevealPassword==null)return;
   if(RevealPassword.IsChecked==true){VisibleLoginPassword.Text=LoginPassword.Password;LoginPassword.Clear();LoginPassword.Visibility=Visibility.Collapsed;VisibleLoginPassword.Visibility=Visibility.Visible;VisibleLoginPassword.Focus();VisibleLoginPassword.CaretIndex=VisibleLoginPassword.Text.Length;}
   else {LoginPassword.Password=VisibleLoginPassword.Text;VisibleLoginPassword.Clear();VisibleLoginPassword.Visibility=Visibility.Collapsed;LoginPassword.Visibility=Visibility.Visible;LoginPassword.Focus();}
  }
  void Diagnostics_Click(object sender,RoutedEventArgs e) {
   try {
    StartupDiagnostics.CheckApplicationFiles();
    StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);
    Message.Text="Kho dữ liệu thử nghiệm qua kiểm tra nhanh. Nhật ký: "+StartupDiagnostics.LogPath;
    StartupDiagnostics.WriteEvent("MANUAL_DIAGNOSTICS_OK");
   } catch(Exception ex) {
    StartupDiagnostics.WriteException("MANUAL_DIAGNOSTICS_FAIL",ex);
    Message.Text="Chẩn đoán chưa đạt ("+ex.GetType().Name+"). Không xóa dữ liệu; xem "+StartupDiagnostics.LogPath;
   }
  }
  void Login_Click(object sender,RoutedEventArgs e) {
   try {
    var s=DataStore.Login(LoginName.Text,CurrentPassword);
    if(s==null){Message.Text="Thông tin đăng nhập không đúng hoặc tài khoản đã bị khóa.";return;}
    var w=new MainWindow(s);LoginPassword.Clear();VisibleLoginPassword.Clear();w.Show();Close();
   }catch(Exception ex){StartupDiagnostics.WriteException("LOGIN_ERROR",ex);Message.Text="Đăng nhập chưa thành công. Vui lòng kiểm tra ứng dụng hoặc báo quản trị viên.";}
  }
  void LoginPassword_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter)Login_Click(sender,new RoutedEventArgs());}
  void Setup_Click(object sender,RoutedEventArgs e){
   try {DataStore.SetupOwner(LoginName.Text,CurrentPassword,AreaCode.Text,AreaName.Text);OwnerSetup.Visibility=Visibility.Collapsed;Message.Text="Đã tạo ADMIN gốc; sử dụng tài khoản vừa thiết lập để đăng nhập.";}
   catch(Exception ex){Message.Text="Không thể tạo ADMIN gốc: "+ex.Message;}
  }
 }
}
