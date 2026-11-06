using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class TestHistoryWindow:Window {
  readonly Session session;int page;string selectedId;bool busy;
  public TestHistoryWindow(Session actor){InitializeComponent();WindowLayout.FitInitialSize(this);session=actor;RunButton.IsEnabled=DataStore.Can(actor,"I09","test");ImportButton.IsEnabled=RunButton.IsEnabled;Reload();}
  void Reload(){try{var result=TestRunHistory.List(session,page);page=result.PageIndex;Runs.ItemsSource=result.Items;Previous.IsEnabled=page>0;Next.IsEnabled=page+1<result.TotalPages;PageInfo.Text="Trang "+(result.TotalPages==0?0:page+1)+"/"+result.TotalPages+" · "+result.TotalCount+" phiên";}
   catch(Exception ex){Runs.ItemsSource=null;Steps.ItemsSource=null;selectedId=null;ExportJson.IsEnabled=false;ExportHtml.IsEnabled=false;Previous.IsEnabled=false;Next.IsEnabled=false;Message.Text="Không tải được lịch sử: "+ex.GetType().Name;}}
  void ShowReport(string id){try{var r=TestRunHistory.Read(session,id);selectedId=id;Steps.ItemsSource=r.Steps;
    SelectedInfo.Text="Phiên "+r.Id+(String.IsNullOrEmpty(r.ParentId)?"":" · Phiên cha "+r.ParentId)+" · "+r.StatusDisplay+" · "+r.EnvironmentName+"\nBắt đầu: "+r.StartedDisplay+" · Kết thúc: "+DisplayLabels.UtcStamp(r.FinishedUtc)+" · "+r.DurationMs+" ms\nMã băm ("+r.HashKind+"): "+r.SourceHash;
    bool export=DataStore.Can(session,"I09","export");ExportJson.IsEnabled=export;ExportHtml.IsEnabled=export;
    Message.Text=r.Origin=="IMPORTED"?"Hồ sơ được nhập từ tệp; nội dung chưa được xác minh độc lập.":r.Status=="RUNNING"?"Phiên chưa có kết quả cuối. Có thể vẫn đang chạy hoặc đã bị gián đoạn; không tính là đạt.":"Đã đọc hồ sơ kết quả theo lớp kiểm thử được ghi.";
   }catch(Exception ex){selectedId=null;Steps.ItemsSource=null;ExportJson.IsEnabled=false;ExportHtml.IsEnabled=false;SelectedInfo.Text="Không đọc được phiên.";Message.Text=ex.GetType().Name;}}
  void Runs_SelectionChanged(object sender,SelectionChangedEventArgs e){var row=Runs.SelectedItem as TestRunReport;if(row==null){selectedId=null;Steps.ItemsSource=null;ExportJson.IsEnabled=false;ExportHtml.IsEnabled=false;return;}ShowReport(row.Id);}
  void Refresh_Click(object sender,RoutedEventArgs e){page=0;Reload();}
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){if(Next.IsEnabled){page++;Reload();}}
  async void Run_Click(object sender,RoutedEventArgs e){if(busy)return;busy=true;RunButton.IsEnabled=false;Message.Text="Đang kiểm tra kho và ghi nhật ký...";
   try{string id=await Task.Run(()=>TestRunHistory.RunDiagnostics(session));page=0;Reload();ShowReport(id);}
   catch(Exception ex){Message.Text="Không chạy hoặc lưu được phiên: "+ex.GetType().Name;}
   finally{busy=false;try{RunButton.IsEnabled=DataStore.Can(session,"I09","test");}catch{RunButton.IsEnabled=false;}}}
  void Import_Click(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Hồ sơ kiểm thử (*.json)|*.json",CheckFileExists=true,Multiselect=false};if(dialog.ShowDialog(this)!=true)return;
   try{string id=TestRunHistory.Import(session,dialog.FileName);page=0;Reload();ShowReport(id);}catch(Exception ex){Message.Text="Không nhập được hồ sơ: "+ex.GetType().Name;}}
  void Export(bool html){if(selectedId==null){Message.Text="Chọn phiên cần xuất.";return;}
   var dialog=new SaveFileDialog{Filter=html?"Báo cáo HTML (*.html)|*.html":"Hồ sơ JSON (*.json)|*.json",DefaultExt=html?".html":".json",AddExtension=true,OverwritePrompt=false,FileName="QLKP-test-"+selectedId+(html?".html":".json")};if(dialog.ShowDialog(this)!=true)return;
   try{TestRunHistory.Export(session,selectedId,dialog.FileName,html);Message.Text="Đã xuất hồ sơ. Tệp đích mới, không ghi đè tệp cũ.";}catch(Exception ex){Message.Text="Không xuất được hồ sơ: "+ex.GetType().Name;}}
  void ExportJson_Click(object sender,RoutedEventArgs e){Export(false);}
  void ExportHtml_Click(object sender,RoutedEventArgs e){Export(true);}
  void Close_Click(object sender,RoutedEventArgs e){Close();}
 }
}
