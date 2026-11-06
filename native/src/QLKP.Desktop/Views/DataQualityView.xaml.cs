using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class DataQualityView : UserControl {
  readonly Session session;readonly Action<string> openHousehold;readonly Action<string> openPerson;
  int page;
  public DataQualityView(Session s,Action<string> household,Action<string> person) {
   InitializeComponent();session=s;openHousehold=household;openPerson=person;
   OpenRecord.IsEnabled=false;Reload();
  }
  void Reload(){try {
   var result=DataStore.GetDataQualityPage(session,page);page=result.PageIndex;
   Issues.ItemsSource=result.Items;
   Previous.IsEnabled=page>0;Next.IsEnabled=page+1<result.TotalPages;
   Status.Text="Trang "+(result.TotalPages==0?0:page+1)+"/"+result.TotalPages+" · "+result.TotalCount+" cảnh báo trong phạm vi quyền.";
  }catch(Exception ex){Issues.ItemsSource=null;Previous.IsEnabled=false;Next.IsEnabled=false;Status.Text="Không thể kiểm tra: "+ex.GetType().Name;}}
  void Reload_Click(object sender,RoutedEventArgs e){page=0;Reload();}
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){if(Next.IsEnabled){page++;Reload();}}
  void Issues_SelectionChanged(object sender,SelectionChangedEventArgs e) {
   var row=Issues.SelectedItem as DataQualityIssue;
   OpenRecord.IsEnabled=row!=null && (row.TargetFeature=="B01"?DataStore.Can(session,"B01","read"):
    row.TargetFeature=="B02"&&DataStore.Can(session,"B02","read"));
  }
  void OpenRecord_Click(object sender,RoutedEventArgs e) {
   var row=Issues.SelectedItem as DataQualityIssue;
   if(row==null||String.IsNullOrEmpty(row.TargetId))return;
   try {
    if(row.TargetFeature=="B01"){
     DataStore.GetHousehold(session,row.TargetId);if(openHousehold!=null)openHousehold(row.TargetId);
    }else if(row.TargetFeature=="B02"){
     DataStore.GetPerson(session,row.TargetId);if(openPerson!=null)openPerson(row.TargetId);
    }else throw new UnauthorizedAccessException("Đích hồ sơ không được hỗ trợ.");
   }catch(Exception ex){Status.Text="Không thể mở hồ sơ đúng ID và quyền: "+ex.GetType().Name;}
  }
 }
}
