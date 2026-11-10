[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'test-run-journal.ps1')
$run=New-QlkpTestRun (Join-Path $env:LOCALAPPDATA 'QLKP-V2-PILOT\test-runs')
$run.report.kind='windows_install';$run.report.layer='install'
$target=Join-Path $env:TEMP ('QLKP-INSTALL-'+[guid]::NewGuid().ToString('N'))
$setup=Join-Path $PSScriptRoot 'dist\QLKP-PILOT-SETUP-X64-UNSIGNED.exe'
$priorParent=[Environment]::GetEnvironmentVariable('QLKP_TEST_PARENT_ID','Process')
function Invoke-QlkpProcess {
 param([string]$File,[string]$Arguments,[string]$WorkingDirectory,[int]$Timeout=120000,[string]$Stdout='', [string]$Stderr='')
 $info=New-Object Diagnostics.ProcessStartInfo
 $info.FileName=$File;$info.Arguments=$Arguments;$info.WorkingDirectory=$WorkingDirectory;$info.UseShellExecute=$false
 $info.RedirectStandardOutput=($Stdout -ne '');$info.RedirectStandardError=($Stderr -ne '')
 $process=New-Object Diagnostics.Process;$process.StartInfo=$info
 try {
  [void]$process.Start()
  if($Stdout){$outTask=$process.StandardOutput.ReadToEndAsync()}
  if($Stderr){$errTask=$process.StandardError.ReadToEndAsync()}
  if(-not $process.WaitForExit($Timeout)){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue;throw 'Installer process timed out'}
  $process.WaitForExit()
  if($Stdout){[IO.File]::WriteAllText($Stdout,$outTask.Result,[Text.Encoding]::UTF8)}
  if($Stderr){[IO.File]::WriteAllText($Stderr,$errTask.Result,[Text.Encoding]::UTF8)}
  if($process.ExitCode -ne 0){throw ('Installer process failed: '+$process.ExitCode)}
 } finally {$process.Dispose()}
}

try {
 Start-QlkpStage $run 'VERIFY_SETUP'
 if(-not(Test-Path $setup)){throw 'Setup file missing'}
 $run.report.source_hash=(Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant();$run.report.hash_kind='setup_sha256'
 Start-QlkpStage $run 'INSTALL_SILENT'
 $installLog=Join-Path $PSScriptRoot 'build-logs\install-smoke.log'
 Invoke-QlkpProcess -File $setup -Arguments ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="'+$target+'" /LOG="'+$installLog+'"') -WorkingDirectory $PSScriptRoot
 Start-QlkpStage $run 'INSTALLED_SELF_TEST'
 $installedExe=Join-Path $target 'QLKP.Desktop.exe'
 if(-not(Test-Path $installedExe)){throw 'Installed executable missing'}
 if((Get-Item $installedExe).VersionInfo.FileVersion -ne '0.20.6.0'){throw 'Installed version mismatch'}
 $env:QLKP_TEST_PARENT_ID=$run.report.session_id
 Invoke-QlkpProcess -File $installedExe -Arguments '--self-test' -WorkingDirectory $target -Stdout (Join-Path $PSScriptRoot 'build-logs\installed-self-test.stdout.log') -Stderr (Join-Path $PSScriptRoot 'build-logs\installed-self-test.stderr.log')
 Start-QlkpStage $run 'UNINSTALL_SILENT'
 $uninstall=Join-Path $target 'unins000.exe'
 if(-not(Test-Path $uninstall)){throw 'Uninstaller missing'}
 Invoke-QlkpProcess -File $uninstall -Arguments '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WorkingDirectory $target
 if(Test-Path $installedExe){throw 'Uninstaller left executable behind'}
 Complete-QlkpTestRun $run 'PASS'
} catch {
 Complete-QlkpTestRun $run 'FAIL' ($_.Exception.GetType().Name)
 throw
} finally {
 [Environment]::SetEnvironmentVariable('QLKP_TEST_PARENT_ID',$priorParent,'Process')
}
