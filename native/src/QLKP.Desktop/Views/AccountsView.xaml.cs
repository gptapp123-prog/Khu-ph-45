using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class AccountsView : UserControl {
  readonly Session session;
  readonly Action signOut;
  readonly Dictionary<CheckBox,string> choices;
  public AccountsView(Session s,Action signOut){InitializeComponent();session=s;this.signOut=signOut;
   RecoveryButton.Visibility=s.Role=="ROOT_ADMIN"?Visibility.Visible:Visibility.Collapsed;
   CreateSection.Visibility=DataStore.Can(s,"I01","manage")?Visibility.Visible:Visibility.Collapsed;
   CustomizeRights.Visibility=s.Role=="ROOT_ADMIN"?Visibility.Collapsed:Visibility.Visible;
   UserRights.Visibility=Visibility.Collapsed;
   AdditionalRightsSection.Visibility=Visibility.Collapsed;
   Instruction.Text="Quyền mặc định của tài khoản nghiệp vụ được cấp trong phạm vi quản trị khu phố được ủy quyền.";
   var defaults=new HashSet<string>(DataStore.OrdinaryAccountDefaults());
   var delegatable=s.Role=="KP_ADMIN"&&CreateSection.Visibility==Visibility.Visible?
    DataStore.GetDelegatableAccountRights(s):new HashSet<string>();
   var baseRights=new Dictionary<CheckBox,string> {
    {ReadHousehold,"B01:read"},{CreateHousehold,"B01:create"},{ReadPeople,"B02:read"},
    {CreatePeople,"B02:create"},{UpdatePeople,"B02:update"},{ReadChanges,"B03:read"},
    {CreateChanges,"B03:create"},{ReadQuality,"I08:read"}
   };
   foreach(var right in ModuleCatalog.AdditionalRights()) {
    var check=new CheckBox{Content=right,Tag=right,Margin=new Thickness(7,3,7,3)};
    AdditionalRights.Children.Add(check);
    baseRights.Add(check,right);
   }
   foreach(var entry in baseRights) {
    entry.Key.IsEnabled=delegatable.Contains(entry.Value);
    entry.Key.IsChecked=entry.Key.IsEnabled&&defaults.Contains(entry.Value);
   }
   choices=baseRights;
   Reload();
  }
  void Customize_Changed(object sender,RoutedEventArgs e){if(UserRights==null||AdditionalRightsSection==null)return;UserRights.Visibility=AdditionalRightsSection.Visibility=CustomizeRights.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
  void Reload(){try{Accounts.ItemsSource=DataStore.ListAccounts(session);}catch(Exception ex){Accounts.ItemsSource=null;AccountsFeedback.Text="Không tải được danh sách tài khoản: "+ex.Message;}}
  void ChangePassword_Click(object sender,RoutedEventArgs e) {
   if(AccountPasswordDialog.Show(Window.GetWindow(this),"Đổi mật khẩu","Mật khẩu hiện tại",(current,next)=>{
    DataStore.ChangeOwnPassword(session,current,next);return null;
   }))signOut();
  }
  void RecoveryCode_Click(object sender,RoutedEventArgs e) {
   AccountPasswordDialog.Show(Window.GetWindow(this),"Tạo mã phục hồi quản trị",
    "Nhập mật khẩu hiện tại để tạo mã phục hồi mới. Mã cũ sẽ hết hiệu lực.",
    (current,unused)=>DataStore.IssueRootRecoveryCode(session,current),false);
  }
  void Create_Click(object sender,RoutedEventArgs e){
   try {
    var rights=new List<string>();
    foreach(var choice in choices)if(choice.Key.IsEnabled&&choice.Key.IsChecked==true)rights.Add(choice.Value);
    DataStore.CreateAccount(session,Username.Text,Password.Password,CustomizeRights.IsChecked==true?rights:null);
    Username.Clear();Password.Clear();Reload();AccountsFeedback.Text="Đã tạo tài khoản theo quyền được cấp.";
   }catch(Exception ex){AccountsFeedback.Text="Không thể cấp tài khoản: "+ex.Message;}
  }
 }
}
