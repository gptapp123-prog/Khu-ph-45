using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class DataIntakeWindow : Window {
  readonly Session session;IntakePreview approved;
  public DataIntakeWindow(Session account) {InitializeComponent();WindowLayout.FitInitialSize(this);session=account;ResultStatus.Text="Chưa kiểm tra hoặc ghi dữ liệu.";}
  string ModeValue {get {return (IntakeMode.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"";} }
  string SourceValue {get {return (SourceKind.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"";} }
  void InputChanged(object sender,RoutedEventArgs e) {
   approved=null;
   if(CommitButton!=null)CommitButton.IsEnabled=false;
   if(PreviewRows!=null)PreviewRows.ItemsSource=null;
   if(ResultStatus!=null)ResultStatus.Text="Nguồn/biểu mẫu đã thay đổi; phải xem trước lại.";
  }
  void ReadCsv_Click(object sender,RoutedEventArgs e) {
   var dialog=new OpenFileDialog {Filter="Bảng dữ liệu (*.csv;*.tsv;*.txt;*.xlsx)|*.csv;*.tsv;*.txt;*.xlsx",CheckFileExists=true,Multiselect=false};
   if(dialog.ShowDialog(this)!=true)return;
   try {
    var info=new FileInfo(dialog.FileName);
    bool xlsx=String.Equals(Path.GetExtension(dialog.FileName),".xlsx",StringComparison.OrdinalIgnoreCase);
    if(info.Length>(xlsx?12:2)*1024*1024)throw new InvalidDataException("Tệp quá lớn; chia thành nhiều lô.");
    if(xlsx)Payload.Text=XlsxIntakeReader.ReadFirstSheet(dialog.FileName);
    else {
     // Reject broken/unknown encodings rather than silently corrupt Vietnamese names.
     using(var reader=new StreamReader(dialog.FileName,new UTF8Encoding(false,true),true))Payload.Text=reader.ReadToEnd();
    }
    SourceKind.SelectedIndex=xlsx?2:1;SourceLabel.Text=Path.GetFileName(dialog.FileName);
    ResultStatus.Text="Đã nhận nội dung tệp, chỉ ở bộ nhớ. Chưa ghi SQLite.";
   }catch(Exception ex){ResultStatus.Text="Không đọc được tệp: "+ex.Message;}
  }
  void PasteClipboard_Click(object sender,RoutedEventArgs e) {
   try {
    if(!Clipboard.ContainsText())throw new InvalidOperationException("Clipboard chưa có bảng dạng chữ.");
    Payload.Text=Clipboard.GetText();SourceKind.SelectedIndex=3;SourceLabel.Text="bang_dan_clipboard";
    ResultStatus.Text="Đã dán văn bản. Chưa ghi vào kho.";
   }catch(Exception ex){ResultStatus.Text="Không đọc Clipboard: "+ex.Message;}
  }
  void Template_Click(object sender,RoutedEventArgs e) {
   if(ModeValue=="CREATE_HOUSEHOLD")Payload.Text="Mã hộ\tĐịa chỉ\tChủ hộ\n";
   else if(ModeValue=="ADD_MEMBER")Payload.Text="Mã hộ\tHọ tên\tNgày sinh\tGiới tính\tQuan hệ\n";
   else Payload.Text="Mã hồ sơ\tPhiên bản\tNgày sinh\tGiới tính\n";
   ResultStatus.Text="Đã chèn tiêu đề cột. Nhập dữ liệu hoặc dán các hàng bên dưới.";
  }
  void Preview_Click(object sender,RoutedEventArgs e) {
   approved=null;CommitButton.IsEnabled=false;PreviewRows.ItemsSource=null;
   try {
    approved=DataStore.PreviewIntake(session,ModeValue,SourceValue,SourceLabel.Text,Payload.Text);
    PreviewRows.ItemsSource=approved.Rows.Take(100).ToArray();
    ResultStatus.Text="Đã đối chiếu "+approved.Rows.Count+" dòng. "+(approved.HasCandidateWarnings?
      "Có nghi trùng; không tự gộp và cần xác nhận thủ công.":"Chưa tìm thấy xung đột hiện tại.")+" Chưa ghi CSDL.";
    CommitButton.IsEnabled=true;
   }catch(Exception ex){ResultStatus.Text="Không đủ điều kiện nhập: "+ex.Message;}
  }
  void Commit_Click(object sender,RoutedEventArgs e) {
   CommitButton.IsEnabled=false;
   if(approved==null || approved.Payload!=Payload.Text || approved.Mode!=ModeValue || approved.SourceKind!=SourceValue || approved.SourceLabel!=Path.GetFileName(SourceLabel.Text.Trim())){
    ResultStatus.Text="Dữ liệu/nguồn đã thay đổi, cần xem trước lại.";return;
   }
   if(approved.HasCandidateWarnings && AcknowledgeCandidates.IsChecked!=true){ResultStatus.Text="Chưa xác nhận đã đối chiếu nghi trùng.";return;}
   if(MessageBox.Show("Xác nhận xử lý "+approved.Rows.Count+" dòng ở khu phố hiện tại? Lô sẽ ghi hoặc hoàn tác toàn bộ; không tự hợp nhất người cùng tên.",
    "Xác nhận ghi dữ liệu",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes){CommitButton.IsEnabled=true;return;}
   try {
    var count=DataStore.CommitIntake(session,approved,AcknowledgeCandidates.IsChecked==true);
    ResultStatus.Text="Đã ghi/xử lý "+count+" dòng. Lô có ID kiểm soát riêng, không ghi lại khi gửi trùng.";
    approved=null;
   }catch(Exception ex){ResultStatus.Text="Không ghi bất kỳ phần nào của lô: "+ex.Message;approved=null;}
  }
 }
}
