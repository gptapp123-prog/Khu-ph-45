using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 public partial class LocalQrIntakeView:UserControl {
  sealed class Target {
   public string Id {get;set;}
   public string Label {get;set;}
  }
  readonly Session session;
  readonly Action<string,string> openLink;
  string preparedPayload;
  int page;
  const int PageSize=25;
  public LocalQrIntakeView(Session s,Action<string,string> navigate) {
   InitializeComponent();session=s;openLink=navigate;
   var types=new List<ComboBoxItem>();
   if(DataStore.Can(s,"B01","read"))types.Add(new ComboBoxItem{Tag="B01",Content="Hộ gia đình"});
   if(DataStore.Can(s,"B02","read"))types.Add(new ComboBoxItem{Tag="B02",Content="Nhân khẩu"});
   TargetType.ItemsSource=types;TargetType.SelectedIndex=0;
   SearchTargets.IsEnabled=types.Count>0;
  }

  string FeatureCode {get{return (TargetType.SelectedItem as ComboBoxItem)?.Tag as string;}}
  void SearchInput_Changed(object sender,RoutedEventArgs e) {
   if(Targets==null)return;
   page=0;Targets.ItemsSource=null;ClearPrepared();
   PreviousTargets.IsEnabled=false;NextTargets.IsEnabled=false;
   ResultMessage.Text="";
  }
  void Target_Changed(object sender,SelectionChangedEventArgs e) {
   ClearPrepared();
   CreateQr.IsEnabled=Targets.SelectedItem is Target;
  }
  void ClearPrepared() {
   preparedPayload=null;PreviewQr.Source=null;PreviewQr.Visibility=Visibility.Collapsed;SaveQr.IsEnabled=false;
  }

  void Search_Click(object sender,RoutedEventArgs e){page=0;LoadTargets();}
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;LoadTargets();}}
  void Next_Click(object sender,RoutedEventArgs e){if(NextTargets.IsEnabled){page++;LoadTargets();}}
  void LoadTargets() {
   Targets.ItemsSource=null;PreviousTargets.IsEnabled=false;NextTargets.IsEnabled=false;
   try {
    if(!DataStore.Can(session,"I05","read"))throw new UnauthorizedAccessException("Không có quyền mở mã QR hồ sơ.");
    if(FeatureCode=="B01") {
     var rows=DataStore.GetHouseholdsPage(session,Keyword.Text,page,PageSize);
     ShowTargets(rows.Items.Select(row=>new Target{Id=row.Id,Label=row.Code+" · "+row.Head+" · "+row.Address}),rows.PageIndex,rows.TotalPages);
    } else if(FeatureCode=="B02") {
     var rows=DataStore.GetPeoplePage(session,Keyword.Text,page,PageSize);
     ShowTargets(rows.Items.Select(row=>new Target{Id=row.Id,Label=row.Display+" · Hộ "+row.HouseholdCode}),rows.PageIndex,rows.TotalPages);
    }
   } catch(Exception ex){ResultMessage.Text="Không thể tìm hồ sơ. "+ex.Message;}
  }
  void ShowTargets(IEnumerable<Target> rows,int index,long totalPages) {
   Targets.ItemsSource=rows.ToArray();page=index;
   PreviousTargets.IsEnabled=page>0;NextTargets.IsEnabled=page+1<totalPages;
   ResultMessage.Text=totalPages==0?"Không tìm thấy hồ sơ.":"Trang "+(page+1)+"/"+totalPages+". Chọn một hồ sơ để tạo mã.";
  }
  void Create_Click(object sender,RoutedEventArgs e) {
   ClearPrepared();
   if(!(Targets.SelectedItem is Target target))return;
   try {
    var reference=DataStore.ParseLocalQrReference(session,"QLKP1|"+FeatureCode+"|"+target.Id);
    string payload="QLKP1|"+reference.Item1+"|"+reference.Item2;
    PreviewQr.Source=LocalQrImage.Create(payload);PreviewQr.Visibility=Visibility.Visible;
    preparedPayload=payload;SaveQr.IsEnabled=true;ResultMessage.Text="Đã tạo mã QR cho hồ sơ đã chọn.";
   } catch(Exception ex){ResultMessage.Text="Không tạo được QR. "+ex.Message;}
  }
  void Save_Click(object sender,RoutedEventArgs e) {
   if(preparedPayload==null)return;
   var picker=new SaveFileDialog{Filter="Ảnh PNG (*.png)|*.png",DefaultExt=".png",AddExtension=true,FileName="QLKP_QR.png"};
   if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
   try {
    DataStore.ParseLocalQrReference(session,preparedPayload);
    if(!String.Equals(System.IO.Path.GetExtension(picker.FileName),".png",StringComparison.OrdinalIgnoreCase))
     throw new InvalidOperationException("Chọn tệp có đuôi .png.");
    LocalQrImage.Save((BitmapSource)PreviewQr.Source,picker.FileName);
    ResultMessage.Text="Đã lưu ảnh QR.";
   } catch(Exception ex){ResultMessage.Text="Không lưu được ảnh QR. "+ex.Message;}
  }
  void OpenImage_Click(object sender,RoutedEventArgs e) {
   var picker=new OpenFileDialog{Filter="Ảnh QR (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true};
   if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
   try {OpenReference(LocalQrImage.Read(picker.FileName));}
   catch(Exception ex){ResultMessage.Text="Không mở được hồ sơ QR. "+ex.Message;}
  }
  void Qr_Click(object sender,RoutedEventArgs e) {
   try {OpenReference(QrPayload.Text);}
   catch(Exception ex){ResultMessage.Text="Không mở được hồ sơ QR. "+ex.Message;}
  }
  void OpenReference(string payload) {
   var key=DataStore.ParseLocalQrReference(session,payload);
   openLink(key.Item1,key.Item2);
  }
 }
}
