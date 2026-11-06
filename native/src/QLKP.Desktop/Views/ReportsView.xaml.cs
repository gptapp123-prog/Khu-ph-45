using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class ReportsView : UserControl {
  readonly Session session;readonly string feature;readonly Action<string,string> openRecord;
  int page;
  public ReportsView(Session s,string code,Action<string,string> navigate){
   if(!new[]{"H01","H02","H03","H04","H05"}.Contains(code))throw new ArgumentException("Không phải màn báo cáo.");
   session=s;feature=code;openRecord=navigate;InitializeComponent();
   Heading.Text=FeatureCatalog.All.First(x=>x.Code==code).ToString();
   var today=DateTime.Today;FromDate.Text=new DateTime(today.Year,today.Month,1).ToString("dd-MM-yyyy",CultureInfo.InvariantCulture);
   ToDate.Text=today.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture);
   try{var available=DataStore.GetReportableSources(session,feature);
    available.Insert(0,"(Tất cả)");Source.ItemsSource=available;Source.SelectedIndex=0;
   }catch(Exception ex){Notice.Text="Không có quyền nguồn: "+ex.Message;}
   ExportButton.Visibility=feature=="H05"?Visibility.Visible:Visibility.Collapsed;
   ExportButton.IsEnabled=feature=="H05"&&DataStore.Can(session,"H05","export");
   if(feature=="H04"){Source.SelectedItem="A02";Source.IsEnabled=false;}
   Refresh();
  }
  void Refresh_Click(object sender,RoutedEventArgs e){page=0;Refresh();}
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;Refresh();}}
  void Next_Click(object sender,RoutedEventArgs e){page++;Refresh();}
  void Refresh(){try {
   var p=DataStore.GetReportPage(session,feature,DisplayLabels.IsoDate(FromDate.Text),DisplayLabels.IsoDate(ToDate.Text),
    Convert.ToString(Source.SelectedItem),StatusFilter.Text.Trim(),page);
   page=p.PageIndex;Results.ItemsSource=p.Items;
   PageInfo.Text="Trang "+(p.TotalPages==0?0:page+1)+"/"+p.TotalPages+" · "+p.TotalCount+" dòng";
   Previous.IsEnabled=page>0;Next.IsEnabled=page+1<p.TotalPages;
   Notice.Text=feature=="H05"?"Chỉ xuất tổng hợp; nhóm 1–4 hồ sơ được ẩn số chính xác. Không xuất danh tính hay ghi chú.":
    (feature=="H04"?"Theo dõi hạn công việc A02 trong kỳ và các việc chưa hoàn thành đã quá hạn từ trước kỳ; trạng thái hiện tại.":
     "Nhóm 1–4 hồ sơ được ẩn số chính xác. Số liệu theo trạng thái hiện tại, KHÔNG phải ảnh chụp lịch sử tại cuối kỳ.");
  }catch(Exception ex){Results.ItemsSource=null;Previous.IsEnabled=false;Next.IsEnabled=false;PageInfo.Text="";Notice.Text="Không thể tạo báo cáo: "+ex.Message;}}
  void OpenRecord_DoubleClick(object sender,MouseButtonEventArgs e){
   if(feature!="H02"&&feature!="H04")return;
   var selected=Results.SelectedItem as ReportLine;
   if(selected==null||String.IsNullOrEmpty(selected.RecordId)||openRecord==null)return;
   try{if(!DataStore.Can(session,selected.Feature,"read"))throw new UnauthorizedAccessException("Không còn quyền mở hồ sơ.");
    DataStore.GetModuleRecord(session,selected.Feature,selected.RecordId);
    openRecord(selected.Feature,selected.RecordId);
   }catch(Exception ex){Notice.Text="Không mở được ID gốc: "+ex.Message;}
  }
  void Export_Click(object sender,RoutedEventArgs e){
   if(feature!="H05")return;
   try {
    string content=DataStore.ExportReportAggregateCsv(session,DisplayLabels.IsoDate(FromDate.Text),DisplayLabels.IsoDate(ToDate.Text),
     Convert.ToString(Source.SelectedItem),StatusFilter.Text.Trim());
    var choose=new SaveFileDialog{Title="Lưu báo cáo tổng hợp (CSV UTF-8)",Filter="CSV UTF-8 (*.csv)|*.csv",FileName="QLKP_H05_TongHop.csv",AddExtension=true,DefaultExt=".csv"};
    if(choose.ShowDialog(Window.GetWindow(this))!=true)return;
    // Fail closed on overwrite; overwrite requires separate explicit user action.
    if(File.Exists(choose.FileName))throw new IOException("Tệp đã tồn tại. Chọn tên khác để tránh ghi đè.");
    using(var fs=new FileStream(choose.FileName,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     byte[] bytes=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
     fs.Write(bytes,0,bytes.Length);fs.Flush(true);
    }
    DataStore.LogReportExport(session);
    Notice.Text="Đã xuất CSV tổng hợp, không có dữ liệu cá nhân. Đường dẫn: "+choose.FileName;
   }catch(Exception ex){Notice.Text="Chưa xuất báo cáo: "+ex.Message;}
  }
 }
}
