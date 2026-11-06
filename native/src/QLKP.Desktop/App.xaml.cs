using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using QLKP.Desktop.Core;
using QLKP.Desktop.Views;

namespace QLKP.Desktop {
 public partial class App : Application {
  private Mutex _singleInstance;
  private bool _ownsMutex;
  protected override void OnStartup(StartupEventArgs e) {
   base.OnStartup(e);
   bool selfTest=Array.Exists(e.Args,x=>String.Equals(x,"--self-test",StringComparison.OrdinalIgnoreCase));
   if(!selfTest) {
    bool created;
    _singleInstance=new Mutex(false,@"Local\QLKP-DESKTOP-7DF94E5B-B7D8-4972-A93B-68F1818733E7",out created);
    try { _ownsMutex=_singleInstance.WaitOne(0,false); }
    catch(AbandonedMutexException) { _ownsMutex=true; }
    if(!_ownsMutex) {
     MessageBox.Show("Ứng dụng đã mở trong phiên Windows này. Hãy sử dụng cửa sổ đang chạy.","Quản lý Khu phố",MessageBoxButton.OK,MessageBoxImage.Information);
     Shutdown(0); return;
    }
   }
   try {
    if(selfTest) { SelfTest.Run(); Shutdown(0); return; }
    StartupDiagnostics.CheckApplicationFiles();
    StartupDiagnostics.CheckPlaintextMaintenanceArtifacts();
    DataStore.Initialize();
    StartupDiagnostics.CheckDatabase(DataStore.DatabasePath);
    StartupDiagnostics.WriteEvent("STARTUP_OK");
    var login=new LoginWindow();
    MainWindow=login;
    login.Show();
   } catch(Exception ex) {
    StartupDiagnostics.WriteException("STARTUP_FAIL",ex);
    if(selfTest) { Console.Error.WriteLine("SELF_TEST_FAIL "+ex.ToString()); Shutdown(2); return; }
    MessageBox.Show("Không thể khởi chạy ứng dụng hoặc mở kho dữ liệu.\nKHÔNG xóa kho hoặc cài lại đè dữ liệu.\nXem nhật ký chẩn đoán tại:\n"+StartupDiagnostics.LogPath,
     "Lỗi khởi động",MessageBoxButton.OK,MessageBoxImage.Error);
    Shutdown(2);
   }
  }
  protected override void OnExit(ExitEventArgs e) {
   if(_singleInstance!=null) {
    try { if(_ownsMutex)_singleInstance.ReleaseMutex(); } catch(ApplicationException) { }
    _singleInstance.Dispose();
   }
   base.OnExit(e);
  }
 }
}
