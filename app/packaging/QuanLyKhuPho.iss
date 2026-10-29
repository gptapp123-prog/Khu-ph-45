#ifndef AppVersion
  #error "Pass /DAppVersion from app/release.properties"
#endif
[Setup]
AppId={{7D38D94A-81B6-4F47-9252-71BC54B75244}
AppName=Quản lý Khu phố
AppVersion={#AppVersion}
DefaultDirName={localappdata}\QuanLyKhuPho\Program
DefaultGroupName=Quản lý Khu phố
PrivilegesRequired=lowest
OutputDir=..\dist_installer
OutputBaseFilename=QuanLyKhuPho_PC_{#AppVersion}_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\QuanLyKhuPho.exe
SetupIconFile=..\pc\web\QuanLyKhuPho.ico

[Files]
Source: "..\dist\QuanLyKhuPho\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Quản lý Khu phố"; Filename: "{app}\QuanLyKhuPho.exe"
Name: "{autodesktop}\Quản lý Khu phố"; Filename: "{app}\QuanLyKhuPho.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Tạo biểu tượng ngoài màn hình"; Flags: unchecked

[Run]
Filename: "{app}\QuanLyKhuPho.exe"; Description: "Khởi động ứng dụng"; Flags: nowait postinstall skipifsilent
