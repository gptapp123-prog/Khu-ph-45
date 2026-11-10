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
  internal bool SavedChanges {get;private set;}
  readonly Session session;IntakePreview approved;WorkbookIntakePreview workbook;
  public DataIntakeWindow(Session account) {InitializeComponent();WindowLayout.FitInitialSize(this);session=account;ResultStatus.Text="Chưa kiểm tra hoặc ghi dữ liệu.";}
  string ModeValue {get {return (IntakeMode.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"";} }
  string SourceValue {get {return (SourceKind.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"";} }
  void InputChanged(object sender,RoutedEventArgs e) {
   approved=null;workbook=null;
   if(AcknowledgeCandidates!=null)AcknowledgeCandidates.IsChecked=false;
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
    if(xlsx) {
     var sheets=XlsxIntakeReader.SheetNames(dialog.FileName);
     int index=sheets.Length==1?0:ChooseSheet(sheets);
     if(index<0)return;
     string table=XlsxIntakeReader.ReadSheet(dialog.FileName,index);
     switch(sheets[index]) {
      case "Hộ và chủ hộ": IntakeMode.SelectedIndex=0;break;
      case "Thêm nhân khẩu": IntakeMode.SelectedIndex=1;break;
      case "Bổ sung nhân khẩu": IntakeMode.SelectedIndex=2;break;
     }
     Payload.Text=table;
    }
    else {
     // Reject broken/unknown encodings rather than silently corrupt Vietnamese names.
     using(var reader=new StreamReader(dialog.FileName,new UTF8Encoding(false,true),true))Payload.Text=reader.ReadToEnd();
    }
    SourceKind.SelectedIndex=xlsx?2:1;SourceLabel.Text=Path.GetFileName(dialog.FileName);
    ResultStatus.Text="Đã nhận nội dung tệp, chỉ ở bộ nhớ. Chưa ghi SQLite.";
   }catch(Exception ex){ResultStatus.Text="Không đọc được tệp: "+ex.Message;}
  }
  void ReadWorkbook_Click(object sender,RoutedEventArgs e) {
   var picker=new OpenFileDialog{Filter="Sổ Excel (*.xlsx)|*.xlsx",Multiselect=false,CheckFileExists=true};
   if(picker.ShowDialog(this)!=true)return;
   Payload.Clear();SourceLabel.Text=Path.GetFileName(picker.FileName);SourceKind.SelectedIndex=2;
   InputChanged(sender,e);
   try {
    var preview=DataStore.PreviewWorkbookIntake(session,picker.FileName);
    Payload.Text=String.Join(Environment.NewLine,preview.Rows.GroupBy(row=>row.SheetName)
     .Select(group=>group.Key+" · "+group.Count()+" dòng"));
    workbook=preview;
    PreviewRows.ItemsSource=preview.Rows.Take(100).ToArray();
    ResultStatus.Text="Đã kiểm tra "+preview.Count+" dòng. "+(preview.HasCandidateWarnings?
     "Có trường hợp nghi trùng cần đối chiếu. ":"")+"Bấm xác nhận để ghi tất cả các trang trong một lần.";
    CommitButton.IsEnabled=true;
   } catch(Exception ex){ResultStatus.Text="Không thể chuẩn bị sổ Excel. "+ex.Message;}
  }
  void ConvertLegacyWorkbook_Click(object sender,RoutedEventArgs e) {
   var source=new OpenFileDialog{Filter="Sổ Dân cư v2.1 (*.xlsx)|*.xlsx",CheckFileExists=true,Multiselect=false};
   if(source.ShowDialog(this)!=true)return;
   InputChanged(sender,e);
   Payload.Clear();
   try {
    var result=DataStore.ConvertLegacyResidentWorkbook(source.FileName);
    if(MessageBox.Show(this,result.Summary+"\n\nTạo tệp Excel cục bộ có dữ liệu dân cư? Chưa ghi vào kho.",
       "Đối chiếu sổ Dân cư v2.1",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
    var target=new SaveFileDialog{Filter="Sổ nhập QLKP (*.xlsx)|*.xlsx",DefaultExt=".xlsx",AddExtension=true,
     FileName="QLKP_DanCu_DaChuyen.xlsx"};
    if(target.ShowDialog(this)!=true)return;
    result.Save(target.FileName);
    ResultStatus.Text="Đã tạo sổ chuyển đổi. Bấm Kiểm tra sổ Excel để xem trước và nhập tệp mới.";
   } catch(Exception ex){ResultStatus.Text="Chưa chuyển sổ v2.1. "+ex.Message;}
  }
  void PasteClipboard_Click(object sender,RoutedEventArgs e) {
   try {
    if(!Clipboard.ContainsText())throw new InvalidOperationException("Clipboard chưa có bảng dạng chữ.");
    Payload.Text=Clipboard.GetText();SourceKind.SelectedIndex=3;SourceLabel.Text="bang_dan_clipboard";
    ResultStatus.Text="Đã dán văn bản. Chưa ghi vào kho.";
   }catch(Exception ex){ResultStatus.Text="Không đọc Clipboard: "+ex.Message;}
  }
  int ChooseSheet(string[] sheets) {
   var selector=new ComboBox{ItemsSource=sheets,SelectedIndex=0,Margin=new Thickness(0,8,0,12)};
   var accept=new Button{Content="Đọc trang đã chọn",IsDefault=true,Padding=new Thickness(12,6,12,6)};
   var cancel=new Button{Content="Hủy",IsCancel=true,Margin=new Thickness(8,0,0,0),Padding=new Thickness(12,6,12,6)};
   var buttons=new StackPanel{Orientation=Orientation.Horizontal};
   buttons.Children.Add(accept);buttons.Children.Add(cancel);
   var panel=new StackPanel{Margin=new Thickness(16)};
   panel.Children.Add(new TextBlock{Text="Chọn trang tính chứa tiêu đề cột và dữ liệu cần nhập."});
   panel.Children.Add(selector);panel.Children.Add(buttons);
   var window=new Window{Owner=this,Title="Chọn trang tính Excel",Width=430,SizeToContent=SizeToContent.Height,
    ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
   accept.Click+=(s,e)=>window.DialogResult=true;
   return window.ShowDialog()==true?selector.SelectedIndex:-1;
  }
  void SaveTemplate_Click(object sender,RoutedEventArgs e) {
   try {
    var picker=new SaveFileDialog{Filter="Sổ Excel (*.xlsx)|*.xlsx",DefaultExt=".xlsx",AddExtension=true,FileName="QLKP_MauNhap_DanCu.xlsx"};
    if(picker.ShowDialog(this)!=true)return;
    DataStore.SaveIntakeTemplate(picker.FileName);
    ResultStatus.Text="Đã lưu mẫu Excel gồm ba trang nhập Dân cư.";
   } catch(Exception ex){ResultStatus.Text="Không lưu được mẫu Excel. "+ex.Message;}
  }
  void Template_Click(object sender,RoutedEventArgs e) {
   Payload.Text=String.Join("\t",DataStore.IntakeHeaders(ModeValue))+"\n";
   ResultStatus.Text="Đã chèn tiêu đề cột. Nhập dữ liệu hoặc dán các hàng bên dưới.";
  }
  void Preview_Click(object sender,RoutedEventArgs e) {
   if(workbook!=null)return;
   approved=null;CommitButton.IsEnabled=false;PreviewRows.ItemsSource=null;
   AcknowledgeCandidates.IsChecked=false;
   try {
    approved=DataStore.PreviewIntake(session,ModeValue,SourceValue,SourceLabel.Text,Payload.Text);
    PreviewRows.ItemsSource=approved.Rows.Take(100).ToArray();
    ResultStatus.Text="Đã đối chiếu "+approved.Rows.Count+" dòng. "+(approved.HasCandidateWarnings?
      "Có nghi trùng; không tự gộp và cần xác nhận thủ công.":"Chưa tìm thấy xung đột hiện tại.")+" Chưa ghi CSDL.";
    CommitButton.IsEnabled=true;
   }catch(Exception ex){ResultStatus.Text="Không đủ điều kiện nhập: "+ex.Message;}
  }
  void Commit_Click(object sender,RoutedEventArgs e) {
   if(workbook!=null) {CommitWorkbook();return;}
   CommitButton.IsEnabled=false;
   if(approved==null || approved.Payload!=Payload.Text || approved.Mode!=ModeValue || approved.SourceKind!=SourceValue || approved.SourceLabel!=Path.GetFileName(SourceLabel.Text.Trim())){
    ResultStatus.Text="Dữ liệu/nguồn đã thay đổi, cần xem trước lại.";return;
   }
   if(approved.HasCandidateWarnings && AcknowledgeCandidates.IsChecked!=true){ResultStatus.Text="Chưa xác nhận đã đối chiếu nghi trùng.";return;}
   if(MessageBox.Show("Xác nhận xử lý "+approved.Rows.Count+" dòng ở khu phố hiện tại? Lô sẽ ghi hoặc hoàn tác toàn bộ; không tự hợp nhất người cùng tên.",
    "Xác nhận ghi dữ liệu",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes){CommitButton.IsEnabled=true;return;}
   try {
    var count=DataStore.CommitIntake(session,approved,AcknowledgeCandidates.IsChecked==true);
    SavedChanges=true;
    ResultStatus.Text="Đã ghi/xử lý "+count+" dòng. Lô có ID kiểm soát riêng, không ghi lại khi gửi trùng.";
    approved=null;
   }catch(Exception ex){ResultStatus.Text="Không ghi bất kỳ phần nào của lô: "+ex.Message;approved=null;}
  }
  void CommitWorkbook() {
   bool allow=AcknowledgeCandidates.IsChecked==true;
   if(workbook.HasCandidateWarnings&&!allow) {
    ResultStatus.Text="Cần xác nhận đã đối chiếu các trường hợp nghi trùng.";return;
   }
   if(MessageBox.Show(this,"Ghi "+workbook.Count+" dòng trong sổ vào kho khu phố hiện tại? Nếu có lỗi, cả sổ sẽ được hoàn tác.",
      "Xác nhận ghi sổ Excel",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
   CommitButton.IsEnabled=false;
   try {
    int count=DataStore.CommitWorkbookIntake(session,workbook,allow);
    SavedChanges=true;
    ResultStatus.Text="Đã ghi "+count+" dòng từ sổ Excel trong một giao dịch.";
   } catch(Exception ex){ResultStatus.Text="Không ghi sổ. "+ex.Message;}
   finally {workbook=null;}
  }
 }
}
