using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public sealed class PersonEditWindow : Window {
  readonly Session actor;readonly PersonRow original;
  readonly TextBox name=new TextBox(),birth=new TextBox();
  readonly ComboBox gender=new ComboBox();readonly TextBlock notice=new TextBlock{TextWrapping=TextWrapping.Wrap};
  public PersonEditWindow(Session session,string personId) {
   actor=session;original=DataStore.GetPerson(session,personId);
   if(!DataStore.Can(session,"B02","update"))throw new UnauthorizedAccessException("Chưa được cấp quyền sửa nhân khẩu.");
   Title="Cập nhật hồ sơ nhân khẩu";Width=510;Height=410;MinWidth=450;MinHeight=380;
   Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/qlkp.ico"));
   WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.CanResize;
   var panel=new StackPanel{Margin=new Thickness(20)};
   panel.Children.Add(new TextBlock{Text="CẬP NHẬT NHÂN KHẨU",FontSize=20,FontWeight=FontWeights.Bold});
   panel.Children.Add(new TextBlock{Text="ID: "+original.Id,Margin=new Thickness(0,8,0,8)});
   panel.Children.Add(new TextBlock{Text="Họ và tên"});name.Text=original.FullName;panel.Children.Add(name);
   panel.Children.Add(new TextBlock{Text="Ngày sinh (dd-MM-yyyy; có thể để trống)",Margin=new Thickness(0,8,0,0)});
   DateTime date;birth.Text=DateTime.TryParseExact(original.BirthDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?date.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture):original.BirthDate;
   panel.Children.Add(birth);panel.Children.Add(new TextBlock{Text="Giới tính",Margin=new Thickness(0,8,0,0)});
   gender.ItemsSource=new[]{"","Nam","Nữ","Khác","Không xác định"};gender.SelectedItem=original.Gender??"";panel.Children.Add(gender);
   var save=new Button{Content="Lưu thay đổi",Margin=new Thickness(0,16,0,8),Padding=new Thickness(10,6,10,6),HorizontalAlignment=HorizontalAlignment.Right};
   save.Click+=Save_Click;panel.Children.Add(save);panel.Children.Add(notice);
   Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
   WindowLayout.FitInitialSize(this);
  }
  void Save_Click(object sender,RoutedEventArgs e) {
   try {
    string iso="",input=birth.Text.Trim();DateTime date;
    if(input.Length>0){
     if(!DateTime.TryParseExact(input,"dd-MM-yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))
      throw new InvalidOperationException("Ngày sinh phải theo dd-MM-yyyy.");
     iso=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    }
    if(MessageBox.Show(this,"Lưu thay đổi cho đúng hồ sơ ID "+original.Id+"?","Xác nhận",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    DataStore.UpdatePersonProfile(actor,original.Id,original.Version,name.Text,iso,Convert.ToString(gender.SelectedItem));
    DialogResult=true;
   }catch(Exception ex){notice.Text="Chưa lưu: "+ex.Message;}
  }
 }
}
