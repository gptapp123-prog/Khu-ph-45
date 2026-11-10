using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QLKP.Desktop.Views;

namespace QLKP.Desktop.Core {
 // Runs real compiled WPF windows and routed button events against an isolated database.
 // This is a focused smoke test, not acceptance of every workflow or every display size.
 public static class NativeUiSelfTest {
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr window,int id);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  static T Control<T>(FrameworkElement owner,string name) where T:FrameworkElement {
   var result=owner.FindName(name) as T;
   if(result==null)throw new InvalidDataException("Missing UI control: "+name);
   return result;
  }
  static void Drain() {Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));}
  static void Click(Button button,bool confirm=false,string confirmationTitle="Xác nhận dữ liệu") {
   if(!button.IsEnabled)throw new InvalidDataException("Disabled button: "+button.Name);
   button.BringIntoView();Drain();
   var owner=Window.GetWindow(button);if(owner!=null)owner.UpdateLayout();
   if(!button.IsVisible||button.ActualWidth<1||button.ActualHeight<1)throw new InvalidDataException("Button is not visible: "+button.Name);
   if(owner!=null){var bounds=button.TransformToAncestor(owner).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));
    if(bounds.Left<0||bounds.Top<0||bounds.Right>owner.ActualWidth+1||bounds.Bottom>owner.ActualHeight+1)throw new InvalidDataException("Button outside window: "+button.Name);}
   DispatcherTimer timer=null;
   if(confirm) {
    timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
    timer.Tick+=(sender,args)=> {
     IntPtr dialog=FindWindow("#32770",confirmationTitle);uint process;
     if(dialog!=IntPtr.Zero) {
      GetWindowThreadProcessId(dialog,out process);
      if(process==(uint)System.Diagnostics.Process.GetCurrentProcess().Id) {
       IntPtr yes=GetDlgItem(dialog,6);
       if(yes!=IntPtr.Zero){timer.Stop();SendMessage(yes,0x00F5,IntPtr.Zero,IntPtr.Zero);}
      }
     }
    };
    timer.Start();
   }
   try{button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Drain();}
   finally{if(timer!=null)timer.Stop();}
  }
  static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject {
   if(root is T)yield return (T)root;
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
    foreach(var item in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return item;
  }
  static void Snapshot(Window window,string directory,string name) {
   window.UpdateLayout();Drain();
   int width=(int)Math.Ceiling(window.ActualWidth),height=(int)Math.Ceiling(window.ActualHeight);
   if(width<100||height<100)throw new InvalidDataException("WPF window has no rendered layout.");
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var output=new FileStream(Path.Combine(directory,name+".png"),FileMode.CreateNew))encoder.Save(output);
  }
  static MainWindow Login(string username,string password) {
   var login=new LoginWindow();login.Show();Drain();
   Control<TextBox>(login,"LoginName").Text=username;
   Control<PasswordBox>(login,"LoginPassword").Password=password;
   var reveal=Control<CheckBox>(login,"RevealPassword");reveal.IsChecked=true;
   if(Control<TextBox>(login,"VisibleLoginPassword").Text!=password)throw new InvalidDataException("Password reveal failed.");
   reveal.IsChecked=false;
   Click(Control<Button>(login,"LoginButton"));
   var main=Application.Current.Windows.OfType<MainWindow>().LastOrDefault();
   if(main==null||Control<PasswordBox>(login,"LoginPassword").Password.Length!=0||Control<TextBox>(login,"VisibleLoginPassword").Text.Length!=0)
    throw new InvalidDataException("Login button did not open main window or clear credentials.");
   return main;
  }
  static UserControl Navigate(MainWindow window,string code) {
   var groups=Control<ListBox>(window,"Groups");
   var group=groups.Items.Cast<string>().FirstOrDefault(x=>x[0]==code[0]);
   if(group==null)throw new InvalidDataException("Feature group unavailable: "+code);
   groups.SelectedItem=group;Drain();
   var features=Control<ListBox>(window,"Features");
   var feature=features.Items.Cast<Feature>().FirstOrDefault(x=>x.Code==code);
   if(feature==null)throw new InvalidDataException("Feature unavailable: "+code);
   features.SelectedItem=feature;Drain();window.UpdateLayout();
   var panel=Control<ContentControl>(window,"FeatureContent").Content as UserControl;
   if(panel==null||panel is NoticeView)throw new InvalidDataException("Feature navigation failed: "+code);
   return panel;
  }
  static void ExerciseModule(Session actor,ModuleRecordsView panel,ModuleDefinition spec,string prefix="UI-") {
   Click(Control<Button>(panel,"NewButton"));
   Control<TextBox>(panel,"RecordCode").Text=prefix+spec.Code;
   Control<TextBox>(panel,"RecordTitle").Text="[MẪU] Kiểm thử cửa sổ "+spec.Code;
   var typed=Control<StackPanel>(panel,"TypedFields");
   for(int i=0;i<spec.Fields.Length;i++) {
    var field=spec.Fields[i];if(!field.Required)continue;
    var input=typed.Children[i*2+1];
    var choice=input as ComboBox;
    if(choice!=null){choice.SelectedIndex=0;continue;}
    var text=input as TextBox;if(text==null)throw new InvalidDataException("Unknown field editor.");
    text.Text=field.Type=="date"?"06-11-2026":field.Type=="time"?"08:00":field.Type=="account"?actor.Id:
     new[]{"number","nonnegative","percentage"}.Contains(field.Type)?"1":"[MẪU]";
   }
   Click(Control<Button>(panel,"SaveButton"),true);
   var rows=DataStore.GetModulePage(actor,spec.Code,prefix+spec.Code,0).Items;
   if(rows.Count!=1||rows[0].Version!=1)throw new InvalidDataException("UI create/read failed: "+spec.Code);
   string id=rows[0].Id;
   Control<TextBox>(panel,"RecordTitle").Text="[MẪU] Đã cập nhật "+spec.Code;
   Click(Control<Button>(panel,"SaveButton"),true);
   var current=DataStore.GetModuleRecord(actor,spec.Code,id);
   if(current.Version!=2||current.Title!="[MẪU] Đã cập nhật "+spec.Code||DataStore.GetModuleHistory(actor,spec.Code,id).Count!=1)
    throw new InvalidDataException("UI update/history failed: "+spec.Code);
  }
  static void DriveIdentityDialog(Button open,string number,bool confirm) {
   Exception error=null;bool completed=false;
   var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
   timer.Tick+=(sender,args)=>{
    var window=Application.Current.Windows.OfType<PersonIdentityWindow>().SingleOrDefault();
    if(window==null)return;
    timer.Stop();
    try {
     Control<PasswordBox>(window,"Number").Password=number;
     Click(Control<Button>(window,"SubmitButton"),confirm,"Xác nhận định danh");
     completed=!window.IsVisible;
    }catch(Exception ex){error=ex;}
    finally{if(window.IsVisible)window.Close();}
   };
   timer.Start();try{Click(open);}finally{timer.Stop();}
   if(!completed||error!=null)throw new InvalidDataException("Identity UI dialog did not complete.",error);
  }
  public static void Run() {
   var app=Application.Current;var priorShutdown=app.ShutdownMode;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   using(var run=new TestRunRecorder("windows_ui_smoke","ui_e2e")) {
    string sandbox=null;
    try {
     run.BeginStep("UI_ISOLATED_STORAGE");sandbox=DataStore.BeginIsolatedSelfTest();DataStore.Initialize();
     string evidence=Environment.GetEnvironmentVariable("QLKP_UI_EVIDENCE");
     if(String.IsNullOrEmpty(evidence))evidence=Path.Combine(TestRunHistory.Root,run.Report.Id,"screenshots");
     if(!Path.IsPathRooted(evidence))throw new InvalidDataException("UI evidence path must be absolute.");
     Directory.CreateDirectory(evidence);
     string rootSecret="T-"+Guid.NewGuid().ToString("N")+"!",adminSecret="T-"+Guid.NewGuid().ToString("N")+"!";
     run.BeginStep("UI_FIRST_RUN_SETUP");
     var setup=new LoginWindow();setup.Show();Drain();Snapshot(setup,evidence,"login-first-run");
     if(Control<Button>(setup,"LoginButton").Visibility!=Visibility.Collapsed)throw new InvalidDataException("First-run login button must be replaced by setup.");
     Click(Control<Button>(setup,"SetupButton"));
     if(!DataStore.IsFirstRun()||String.IsNullOrEmpty(Control<TextBlock>(setup,"Message").Text))throw new InvalidDataException("Missing setup fields not rejected visibly.");
     Control<TextBox>(setup,"LoginName").Text="ui_root";Control<PasswordBox>(setup,"LoginPassword").Password="short";
     Control<TextBox>(setup,"AreaCode").Text="UI-TEST";Control<TextBox>(setup,"AreaName").Text="[MẪU] Khu phố kiểm thử";
     run.BeginStep("UI_SETUP_SHORT_PASSWORD_DENIED");Click(Control<Button>(setup,"SetupButton"));
     if(!DataStore.IsFirstRun()||!Control<TextBlock>(setup,"Message").Text.Contains("12"))throw new InvalidDataException("Short setup password not rejected.");
     Control<PasswordBox>(setup,"LoginPassword").Password=rootSecret;
     run.BeginStep("UI_SETUP_CONFIRMATION_DENIED");Click(Control<Button>(setup,"SetupButton"));
     if(!DataStore.IsFirstRun())throw new InvalidDataException("Setup accepted mismatched confirmation.");
     Control<PasswordBox>(setup,"ConfirmPassword").Password=rootSecret;
     run.BeginStep("UI_SETUP_ENTER_AND_AUTO_LOGIN");
     Control<PasswordBox>(setup,"LoginPassword").RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(setup),0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});Drain();
     if(DataStore.IsFirstRun())throw new InvalidDataException("Setup button did not create owner.");
     var rootWindow=Application.Current.Windows.OfType<MainWindow>().SingleOrDefault();
     if(rootWindow==null||Application.Current.Windows.OfType<LoginWindow>().Any())throw new InvalidDataException("Setup did not enter application automatically.");
     bool duplicateDenied=false;try{DataStore.SetupOwner("ui_second_root",rootSecret,"OTHER","Other");}catch(InvalidOperationException){duplicateDenied=true;}
     if(!duplicateDenied)throw new InvalidDataException("Setup created a second root.");
     var root=DataStore.Login("ui_root",rootSecret);
     run.BeginStep("UI_ACCOUNT_HEADER_BUTTON");Click(Control<Button>(rootWindow,"AccountButton"));
     if(!(Control<ContentControl>(rootWindow,"FeatureContent").Content is AccountsView))throw new InvalidDataException("Account header route failed.");
     run.BeginStep("UI_CREATE_ADMIN");var accounts=Navigate(rootWindow,"I01");
     Control<TextBox>(accounts,"Username").Text="ui_admin";Control<PasswordBox>(accounts,"Password").Password=adminSecret;
     Click(Control<Button>(accounts,"CreateButton"));
     var admin=DataStore.Login("ui_admin",adminSecret);if(admin==null||admin.Role!="KP_ADMIN")throw new InvalidDataException("Account UI create failed.");
     if(FeatureCatalog.All.Any(f=>!DataStore.Can(admin,f.Code,"read")))throw new InvalidDataException("New KP admin does not see all features by default.");
     if(DataStore.Can(admin,"B02","identity_lookup")||DataStore.Can(admin,"B02","identity_write"))
      throw new InvalidDataException("Sensitive identity rights were granted by default.");
     var adminRights=DataStore.GetAccountRights(root,admin.Id);
     adminRights.AddRange(new[]{"B02:identity_lookup","B02:identity_write"});
     DataStore.SetAccountRights(root,admin.Id,adminRights);
     var visited=new HashSet<string>();
     foreach(var code in FeatureCatalog.All.Select(x=>x.Code)) {run.BeginStep("UI_NAV_"+code);Navigate(rootWindow,code);visited.Add(code);}
     Snapshot(rootWindow,evidence,"root-system");rootWindow.Close();
     run.BeginStep("UI_ADMIN_LOGIN");var main=Login("ui_admin",adminSecret);admin=DataStore.Login("ui_admin",adminSecret);
     run.BeginStep("UI_DATA_MERGE_BUTTON");
     string mergeSource=DataStore.CreateMergeFixture(admin,"DIALOG");var quality=Navigate(main,"I08");Exception mergeError=null;
     var mergeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
     mergeTimer.Tick+=(o,args)=>{
      var window=Application.Current.Windows.OfType<DataMergeWindow>().SingleOrDefault();if(window==null)return;mergeTimer.Stop();
      try {
       Control<TextBox>(window,"SourcePath").Text=mergeSource;
       run.BeginStep("UI_DATA_MERGE_PREVIEW");Click(Control<Button>(window,"PreviewButton"));
       if(!Control<Button>(window,"ApplyButton").IsEnabled||!Control<DataGrid>(window,"Summary").Items.Cast<MergeSummary>().Any(x=>x.Table=="person_residence_periods"))throw new InvalidDataException("Merge preview UI failed.");
       Snapshot(window,evidence,"merge-preview");
       run.BeginStep("UI_DATA_MERGE_CONFIRM_APPLY");Click(Control<Button>(window,"ApplyButton"),true,"Xác nhận hợp nhất");
       if(DataStore.FindHouseholds(admin,"MERGE-DIALOG").Count!=1)throw new InvalidDataException("Merge button did not persist records.");
       Snapshot(window,evidence,"merge-applied");
      }catch(Exception ex){mergeError=ex;}
      finally{if(window!=null)window.Close();}
     };
     mergeTimer.Start();try{Click(Control<Button>(quality,"MergeButton"));}finally{mergeTimer.Stop();}if(mergeError!=null)throw new InvalidDataException("Merge dialog UI failed.",mergeError);
     run.BeginStep("UI_B03_RESIDENCE_PERIOD_BUTTON");
     var person=DataStore.HouseholdMembers(admin,DataStore.FindHouseholds(admin,"MERGE-DIALOG").Single().Id).Single();
     var peoplePanel=Navigate(main,"B02");
     Control<DataGrid>(peoplePanel,"People").SelectedItem=Control<DataGrid>(peoplePanel,"People").Items.Cast<PersonRow>().Single(row=>row.Id==person.Id);
     Drain();
     Exception periodError=null;bool periodExercised=false;
     var periodTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
     periodTimer.Tick+=(o,args)=>{
      var window=Application.Current.Windows.OfType<ResidencePeriodsWindow>().SingleOrDefault();
      if(window==null)return;
      periodTimer.Stop();
      try {
       var save=Control<Button>(window,"SavePeriod");
       Click(save);
       if(DataStore.GetResidencePeriods(admin,person.Id,0).TotalCount!=0)
        throw new InvalidDataException("Blank address created a period.");
       Control<TextBox>(window,"Address").Text="[MẪU] Địa chỉ cư trú UI";
       Control<TextBox>(window,"ValidFrom").Text="01-01-2020";
       Click(save);
       var row=DataStore.GetResidencePeriods(admin,person.Id,0).Items.Single();
       if(row.Address!="[MẪU] Địa chỉ cư trú UI"||row.ValidFrom!="2020-01-01"||Control<DataGrid>(window,"Periods").Items.Count!=1)
        throw new InvalidDataException("Residence UI did not write and reload the correct person.");
       Snapshot(window,evidence,"B03-residence-period");
       periodExercised=true;
      }catch(Exception ex){periodError=ex;}
      finally{window.Close();}
     };
     periodTimer.Start();
     try{Click(Control<Button>(peoplePanel,"OpenResidencePeriods"));}finally{periodTimer.Stop();}
     if(!periodExercised||periodError!=null)throw new InvalidDataException("Residence period dialog UI failed.",periodError);
     run.BeginStep("UI_B02_IDENTITY_SAVE_AND_LOOKUP");
     string testIdentity="000000123456";
     DriveIdentityDialog(Control<Button>(peoplePanel,"EditIdentity"),testIdentity,true);
     if(DataStore.GetMaskedPersonIdentity(admin,person.Id)!="••••••••3456")
      throw new InvalidDataException("Identity write did not persist masked data.");
     Control<DataGrid>(peoplePanel,"People").SelectedItem=null;
     DriveIdentityDialog(Control<Button>(peoplePanel,"LookupIdentity"),testIdentity,false);
     var foundIdentity=Control<DataGrid>(peoplePanel,"People").SelectedItem as PersonRow;
     if(foundIdentity==null||foundIdentity.Id!=person.Id)
      throw new InvalidDataException("Identity lookup did not select the exact person.");
     run.BeginStep("UI_F08_VEHICLE_REGISTER");
     var vehiclePanel=Navigate(main,"F08");
     Exception vehicleError=null;bool vehicleExercised=false;
     var vehicleTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
     vehicleTimer.Tick+=(sender,args)=>{
      var window=Application.Current.Windows.OfType<VehicleEditWindow>().SingleOrDefault();
      if(window==null)return;
      vehicleTimer.Stop();
      try {
       Control<TextBox>(window,"Plate").Text="00-QA-01234";
       Control<TextBox>(window,"Brand").Text="Hãng thử";
       Control<ComboBox>(window,"Kind").SelectedIndex=1;
       Control<TextBox>(window,"Measure").Text="7";
       Control<TextBox>(window,"OwnerSearch").Text=person.FullName;
       Click(Control<Button>(window,"FindOwner"));
       var owners=Control<ListBox>(window,"Owners");
       owners.SelectedItem=owners.Items.Cast<PersonRow>().Single(x=>x.Id==person.Id);
       Click(Control<Button>(window,"ChooseOwner"));
       Click(Control<Button>(window,"SaveVehicle"));
       var vehicle=DataStore.GetVehicles(admin,"00QA01234",0).Items.Single();
       if(vehicle.OwnerId!=person.Id||vehicle.Seats!=7||vehicle.PayloadKg.HasValue)
        throw new InvalidDataException("Vehicle UI saved wrong owner or capacity.");
       vehicleExercised=true;
      }catch(Exception ex){vehicleError=ex;}
      finally{if(window.IsVisible)window.Close();}
     };
     vehicleTimer.Start();try{Click(Control<Button>(vehiclePanel,"AddVehicle"));}finally{vehicleTimer.Stop();}
     if(!vehicleExercised||vehicleError!=null)throw new InvalidDataException("Vehicle UI dialog failed.",vehicleError);
     var selectedVehicle=Control<DataGrid>(vehiclePanel,"Vehicles").SelectedItem as VehicleRow;
     if(selectedVehicle==null||selectedVehicle.Plate!="00QA01234")
      throw new InvalidDataException("Vehicle UI did not reload the saved record.");
     Snapshot(main,evidence,"F08-vehicles");
     run.BeginStep("UI_I05_QR_CREATE_OPEN");
     var qrPanel=Navigate(main,"I05");
     Control<TextBox>(qrPanel,"Keyword").Text="MERGE-DIALOG";
     Click(Control<Button>(qrPanel,"SearchTargets"));
     var qrTargets=Control<ListBox>(qrPanel,"Targets");
     if(qrTargets.Items.Count!=1)throw new InvalidDataException("QR household search returned the wrong count.");
     qrTargets.SelectedIndex=0;
     Click(Control<Button>(qrPanel,"CreateQr"));
     var qrImage=Control<Image>(qrPanel,"PreviewQr").Source as BitmapSource;
     if(qrImage==null||!Control<Button>(qrPanel,"SaveQr").IsEnabled)throw new InvalidDataException("QR preview did not render.");
     Snapshot(main,evidence,"I05-qr");
     var qrHousehold=DataStore.FindHouseholds(admin,"MERGE-DIALOG").Single();
     Control<TextBox>(qrPanel,"QrPayload").Text="QLKP1|B01|"+qrHousehold.Id;
     Click(Control<Button>(qrPanel,"OpenQrText"));
     var qrDestination=Control<ContentControl>(main,"FeatureContent").Content as HouseholdView;
     if(qrDestination==null||(Control<DataGrid>(qrDestination,"Households").SelectedItem as HouseholdRow)?.Id!=qrHousehold.Id)
      throw new InvalidDataException("QR open did not navigate to the selected household.");
     Console.WriteLine("UI_QR_PASS create and navigate");

     foreach(var feature in FeatureCatalog.All) {
      run.BeginStep("UI_NAV_"+feature.Code);var panel=Navigate(main,feature.Code);visited.Add(feature.Code);
      if(ModuleCatalog.IsEditable(feature.Code)){run.BeginStep("UI_WRITE_"+feature.Code);ExerciseModule(admin,(ModuleRecordsView)panel,ModuleCatalog.Get(feature.Code));}
      if(new[]{"B01","A02","C01","H03"}.Contains(feature.Code))Snapshot(main,evidence,feature.Code);
     }
     if(visited.Count!=FeatureCatalog.All.Count)throw new InvalidDataException("Not all routes opened.");
     Console.WriteLine("UI_NAVIGATION_PASS "+visited.Count);Console.WriteLine("UI_MODULE_WRITE_PASS "+ModuleCatalog.Editable.Length);
     run.BeginStep("UI_CREATE_DEFAULT_ORDINARY_ACCOUNT");var accountPanel=Navigate(main,"I01");
     string ordinarySecret="T-"+Guid.NewGuid().ToString("N")+"!";
     Control<TextBox>(accountPanel,"Username").Text="ui_ordinary";Control<PasswordBox>(accountPanel,"Password").Password=ordinarySecret;
     Click(Control<Button>(accountPanel,"CreateButton"));main.Close();
     var ordinary=DataStore.Login("ui_ordinary",ordinarySecret);main=Login("ui_ordinary",ordinarySecret);
     foreach(var feature in FeatureCatalog.All){run.BeginStep("UI_ORDINARY_NAV_"+feature.Code);var panel=Navigate(main,feature.Code);if(ModuleCatalog.IsEditable(feature.Code)){run.BeginStep("UI_ORDINARY_WRITE_"+feature.Code);ExerciseModule(ordinary,(ModuleRecordsView)panel,ModuleCatalog.Get(feature.Code),"USER-");}}
     var protectedAccounts=Navigate(main,"I01");if(Control<Border>(protectedAccounts,"CreateSection").Visibility!=Visibility.Collapsed)throw new InvalidDataException("Ordinary account can create administrator accounts.");
     Snapshot(main,evidence,"ordinary-protected-accounts");Navigate(main,"I07");Snapshot(main,evidence,"ordinary-catalog");
     run.BeginStep("UI_LOGOUT_BUTTON");Click(Control<Button>(main,"LogoutButton"));
     if(Application.Current.Windows.OfType<MainWindow>().Any())throw new InvalidDataException("Logout left a main window open.");
     var returned=Application.Current.Windows.OfType<LoginWindow>().SingleOrDefault();
     if(returned==null)throw new InvalidDataException("Logout did not return to login.");
     returned.Close();run.BeginStep("UI_CLOSE_AND_CLEANUP");Drain();
     DataStore.EndIsolatedSelfTest(sandbox);sandbox=null;run.Complete("PASS");Console.WriteLine("UI_SMOKE_PASS");
    } catch(Exception ex){run.Complete("FAIL",ex.GetType().Name);throw;}
    finally {
     foreach(var window in app.Windows.Cast<Window>().ToArray())window.Close();
     if(sandbox!=null)DataStore.EndIsolatedSelfTest(sandbox);
     app.ShutdownMode=priorShutdown;
    }
   }
  }
 }
}
