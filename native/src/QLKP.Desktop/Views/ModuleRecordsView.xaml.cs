using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;
namespace QLKP.Desktop.Views {
 public partial class ModuleRecordsView : UserControl {
  readonly Session session;readonly string moduleCode;readonly bool backlog;
  readonly Action<string,string> navigateTo; string preselectId;bool pendingPreselection;
  readonly Dictionary<string,Control> editors=new Dictionary<string,Control>();
  int pageIndex;long version;string activeId;
  int linkPageIndex;string lastLinkFeature;string lastLinkKeyword;
  public ModuleRecordsView(Session s,string feature,Action<string,string> openRelated=null,string selectedId=null) {
   InitializeComponent();session=s;navigateTo=openRelated;preselectId=selectedId;pendingPreselection=(selectedId!=null);
   backlog=feature=="A05";moduleCode=backlog?"A02":feature;
   var spec=ModuleCatalog.Get(moduleCode);
   FeatureTitle.Text=(backlog?"A05 · Công việc tồn đọng":moduleCode+" · "+spec.Label);
   Guidance.Text=backlog?"Chọn nhiệm vụ rồi bấm Mở nhiệm vụ để xử lý.":"Chọn hồ sơ để cập nhật hoặc tạo hồ sơ mới.";
   RecordStatus.ItemsSource=spec.Statuses;
   AssigneePicker.Visibility=spec.Fields.Any(x=>x.Type=="account")?Visibility.Visible:Visibility.Collapsed;
   foreach(var f in spec.Fields){
    string label=f.Type=="date"?f.Label.Replace(" (yyyy-MM-dd)","")+" (dd-MM-yyyy)":f.Label;
    TypedFields.Children.Add(new TextBlock {Text=label+(f.Required?" *":""),Margin=new Thickness(0,6,0,1)});
    Control editor;
    if(f.Type.StartsWith("choice:",StringComparison.Ordinal)) {
     var options=new ComboBox();
     options.ItemsSource=f.Type.Substring(7).Split('|');
     options.ToolTip="Chọn trong danh mục hợp lệ, không tự suy đoán phân loại.";
     editor=options;
    } else editor=new TextBox();
    TypedFields.Children.Add(editor);editors[f.Key]=editor;
   }
   RelatedFeature.Items.Add("(Không liên kết)");
   foreach(var f in ModuleCatalog.AllowedTargets(moduleCode).Where(x=>x!=moduleCode && DataStore.Can(s,x,"read")))RelatedFeature.Items.Add(f);
   RelatedFeature.SelectedIndex=0;
   RelationKind.ItemsSource=new[]{"THAM_CHIEU","NGUON","TRA_LOI","PHAN_CONG","BIEN_BAN","PHOI_HOP"};
   RelationKind.SelectedIndex=0;
   bool editable=!backlog && (DataStore.Can(s,moduleCode,"create")||DataStore.Can(s,moduleCode,"update"));
   NewButton.IsEnabled=!backlog&&DataStore.Can(s,moduleCode,"create");SaveButton.IsEnabled=editable;
   NewButton.Visibility=NewButton.IsEnabled?Visibility.Visible:Visibility.Collapsed;
   SaveButton.Visibility=editable?Visibility.Visible:Visibility.Collapsed;
   AddRelationButton.Visibility=!backlog&&moduleCode!="E06"&&moduleCode!="F06"&&DataStore.Can(s,moduleCode,"update")?Visibility.Visible:Visibility.Collapsed;
   OpenTaskButton.Visibility=backlog?Visibility.Visible:Visibility.Collapsed;
   CreateTaskButton.Visibility=(moduleCode=="C01"||moduleCode=="G01")&&DataStore.Can(s,moduleCode,"read")&&DataStore.Can(s,"A02","create")
      ?Visibility.Visible:Visibility.Collapsed;
   if(backlog){RecordCode.IsReadOnly=true;RecordTitle.IsReadOnly=true;RecordNotes.IsReadOnly=true;
    RecordStatus.IsEnabled=false;RelatedFeature.IsEnabled=false;RelatedId.IsReadOnly=true;
    foreach(var v in editors.Values){
     var textbox=v as TextBox;var dropdown=v as ComboBox;
     if(textbox!=null)textbox.IsReadOnly=true;
     if(dropdown!=null)dropdown.IsEnabled=false;
    }}
   ResetEditor();Reload();ApplyLayout();
  }
  void Layout_SizeChanged(object sender,SizeChangedEventArgs e){ApplyLayout();}
  void ApplyLayout(){
   if(RecordsLayout==null||ListRegion==null||DetailRegion==null)return;
   bool compact=ActualWidth<900;
   RecordsLayout.ColumnDefinitions[0].Width=new GridLength(compact?1:3,GridUnitType.Star);
   RecordsLayout.ColumnDefinitions[1].Width=compact?new GridLength(0):new GridLength(2.5,GridUnitType.Star);
   RecordsLayout.RowDefinitions[0].Height=compact?new GridLength(190):new GridLength(1,GridUnitType.Star);
   RecordsLayout.RowDefinitions[1].Height=compact?new GridLength(1,GridUnitType.Star):new GridLength(0);
   Grid.SetRow(DetailRegion,compact?1:0);Grid.SetColumn(DetailRegion,compact?0:1);
   ListRegion.Margin=compact?new Thickness(0,0,0,8):new Thickness(0,0,10,0);
  }
  void ResetEditor(){activeId=null;version=0;RecordCode.Clear();RecordTitle.Clear();RecordNotes.Clear();
   RecordCode.IsReadOnly=backlog;RecordStatus.SelectedIndex=0;RelatedFeature.SelectedIndex=0;RelatedId.Clear();
   foreach(var editor in editors.Values){
    var textbox=editor as TextBox;var dropdown=editor as ComboBox;
    if(textbox!=null)textbox.Clear();
    if(dropdown!=null)dropdown.SelectedIndex=-1;
   }
   History.ItemsSource=null;
   // Presentation state only; repository authorization is always rechecked on save.
   SaveButton.IsEnabled=!backlog&&NewButton.IsEnabled;
   lastLinkFeature=null;lastLinkKeyword=null;linkPageIndex=0;
   if(LinkResults!=null)LinkResults.ItemsSource=null;
   if(LinkPageInfo!=null)LinkPageInfo.Text="";
   if(Relations!=null)Relations.ItemsSource=null;
   if(AddRelationButton!=null)AddRelationButton.IsEnabled=false;
  }
  void New_Click(object sender,RoutedEventArgs e){Records.SelectedItem=null;ResetEditor();Notice.Text="Hồ sơ mới, chưa lưu.";}
  void Refresh_Click(object sender,RoutedEventArgs e){Reload();}
  void Search_Changed(object sender,TextChangedEventArgs e){if(Records!=null){pageIndex=0;Reload();}}
  void Previous_Click(object sender,RoutedEventArgs e){if(pageIndex>0){pageIndex--;Reload();}}
  void Next_Click(object sender,RoutedEventArgs e){pageIndex++;Reload();}
  void Reload(){try {
   string selected=activeId;
   if(pendingPreselection)pageIndex=DataStore.GetModulePageIndexById(session,moduleCode,preselectId,50);
   var page=DataStore.GetModulePage(session,moduleCode,Search.Text,pageIndex,50,backlog);
   pageIndex=page.PageIndex;Records.ItemsSource=page.Items;
   Previous.IsEnabled=pageIndex>0;Next.IsEnabled=page.TotalPages>pageIndex+1;
   PageInfo.Text="Trang "+(page.TotalPages==0?0:pageIndex+1)+"/"+page.TotalPages+" · "+page.TotalCount+" hồ sơ";
   if(pendingPreselection) {
    var target=page.Items.FirstOrDefault(x=>x.Id==preselectId);
    if(target==null)throw new InvalidOperationException("Không tìm được ID hồ sơ ở trang đã xác minh.");
    Records.SelectedItem=target;pendingPreselection=false;
   } else if(selected!=null)Records.SelectedItem=page.Items.FirstOrDefault(x=>x.Id==selected);
  }catch(Exception ex){Records.ItemsSource=null;ResetEditor();SaveButton.IsEnabled=false;Previous.IsEnabled=false;Next.IsEnabled=false;Notice.Text="Không tải được danh sách: "+ex.Message;}
  }
  void Records_SelectionChanged(object sender,SelectionChangedEventArgs e){
   var selected=Records.SelectedItem as ModuleRecord;if(selected==null){ResetEditor();return;}
   try{
    // Never rely on row text or assumed index; fetch exact ID under current authorization.
    var value=DataStore.GetModuleRecord(session,moduleCode,selected.Id);
    activeId=value.Id;version=value.Version;RecordCode.Text=value.Code;RecordCode.IsReadOnly=true;
    RecordTitle.Text=value.Title;RecordNotes.Text=value.Notes;RecordStatus.SelectedItem=value.Status;
    foreach(var p in editors){string str;string val=value.Fields.TryGetValue(p.Key,out str)?str:"";
     var textbox=p.Value as TextBox;var dropdown=p.Value as ComboBox;
     if(textbox!=null)textbox.Text=ModuleCatalog.Get(moduleCode).Fields.First(x=>x.Key==p.Key).Type=="date"&&!String.IsNullOrWhiteSpace(val)?DisplayLabels.Date(val):val;
     if(dropdown!=null)dropdown.SelectedItem=String.IsNullOrEmpty(val)?null:val;
    }
    // Keep the saved typed link even if its target is no longer in the readable picker.
    // Save/open will reauthorize the target; never turn a missing option into unlinking.
    if(!String.IsNullOrEmpty(value.RelatedFeature)&&!RelatedFeature.Items.Contains(value.RelatedFeature))RelatedFeature.Items.Add(value.RelatedFeature);
    RelatedFeature.SelectedItem=String.IsNullOrEmpty(value.RelatedFeature)?"(Không liên kết)":value.RelatedFeature;
    RelatedId.Text=value.RelatedId;
    History.ItemsSource=DataStore.GetModuleHistory(session,moduleCode,value.Id);
    Relations.ItemsSource=DataStore.GetModuleRelations(session,moduleCode,value.Id);
    AddRelationButton.IsEnabled=!backlog&&moduleCode!="E06"&&moduleCode!="F06"&&DataStore.Can(session,moduleCode,"update");
    SaveButton.IsEnabled=!backlog&&DataStore.Can(session,moduleCode,"update");
    Notice.Text="Đã đọc theo ID: "+activeId+" · Phiên bản "+version;
   }catch(Exception ex){ResetEditor();Notice.Text="Không thể mở hồ sơ: "+ex.Message;}
  }
  void LoadAssignees_Click(object sender,RoutedEventArgs e){
   try{AssigneeOptions.ItemsSource=DataStore.GetModuleAssignees(session,moduleCode);}
   catch(Exception ex){Notice.Text="Không thể tải người phụ trách: "+ex.Message;}
  }
  void AssigneeOptions_SelectionChanged(object sender,SelectionChangedEventArgs e){
   var chosen=AssigneeOptions.SelectedItem as AccountRow;
   Control field;
   if(chosen!=null){
    foreach(var name in new[]{"assignee_id","responsible_id","owner_id","account_id"})
     if(editors.TryGetValue(name,out field) && field is TextBox){((TextBox)field).Text=chosen.Id;break;}
   }
  }
  void ClearLinkPreview(){
   lastLinkFeature=null;lastLinkKeyword=null;linkPageIndex=0;
   if(LinkResults!=null)LinkResults.ItemsSource=null;
   if(RelatedId!=null)RelatedId.Clear();
   if(LinkPageInfo!=null)LinkPageInfo.Text="";
   if(LinkPrevious!=null)LinkPrevious.IsEnabled=false;
   if(LinkNext!=null)LinkNext.IsEnabled=false;
  }
  void RelatedFeature_Changed(object sender,SelectionChangedEventArgs e){ClearLinkPreview();}
  void RelatedKeyword_Changed(object sender,TextChangedEventArgs e){ClearLinkPreview();}
  void Lookup_Click(object sender,RoutedEventArgs e){
   try {
    string feature=RelatedFeature.SelectedItem as string;
    if(moduleCode=="E06" || moduleCode=="F06")throw new InvalidOperationException("Sổ tổng hợp E06/F06 không liên kết hồ sơ cá nhân.");
    if(String.IsNullOrEmpty(feature)||feature=="(Không liên kết)")throw new InvalidOperationException("Chọn loại hồ sơ cần liên kết trước khi tìm.");
    linkPageIndex=0;lastLinkFeature=feature;lastLinkKeyword=RelatedKeyword.Text;
    LoadLinkPage();
   }catch(Exception ex){LinkResults.ItemsSource=null;LinkPageInfo.Text="";Notice.Text="Không tìm liên kết: "+ex.Message;}
  }
  void LoadLinkPage(){
   string selectedFeature=RelatedFeature.SelectedItem as string;
   if(selectedFeature!=lastLinkFeature || RelatedKeyword.Text!=lastLinkKeyword)
    throw new InvalidOperationException("Đã thay đổi bộ lọc hoặc loại hồ sơ; hãy tìm lại để tránh dùng ID sai ngữ cảnh.");
   var page=DataStore.GetLinkCandidatesPage(session,lastLinkFeature,lastLinkKeyword,linkPageIndex,30);
   linkPageIndex=page.PageIndex;
   // A stale ID must not survive a filter or page change.
   RelatedId.Clear();LinkResults.ItemsSource=page.Items;
   LinkPrevious.IsEnabled=linkPageIndex>0;LinkNext.IsEnabled=page.TotalPages>linkPageIndex+1;
   LinkPageInfo.Text="Trang "+(page.TotalPages==0?0:linkPageIndex+1)+"/"+page.TotalPages+" · "+page.TotalCount+" kết quả";
   Notice.Text="Chọn kết quả đúng theo ID; trùng tên không tự ghép hoặc tự mở hồ sơ.";
  }
  void LinkPrevious_Click(object sender,RoutedEventArgs e){if(linkPageIndex>0){try{linkPageIndex--;LoadLinkPage();}catch(Exception ex){Notice.Text=ex.Message;}}}
  void LinkNext_Click(object sender,RoutedEventArgs e){try{linkPageIndex++;LoadLinkPage();}catch(Exception ex){Notice.Text=ex.Message;}}
  void LinkResults_SelectionChanged(object sender,SelectionChangedEventArgs e){
   var selected=LinkResults.SelectedItem as LinkCandidate;if(selected==null)return;
   if(Convert.ToString(RelatedFeature.SelectedItem)!=selected.Feature)return;
   RelatedId.Text=selected.Id;
  }
  void OpenRelated_Click(object sender,RoutedEventArgs e){
   if(activeId==null){Notice.Text="Lưu hồ sơ trước khi mở liên kết.";return;}
   try {
    var saved=DataStore.GetModuleRecord(session,moduleCode,activeId);
    if(String.IsNullOrEmpty(saved.RelatedFeature)||String.IsNullOrEmpty(saved.RelatedId))
     throw new InvalidOperationException("Hồ sơ đã lưu không có liên kết đích.");
    if(navigateTo==null)throw new InvalidOperationException("Chưa thiết lập điều hướng đích.");
    navigateTo(saved.RelatedFeature,saved.RelatedId);
   }catch(Exception ex){Notice.Text="Không thể mở đích: "+ex.Message;}
  }
  void CreateTask_Click(object sender,RoutedEventArgs e){
   if((moduleCode!="C01"&&moduleCode!="G01")||activeId==null){Notice.Text="Chọn văn bản hoặc phản ánh đã lưu trước khi tạo nhiệm vụ.";return;}
   if(MessageBox.Show(moduleCode=="G01"?"Tạo một nhiệm vụ A02 từ mã phản ánh? Không sao chép nội dung riêng tư; chỉ liên kết ID.":"Tạo một nhiệm vụ A02 từ văn bản đang chọn? Chỉ tạo hồ sơ nội bộ, không gửi văn bản.",
     "Xác nhận",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try{
    string taskId=moduleCode=="G01"?DataStore.CreateTaskFromFeedback(session,activeId):DataStore.CreateTaskFromIncoming(session,activeId);
    Notice.Text="Nhiệm vụ A02 theo ID "+taskId+" đã sẵn sàng; không tạo trùng khi bấm lại.";
    if(navigateTo!=null&&DataStore.Can(session,"A02","read"))navigateTo("A02",taskId);
   }catch(Exception ex){Notice.Text="Không tạo nhiệm vụ: "+ex.Message;}
  }
  void OpenTask_Click(object sender,RoutedEventArgs e){
   var selected=Records.SelectedItem as ModuleRecord;
   if(!backlog||selected==null||navigateTo==null){Notice.Text="Chọn nhiệm vụ cần mở.";return;}
   try{DataStore.GetModuleRecord(session,"A02",selected.Id);navigateTo("A02",selected.Id);}
   catch(Exception ex){Notice.Text="Không mở được nhiệm vụ: "+ex.Message;}
  }
  void AddRelation_Click(object sender,RoutedEventArgs e){
   if(backlog||String.IsNullOrWhiteSpace(activeId)){Notice.Text="Cần lưu hồ sơ nguồn trước khi gắn liên kết.";return;}
   try{
    string feature=RelatedFeature.SelectedItem as string;
    if(moduleCode=="E06" || moduleCode=="F06")throw new InvalidOperationException("Sổ tổng hợp E06/F06 không gắn hồ sơ cá nhân.");
    if(feature==null||feature=="(Không liên kết)")throw new InvalidOperationException("Chọn loại hồ sơ đích trước.");
    if(MessageBox.Show("Xác nhận gắn liên kết đúng ID đã chọn?","Xác nhận",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
    DataStore.AddModuleRelation(session,moduleCode,activeId,Convert.ToString(RelationKind.SelectedItem),feature,RelatedId.Text.Trim(),version);
    var fresh=DataStore.GetModuleRecord(session,moduleCode,activeId);
    version=fresh.Version;
    Relations.ItemsSource=DataStore.GetModuleRelations(session,moduleCode,activeId);
    History.ItemsSource=DataStore.GetModuleHistory(session,moduleCode,activeId);
    Notice.Text="Đã liên kết theo đúng ID; hồ sơ nguồn ở phiên bản "+version;
   }catch(Exception ex){Notice.Text="Không thể gắn liên kết: "+ex.Message;}
  }
  void OpenRelation_Click(object sender,RoutedEventArgs e){
   var item=Relations.SelectedItem as ModuleRelation;
   if(item==null||navigateTo==null||activeId==null){Notice.Text="Chọn một liên kết đã lưu để mở hồ sơ đích.";return;}
   try{
    // Reload the authenticated list before navigation; do not trust stale UI selection.
    bool authorized=DataStore.GetModuleRelations(session,moduleCode,activeId)
      .Any(x=>x.Feature==item.Feature&&x.TargetId==item.TargetId&&x.Kind==item.Kind);
    if(!authorized)throw new UnauthorizedAccessException("Liên kết không còn trong phạm vi quyền.");
    navigateTo(item.Feature,item.TargetId);
   }catch(Exception ex){Notice.Text="Không mở được liên kết: "+ex.Message;}
  }
  void Save_Click(object sender,RoutedEventArgs e){
   if(backlog)return;
   string related=(RelatedFeature.SelectedItem as string)??"(Không liên kết)";
   var draft=new ModuleRecord {Id=activeId,Feature=moduleCode,Code=RecordCode.Text.Trim(),Title=RecordTitle.Text.Trim(),
    Notes=RecordNotes.Text,Status=Convert.ToString(RecordStatus.SelectedItem),
    RelatedFeature=related=="(Không liên kết)"?"":related,
    RelatedId=related=="(Không liên kết)"?"":RelatedId.Text.Trim()};
   foreach(var kv in editors){
    var tb=kv.Value as TextBox;var cb=kv.Value as ComboBox;
    draft.Fields[kv.Key]=(tb!=null?tb.Text:cb==null?"":Convert.ToString(cb.SelectedItem)??"").Trim();
   }
   if(MessageBox.Show("Xác nhận ghi hồ sơ "+draft.Code+"?", "Xác nhận dữ liệu",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try {
    foreach(var field in ModuleCatalog.Get(moduleCode).Fields.Where(x=>x.Type=="date"))
     draft.Fields[field.Key]=DisplayLabels.IsoDate(draft.Fields[field.Key]);
    string saved=DataStore.SaveModuleRecord(session,moduleCode,draft,version);
    Notice.Text="Đã ghi hồ sơ theo ID "+saved+". Tải lại để kiểm tra phiên bản mới.";
    // A new or updated record can move to another page or leave the current search.
    // Reopen the saved ID instead of leaving an obsolete editor/version on screen.
    preselectId=saved;pendingPreselection=true;
    Search.Clear();Reload();
   }catch(Exception ex){Notice.Text="Không ghi hồ sơ: "+ex.Message;}
  }
 }
}
