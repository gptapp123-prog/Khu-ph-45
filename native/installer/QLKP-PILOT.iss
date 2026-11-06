#define MyAppName "Quản lý Khu phố"
#define MyAppVersion "0.20.1-PILOT"
[Setup]
AppId={{8E73A39E-D3C2-417E-987C-83C4D105A2CD}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\QLKP-PILOT
DisableDirPage=yes
DefaultGroupName=Quản lý Khu phố
OutputDir=..\dist
OutputBaseFilename=QLKP-PILOT-SETUP-X64-UNSIGNED
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\QLKP.Desktop\Assets\qlkp.ico
UninstallDisplayIcon={app}\QLKP.Desktop.exe
MinVersion=6.1sp1
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayName=Quản lý Khu phố - 0.20.0
AppMutex=Local\QLKP-DESKTOP-7DF94E5B-B7D8-4972-A93B-68F1818733E7
[Files]
Source: "..\build-output\Release\x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"
[Icons]
Name: "{autoprograms}\Quản lý Khu phố PILOT"; Filename: "{app}\QLKP.Desktop.exe"
Name: "{autodesktop}\Quản lý Khu phố PILOT"; Filename: "{app}\QLKP.Desktop.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng Desktop"; GroupDescription: "Biểu tượng bổ sung:"
[Run]
Filename: "{app}\QLKP.Desktop.exe"; Description: "Khởi chạy bản thử nghiệm"; Flags: nowait postinstall skipifsilent
[Code]
function HasNet48: Boolean;
var
  ReleaseValue: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040);
end;
function InitializeSetup: Boolean;
begin
  Result := HasNet48;
  if not Result then
    MsgBox('Cần .NET Framework 4.8. Hãy cài từ trang chính thức của Microsoft rồi chạy lại bộ cài. Không xóa dữ liệu cũ.', mbCriticalError, MB_OK);
end;
// Tệp dữ liệu nằm ngoài thư mục chương trình (%LOCALAPPDATA%).
// Uninstall deliberately never deletes any user database or document store.
// Encrypted vault. Publisher signing and full workflow acceptance are pending.
