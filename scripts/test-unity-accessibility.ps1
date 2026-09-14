param([string]$Executable='', [string]$Output='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-accessibility/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
if(!('UnityAccessibilityCheck' -as [type])){Add-Type -Path (Join-Path $root 'tools/UnityAccessibilityCheck.cs')}
$before=[UnityAccessibilityCheck]::Read()
$before | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'before.json')
$profile=Join-Path $env:LOCALAPPDATA 'KeyLearner';$profileBefore=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $profile $name;$profileBefore[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
$probeRoot=Join-Path ([IO.Path]::GetTempPath()) 'KeyLearner-accessibility-probe'
$existing=@(if(Test-Path -LiteralPath $probeRoot){Get-ChildItem -LiteralPath $probeRoot -File -Filter 'accessibility-*.json' | Select-Object -ExpandProperty FullName})
$owned=[Collections.Generic.List[object]]::new();$normalRestored=$false;$crashRestored=$false;$duringSafe=$false;$profilesUnchanged=$false;$finalRestored=$false;$errorText='';$ownedLease='';$guardianId=0
function Start-Probe([string]$label,[bool]$wait){
 $arguments=@('-batchmode','-nographics','--probe-accessibility','-logFile',('"'+(Join-Path $Output ($label+'.log'))+'"'))
 if($wait){$arguments+='--wait'}
 $process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
 $record=[pscustomobject]@{Process=$process;Id=$process.Id;StartTicks=$process.StartTime.ToUniversalTime().Ticks}
 $owned.Add($record)
 return $record
}
function Stop-Owned($record){
 if($record.Process.HasExited){return}
 $current=Get-Process -Id $record.Id -ErrorAction SilentlyContinue
 if(!$current){return}
 try{
  if($current.StartTime.ToUniversalTime().Ticks -ne $record.StartTicks -or $current.MainModule.FileName -ne $Executable){throw 'Refusing to terminate a process whose identity no longer matches the diagnostic launch.'}
  $current.Kill()
  if(!$current.WaitForExit(10000)){throw 'Owned accessibility diagnostic did not exit.'}
 }finally{$current.Dispose()}
}
try{
 $normal=Start-Probe 'normal' $false
 if(!$normal.Process.WaitForExit(60000)){throw 'Normal accessibility diagnostic exceeded its deadline.'}
 if($normal.Process.ExitCode -ne 0){throw "Normal diagnostic exit code $($normal.Process.ExitCode)"}
 $normalRestored=[UnityAccessibilityCheck]::Equal($before,[UnityAccessibilityCheck]::Read())
 if(!$normalRestored){throw 'Normal lease disposal did not restore the original full accessibility structures.'}
 $crash=Start-Probe 'forced-exit' $true
 $deadline=[DateTime]::UtcNow.AddSeconds(20)
 while(!$crash.Process.HasExited -and [DateTime]::UtcNow -lt $deadline){
  $lease=@(if(Test-Path -LiteralPath $probeRoot){Get-ChildItem -LiteralPath $probeRoot -File -Filter 'accessibility-*.json' | Where-Object {$_.FullName -notin $existing}}) | Select-Object -First 1
  $during=[UnityAccessibilityCheck]::Read()
  if($lease -and [UnityAccessibilityCheck]::OnlyOwnedBitsApplied($before,$during)){
   $helperPath=[IO.Path]::GetFullPath((Join-Path (Split-Path $Executable -Parent) 'KeyLearner_Data/StreamingAssets/Platform/KeyLearner.PlatformHelper.exe'))
   $guardian=Get-CimInstance Win32_Process -Filter "Name='KeyLearner.PlatformHelper.exe'" | Where-Object {$_.ParentProcessId -eq $crash.Id -and $_.ExecutablePath -eq $helperPath -and $_.CommandLine -and $_.CommandLine.Contains('--restore-accessibility') -and $_.CommandLine.Contains($lease.FullName)} | Select-Object -First 1
   if($guardian){$ownedLease=$lease.FullName;$guardianId=$guardian.ProcessId;$duringSafe=$true;break}
  }
  Start-Sleep -Milliseconds 100
 }
 if(!$duringSafe){throw 'The diagnostic did not apply only the 0x0c shortcut bits within its bounded startup.'}
 $during | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'during.json')
 Stop-Owned $crash
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 while([DateTime]::UtcNow -lt $deadline){
  if([UnityAccessibilityCheck]::Equal($before,[UnityAccessibilityCheck]::Read()) -and !(Test-Path -LiteralPath $ownedLease)){$crashRestored=$true;break}
  Start-Sleep -Milliseconds 100
 }
 if(!$crashRestored){throw 'The packaged guardian did not restore the original structures and remove its own lease after forced diagnostic exit.'}
 Write-Output 'PASS actual Unity accessibility lease: normal disposal and packaged-helper restoration after owned process termination; only shortcut/confirmation bits changed.'
}catch{$errorText=$_.Exception.Message;throw}
finally{
 try{
  try{foreach($record in $owned){Stop-Owned $record}}
  finally{[UnityAccessibilityCheck]::RestoreOwned($before)}
  # Even a failed guardian check must release only the exact bits still owned by
  # this test. Current feature-enabled flags, timing values and all other bits stay intact.
  # The nested finally above runs restoration even if process cleanup fails.
  $after=[UnityAccessibilityCheck]::Read()
  $finalRestored=[UnityAccessibilityCheck]::Equal($before,$after)
  $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'after.json')
  if(!$finalRestored){throw 'Final accessibility state differs from the snapshot; unrelated changes were preserved.'}
  foreach($name in $profileBefore.Keys){$path=Join-Path $profile $name;$hash=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($hash -ne $profileBefore[$name]){throw "A real parent profile changed: $name"}}
  $profilesUnchanged=$true
 }catch{if(!$errorText){$errorText=$_.Exception.Message};throw}
 finally{
  foreach($record in $owned){$record.Process.Dispose()}
  [pscustomobject]@{passed=(!$errorText);normalRestored=$normalRestored;crashGuardianRestored=$crashRestored;onlyOwnedBitsChanged=$duringSafe;finalStateRestored=$finalRestored;profileFilesUnchanged=$profilesUnchanged;error=$errorText;leasePath=$ownedLease;packagedGuardianPid=$guardianId} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'result.json')
 }
}
Write-Output "Evidence: $Output. No keyboard capture, parent shortcut synthesis, or production profile launch occurred."
