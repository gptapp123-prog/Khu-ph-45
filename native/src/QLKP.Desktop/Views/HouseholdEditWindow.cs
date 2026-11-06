using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public sealed class HouseholdEditWindow:Window {
  readonly Session actor;readonly HouseholdRow original;
  readonly TextBox address=new TextBox();readonly TextBlock result=new TextBlock{TextWrapping=TextWrapping.Wrap};
  public HouseholdEditWindow(Session session,string id) {
   actor=session;original=DataStore.GetHousehold(session,id);
   if(!DataStore.Can(session,"B01","update"))throw new UnauthorizedAccessException("Chưa được cấp quyền cập nhật hộ.");
   Title="Cập nhật địa chỉ hộ";Width=560;Height=330;MinWidth=420;MinHeight=280;
   WindowStartupLocation=WindowStartupLocation.CenterOwner;Icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/qlkp.ico"));
   var panel=new StackPanel{Margin=new Thickness(20)};
   panel.Children.Add(new TextBlock{Text="CẬP NHẬT ĐỊA CHỈ HỘ",FontSize=20,FontWeight=FontWeights.Bold});
   panel.Children.Add(new TextBlock{Text="Mã hộ: "+original.Code,Margin=new Thickness(4,12,4,4)});
   panel.Children.Add(new TextBlock{Text="Địa chỉ",FontWeight=FontWeights.SemiBold});
   address.Text=original.Address;address.MaxLength=300;panel.Children.Add(address);
   var save=new Button{Content="Lưu địa chỉ",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(4,16,4,8)};
   save.Click+=Save_Click;panel.Children.Add(save);panel.Children.Add(result);
   Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};WindowLayout.FitInitialSize(this);
  }
  void Save_Click(object sender,RoutedEventArgs e){
   if(MessageBox.Show(this,"Xác nhận cập nhật địa chỉ cho hộ "+original.Code+"?","Xác nhận",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   try{DataStore.UpdateHouseholdAddress(actor,original.Id,original.Version,address.Text);DialogResult=true;}
   catch(Exception ex){result.Text="Chưa lưu: "+ex.Message;}
  }
 }
}
