using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class VersionDiagnosticsView:UserControl {
  readonly Session session;
  public VersionDiagnosticsView(Session s){InitializeComponent();session=s;Refresh();}
  void Refresh(){try{Diagnostics.Text=DataStore.VersionDiagnostics(session);}catch(Exception ex){Diagnostics.Text="Lỗi kiểm tra: "+ex.Message;}}
  void Status_Click(object sender,RoutedEventArgs e){Refresh();}
  void History_Click(object sender,RoutedEventArgs e){try{var history=new TestHistoryWindow(session){Owner=Window.GetWindow(this)};history.ShowDialog();}catch(Exception ex){Diagnostics.Text="Không mở được lịch sử kiểm thử: "+ex.GetType().Name;}}
  void Browse_Click(object sender,RoutedEventArgs e){
   var dialog=new OpenFileDialog{Filter="Gói kiểm tra (*.zip;*.exe)|*.zip;*.exe",CheckFileExists=true,Multiselect=false};
   if(dialog.ShowDialog(Window.GetWindow(this))==true){PackagePath.Text=dialog.FileName;HashResult.Text="Chưa xác thực gói.";}
  }
  void Hash_Click(object sender,RoutedEventArgs e){try{
   HashResult.Text=DataStore.VerifyPackageSha256(session,PackagePath.Text,ExpectedHash.Text);
  }catch(Exception ex){HashResult.Text="Không thể kiểm tra: "+ex.Message;}}
 }
}
