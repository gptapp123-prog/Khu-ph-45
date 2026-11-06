using System;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class SystemCatalogView:UserControl {
  readonly Session session;CatalogEntry selected;int page;
  public SystemCatalogView(Session s) {InitializeComponent();session=s;
   bool edit=DataStore.Can(s,"I07","manage");NewButton.IsEnabled=edit;SaveButton.IsEnabled=edit;
   LoadStandard.Visibility=edit?Visibility.Visible:Visibility.Collapsed;
   Reload();
  }
  void Reload() {try {
   var set=DataStore.ListCatalog(session,SearchTerm.Text,page,50);
   SaveButton.IsEnabled=DataStore.Can(session,"I07","manage");
   CatalogGrid.ItemsSource=set.Items;
   PageInfo.Text="Trang "+(page+1)+" / "+Math.Max(1,(set.TotalCount+49)/50)+" ("+set.TotalCount+" mục)";
   if(page>0 && page*50>=set.TotalCount){page=0;Reload();}
  }catch(Exception ex){CatalogGrid.ItemsSource=null;selected=null;SaveButton.IsEnabled=false;ResultMessage.Text="Không tải được danh mục: "+ex.Message;}}
  void Reload_Click(object sender,RoutedEventArgs e){page=0;selected=null;Reload();}
  void LoadStandard_Click(object sender,RoutedEventArgs e){
   if(MessageBox.Show("Nạp 13 mã loại nơi ở và nhóm cư trú chuẩn? Danh mục đã tồn tại được giữ nguyên.","Xác nhận",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try{int count=DataStore.EnsureReferenceCatalog(session);page=0;selected=null;SearchTerm.Clear();Reload();ResultMessage.Text="Đã thêm "+count+" danh mục chuẩn; giữ nguyên các mục đã có.";}
   catch(Exception ex){ResultMessage.Text="Không nạp được danh mục: "+ex.Message;}
  }
  void Previous_Click(object sender,RoutedEventArgs e){if(page>0){page--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){try{var set=DataStore.ListCatalog(session,SearchTerm.Text,page+1,50);if(set.Items.Count>0){page++;Reload();}}catch(Exception ex){ResultMessage.Text="Không chuyển được trang: "+ex.Message;}}
  void New_Click(object sender,RoutedEventArgs e){selected=null;CatalogGrid.SelectedItem=null;EntryCode.Clear();EntryLabel.Clear();EntryDetails.Clear();EntryActive.IsChecked=true;ResultMessage.Text="Đang tạo mới; mã nhóm và mã mục không thể đổi sau khi lưu.";}
  void CatalogGrid_SelectionChanged(object sender,SelectionChangedEventArgs e){
   selected=CatalogGrid.SelectedItem as CatalogEntry;if(selected==null)return;
   CategoryCode.Text=selected.Category;EntryCode.Text=selected.Code;EntryLabel.Text=selected.Label;
   EntryDetails.Text=selected.Details;EntryActive.IsChecked=selected.Active;
   ResultMessage.Text="Chọn "+selected.Id+" · phiên bản "+selected.Version;
  }
  void Save_Click(object sender,RoutedEventArgs e){try {
   string id=DataStore.SaveCatalog(session,new CatalogEntry{
    Id=selected==null?null:selected.Id,Version=selected==null?0:selected.Version,
    Category=CategoryCode.Text,Code=EntryCode.Text,Label=EntryLabel.Text,
    Details=EntryDetails.Text,Active=EntryActive.IsChecked==true
   });
   selected=null;Reload();ResultMessage.Text="Đã ghi danh mục ID "+id+"; không xóa mã gốc.";
  }catch(Exception ex){ResultMessage.Text="Từ chối lưu: "+ex.Message;}}
 }
}
