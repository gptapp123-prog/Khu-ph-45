using System;
using System.Windows;
using System.Windows.Input;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class LoginWindow : Window {
  bool firstRun;
  public LoginWindow(){InitializeComponent();WindowLayout.FitInitialSize(this);RefreshMode();Loaded+=(s,e)=>LoginName.Focus();}
  void RefreshMode(){firstRun=DataStore.IsFirstRun();OwnerSetup.Visibility=SetupButton.Visibility=firstRun?Visibility.Visible:Visibility.Collapsed;LoginButton.Visibility=RecoveryButton.Visibility=firstRun?Visibility.Collapsed:Visibility.Visible;ModeTitle.Text=firstRun?"KHỞI TẠO HỆ THỐNG":"ĐĂNG NHẬP HỆ THỐNG";ModeHint.Text=firstRun?"Tạo quản trị hệ thống đầu tiên và thiết lập khu phố.":"Kho đã có tài khoản. Đăng nhập bằng tài khoản được cấp; quản trị viên cấp thêm tài khoản trong mục Tài khoản.";}
  void ShowMessage(string text){Message.Text=text;Message.BringIntoView();}
  void EnterApp(Session session){var w=new MainWindow(session);LoginPassword.Clear();VisibleLoginPassword.Clear();ConfirmPassword.Clear();w.Show();Close();}
  string CurrentPassword {get{return RevealPassword.IsChecked==true?VisibleLoginPassword.Text:LoginPassword.Password;}}
  void RevealPassword_Changed(object sender,RoutedEventArgs e){
   if(LoginPassword==null||VisibleLoginPassword==null||RevealPassword==null)return;
   if(RevealPassword.IsChecked==true){VisibleLoginPassword.Text=LoginPassword.Password;LoginPassword.Clear();LoginPassword.Visibility=Visibility.Collapsed;VisibleLoginPassword.Visibility=Visibility.Visible;VisibleLoginPassword.Focus();VisibleLoginPassword.CaretIndex=VisibleLoginPassword.Text.Length;}
   else {LoginPassword.Password=VisibleLoginPassword.Text;VisibleLoginPassword.Clear();VisibleLoginPassword.Visibility=Visibility.Collapsed;LoginPassword.Visibility=Visibility.Visible;LoginPassword.Focus();}
  }
  void RecoverRoot_Click(object sender,RoutedEventArgs e) {
   var username=LoginName.Text.Trim();
   if(String.IsNullOrEmpty(username)){ShowMessage("Nhập tên tài khoản quản trị hệ thống trước.");LoginName.Focus();return;}
   if(AccountPasswordDialog.Show(this,"Khôi phục quản trị hệ thống","Mã phục hồi đã lưu",(code,next)=>{
    var replacement=DataStore.RecoverRootPassword(username,code,next);
    if(replacement==null)throw new InvalidOperationException("Không xác minh được mã hoặc xác minh đang tạm khóa.");
    return replacement;
   })) {LoginPassword.Clear();VisibleLoginPassword.Clear();ShowMessage("Đã đặt mật khẩu mới. Hãy đăng nhập lại.");}
  }
  void Diagnostics_Click(object sender,RoutedEventArgs e) {
   try {
    StartupDiagnostics.CheckApplicationFiles();
    StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);
    ShowMessage("Kho dữ liệu qua kiểm tra nhanh. Nhật ký: "+StartupDiagnostics.LogPath);
    StartupDiagnostics.WriteEvent("MANUAL_DIAGNOSTICS_OK");
   } catch(Exception ex) {
    StartupDiagnostics.WriteException("MANUAL_DIAGNOSTICS_FAIL",ex);
    ShowMessage("Chẩn đoán chưa đạt ("+ex.GetType().Name+"). Không xóa dữ liệu; xem "+StartupDiagnostics.LogPath);
   }
  }
  void Login_Click(object sender,RoutedEventArgs e) {
   try {
    var s=DataStore.Login(LoginName.Text,CurrentPassword);
    if(s==null){ShowMessage("Thông tin đăng nhập không đúng hoặc tài khoản đã bị khóa.");return;}
    EnterApp(s);
   }catch(Exception ex){StartupDiagnostics.WriteException("LOGIN_ERROR",ex);ShowMessage("Đăng nhập chưa thành công. Vui lòng kiểm tra ứng dụng hoặc báo quản trị viên.");}
  }
  void LoginPassword_KeyDown(object sender,KeyEventArgs e){if(e.Key!=Key.Enter)return;e.Handled=true;if(firstRun)Setup_Click(sender,new RoutedEventArgs());else Login_Click(sender,new RoutedEventArgs());}
  void Setup_Click(object sender,RoutedEventArgs e){
   try {
    if(!firstRun||!DataStore.IsFirstRun()){RefreshMode();ShowMessage("Kho đã có tài khoản; không thể tạo quản trị hệ thống lần thứ hai.");return;}
    if(String.IsNullOrWhiteSpace(LoginName.Text)||String.IsNullOrWhiteSpace(AreaCode.Text)||String.IsNullOrWhiteSpace(AreaName.Text)){ShowMessage("Nhập đủ tài khoản, mã khu phố và tên khu phố.");return;}
    if(CurrentPassword.Length<12){ShowMessage("Mật khẩu phải có ít nhất 12 ký tự.");return;}
    if(CurrentPassword!=ConfirmPassword.Password){ShowMessage("Hai lần nhập mật khẩu chưa trùng khớp.");return;}
    SetupButton.IsEnabled=false;
    DataStore.SetupOwner(LoginName.Text,CurrentPassword,AreaCode.Text,AreaName.Text);
    var session=DataStore.Login(LoginName.Text,CurrentPassword);RefreshMode();
    if(session==null){ShowMessage("Đã tạo quản trị hệ thống. Hãy đăng nhập bằng tài khoản vừa tạo.");return;}
    EnterApp(session);
   }catch(InvalidOperationException ex){ShowMessage(ex.Message);}
   catch(Exception ex){StartupDiagnostics.WriteException("OWNER_SETUP_ERROR",ex);RefreshMode();ShowMessage(firstRun?"Khởi tạo chưa thành công. Kiểm tra quyền ghi và nhật ký khởi động.":"Tài khoản đã được tạo. Hãy đăng nhập để tiếp tục.");}
   finally{SetupButton.IsEnabled=true;}
  }
 }
}
