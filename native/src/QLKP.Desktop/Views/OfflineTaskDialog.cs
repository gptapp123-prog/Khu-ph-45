using System.Windows;
using System.Windows.Controls;

namespace QLKP.Desktop.Views {
 sealed class OfflineTaskDialog : Window {
  readonly PasswordBox password=new PasswordBox{MaxLength=128,Margin=new Thickness(0,5,0,12)};
  readonly TextBox briefing=new TextBox{MaxLength=2000,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
   Height=115,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,5,0,12)};
  public string Password {get;private set;}
  public string Briefing {get;private set;}

  public OfflineTaskDialog(Window owner,bool exporting) {
   Owner=owner;Title=exporting?"Xuất gói giao việc":"Mở kết quả Android";Width=450;
   WindowStartupLocation=WindowStartupLocation.CenterOwner;
   SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
   var form=new StackPanel{Margin=new Thickness(18)};
   if(exporting) {
    form.Children.Add(new TextBlock{Text="Nội dung giao việc (tối đa 2.000 ký tự). Chỉ nhập nội dung cần chuyển và tránh đưa thông tin cá nhân vào gói.",TextWrapping=TextWrapping.Wrap});
    form.Children.Add(briefing);
   }
   form.Children.Add(new TextBlock{Text="Mật khẩu gói (14–128 ký tự ASCII, không khoảng trắng)",TextWrapping=TextWrapping.Wrap});
   form.Children.Add(password);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   var cancel=new Button{Content="Hủy",IsCancel=true,Padding=new Thickness(12,5,12,5)};
   var accept=new Button{Content="Tiếp tục",IsDefault=true,Padding=new Thickness(12,5,12,5),Margin=new Thickness(8,0,0,0)};
   accept.Click+=(s,e)=>{Password=password.Password;Briefing=briefing.Text;password.Clear();DialogResult=true;};
   actions.Children.Add(cancel);actions.Children.Add(accept);form.Children.Add(actions);
   Content=form;
   Loaded+=(s,e)=>{if(exporting)briefing.Focus();else password.Focus();};
  }
 }
}
