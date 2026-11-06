using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public sealed class CurrentRightsView:UserControl {
  public CurrentRightsView(Session actor) {
   var panel=new StackPanel{Margin=new Thickness(20)};
   panel.Children.Add(new TextBlock{Text="QUYỀN SỬ DỤNG",FontSize=23,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(16,45,89))});
   panel.Children.Add(new TextBlock{Text="Quyền nghiệp vụ của tài khoản trong khu phố "+DataStore.AreaName(actor)+". Tài khoản quản trị được bảo vệ riêng.",TextWrapping=TextWrapping.Wrap});
   foreach(var f in FeatureCatalog.All){var actions=new[]{Tuple.Create("read","Xem"),Tuple.Create("create","Thêm"),Tuple.Create("update","Chỉnh sửa"),Tuple.Create("manage","Quản lý"),Tuple.Create("export","Xuất"),Tuple.Create("merge","Hợp nhất")}.Where(x=>DataStore.Can(actor,f.Code,x.Item1)).Select(x=>x.Item2);panel.Children.Add(new TextBlock{Text=f.Name+": "+String.Join(", ",actions),TextWrapping=TextWrapping.Wrap});}
   Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  }
 }
}
