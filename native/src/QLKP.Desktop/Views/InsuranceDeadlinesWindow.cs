using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 internal sealed class InsuranceDeadlinesWindow : Window {
  readonly Session session;
  int page;
  readonly ComboBox filter=new ComboBox {Width=170,ItemsSource=new[]{"Sắp hết hạn (30 ngày)","Đã hết hạn"}};
  readonly DataGrid grid=new DataGrid {AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single};
  readonly Button previous=new Button {Content="◀ Trước",Margin=new Thickness(0,0,8,0)};
  readonly Button next=new Button {Content="Sau ▶",Margin=new Thickness(0,0,8,0)};
  readonly Button open=new Button {Content="Mở hồ sơ BHYT",Margin=new Thickness(0,0,8,0)};
  readonly TextBlock status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};

  internal InsuranceDeadlinesWindow(Session session) {
   this.session=session;
   Title="Theo dõi hạn bảo hiểm y tế";Width=690;Height=460;MinWidth=485;MinHeight=360;
   WindowStartupLocation=WindowStartupLocation.CenterOwner;
   WindowLayout.FitInitialSize(this);
   var layout=new DockPanel {Margin=new Thickness(16)};
   var toolbar=new WrapPanel {Margin=new Thickness(0,0,0,10)};
   toolbar.Children.Add(filter);
   var refresh=new Button {Content="Làm mới",Margin=new Thickness(8,0,0,0)};
   toolbar.Children.Add(refresh);
   DockPanel.SetDock(toolbar,Dock.Top);layout.Children.Add(toolbar);
   var footer=new StackPanel {Margin=new Thickness(0,10,0,0)};
   var actions=new WrapPanel();
   actions.Children.Add(previous);actions.Children.Add(next);actions.Children.Add(open);
   actions.Children.Add(new Button {Content="Đóng",IsCancel=true});
   footer.Children.Add(actions);footer.Children.Add(status);
   DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
   grid.Columns.Add(new DataGridTextColumn {Header="Họ và tên",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   grid.Columns.Add(new DataGridTextColumn {Header="Ngày sinh",Binding=new Binding("BirthDate"),Width=115});
   grid.Columns.Add(new DataGridTextColumn {Header="Hết hạn ngày",Binding=new Binding("ExpiryDate"),Width=115});
   grid.Columns.Add(new DataGridTextColumn {Header="Tình trạng",Binding=new Binding("State"),Width=120});
   layout.Children.Add(grid);Content=layout;
   filter.SelectionChanged+=(sender,args)=>{page=0;Reload();};
   refresh.Click+=(sender,args)=>Reload();
   previous.Click+=(sender,args)=>{if(page>0){page--;Reload();}};
   next.Click+=(sender,args)=>{page++;Reload();};
   grid.SelectionChanged+=(sender,args)=>open.IsEnabled=grid.SelectedItem is InsuranceDeadlineRow;
   open.Click+=OpenRecord;
   filter.SelectedIndex=0;
  }

  void Reload() {
   grid.ItemsSource=null;previous.IsEnabled=false;next.IsEnabled=false;open.IsEnabled=false;
   try {
    var result=DataStore.GetInsuranceDeadlines(session,DataStore.ReportLocalToday,filter.SelectedIndex==1,page);
    page=result.PageIndex;
    grid.ItemsSource=result.Items;
    status.Text=result.TotalCount==0?"Không có hồ sơ phù hợp.":"Trang "+(page+1)+"/"+result.TotalPages+" · "+result.TotalCount+" hồ sơ";
    previous.IsEnabled=page>0;next.IsEnabled=page+1<result.TotalPages;
   } catch(Exception ex) {status.Text="Không tải được danh sách. "+ex.Message;}
  }

  void OpenRecord(object sender,RoutedEventArgs args) {
   var selected=grid.SelectedItem as InsuranceDeadlineRow;
   if(selected==null)return;
   try {
    if(!DataStore.AllowsPersonalService(session,"E06",false))throw new UnauthorizedAccessException("Phiên không còn quyền xem BHYT.");
    var person=DataStore.GetPerson(session,selected.PersonId);
    new PersonalServicesWindow(session,person,"E06"){Owner=this}.ShowDialog();
    Reload();
   } catch(Exception ex) {Reload();status.Text="Không mở được hồ sơ. "+ex.Message;}
  }
 }
}
