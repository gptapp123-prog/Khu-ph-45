using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 public partial class VehicleEditWindow : Window {
  readonly Session session;
  readonly VehicleRow original;
  string ownerId,ownerTerm="";
  int ownerPage;
  public string SavedId {get;private set;}
  public string SavedPlate {get;private set;}
  public VehicleEditWindow(Session actor,VehicleRow selected) {
   InitializeComponent();session=actor;original=selected;
   Kind.SelectedIndex=0;
   if(selected!=null){
    Plate.Text=selected.Plate;Brand.Text=selected.Brand;Active.IsChecked=selected.Active;
    Kind.SelectedItem=Kind.Items.Cast<ComboBoxItem>().Single(x=>(string)x.Tag==selected.Kind);
    Measure.Text=Convert.ToString(selected.Seats??selected.PayloadKg);
    ownerId=selected.OwnerId;OwnerName.Text="Chủ xe: "+selected.OwnerName;
   }
   PreviousOwner.IsEnabled=false;NextOwner.IsEnabled=false;
  }
  string KindCode {get{return (Kind.SelectedItem as ComboBoxItem)?.Tag as string;}}
  void Kind_Changed(object sender,SelectionChangedEventArgs e) {
   if(MeasurePanel==null)return;
   string kind=KindCode;
   Measure.Text="";
   MeasurePanel.Visibility=kind=="passenger"||kind=="truck"?Visibility.Visible:Visibility.Collapsed;
   MeasureLabel.Content=kind=="truck"?"Tải trọng (kg)":"Số chỗ";
  }
  void ReloadOwners(){
   try {
    var rows=DataStore.GetPeoplePage(session,ownerTerm,ownerPage,15);
    Owners.ItemsSource=rows.Items;ownerPage=rows.PageIndex;
    PreviousOwner.IsEnabled=ownerPage>0;NextOwner.IsEnabled=ownerPage+1<rows.TotalPages;
   }catch(Exception){Owners.ItemsSource=null;PreviousOwner.IsEnabled=false;NextOwner.IsEnabled=false;Message.Text="Không tải được nhân khẩu.";}
  }
  void FindOwner_Click(object sender,RoutedEventArgs e){ownerTerm=OwnerSearch.Text;ownerPage=0;ReloadOwners();}
  void PreviousOwner_Click(object sender,RoutedEventArgs e){if(PreviousOwner.IsEnabled){ownerPage--;ReloadOwners();}}
  void NextOwner_Click(object sender,RoutedEventArgs e){if(NextOwner.IsEnabled){ownerPage++;ReloadOwners();}}
  void ChooseOwner_Click(object sender,RoutedEventArgs e){
   var selected=Owners.SelectedItem as PersonRow;
   if(selected==null){Message.Text="Chọn một nhân khẩu trong danh sách.";return;}
   ownerId=selected.Id;OwnerName.Text="Chủ xe: "+selected.Display;Message.Text="";
  }
  void Save_Click(object sender,RoutedEventArgs e){
   if(String.IsNullOrEmpty(ownerId)){Message.Text="Chọn hồ sơ nhân khẩu của chủ xe.";return;}
   try {
    SavedId=DataStore.SaveVehicle(session,original,Plate.Text,Brand.Text,KindCode,Measure.Text,ownerId,Active.IsChecked==true);
    SavedPlate=Plate.Text;DialogResult=true;
   }catch(InvalidOperationException ex){Message.Text=ex.Message;}
    catch(UnauthorizedAccessException){Message.Text="Không có quyền hoặc chủ xe ngoài phạm vi được cấp.";}
    catch(Exception){Message.Text="Chưa lưu được phương tiện. Tải lại rồi thử lại.";}
  }
 }
}
