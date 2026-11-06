using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class OperationsDashboardView : UserControl {
  readonly Session session;
  public OperationsDashboardView(Session s){InitializeComponent();session=s;Reload();}
  void Refresh_Click(object sender,RoutedEventArgs e){Reload();}
  void Reload(){try{var v=DataStore.GetModuleOverview(session);Summary.ItemsSource=v;
   Message.Text=DataStore.GetWorkspaceSummary(session).Display;
  }catch(Exception ex){Summary.ItemsSource=null;Message.Text="Không tải được tổng hợp: "+ex.Message;}}
 }
}
