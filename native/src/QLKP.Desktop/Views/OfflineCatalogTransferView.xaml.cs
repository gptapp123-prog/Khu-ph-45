using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class OfflineCatalogTransferView:UserControl {
  readonly Session session;
  public OfflineCatalogTransferView(Session s){InitializeComponent();session=s;}
  void Export_Click(object sender,RoutedEventArgs e){try {
   if(!DataStore.Can(session,"I06","export") || !DataStore.Can(session,"I07","read"))
    throw new UnauthorizedAccessException("Cần đồng thời quyền I06:export và I07:read.");
   var picker=new SaveFileDialog{Filter="Gói danh mục mã hóa (*.qlkpsync)|*.qlkpsync",DefaultExt=".qlkpsync",AddExtension=true,OverwritePrompt=true};
   if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
   TransferStatus.Text=OfflineCatalogTransfer.Export(session,picker.FileName,PackagePassword.Password);
   PackagePassword.Clear();
  }catch(Exception ex){PackagePassword.Clear();TransferStatus.Text="Không xuất được gói: "+ex.Message;}}
 }
}
