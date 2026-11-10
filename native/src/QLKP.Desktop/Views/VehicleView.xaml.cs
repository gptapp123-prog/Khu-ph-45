using System;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 public partial class VehicleView : UserControl {
  readonly Session session;
  readonly Action<string> openPerson;
  int page;
  string term="";
  public VehicleView(Session actor,Action<string> navigatePerson) {
   InitializeComponent();session=actor;openPerson=navigatePerson;
   AddVehicle.Visibility=DataStore.Can(session,"F08","create")?Visibility.Visible:Visibility.Collapsed;
   EditVehicle.Visibility=DataStore.Can(session,"F08","update")?Visibility.Visible:Visibility.Collapsed;
   Reload();
  }
  void Reload() {
   try {
    var rows=DataStore.GetVehicles(session,term,page);
    Vehicles.ItemsSource=rows.Items;page=rows.PageIndex;
    Previous.IsEnabled=page>0;Next.IsEnabled=page+1<rows.TotalPages;
    PageInfo.Text=rows.TotalCount==0?"Chưa có phương tiện phù hợp.":"Trang "+(page+1)+"/"+rows.TotalPages+" · "+rows.TotalCount+" xe";
   }catch(Exception){Vehicles.ItemsSource=null;Previous.IsEnabled=false;Next.IsEnabled=false;PageInfo.Text="Không tải được danh sách.";}
  }
  void Search_Click(object sender,RoutedEventArgs e){term=Search.Text;page=0;Message.Text="";Reload();}
  void Previous_Click(object sender,RoutedEventArgs e){if(Previous.IsEnabled){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){if(Next.IsEnabled){page++;Reload();}}
  void Add_Click(object sender,RoutedEventArgs e){Edit(null);}
  void Edit_Click(object sender,RoutedEventArgs e){
   var selected=Vehicles.SelectedItem as VehicleRow;
   if(selected==null){Message.Text="Chọn xe cần cập nhật.";return;}
   Edit(selected);
  }
  void Edit(VehicleRow selected) {
   try {
    var dialog=new VehicleEditWindow(session,selected){Owner=Window.GetWindow(this)};
    if(dialog.ShowDialog()==true){
     Search.Text=dialog.SavedPlate;term=dialog.SavedPlate;page=0;Reload();
     Vehicles.SelectedItem=Vehicles.Items.OfType<VehicleRow>().FirstOrDefault(row=>row.Id==dialog.SavedId);
     Message.Text="Đã lưu phương tiện.";
    }
   }catch(Exception){Message.Text="Không mở được hồ sơ phương tiện.";}
  }
  void Owner_Click(object sender,RoutedEventArgs e){
   var selected=Vehicles.SelectedItem as VehicleRow;
   if(selected==null){Message.Text="Chọn xe cần mở hồ sơ chủ xe.";return;}
   try{DataStore.GetPerson(session,selected.OwnerId);openPerson?.Invoke(selected.OwnerId);}
   catch(Exception){Message.Text="Không mở được hồ sơ chủ xe.";}
  }
 }
}
