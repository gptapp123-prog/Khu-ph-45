using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 public partial class ResidencePeriodsWindow : Window {
  readonly Session session;
  readonly string personId;
  int page;

  public ResidencePeriodsWindow(Session actor,string selectedPersonId) {
   InitializeComponent();
   session=actor;personId=selectedPersonId;
   var person=DataStore.GetPerson(session,personId);
   PersonTitle.Text=person.FullName;
   ValidFrom.Text=DateTime.Today.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture);
   Reload();
  }

  void Reload() {
   Previous.IsEnabled=false;Next.IsEnabled=false;
   Editor.Visibility=Visibility.Collapsed;
   try {
    var result=DataStore.GetResidencePeriods(session,personId,page);
    Periods.ItemsSource=result.Items;
    page=result.PageIndex;
    Previous.IsEnabled=page>0;Next.IsEnabled=page+1<result.TotalPages;
    PageInfo.Text=result.TotalCount==0?"Chưa có kỳ cư trú.":"Trang "+(page+1)+"/"+result.TotalPages+" · "+result.TotalCount+" kỳ";
    Editor.Visibility=DataStore.Can(session,"B03","create")?Visibility.Visible:Visibility.Collapsed;
   }catch(Exception) {
    Periods.ItemsSource=null;PageInfo.Text="";
    Message.Text="Không thể tải lịch sử. Kiểm tra lại quyền hoặc mở lại hồ sơ.";
   }
  }

  void Save_Click(object sender,RoutedEventArgs e) {
   string kind=(Kind.SelectedItem as ComboBoxItem)?.Tag as string;
   try {
    DataStore.AddResidencePeriod(session,personId,kind,Address.Text,ValidFrom.Text,ValidUntil.Text);
   }catch(InvalidOperationException ex){Message.Text=ex.Message;return;}
    catch(UnauthorizedAccessException){Message.Text="Tài khoản chưa được cấp quyền ghi kỳ cư trú.";return;}
    catch(Exception){Message.Text="Chưa lưu được kỳ cư trú. Tải lại lịch sử rồi thử lại.";return;}
   Address.Clear();ValidUntil.Clear();page=0;
   Message.Text="Đã ghi kỳ địa chỉ cư trú.";
   Reload();
  }

  void Previous_Click(object sender,RoutedEventArgs e){if(Previous.IsEnabled){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){if(Next.IsEnabled){page++;Reload();}}
 }
}
