param([string]$Executable='', [string]$Output='', [ValidateRange(500,2000)][int]$HoldMilliseconds=1200)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-steering/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $Output | Out-Null
if(!('UnityPreviewWindow' -as [type])){Add-Type -Path (Join-Path $root 'tools/UnityPreviewWindow.cs')}
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner';$before=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $before[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
$statePath=Join-Path $Output 'interaction.json';$capture=Join-Path $Output 'gameplay.png';$log=Join-Path $Output 'player.log'
$arguments=@('-screen-fullscreen','0','-screen-width','1366','-screen-height','768','--preview','--mute','--data',('"'+(Join-Path $Output 'profile')+'"'),'--mode','4','--interaction-report',('"'+$statePath+'"'),'--seconds','12','--screenshot',('"'+$capture+'"'),'-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Normal -PassThru
$deadline=[DateTime]::UtcNow.AddSeconds(55);$focused=$false;$baseline=$null;$nativeTask=$null;$during=[Collections.Generic.List[object]]::new();$final=$null
function Read-Interaction {
 if(Test-Path -LiteralPath $statePath){try{return [UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json}catch{}}
 return $null
}
try{
 while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){
  if(!$focused){try{$focused=[UnityPreviewWindow]::Focus($process.Id)}catch{}}
  $state=Read-Interaction
  if($focused -and $state -and $state.mode -eq 4 -and $state.time -ge 2){$baseline=$state;break}
  Start-Sleep -Milliseconds 60
 }
 if(!$baseline){throw 'The displayed Racer did not provide an active baseline.'}
 if($baseline.rightHeld){throw 'The right key was already held before the test.'}
 $baseline | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'before-turn.json')
 $nativeTask=[UnityPreviewWindow]::HoldRightAsync($process.Id,$HoldMilliseconds)
 while(!$nativeTask.IsCompleted){
  $state=Read-Interaction
  if($state -and $state.rightHeld -and ($during.Count -eq 0 -or $state.time -ne $during[$during.Count-1].time)){$during.Add($state)}
  Start-Sleep -Milliseconds 25
 }
 $nativeTask.GetAwaiter().GetResult()
 if($during.Count -lt 3){throw 'Too few reported frames acknowledged the actual right-arrow hold.'}
 $last=$during[$during.Count-1];$delta=[double]$last.lateralPixels-[double]$baseline.lateralPixels
 $during | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'during-turn.json')
 if($delta -le 3){throw "Right arrow moved car $([Math]::Round($delta,2))px relative to the road; expected visible movement to the right."}
 Start-Sleep -Milliseconds 300
 $final=Read-Interaction
 if(!$final -or $final.rightHeld){throw 'The right-arrow release was not acknowledged by the player.'}
 $final | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'after-release.json')
 while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(!$process.HasExited){throw 'Steering preview exceeded its bounded deadline.'}
 if($process.ExitCode -ne 0){throw "Player exit code $($process.ExitCode)"}
 if(!(Test-Path -LiteralPath $capture) -or !(Test-Path -LiteralPath ($capture+'.json'))){throw 'Player screenshot/report missing.'}
 $captureReport=Get-Content -LiteralPath ($capture+'.json') -Raw | ConvertFrom-Json
   if($captureReport.runtimeErrors){throw "Unity runtime errors: $($captureReport.runtimeErrors -join '; ')"}
 if($captureReport.error -or $captureReport.frameCount -lt 30 -or $null -eq $captureReport.activeSeconds -or $captureReport.activeSeconds -lt 11.9){throw 'Player reported an error or insufficient rendered frames.'}
 if((Get-Content -LiteralPath $log -Raw) -match '(?m)^(?:[\w.]*Exception:|Shader error|Could not load file or assembly)'){throw 'Player log contains a runtime/graphics error.'}
 $result=[pscustomobject]@{passed=$true;heldMilliseconds=$HoldMilliseconds;acknowledgedSamples=$during.Count;deltaRightPixels=$delta;rightReleased=$true;capture=$capture}
 $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'result.json')
 Write-Output "PASS actual Racer right-arrow hold: +$([Math]::Round($delta,2))px relative to road, $($during.Count) acknowledged samples, key released. Evidence: $Output"
}catch{
 [pscustomobject]@{passed=$false;error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'result.json')
 throw
}finally{
 if($nativeTask -and !$nativeTask.IsCompleted){try{$nativeTask.GetAwaiter().GetResult()}catch{}}
 if(!$process.HasExited){$process.Kill()};$process.Dispose()
 foreach($name in $before.Keys){$path=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($before[$name] -ne $after){throw "The real parent save changed: $name"}}
}
