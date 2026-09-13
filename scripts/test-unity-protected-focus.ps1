param([string]$Executable='', [string]$Output='', [switch]$VerifyDiagnostics)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-protected-focus/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
Add-Type -Path (Join-Path $root 'tools/UnityPreviewWindow.cs')
Add-Type -Path (Join-Path $root 'tools/UnityAccessibilityCheck.cs')
$before=[UnityAccessibilityCheck]::Read()
$clipBefore=[UnityAccessibilityCheck]::ReadCursorClip() -join ','
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner';$hashes=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $hashes[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
$log=Join-Path $Output 'player.log'
$arguments=@('-screen-fullscreen','1','--mute','--data',('"'+(Join-Path $Output 'profile')+'"'),'-logFile',('"'+$log+'"'))
$p=Start-Process -FilePath $Executable -ArgumentList $arguments -PassThru -WindowStyle Normal
# Independent process deadline; never rely on the game loop or parent chord for test cleanup.
$watchdog=$null
try { $watchdog=Start-Job -ArgumentList $p.Id,$p.StartTime.ToUniversalTime().Ticks -ScriptBlock {
 param($ownedId,$startTicks)
 Start-Sleep -Seconds 90
 $owned=Get-Process -Id $ownedId -ErrorAction SilentlyContinue
 if($owned -and $owned.StartTime.ToUniversalTime().Ticks -eq $startTicks){Stop-Process -Id $ownedId}
} } catch {
 if(!$p.HasExited){$p.Kill();[void]$p.WaitForExit(5000)}
 $p.Dispose();throw
}
$rounds=0;$success=$false
function Read-PlayerLog {if(Test-Path -LiteralPath $log){return [UnityPreviewWindow]::ReadSharedReport($log)};return ''}
function Wait-Input([bool]$active,[int]$minimumTransitions,[int]$timeoutSeconds=6){
 $deadline=[DateTime]::UtcNow.AddSeconds($timeoutSeconds)
 while(!$p.HasExited -and [DateTime]::UtcNow -lt $deadline){
  $text=Read-PlayerLog
  $transitions=[regex]::Matches($text,'KEYLEARNER_INPUT_TRANSITION active=(True|False)')
  if($transitions.Count -ge $minimumTransitions -and $transitions[$transitions.Count-1].Groups[1].Value -eq $active.ToString()){return $transitions.Count}
  Start-Sleep -Milliseconds 100
 }
 throw "Protected player did not reach active=$active within its bounded wait."
}
try {
 $count=Wait-Input $true 1 30
 if((Read-PlayerLog) -notmatch 'KEYLEARNER_INPUT_WINDOW.*fullscreen=True'){throw 'Installed-play startup was not already fullscreen.'}
 $diagnosticVerified=$false
 if($VerifyDiagnostics){
  $diagnosticRoot=Join-Path $Output 'profile/diagnostics'
  $diagnosticDeadline=[DateTime]::UtcNow.AddSeconds(15)
  $baseline=$null
  while([DateTime]::UtcNow -lt $diagnosticDeadline -and !$p.HasExited){
   $diagnosticFile=Get-ChildItem -LiteralPath $diagnosticRoot -Filter 'input-session-*.jsonl' -ErrorAction SilentlyContinue | Select-Object -First 1
   if($diagnosticFile){
    $records=@([UnityPreviewWindow]::ReadSharedReport($diagnosticFile.FullName) -split '\r?\n' | Where-Object {$_} | ForEach-Object {$_ | ConvertFrom-Json})
    $baseline=$records | Where-Object {$_.kind -eq 'heartbeat' -and $_.data.view -eq 'picker' -and $_.data.input.active} | Select-Object -Last 1
    if($baseline){break}
   }
   Start-Sleep -Milliseconds 100
  }
  if(!$baseline){throw 'No flushed protected picker heartbeat after the introduction.'}
  $beforeSynthetic=[long]$baseline.data.input.guard.syntheticPackets
  [UnityPreviewWindow]::HoldRight($p.Id,500)
  $diagnosticDeadline=[DateTime]::UtcNow.AddSeconds(6)
  while([DateTime]::UtcNow -lt $diagnosticDeadline -and !$p.HasExited){
   $diagnosticText=[UnityPreviewWindow]::ReadSharedReport($diagnosticFile.FullName)
   $records=@($diagnosticText -split '\r?\n' | Where-Object {$_} | ForEach-Object {$_ | ConvertFrom-Json})
   $observed=$records | Where-Object {$_.kind -eq 'heartbeat' -and $_.data.input.guard.syntheticPackets -ge ($beforeSynthetic+2)} | Select-Object -Last 1
   if($observed){break}
   Start-Sleep -Milliseconds 100
  }
  if(!$observed){throw 'Diagnostics did not acknowledge the OS-tagged synthetic press and release.'}
  if($observed.data.pickerKeyEvents -ne $baseline.data.pickerKeyEvents -or $observed.data.input.guard.heldCount -ne 0){throw 'Synthetic input incorrectly entered protected gameplay.'}
  if($diagnosticText -match '(?i)"(?:key|scan|heldKeys|text|password|arguments)"\s*:'){throw 'Unexpected sensitive input field in the diagnostic report.'}
  $diagnosticVerified=$true
 }
 for($i=0;$i -lt 3;$i++){
  [UnityPreviewWindow]::Minimize($p.Id)
  $count=Wait-Input $false ($count+1)
  if(![UnityAccessibilityCheck]::Equal($before,[UnityAccessibilityCheck]::Read())){throw 'Background player retained accessibility changes.'}
  if(([UnityAccessibilityCheck]::ReadCursorClip() -join ',') -ne $clipBefore){throw 'Background player retained cursor confinement.'}
  [UnityPreviewWindow]::Restore($p.Id)
  $count=Wait-Input $true ($count+1)
  $rounds++
 }
 [UnityPreviewWindow]::Minimize($p.Id)
 $count=Wait-Input $false ($count+1)
 if(!$p.CloseMainWindow() -or !$p.WaitForExit(5000) -or $p.ExitCode -ne 0){throw 'Inactive protected player vetoed normal close or did not exit cleanly.'}
 if((Read-PlayerLog) -match '(?m)^(?:[\w.]*Exception:|Shader error|Could not load file or assembly)'){throw 'Player log contains a runtime error.'}
 $success=$true
} finally {
 if(!$p.HasExited){$p.Kill();[void]$p.WaitForExit(5000)}
 $p.Dispose();if($watchdog){Stop-Job $watchdog;Remove-Job $watchdog}
 Start-Sleep -Milliseconds 700
 $restored=[UnityAccessibilityCheck]::Equal($before,[UnityAccessibilityCheck]::Read())
 if(!$restored){[UnityAccessibilityCheck]::RestoreOwned($before)}
 foreach($name in $hashes.Keys){$path=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($after -ne $hashes[$name]){throw "Parent profile changed: $name"}}
 [pscustomobject]@{passed=($success -and $restored);protectedFocusCycles=$rounds;accessibilityRestored=$restored;profilesUnchanged=$true;physicalKeyboardTested=$false;diagnosticsVerified=($success -and $diagnosticVerified)} | ConvertTo-Json | Set-Content (Join-Path $Output 'result.json')
 if(!$restored){throw 'Final accessibility restoration required explicit cleanup.'}
}
Write-Output "PASS: $rounds protected focus cycles, background cursor/accessibility release, inactive close and profile preservation. Physical keystrokes are not synthesized by this test. Evidence: $Output"