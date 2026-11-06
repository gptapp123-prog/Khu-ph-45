using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class LocalQrIntakeView:UserControl {
  readonly Session session;readonly Action<string,string> openLink;
  public LocalQrIntakeView(Session s,Action<string,string> navigate){InitializeComponent();session=s;openLink=navigate;}
  void TextFile_Click(object sender,RoutedEventArgs e){try {
   if(!DataStore.Can(session,"I05","read"))throw new UnauthorizedAccessException("Không có quyền I05:read.");
   var select=new OpenFileDialog{Filter="Văn bản OCR UTF-8 (*.txt)|*.txt",Multiselect=false,CheckFileExists=true};
   if(select.ShowDialog(Window.GetWindow(this))!=true)return;
   if(new FileInfo(select.FileName).Length>1024*1024)throw new InvalidDataException("Tệp OCR vượt 1 MB.");
   using(var rd=new StreamReader(select.FileName,new UTF8Encoding(false,true),true))OcrText.Text=rd.ReadToEnd();
   ResultMessage.Text="Đã nhận văn bản để xem trước; chưa tự nhận dạng hay ghi vào kho.";
  }catch(Exception ex){ResultMessage.Text="Từ chối: "+ex.Message;}}
  void Qr_Click(object sender,RoutedEventArgs e){try{
   if(!DataStore.Can(session,"I05","read"))throw new UnauthorizedAccessException("Không có quyền I05:read.");
   var key=DataStore.ParseLocalQrReference(session,QrPayload.Text);
   ResultMessage.Text="QR hợp lệ, mở hồ sơ theo ID đã xác minh, không tìm theo họ tên.";
   openLink(key.Item1,key.Item2);
  }catch(Exception ex){ResultMessage.Text="Từ chối liên kết QR: "+ex.Message;}}
 }
}
