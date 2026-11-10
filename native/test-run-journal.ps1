# Structured build journal. No command arguments, credentials or business-record values are recorded.
function Save-QlkpRun($Run,[string]$EventName,$Step) {
 $Run.report.last_sequence++
 $Run.report.total=$Run.report.steps.Count
 $Run.report.passed=@($Run.report.steps | Where-Object {$_.status -eq 'PASS'}).Count
 $Run.report.failed=@($Run.report.steps | Where-Object {$_.status -eq 'FAIL'}).Count
 $Run.report.skipped=@($Run.report.steps | Where-Object {$_.status -eq 'SKIP'}).Count
 $encoding=[System.Text.UTF8Encoding]::new($false)
 $entry=[ordered]@{session_id=$Run.report.session_id;sequence=$Run.report.last_sequence;event=$EventName;at_utc=[DateTime]::UtcNow.ToString('o');step=$Step}
 $bytes=$encoding.GetBytes(($entry | ConvertTo-Json -Depth 12 -Compress)+"`n")
 $stream=[System.IO.FileStream]::new((Join-Path $Run.directory 'events.jsonl'),[IO.FileMode]::Append,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
 $path=Join-Path $Run.directory 'session.json'
 $temp=$path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try {
  $bytes=$encoding.GetBytes(($Run.report | ConvertTo-Json -Depth 12))
  $stream=[System.IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
  try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
  if(Test-Path -LiteralPath $path){
   # Windows PowerShell 5.1 converts $null to an empty string for this overload.
   # Use an explicit same-directory backup path and remove it after atomic replacement.
   $backup=$temp+'.bak'
   [IO.File]::Replace($temp,$path,$backup)
   [IO.File]::Delete($backup)
  }else{[IO.File]::Move($temp,$path)}
 }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}}
}
function New-QlkpTestRun([string]$Root) {
 $id=[guid]::NewGuid().ToString('N');$directory=Join-Path $Root $id
 New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
 $steps=New-Object System.Collections.ArrayList
 $report=[ordered]@{format='QLKP_TEST_RUN_V1';session_id=$id;parent_session_id='';kind='windows_build';layer='build';status='RUNNING';started_utc=[DateTime]::UtcNow.ToString('o');finished_utc='';duration_ms=0;source_version='0.20.2-PILOT';source_hash='';hash_kind='';environment=([Environment]::OSVersion.VersionString+' '+$env:PROCESSOR_ARCHITECTURE);origin='LOCAL';original_session_id='';import_sha256='';last_sequence=0;total=0;passed=0;failed=0;skipped=0;steps=$steps}
 $run=@{report=$report;directory=$directory;clock=[Diagnostics.Stopwatch]::StartNew();active=$null;stepClock=$null;completed=$false}
 $f=[System.IO.FileStream]::new((Join-Path $directory 'events.jsonl'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 $f.Dispose();Save-QlkpRun $run 'SESSION_START' $null;return $run
}
function End-QlkpStage($Run,[ValidateSet('PASS','FAIL','SKIP','INTERRUPTED')][string]$Status,[string]$ErrorType='') {
 if($null -eq $Run.active){return}
 $step=$Run.active;$step.status=$Status;$step.finished_utc=[DateTime]::UtcNow.ToString('o');$step.duration_ms=$Run.stepClock.ElapsedMilliseconds
 $step.error_type=if($ErrorType -match '^[A-Za-z0-9_.]{0,120}$'){$ErrorType}else{'Error'}
 $Run.active=$null;Save-QlkpRun $Run 'STEP_END' $step
}
function Start-QlkpStage($Run,[string]$Code) {
 if($Run.completed){throw 'Phiên đã kết thúc.'}
 if($null -ne $Run.active){End-QlkpStage $Run 'PASS'}
 $step=[ordered]@{code=$Code;status='RUNNING';started_utc=[DateTime]::UtcNow.ToString('o');finished_utc='';duration_ms=0;error_type=''}
 [void]$Run.report.steps.Add($step);$Run.active=$step;$Run.stepClock=[Diagnostics.Stopwatch]::StartNew();Save-QlkpRun $Run 'STEP_START' $step
}
function Skip-QlkpStage($Run,[string]$Code){Start-QlkpStage $Run $Code;End-QlkpStage $Run 'SKIP'}
function Complete-QlkpTestRun($Run,[ValidateSet('PASS','FAIL','INTERRUPTED')][string]$Status,[string]$ErrorType='') {
 if($Run.completed){return}
 if($null -ne $Run.active){End-QlkpStage $Run $Status $ErrorType}
 if(@($Run.report.steps | Where-Object {$_.status -eq 'FAIL'}).Count -gt 0){$Status='FAIL'}
 elseif(@($Run.report.steps | Where-Object {$_.status -eq 'INTERRUPTED'}).Count -gt 0){$Status='INTERRUPTED'}
 $Run.report.status=$Status;$Run.report.finished_utc=[DateTime]::UtcNow.ToString('o');$Run.report.duration_ms=$Run.clock.ElapsedMilliseconds
 Save-QlkpRun $Run 'SESSION_END' $null;$Run.completed=$true
}
