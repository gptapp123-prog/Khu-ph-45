using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 internal sealed class PersonalServicesWindow : Window {
  readonly Session session;
  readonly string personId;
  readonly ComboBox services;
  readonly StackPanel fields=new StackPanel();
  readonly TextBlock notice=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
  readonly Button save=new Button {Content="Lưu hồ sơ",Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,8,0,0)};
  readonly Dictionary<string,Control> editors=new Dictionary<string,Control>();
  PersonalServiceRecord record;

  internal PersonalServicesWindow(Session session,PersonRow person,string preferredFeature=null) {
   this.session=session;personId=person.Id;
   Title="Hồ sơ cá nhân theo nhóm nghiệp vụ";
   Width=570;Height=620;MinWidth=420;MinHeight=380;
   WindowStartupLocation=WindowStartupLocation.CenterOwner;
   WindowLayout.FitInitialSize(this);
   var root=new DockPanel {Margin=new Thickness(18)};
   var header=new StackPanel();
   header.Children.Add(new TextBlock{Text=person.FullName+" · "+person.BirthDateDisplay,FontSize=18,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
   services=new ComboBox {ItemsSource=DataStore.PersonalServices.Where(service=>DataStore.AllowsPersonalService(session,service.Feature,false)).ToList(),
    DisplayMemberPath="Label",Margin=new Thickness(0,10,0,12)};
   header.Children.Add(services);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
   var actions=new StackPanel();
   actions.Children.Add(notice);
   var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};
   buttons.Children.Add(new Button {Content="Đóng",IsCancel=true,Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,8,8,0)});
   buttons.Children.Add(save);actions.Children.Add(buttons);
   DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
   root.Children.Add(new ScrollViewer {Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   Content=root;
   save.Click+=Save;
   services.SelectionChanged+=(_,args)=>LoadService();
   var options=(List<PersonalServiceDefinition>)services.ItemsSource;
   int preferred=options.FindIndex(service=>service.Feature==preferredFeature);
   services.SelectedIndex=preferredFeature==null?0:preferred;
  }

  void LoadService() {
   record=null;notice.Text="";fields.Children.Clear();editors.Clear();save.IsEnabled=false;
   var service=services.SelectedItem as PersonalServiceDefinition;
   if(service==null)return;
   try {
    record=DataStore.ReadPersonalService(session,personId,service.Feature);
    bool writable=DataStore.AllowsPersonalService(session,service.Feature,true);
    foreach(var field in service.Fields) {
     fields.Children.Add(new TextBlock {Text=field.Label+(field.Type=="date"?" (dd-MM-yyyy)":""),Margin=new Thickness(0,9,0,2),TextWrapping=TextWrapping.Wrap});
     record.Values.TryGetValue(field.Key,out string value);
     Control editor;
     if(field.Type.StartsWith("choice:",StringComparison.Ordinal)) {
      var options=new ComboBox {ItemsSource=new[]{""}.Concat(field.Type.Substring(7).Split('|')).ToArray(),IsEnabled=writable};
      options.SelectedItem=value??"";
      editor=options;
     } else {
      editor=new TextBox {Text=field.Type=="date"&&!String.IsNullOrWhiteSpace(value)?DisplayLabels.Date(value):value??"",IsReadOnly=!writable,MaxLength=250};
     }
     editors.Add(field.Key,editor);fields.Children.Add(editor);
    }
    save.IsEnabled=writable;
    if(service.Feature=="E06")notice.Text=DataStore.PersonalInsuranceState(record,DataStore.ReportLocalToday);
   } catch(Exception ex) {notice.Text="Không mở được hồ sơ. "+ex.Message;}
  }

  void Save(object sender,RoutedEventArgs args) {
   var service=services.SelectedItem as PersonalServiceDefinition;
   if(service==null||record==null)return;
   try {
    var changes=new PersonalServiceRecord {Version=record.Version};
    foreach(var pair in editors)changes.Values.Add(pair.Key,pair.Value is TextBox text?text.Text:((ComboBox)pair.Value).SelectedItem as string);
    DataStore.WritePersonalService(session,personId,service.Feature,changes);
    LoadService();
    if(record!=null)notice.Text="Đã lưu hồ sơ. "+notice.Text;
   } catch(Exception ex) {notice.Text="Không lưu được hồ sơ. "+ex.Message;}
  }
 }
}
