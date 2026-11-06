using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class BackupView : UserControl {
  readonly Session admin;
  string restoreFile;
  public BackupView(Session s){InitializeComponent();admin=s;
   if(!DataStore.Can(s,"I04","read"))throw new UnauthorizedAccessException();
   if(s.Role!="ROOT_ADMIN"){BackupActions.Visibility=RestoreActions.Visibility=Visibility.Collapsed;Result.Text="Kho đang được mã hóa. Quản trị hệ thống tạo bản sao và khôi phục toàn bộ kho vì bản sao có chứa tài khoản quản trị. Để đưa hồ sơ vào kho hiện tại, sử dụng Hợp nhất dữ liệu tại I08; tài khoản và quyền đang có được giữ nguyên.";}
  }
  void Backup_Click(object sender,RoutedEventArgs e) {
   if(BackupPass.Password!=BackupPassAgain.Password){Result.Text="Hai mật khẩu không trùng nhau.";return;}
   var dialog=new SaveFileDialog{Title="Chọn nơi lưu bản sao mã hóa",Filter="QLKP encrypted backup (*.qlkpbak)|*.qlkpbak",DefaultExt=".qlkpbak",AddExtension=true,
    FileName="QLKP-BACKUP-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".qlkpbak",OverwritePrompt=false};
   if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
   try {EncryptedBackup.Create(admin,dialog.FileName,BackupPass.Password);Result.Text="Đã tạo tệp sao lưu mã hóa. Hãy lưu mật khẩu phục hồi ở nơi an toàn.";}
   catch(Exception ex){Result.Text="Không thể sao lưu: "+ex.GetType().Name+". Kiểm tra quyền, dung lượng và mật khẩu.";}
   finally {BackupPass.Clear();BackupPassAgain.Clear();}
  }
  void ChooseRestore_Click(object sender,RoutedEventArgs e) {
   var dialog=new OpenFileDialog{Title="Chọn bản sao QLKP",Filter="Bản sao mã hóa (*.qlkpbak;*.qlkpvault)|*.qlkpbak;*.qlkpvault",CheckFileExists=true};
   if(dialog.ShowDialog(Window.GetWindow(this))==true){restoreFile=dialog.FileName;RestorePass.Clear();RestoreFileName.Text=Path.GetFileName(restoreFile);Result.Text="Đã chọn bản sao. Nhập mật khẩu để kiểm tra hoặc khôi phục.";}
  }
  void Validate_Click(object sender,RoutedEventArgs e) {
   try {EncryptedBackup.Validate(admin,restoreFile,RestorePass.Password);Result.Text="Bản sao đã xác thực và vượt kiểm tra toàn vẹn SQLite. Nhập lại mật khẩu khi bấm Khôi phục.";}
   catch(Exception ex){Result.Text="Không thể xác minh bản sao: "+ex.GetType().Name+". Không thay đổi kho hiện tại.";}
   finally {RestorePass.Clear();}
  }
  void Restore_Click(object sender,RoutedEventArgs e) {
   if(String.IsNullOrWhiteSpace(restoreFile)){Result.Text="Chưa chọn bản sao.";return;}
   if(MessageBox.Show("Khôi phục sẽ thay thế toàn bộ kho hiện tại và đăng xuất. Trước khi thay thế, hệ thống sẽ giữ bản rollback đã mã hóa; vẫn phải bảo vệ mật khẩu và kiểm tra khả năng phục hồi. Tiếp tục?",
     "Xác nhận thay thế kho",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   try {EncryptedBackup.Restore(admin,restoreFile,RestorePass.Password);RestorePass.Clear();MessageBox.Show("Khôi phục hoàn tất. Ứng dụng sẽ đóng để buộc đăng nhập lại.");Application.Current.Shutdown();}
   catch(Exception ex){RestorePass.Clear();Result.Text="Khôi phục không thành công: "+ex.GetType().Name+". Kiểm tra nhật ký kỹ thuật; không tự xóa kho.";}
  }
 }
}
