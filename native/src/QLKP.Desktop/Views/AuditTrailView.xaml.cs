using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class AuditTrailView : UserControl {
  readonly Session session;
  int page;
  public AuditTrailView(Session s){InitializeComponent();session=s;Reload();}
  void Reload(){try{var result=DataStore.GetAuditPage(session,page);page=result.PageIndex;AuditEvents.ItemsSource=result.Items;
    Previous.IsEnabled=page>0;Next.IsEnabled=page+1<result.TotalPages;
    Info.Text="Trang "+(result.TotalPages==0?0:page+1)+"/"+result.TotalPages+" · "+result.TotalCount+" sự kiện";}
   catch(Exception ex){Info.Text="Không thể đọc nhật ký: "+ex.GetType().Name;AuditEvents.ItemsSource=null;Previous.IsEnabled=false;Next.IsEnabled=false;}}
  void Refresh_Click(object sender,RoutedEventArgs e){page=0;Reload();}
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){if(Next.IsEnabled){page++;Reload();}}
 }
}
