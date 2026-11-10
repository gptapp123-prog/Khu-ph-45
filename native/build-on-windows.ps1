[CmdletBinding()]
param(
 [switch]$BootstrapPrereqs,
 [switch]$Installer,
 [switch]$SmokeTest,
 [switch]$NoInstaller
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
Set-Location $root
$logDir = Join-Path $root 'build-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp=(Get-Date -Format 'yyyyMMdd-HHmmssfff')+'-'+([guid]::NewGuid().ToString('N').Substring(0,8))
$log=Join-Path $logDir "build-$stamp.log"
try { Start-Transcript -Path $log -ErrorAction Stop | Out-Null } catch { Write-Warning 'Không ghi được transcript; sẽ ghi hồ sơ phiên riêng nếu có thể.' }
$run=$null
try {
 . (Join-Path $root 'test-run-journal.ps1')
 $runRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'QLKP-V2-PILOT\test-runs'
 New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
 $run=New-QlkpTestRun $runRoot
 Start-QlkpStage $run 'PRECHECK'
 Write-Host '=== QLKP PILOT V0.21.1 - WINDOWS NATIVE BUILD ===' -ForegroundColor Cyan
 # Packaging always requires the runtime gate, even when invoked without -SmokeTest.
 if($Installer -and -not $NoInstaller) { $SmokeTest=$true }
 # Do not mistake a setup file left over from an earlier build for a current release.
 $stale=Join-Path $root 'dist\QLKP-PILOT-SETUP-X64-UNSIGNED.exe'
 $staleHash=Join-Path $root 'dist\SHA256SUMS.txt'
 if(Test-Path $stale){Remove-Item -LiteralPath $stale -Force}
 if(Test-Path $staleHash){Remove-Item -LiteralPath $staleHash -Force}
 if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Phải BUILD WPF trên Windows, không chạy Linux/macOS.' }
 if (-not [Environment]::Is64BitOperatingSystem) { throw 'Mục tiêu bản build hiện tại là x64. Cần máy Windows 64-bit.' }
 # Windows 10/11 supported for BUILD. Windows 7 SP1 is only a RUN target awaiting test.
 $winget=Get-Command winget -ErrorAction SilentlyContinue
 function Install-WingetPackage([string]$id, [string]$override) {
  if (-not $winget) { throw "Thiếu winget để tự cài $id. Hãy cài công cụ chính thức từ Microsoft hoặc thực hiện bước cài thủ công theo README." }
  $argsList=@('install','--id',$id,'-e','--source','winget','--silent','--accept-package-agreements','--accept-source-agreements')
  if($override) {$argsList+=@('--override',$override)}
  & $winget.Source @argsList
  if($LASTEXITCODE -ne 0) {throw "Cài $id không thành công. Mã lỗi $LASTEXITCODE"}
 }
 Start-QlkpStage $run 'PREREQUISITES'
 if($BootstrapPrereqs) {
  if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
   Write-Host 'Cài .NET SDK chính thức bằng winget...' -ForegroundColor Yellow
   Install-WingetPackage 'Microsoft.DotNet.SDK.8' ''
   $env:PATH=[Environment]::GetEnvironmentVariable('Path','Machine')+';'+[Environment]::GetEnvironmentVariable('Path','User')
  }
  $msbuildPaths=@(
   "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
   "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
  )
  $vswhere=$msbuildPaths | Where-Object {Test-Path $_} | Select-Object -First 1
  $refDir=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
  $hasWpfRef=(Test-Path (Join-Path $refDir 'PresentationFramework.dll'))
  if((-not $vswhere) -or (-not $hasWpfRef)) {
   Write-Host 'Cài Visual Studio Build Tools với WPF/.NET Framework targeting...' -ForegroundColor Yellow
   Install-WingetPackage 'Microsoft.VisualStudio.2022.BuildTools' '--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended'
  }
  if($Installer -and -not $NoInstaller) {
   $iscc=Get-Command ISCC.exe -ErrorAction SilentlyContinue
   if(-not $iscc) {
    $available=Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup*\ISCC.exe","$env:ProgramFiles\Inno Setup*\ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if(-not $available) { Write-Host 'Cài Inno Setup chính thức...' -ForegroundColor Yellow; Install-WingetPackage 'JRSoftware.InnoSetup' '' }
   }
  }
 }
 # Use full-framework MSBuild from Visual Studio Build Tools for WPF/net48.
 $candidateVswhere=@(
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
  "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
 ) | Where-Object {Test-Path $_} | Select-Object -First 1
 if(-not $candidateVswhere) {throw 'Thiếu vswhere/Visual Studio Build Tools. Chạy lại BUILD_SETUP_1_CLICK.cmd để cài workload Desktop WPF.'}
 $buildPath=@(& $candidateVswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null) | Where-Object {$_ -and (Test-Path $_)} | Select-Object -First 1
 if(-not $buildPath) {throw 'Chưa có MSBuild.exe từ Visual Studio. Không thể biên dịch WPF.'}
 $dotnet=Get-Command dotnet -ErrorAction SilentlyContinue
 if(-not $dotnet) {throw 'Thiếu .NET SDK để restore NuGet.'}
 Write-Host ('MSBuild: '+$buildPath)
 Write-Host ('dotnet SDK: '+(& $dotnet.Source --version))
 $proj='src\QLKP.Desktop\QLKP.Desktop.csproj'
 Start-QlkpStage $run 'NUGET_RESTORE'
 & $dotnet.Source restore $proj -p:Platform=x64 --source 'https://api.nuget.org/v3/index.json' --force-evaluate
 if($LASTEXITCODE -ne 0) {throw 'NuGet restore lỗi. Không tiếp tục.'}
 $out=Join-Path $root 'build-output\Release\x64'
 if(Test-Path $out){Remove-Item -LiteralPath $out -Recurse -Force}
 New-Item -ItemType Directory -Force -Path $out | Out-Null
 $outDir=$out+'\'
 Start-QlkpStage $run 'WPF_COMPILE'
 & $buildPath $proj /t:Build /p:Configuration=Release /p:Platform=x64 /p:Restore=false /p:AppendTargetFrameworkToOutputPath=false "/p:OutDir=$outDir" /v:minimal /nologo
 if($LASTEXITCODE -ne 0) {throw 'WPF compile lỗi. Không tạo bộ cài.'}
 Start-QlkpStage $run 'DEPENDENCIES'
 $exe=Join-Path $out 'QLKP.Desktop.exe'
 foreach($file in @('QLKP.Desktop.exe','QLKP.Desktop.exe.config','System.Data.SQLite.dll','e_sqlite3.dll','BouncyCastle.Cryptography.dll','schema.sql')) {
  if(-not(Test-Path (Join-Path $out $file))) {
   if($file -eq 'QLKP.Desktop.exe.config') {continue}
   throw "Thiếu phụ thuộc của app sau build: $file. Không tạo bộ cài."
  }
 }
 $run.report.source_hash=(Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
 $run.report.hash_kind='exe_sha256'
 if($SmokeTest) {
  Start-QlkpStage $run 'WINDOWS_SELF_TEST'
  Write-Host 'Chạy self-test thực trên Windows (khởi tạo DB thử, quick_check)...' -ForegroundColor Yellow
  # Never hang indefinitely if startup deadlocks or a window unexpectedly blocks --self-test.
  $selfTestOut=Join-Path $logDir "self-test-$stamp.stdout.log"
  $selfTestErr=Join-Path $logDir "self-test-$stamp.stderr.log"
  $priorParent=[Environment]::GetEnvironmentVariable('QLKP_TEST_PARENT_ID','Process')
  $check=$null
  try {
   $env:QLKP_TEST_PARENT_ID=$run.report.session_id
   $info=New-Object System.Diagnostics.ProcessStartInfo
   $info.FileName=$exe;$info.WorkingDirectory=$out;$info.Arguments='--self-test';$info.UseShellExecute=$false
   $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
   $check=New-Object System.Diagnostics.Process;$check.StartInfo=$info
   [void]$check.Start();$stdout=$check.StandardOutput.ReadToEndAsync();$stderr=$check.StandardError.ReadToEndAsync()
   if (-not $check.WaitForExit(120000)) {
    Stop-Process -Id $check.Id -Force -ErrorAction SilentlyContinue
    throw 'Self-test WPF vượt 120 giây, dừng đóng gói để điều tra lỗi.'
   }
   $check.WaitForExit()
   [IO.File]::WriteAllText($selfTestOut,$stdout.Result,[Text.Encoding]::UTF8)
   [IO.File]::WriteAllText($selfTestErr,$stderr.Result,[Text.Encoding]::UTF8)
   if($check.ExitCode -ne 0) {throw "Self-test trên kho tạm thất bại (exit=$($check.ExitCode)). Dừng phát hành."}
  } finally { if($null -ne $check){$check.Dispose()};[Environment]::SetEnvironmentVariable('QLKP_TEST_PARENT_ID',$priorParent,'Process') }
 } else {Skip-QlkpStage $run 'WINDOWS_SELF_TEST'}
 if($SmokeTest) {
  Start-QlkpStage $run 'WINDOWS_UI_SMOKE'
  $priorUiEvidence=[Environment]::GetEnvironmentVariable('QLKP_UI_EVIDENCE','Process')
  $priorParent=[Environment]::GetEnvironmentVariable('QLKP_TEST_PARENT_ID','Process')
  $uiCheck=$null
  try {
   $env:QLKP_UI_EVIDENCE=Join-Path $logDir ("ui-"+$stamp)
   $env:QLKP_TEST_PARENT_ID=$run.report.session_id
   $uiInfo=New-Object System.Diagnostics.ProcessStartInfo
   $uiInfo.FileName=$exe;$uiInfo.WorkingDirectory=$out;$uiInfo.Arguments='--ui-smoke-test';$uiInfo.UseShellExecute=$false
   $uiInfo.RedirectStandardOutput=$true;$uiInfo.RedirectStandardError=$true
   $uiCheck=New-Object System.Diagnostics.Process;$uiCheck.StartInfo=$uiInfo
   [void]$uiCheck.Start();$uiOut=$uiCheck.StandardOutput.ReadToEndAsync();$uiErr=$uiCheck.StandardError.ReadToEndAsync()
   if(-not $uiCheck.WaitForExit(240000)){Stop-Process -Id $uiCheck.Id -Force -ErrorAction SilentlyContinue;throw 'WPF UI smoke exceeded 240 seconds'}
   $uiCheck.WaitForExit()
   [IO.File]::WriteAllText((Join-Path $logDir "ui-smoke-$stamp.stdout.log"),$uiOut.Result,[Text.Encoding]::UTF8)
   [IO.File]::WriteAllText((Join-Path $logDir "ui-smoke-$stamp.stderr.log"),$uiErr.Result,[Text.Encoding]::UTF8)
   if($uiCheck.ExitCode -ne 0){throw 'WPF UI smoke failed. No installer will be created.'}
  } finally {
   if($null -ne $uiCheck){$uiCheck.Dispose()}
   [Environment]::SetEnvironmentVariable('QLKP_UI_EVIDENCE',$priorUiEvidence,'Process')
   [Environment]::SetEnvironmentVariable('QLKP_TEST_PARENT_ID',$priorParent,'Process')
  }
 } else {Skip-QlkpStage $run 'WINDOWS_UI_SMOKE'}
 if($Installer -and -not $NoInstaller) {
  Start-QlkpStage $run 'INSTALLER'
  $iscc=Get-Command ISCC.exe -ErrorAction SilentlyContinue
  if($iscc){$isccPath=$iscc.Source}
  else {
   $isccObj=Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup*\ISCC.exe","$env:ProgramFiles\Inno Setup*\ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
   if(-not $isccObj){throw 'Không tìm thấy ISCC.exe (Inno Setup). Không tạo bộ cài.'}
   $isccPath=$isccObj.FullName
  }
  $outputPath=Join-Path $root 'dist\QLKP-PILOT-SETUP-X64-UNSIGNED.exe'
  if(Test-Path $outputPath) {Remove-Item -LiteralPath $outputPath -Force}
  & $isccPath 'installer\QLKP-PILOT.iss'
  if($LASTEXITCODE -ne 0) {throw 'Inno Setup đóng gói thất bại.'}
  if(-not (Test-Path $outputPath)) {throw 'Inno Setup báo thành công nhưng không có setup.exe mới.'}
  $setup=Get-Item -LiteralPath $outputPath
  if($setup.Length -lt 100000) {throw 'Dung lượng bộ cài không hợp lệ.'}
  Start-QlkpStage $run 'SETUP_SHA256'
  $hash=Get-FileHash $setup.FullName -Algorithm SHA256
  ('SHA256 '+$hash.Hash+' '+$setup.Name) | Set-Content (Join-Path $root 'dist\SHA256SUMS.txt') -Encoding Ascii
  Write-Host "SETUP ĐÃ TẠO (UNSIGNED PILOT): $($setup.FullName)" -ForegroundColor Green
  Write-Host "SHA256: $($hash.Hash)"
 } else {Skip-QlkpStage $run 'INSTALLER';Skip-QlkpStage $run 'SETUP_SHA256';Write-Host "BUILD THÀNH CÔNG: $exe" -ForegroundColor Green}
 Complete-QlkpTestRun $run 'PASS'
 Write-Host ("Hồ sơ kiểm thử: "+(Join-Path $run.directory 'session.json'))
 Write-Host "Build log: $log"
 Write-Warning 'Kho đã mã hóa; bản PILOT còn chờ nghiệm thu toàn bộ quy trình và ký nhà phát hành.'
}
catch {
 $buildFailure=$_
 try {if($null -ne $run){Complete-QlkpTestRun $run 'FAIL' ($buildFailure.Exception.GetType().Name)}} catch {Write-Warning 'Không hoàn tất được hồ sơ phiên; kết quả chưa được xác nhận.'}
 # A failed compiler/packager/hash step must never leave a release-looking artifact.
 foreach($failedArtifact in @('dist\QLKP-PILOT-SETUP-X64-UNSIGNED.exe','dist\SHA256SUMS.txt')) {
  $failedPath=Join-Path $root $failedArtifact
  if(Test-Path -LiteralPath $failedPath) {
   try { Remove-Item -LiteralPath $failedPath -Force -ErrorAction Stop }
   catch { Write-Warning "Không thể xóa sản phẩm build lỗi: $failedPath. KHÔNG sử dụng tệp này." }
  }
 }
 throw $buildFailure
}
finally {try {Stop-Transcript | Out-Null} catch { }}
