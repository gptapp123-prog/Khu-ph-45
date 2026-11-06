using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class DataMergeWindow : Window {
  readonly Session session;MergePlan plan;
  public DataMergeWindow(Session s){InitializeComponent();session=s;WindowLayout.FitInitialSize(this);Closed+=(o,e)=>{if(plan!=null)plan.Dispose();SourcePassword.Clear();};}
  void Choose_Click(object sender,RoutedEventArgs e) {
   var dialog=new OpenFileDialog{Title="Chọn kho QLKP để đối chiếu",Filter="Kho QLKP hoặc bản sao (*.db;*.qlkpbak)|*.db;*.qlkpbak",CheckFileExists=true};
   if(dialog.ShowDialog(this)!=true)return;
   if(plan!=null){plan.Dispose();plan=null;}ApplyButton.IsEnabled=false;Summary.ItemsSource=null;SourcePath.Text=dialog.FileName;SourcePassword.Clear();Result.Text="Đã chọn nguồn. Bấm Đối chiếu để kiểm tra trước khi áp dụng.";
  }
  void Preview_Click(object sender,RoutedEventArgs e) {
   if(plan!=null){plan.Dispose();plan=null;}ApplyButton.IsEnabled=false;Summary.ItemsSource=null;
   try {
    PreviewButton.IsEnabled=false;
    var prepared=DataStore.PreviewMerge(session,SourcePath.Text,SourcePassword.Password);plan=prepared;
    Summary.ItemsSource=plan.Summary;Result.Text="Khu phố "+plan.AreaCode+": "+plan.Added+" dòng mới, "+plan.Identical+" dòng trùng hoàn toàn. Đã kiểm tra liên kết. Không nhập tài khoản, quyền hoặc danh mục toàn hệ thống.";ApplyButton.IsEnabled=plan.Added>0;
   }catch(InvalidOperationException ex){Result.Text=ex.Message;}
   catch(UnauthorizedAccessException){Result.Text="Chưa đủ quyền hợp nhất hoặc quyền đọc và tạo của nhóm dữ liệu nguồn. Đề nghị quản trị hệ thống cấp quyền phù hợp.";}
   catch(Exception ex){StartupDiagnostics.WriteException("DATA_MERGE_PREVIEW_FAILED",ex);Result.Text="Đối chiếu chưa đạt ("+ex.GetType().Name+"). Kiểm tra định dạng, mật khẩu, khóa kho nguồn và liên kết dữ liệu. Kho đang dùng chưa thay đổi.";}
   finally{SourcePassword.Clear();PreviewButton.IsEnabled=true;}
  }
  void Apply_Click(object sender,RoutedEventArgs e) {
   if(plan==null||!ApplyButton.IsEnabled)return;
   if(MessageBox.Show(this,"Áp dụng "+plan.Added+" dòng mới vào khu phố "+plan.AreaCode+"?", "Xác nhận hợp nhất",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   try{ApplyButton.IsEnabled=false;DataStore.ApplyMerge(session,plan);Result.Text="Đã hợp nhất. Bản phục hồi cục bộ: "+plan.RecoveryFile+". Quản trị hệ thống có thể khôi phục tại Sao lưu và khôi phục.";}
   catch(InvalidOperationException ex){Result.Text=ex.Message;}
   catch(Exception ex){StartupDiagnostics.WriteException("DATA_MERGE_APPLY_FAILED",ex);Result.Text="Chưa áp dụng hợp nhất ("+ex.GetType().Name+"). Dừng thao tác và đối chiếu lại; xem nhật ký khởi động nếu cần phục hồi.";}
  }
  void Close_Click(object sender,RoutedEventArgs e){Close();}
 }
}
