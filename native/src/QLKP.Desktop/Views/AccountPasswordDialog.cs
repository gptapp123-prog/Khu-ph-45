using System;
using System.Windows;
using System.Windows.Controls;

namespace QLKP.Desktop.Views {
 static class AccountPasswordDialog {
  internal static bool Show(Window owner,string title,string proofLabel,Func<string,string,string> save,
   bool changesPassword=true) {
   var proof=new PasswordBox {Margin=new Thickness(0,4,0,12),MinHeight=34};
   var next=new PasswordBox {Margin=new Thickness(0,4,0,12),MinHeight=34};
   var confirm=new PasswordBox {Margin=new Thickness(0,4,0,12),MinHeight=34};
   var feedback=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
   var panel=new StackPanel {Margin=new Thickness(18)};
   panel.Children.Add(new TextBlock {Text=proofLabel,TextWrapping=TextWrapping.Wrap});
   panel.Children.Add(proof);
   if(changesPassword) {
    panel.Children.Add(new TextBlock {Text="Mật khẩu mới (ít nhất 12 ký tự)"});
    panel.Children.Add(next);
    panel.Children.Add(new TextBlock {Text="Nhập lại mật khẩu mới"});
    panel.Children.Add(confirm);
   }
   var buttons=new StackPanel {Orientation=Orientation.Horizontal};
   var submit=new Button {Content="Xác nhận",IsDefault=true,Padding=new Thickness(14,7,14,7)};
   var cancel=new Button {Content="Hủy",IsCancel=true,Margin=new Thickness(8,0,0,0),Padding=new Thickness(14,7,14,7)};
   buttons.Children.Add(submit);
   buttons.Children.Add(cancel);
   panel.Children.Add(buttons);
   panel.Children.Add(feedback);
   var dialog=new Window {
    Owner=owner,Title=title,Width=450,SizeToContent=SizeToContent.Height,
    WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Content=panel
   };
   bool saved=false;
   dialog.Loaded+=(s,e)=>proof.Focus();
   submit.Click+=(s,e)=>{
    if(changesPassword&&(next.Password.Length<12||next.Password!=confirm.Password)) {
     feedback.Text="Nhập mật khẩu ít nhất 12 ký tự và kiểm tra hai lần nhập.";
     return;
    }
    try {
     string recoveryCode=save(proof.Password,next.Password);
     saved=true;
     proof.Clear();
     next.Clear();
     confirm.Clear();
     if(recoveryCode==null) {
      dialog.DialogResult=true;
      return;
     }
     panel.Children.Clear();
     panel.Children.Add(new TextBlock {
      Text="Ghi lại mã này và cất ở nơi an toàn. Mã chỉ hiển thị một lần. Các mã đã cấp trước đó không còn hiệu lực.",
      TextWrapping=TextWrapping.Wrap
     });
     panel.Children.Add(new TextBox {Text=recoveryCode,IsReadOnly=true,Margin=new Thickness(0,12,0,12),FontSize=17});
     var done=new Button {Content="Đóng",IsDefault=true,IsCancel=true,Padding=new Thickness(14,7,14,7)};
     done.Click+=(source,args)=>dialog.DialogResult=true;
     panel.Children.Add(done);
    } catch(InvalidOperationException ex) { feedback.Text=ex.Message; }
      catch(UnauthorizedAccessException ex) { feedback.Text=ex.Message; }
      catch(Exception ex) {
       Core.StartupDiagnostics.WriteException("ACCOUNT_PASSWORD_OPERATION",ex);
       feedback.Text="Không thể hoàn tất thao tác. Vui lòng thử lại.";
      }
   };
   dialog.ShowDialog();
   return saved;
  }
 }
}
