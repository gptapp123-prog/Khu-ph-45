using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class ResidencyChangeView : UserControl {
  readonly Session session; readonly Action<string> openPerson; readonly Action<string> openHousehold;
  string previewPersonId,previewFromId,previewToId,previewReason,previewRelationship;
  long previewPersonVersion;
  int historyPage,fromPage,toPage;
  public ResidencyChangeView(Session s,Action<string> navigatePerson,Action<string> navigateHousehold) {
   InitializeComponent();session=s;openPerson=navigatePerson;openHousehold=navigateHousehold;
   bool canDraft=DataStore.Can(s,"B03","create") && DataStore.Can(s,"B01","read") && DataStore.Can(s,"B02","read");
   CheckTransferButton.IsEnabled=canDraft;
   Reload();
  }
  void InvalidatePreview() {
   previewPersonId=null;previewFromId=null;previewToId=null;
   previewReason=null;previewRelationship=null;
   if(SaveTransferButton!=null)SaveTransferButton.IsEnabled=false;
  }
  void Reload() {
   ReloadHistory();ReloadFromHouseholds();ReloadToHouseholds();InvalidatePreview();
  }
  void ReloadHistory() {
   if(History==null||HistorySearch==null||session==null)return;
   try { var result=DataStore.GetResidencyChangePage(session,HistorySearch.Text,historyPage);historyPage=result.PageIndex;History.ItemsSource=result.Items;
    HistoryPrevious.IsEnabled=historyPage>0;HistoryNext.IsEnabled=historyPage+1<result.TotalPages;
    HistoryPageInfo.Text="Trang "+(result.TotalPages==0?0:historyPage+1)+"/"+result.TotalPages+" · "+result.TotalCount+" biến động"; }
   catch(Exception ex){History.ItemsSource=null;HistoryPrevious.IsEnabled=false;HistoryNext.IsEnabled=false;TransferMessage.Text="Không thể tìm lịch sử: "+ex.GetType().Name;}
  }
  void ReloadFromHouseholds() {
   if(FromHousehold==null||FromFilter==null||session==null)return;
   var previous=FromHousehold.SelectedItem as HouseholdRow;
   try {
    if(!DataStore.Can(session,"B01","read"))throw new UnauthorizedAccessException();
    var result=DataStore.GetHouseholdsPage(session,FromFilter.Text,fromPage);fromPage=result.PageIndex;var rows=result.Items;
    FromPrevious.IsEnabled=fromPage>0;FromNext.IsEnabled=fromPage+1<result.TotalPages;
    FromPageInfo.Text="Trang "+(result.TotalPages==0?0:fromPage+1)+"/"+result.TotalPages;
    FromHousehold.ItemsSource=rows;
    if(previous!=null)FromHousehold.SelectedItem=rows.Find(x=>x.Id==previous.Id);
   }catch(Exception ex){FromHousehold.ItemsSource=null;CurrentMembers.ItemsSource=null;FromPrevious.IsEnabled=false;FromNext.IsEnabled=false;InvalidatePreview();TransferMessage.Text="Không thể tìm hộ chuyển đi: "+ex.GetType().Name;}
  }
  void ReloadToHouseholds() {
   if(ToHousehold==null||ToFilter==null||session==null)return;
   var previous=ToHousehold.SelectedItem as HouseholdRow;
   try {
    if(!DataStore.Can(session,"B01","read"))throw new UnauthorizedAccessException();
    var result=DataStore.GetHouseholdsPage(session,ToFilter.Text,toPage);toPage=result.PageIndex;var rows=result.Items;
    ToPrevious.IsEnabled=toPage>0;ToNext.IsEnabled=toPage+1<result.TotalPages;
    ToPageInfo.Text="Trang "+(result.TotalPages==0?0:toPage+1)+"/"+result.TotalPages;
    ToHousehold.ItemsSource=rows;
    if(previous!=null)ToHousehold.SelectedItem=rows.Find(x=>x.Id==previous.Id);
   }catch(Exception ex){ToHousehold.ItemsSource=null;ToPrevious.IsEnabled=false;ToNext.IsEnabled=false;InvalidatePreview();TransferMessage.Text="Không thể tìm hộ chuyển đến: "+ex.GetType().Name;}
  }
  void FromFilter_TextChanged(object sender,TextChangedEventArgs e) {fromPage=0;InvalidatePreview();ReloadFromHouseholds();}
  void ToFilter_TextChanged(object sender,TextChangedEventArgs e) {toPage=0;InvalidatePreview();ReloadToHouseholds();}
  void HistorySearch_TextChanged(object sender,TextChangedEventArgs e) {historyPage=0;ReloadHistory();}
  void HistoryPrevious_Click(object sender,RoutedEventArgs e){if(historyPage>0){historyPage--;ReloadHistory();}}
  void HistoryNext_Click(object sender,RoutedEventArgs e){if(HistoryNext.IsEnabled){historyPage++;ReloadHistory();}}
  void FromPrevious_Click(object sender,RoutedEventArgs e){if(fromPage>0){fromPage--;InvalidatePreview();ReloadFromHouseholds();}}
  void FromNext_Click(object sender,RoutedEventArgs e){if(FromNext.IsEnabled){fromPage++;InvalidatePreview();ReloadFromHouseholds();}}
  void ToPrevious_Click(object sender,RoutedEventArgs e){if(toPage>0){toPage--;InvalidatePreview();ReloadToHouseholds();}}
  void ToNext_Click(object sender,RoutedEventArgs e){if(ToNext.IsEnabled){toPage++;InvalidatePreview();ReloadToHouseholds();}}
  void Reload_Click(object sender,RoutedEventArgs e){Reload();}
  void FromHousehold_SelectionChanged(object sender,SelectionChangedEventArgs e) {
   InvalidatePreview();
   var from=FromHousehold.SelectedItem as HouseholdRow;
   if(CurrentMembers==null)return;
   if(from==null){CurrentMembers.ItemsSource=null;return;}
   try {CurrentMembers.ItemsSource=DataStore.HouseholdMembers(session,from.Id);}
   catch(Exception ex){CurrentMembers.ItemsSource=null;TransferMessage.Text="Không tải được thành viên: "+ex.GetType().Name;}
  }
  void TransferInput_Changed(object sender,EventArgs e){InvalidatePreview();}
  void CurrentMembers_SelectionChanged(object sender,SelectionChangedEventArgs e){InvalidatePreview();}
  string SelectedRelation() {var item=NewRelationship.SelectedItem as ComboBoxItem;return item==null?null:Convert.ToString(item.Content);}
  void CheckTransfer_Click(object sender,RoutedEventArgs e) {
   InvalidatePreview();
   var from=FromHousehold.SelectedItem as HouseholdRow;
   var dest=ToHousehold.SelectedItem as HouseholdRow;
   var member=CurrentMembers.SelectedItem as PersonRow;
   if(from==null||dest==null||member==null){TransferMessage.Text="Chọn hộ nguồn, hộ đích và đúng thành viên cần chuyển.";return;}
   try {
    var verified=DataStore.GetPerson(session,member.Id);
    if(verified.HouseholdId!=from.Id)throw new InvalidOperationException("Nhân khẩu đã thay đổi hộ. Tải lại.");
    DataStore.CheckTransfer(session,verified.Id,from.Id,dest.Id,SelectedRelation(),Reason.Text,verified.Version);
    previewPersonId=verified.Id;previewPersonVersion=verified.Version;
    previewFromId=from.Id;previewToId=dest.Id;previewReason=Reason.Text;previewRelationship=SelectedRelation();
    SaveTransferButton.IsEnabled=true;
    TransferMessage.Text="Đã kiểm tra: "+verified.FullName+" (ID "+verified.Id+") từ hộ "+from.Code+" sang "+dest.Code+". CHƯA GHI.";
   }catch(Exception ex){TransferMessage.Text="Không hợp lệ: "+ex.Message;}
  }
  void SaveTransfer_Click(object sender,RoutedEventArgs e) {
   var from=FromHousehold.SelectedItem as HouseholdRow;
   var dest=ToHousehold.SelectedItem as HouseholdRow;
   var member=CurrentMembers.SelectedItem as PersonRow;
   SaveTransferButton.IsEnabled=false;
   if(member==null||from==null||dest==null||member.Id!=previewPersonId||from.Id!=previewFromId||dest.Id!=previewToId||
      Reason.Text!=previewReason||SelectedRelation()!=previewRelationship) {
    TransferMessage.Text="Hồ sơ hoặc thông tin đã thay đổi, phải kiểm tra lại.";InvalidatePreview();return;
   }
   if(MessageBox.Show("Xác nhận chuyển nhân khẩu ID "+member.Id+" từ "+from.Code+" sang "+dest.Code+"?\n"+
       "Thao tác sẽ ghi lịch sử, không xóa hồ sơ gốc.","Xác nhận biến động",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) {
    InvalidatePreview();return;
   }
   try {
    DataStore.TransferMember(session,previewPersonId,previewFromId,previewToId,previewRelationship,previewReason,previewPersonVersion);
    TransferMessage.Text="Đã ghi chuyển hộ; lịch sử và liên kết đã được cập nhật.";
    InvalidatePreview();Reload();
   }catch(Exception ex){TransferMessage.Text="Không thể chuyển hộ: "+ex.Message;InvalidatePreview();}
  }
  void OpenPerson_Click(object sender,RoutedEventArgs e) {
   var selected=History.SelectedItem as ResidencyChangeRow;if(selected==null){TransferMessage.Text="Chọn một biến động.";return;}
   try {DataStore.GetPerson(session,selected.PersonId);if(openPerson!=null)openPerson(selected.PersonId);}
   catch(Exception ex){TransferMessage.Text="Không mở được nhân khẩu: "+ex.GetType().Name;}
  }
  void OpenSource_Click(object sender,RoutedEventArgs e){OpenHouseholdOfSelected(false);}
  void OpenDestination_Click(object sender,RoutedEventArgs e){OpenHouseholdOfSelected(true);}
  void OpenHouseholdOfSelected(bool destination) {
   var row=History.SelectedItem as ResidencyChangeRow;if(row==null){TransferMessage.Text="Chọn một biến động.";return;}
   string id=destination?row.ToHouseholdId:row.FromHouseholdId;
   try {DataStore.GetHousehold(session,id);if(openHousehold!=null)openHousehold(id);}
   catch(Exception ex){TransferMessage.Text="Không mở được hồ sơ hộ: "+ex.GetType().Name;}
  }
 }
}
