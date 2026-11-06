using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class HouseholdView : UserControl {
  readonly Session session;readonly Action<string> navigatePerson;string preselectId;bool pendingPreselection;const int PageSize=50;int pageIndex;string validatedText;string validatedMemberText;string validatedMemberHouseholdId;string validatedMemberKey;
  public HouseholdView(Session s,Action<string> openPerson=null,string householdId=null){InitializeComponent();session=s;navigatePerson=openPerson;preselectId=householdId;pendingPreselection=(householdId!=null);OpenPerson.IsEnabled=DataStore.Can(s,"B02","read");AddHouseholdButton.IsEnabled=DataStore.Can(s,"B01","create");AddMemberButton.IsEnabled=DataStore.Can(s,"B02","create") && DataStore.Can(s,"B01","read");CheckButton.IsEnabled=AddHouseholdButton.IsEnabled;CheckMembersBatchButton.IsEnabled=DataStore.Can(s,"B02","create") && DataStore.Can(s,"B01","read");Reload();}
  void Reload(){try{
   var previous=Households.SelectedItem as HouseholdRow;
   if(pendingPreselection){pageIndex=DataStore.GetHouseholdPageIndexById(session,preselectId,PageSize);}
   var page=DataStore.GetHouseholdsPage(session,Search.Text,pageIndex,PageSize,(HousingFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"");
   pageIndex=page.PageIndex;
   // Deep link uses exact ID. Never insert an unfiltered row into a different page.
   HouseholdRow chosen=null;
   if(pendingPreselection) {
    chosen=page.Items.Find(r=>r.Id==preselectId);
    if(chosen==null)throw new InvalidOperationException("Không định vị được hồ sơ hộ theo ID.");
    pendingPreselection=false;
   }else if(previous!=null)chosen=page.Items.Find(r=>r.Id==previous.Id);
   Households.ItemsSource=page.Items;
   if(chosen!=null)Households.SelectedItem=chosen;
   PageStatus.Text="Trang "+(page.TotalPages==0?0:pageIndex+1)+"/"+page.TotalPages+" · "+page.TotalCount+" hộ";
   PreviousPage.IsEnabled=pageIndex>0;NextPage.IsEnabled=pageIndex+1<page.TotalPages;
  }catch(Exception ex){Households.ItemsSource=null;Members.ItemsSource=null;InvalidateMemberBatchPreview();PreviousPage.IsEnabled=false;NextPage.IsEnabled=false;PageStatus.Text="Lỗi tải danh sách";MessageBox.Show(ex.Message);}}
  void Search_TextChanged(object sender,TextChangedEventArgs e){if(Households!=null){pageIndex=0;Reload();}}
  void HousingFilter_SelectionChanged(object sender,SelectionChangedEventArgs e){if(Households!=null){pageIndex=0;Reload();}}
  void Reload_Click(object sender,RoutedEventArgs e){Reload();}
  void EditHousehold_Click(object sender,RoutedEventArgs e){
   var row=Households.SelectedItem as HouseholdRow;if(row==null){MessageBox.Show("Chọn hộ cần cập nhật.");return;}
   try{var window=new HouseholdEditWindow(session,row.Id){Owner=Window.GetWindow(this)};if(window.ShowDialog()==true){
    Search.Clear();HousingFilter.SelectedIndex=0;preselectId=row.Id;pendingPreselection=true;Reload();
   }}
   catch(Exception ex){MessageBox.Show("Không thể cập nhật hộ: "+ex.Message);}
  }
  void PreviousPage_Click(object sender,RoutedEventArgs e){if(pageIndex>0){pageIndex--;Reload();}}
  void NextPage_Click(object sender,RoutedEventArgs e){if(NextPage.IsEnabled && pageIndex<int.MaxValue){pageIndex++;Reload();}}
  void Households_SelectionChanged(object sender,SelectionChangedEventArgs e) {
   InvalidateMemberBatchPreview();
   if(EditHousehold!=null)EditHousehold.IsEnabled=false;
   var h=Households.SelectedItem as HouseholdRow;if(h==null){Members.ItemsSource=null;MembersNotice.Text="";SelectedDetail.Text="Chọn hộ từ danh sách.";return;}
   try{
    var current=DataStore.GetHousehold(session,h.Id);
    EditHousehold.IsEnabled=DataStore.Can(session,"B01","update");
    EditHousehold.Visibility=EditHousehold.IsEnabled?Visibility.Visible:Visibility.Collapsed;
    SelectedDetail.Text="Mã hộ: "+current.Code+"\nChủ hộ: "+current.Head+"\nĐịa chỉ: "+current.Address+"\nSố thành viên: "+current.MemberCount;
    if(DataStore.Can(session,"B02","read")){
     Members.ItemsSource=DataStore.HouseholdMembers(session,current.Id);
     MembersNotice.Text="Thành viên được tải theo quyền B01 + B02.";
    }else{
     Members.ItemsSource=null;
     MembersNotice.Text="Chỉ hiển thị thông tin hộ; tài khoản chưa được cấp quyền xem nhân khẩu B02.";
    }
   }
   catch(Exception ex){SelectedDetail.Text="Không thể đọc hồ sơ: "+ex.Message;Members.ItemsSource=null;}
  }
  void OpenPerson_Click(object sender,RoutedEventArgs e){
   var p=Members.SelectedItem as PersonRow;
   if(p==null){MessageBox.Show("Chọn thành viên cần mở hồ sơ.");return;}
   try { DataStore.GetPerson(session,p.Id);if(navigatePerson!=null)navigatePerson(p.Id); }
   catch(Exception ex){MessageBox.Show("Không thể mở hồ sơ liên quan: "+ex.Message);}
  }
  void AddHousehold_Click(object sender,RoutedEventArgs e){
   try{DataStore.AddHousehold(session,NewCode.Text,NewAddress.Text,NewHead.Text);NewCode.Clear();NewAddress.Clear();NewHead.Clear();Reload();}
   catch(Exception ex){MessageBox.Show("Không thể lưu hộ: "+ex.Message);}
  }
  void AddMember_Click(object sender,RoutedEventArgs e) {
   var h=Households.SelectedItem as HouseholdRow;if(h==null){MessageBox.Show("Cần chọn đúng mã hộ trước khi thêm người.");return;}
   try{var relation=(Relationship.SelectedItem as ComboBoxItem).Content.ToString();DataStore.AddMember(session,h.Id,MemberName.Text,relation,null,null);MemberName.Clear();Reload();}
   catch(Exception ex){MessageBox.Show("Không thể bổ sung thành viên: "+ex.Message);}
  }
  void InvalidateMemberBatchPreview() {
   validatedMemberText=null;validatedMemberHouseholdId=null;validatedMemberKey=null;
   if(ImportMembersButton!=null)ImportMembersButton.IsEnabled=false;
  }
  void MembersBatchText_TextChanged(object sender,TextChangedEventArgs e) {
   InvalidateMemberBatchPreview();
   if(MemberBatchStatus!=null)MemberBatchStatus.Text="Đã thay đổi nội dung, cần kiểm tra lại.";
  }
  void CheckMembersBatch_Click(object sender,RoutedEventArgs e) {
   InvalidateMemberBatchPreview();
   var household=Households.SelectedItem as HouseholdRow;
   if(household==null){MemberBatchStatus.Text="Chọn một hộ hợp lệ trước khi kiểm tra.";return;}
   try {
    int n=DataStore.CheckMembersBatch(session,household.Id,MembersBatchText.Text);
    validatedMemberHouseholdId=household.Id;validatedMemberText=MembersBatchText.Text;validatedMemberKey=Guid.NewGuid().ToString("N");
    MemberBatchStatus.Text=n+" người hợp lệ cho hộ "+household.Code+". Chưa ghi.";
    ImportMembersButton.IsEnabled=true;
   }catch(Exception ex){MemberBatchStatus.Text="LỖI: "+ex.Message;}
  }
  void ImportMembersBatch_Click(object sender,RoutedEventArgs e) {
   var household=Households.SelectedItem as HouseholdRow;
   ImportMembersButton.IsEnabled=false;
   if(household==null||household.Id!=validatedMemberHouseholdId||validatedMemberText==null||validatedMemberKey==null||validatedMemberText!=MembersBatchText.Text) {
    MemberBatchStatus.Text="Hộ hoặc nội dung đã thay đổi; kiểm tra lại trước khi lưu.";
    InvalidateMemberBatchPreview();return;
   }
   try {
    var n=DataStore.ImportMembersBatch(session,household.Id,validatedMemberText,validatedMemberKey);
    InvalidateMemberBatchPreview();MembersBatchText.Clear();Reload();
    MemberBatchStatus.Text="Đã ghi "+n+" nhân khẩu vào đúng hộ "+household.Code+".";
   }catch(Exception ex){MemberBatchStatus.Text="Không ghi lô: "+ex.Message;InvalidateMemberBatchPreview();}
  }
  void CheckBatch_Click(object sender,RoutedEventArgs e){
   validatedText=null;ImportButton.IsEnabled=false;
   try{int count=DataStore.CheckBatch(session,BatchText.Text);validatedText=BatchText.Text;BatchStatus.Text=count+" dòng hợp lệ. Chưa ghi.";ImportButton.IsEnabled=true;}
   catch(Exception ex){BatchStatus.Text="LỖI: "+ex.Message;}
  }
  void ImportBatch_Click(object sender,RoutedEventArgs e){
   ImportButton.IsEnabled=false;
   if(validatedText==null||validatedText!=BatchText.Text){BatchStatus.Text="Nội dung thay đổi; kiểm tra lại trước khi lưu.";return;}
   try{var n=DataStore.ImportBatch(session,validatedText);BatchStatus.Text="Đã ghi "+n+" dòng cùng giao dịch.";validatedText=null;BatchText.Clear();Reload();}
   catch(Exception ex){BatchStatus.Text="Không ghi lô: "+ex.Message;}
  }
  void OpenDataIntake_Click(object sender,RoutedEventArgs e) {
   var window=new DataIntakeWindow(session);
   window.Owner=Window.GetWindow(this);
   window.ShowDialog();Reload();
  }

 }
}
