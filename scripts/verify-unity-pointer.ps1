param([string]$Executable='', [string]$Output='', [int[]]$Modes=@(6,7,8,9,10,11), [switch]$PortraitOnly, [switch]$SkipPortrait, [switch]$HoldReportReadLock)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-pointer/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
if(!('UnityPreviewWindow' -as [type])){Add-Type -Path (Join-Path $root 'tools/UnityPreviewWindow.cs')}
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner';$before=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $before[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
$cases=@()
foreach($mode in $Modes){$cases+=,@{Mode=$mode;Width=if($PortraitOnly){720}else{1366};Height=if($PortraitOnly){1080}else{768}}}
if(!$PortraitOnly -and !$SkipPortrait -and 6 -in $Modes){$cases+=,@{Mode=6;Width=720;Height=1080}}
$results=[Collections.Generic.List[object]]::new()
try{
 foreach($case in $cases){
  $name='mode-'+$case.Mode+'-'+$case.Width+'x'+$case.Height
  $folder=Join-Path $Output $name;New-Item -ItemType Directory -Force $folder | Out-Null
  $statePath=Join-Path $folder 'interaction.json';$capture=Join-Path $folder 'gameplay.png';$log=Join-Path $folder 'player.log'
  $scenario=if($case.Mode -eq 6){'dots-pointer'}else{'math-pointer'}
  $a=2;$b=1;if($case.Mode -eq 9){$a=0;$b=3};if($case.Mode -eq 10){$a=1;$b=3}
  $arguments=@('-screen-fullscreen','0','-screen-width',[string]$case.Width,'-screen-height',[string]$case.Height,'--preview','--mute','--data',('"'+(Join-Path $folder 'profile')+'"'),'--mode',[string]$case.Mode,'--scenario',$scenario,'--interaction-report',('"'+$statePath+'"'),'--seconds','18','--screenshot',('"'+$capture+'"'),'--width',[string]$case.Width,'--height',[string]$case.Height,'--math-level','1','--math-a',[string]$a,'--math-b',[string]$b,'--math-operation','add','-logFile',('"'+$log+'"'))
  $process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Normal -PassThru
  $focused=$false;$clicked=@{};$correct=$false;$rightIgnored=$false;$doubleSuppressed=$false;$reportLockExercised=$false;$deadline=[DateTime]::UtcNow.AddSeconds(50)
  try{
   while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){
    if(!$focused){try{$focused=[UnityPreviewWindow]::Focus($process.Id)}catch{}}
    $state=$null
    if(Test-Path -LiteralPath $statePath){try{$state=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json}catch{}}
    if($state -and $state.Correct -and !$correct){$correct=$true;$state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $folder 'accepted-answer.json')}
    if($state -and $state.CanAnswer -and !$correct){
     $target=if($case.Mode -eq 9){$state.Targets | Where-Object {!( $state.BuiltMask -band (1 -shl $_.Value)) -and !$clicked.ContainsKey($_.Value)} | Select-Object -First 1}else{$state.Targets | Where-Object Value -eq $state.Answer | Select-Object -First 1}
     if($target -and !$clicked.ContainsKey($target.Value)){
      # Independent layout mapping check before a native pointer event in the owned foreground player.
      $scale=[Math]::Min($state.Width/$state.LayoutWidth,$state.Height/$state.LayoutHeight)
      $x=[int][Math]::Round($target.LogicalX*$scale+($state.Width-$state.LayoutWidth*$scale)/2)
      $y=[int][Math]::Round($target.LogicalY*$scale+($state.Height-$state.LayoutHeight*$scale)/2)
      if([Math]::Abs($x-$target.X) -gt 1 -or [Math]::Abs($y-$target.Y) -gt 1){throw 'Reported answer target disagrees with independently calculated viewport mapping.'}
      if(!$rightIgnored){
       [UnityPreviewWindow]::Click($process.Id,$x,$y,$true)
       Start-Sleep -Milliseconds 300
       $afterRight=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json
       if(!$afterRight.CanAnswer -or $afterRight.Stage -ne $state.Stage -or $afterRight.BuiltMask -ne $state.BuiltMask -or $afterRight.Errors -ne $state.Errors -or $afterRight.WrongAttempts -ne $state.WrongAttempts){throw 'Right click changed the answer state.'}
       $rightIgnored=$true
      }
      if($HoldReportReadLock -and $case.Mode -eq 6 -and !$reportLockExercised){
       # Deliberately reproduce a legacy observer that temporarily denies atomic
       # replacement. Gameplay must continue and the writer must retry afterward.
       $heldReport=[IO.File]::Open($statePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
       try{[UnityPreviewWindow]::Click($process.Id,$x,$y,$false);Start-Sleep -Milliseconds 500}
       finally{$heldReport.Dispose()}
       $reportLockExercised=$true
      }else{[UnityPreviewWindow]::Click($process.Id,$x,$y,$false)}
      if($case.Mode -eq 9 -and $clicked.Count -eq 0){
       [UnityPreviewWindow]::Click($process.Id,$x,$y,$false)
       Start-Sleep -Milliseconds 250
       $afterDouble=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json
       if(!($afterDouble.BuiltMask -band (1 -shl $target.Value))){throw 'Rapid second press toggled the first constructed dot off.'}
       $doubleSuppressed=$true
      }
      $clicked[$target.Value]=$true
      Start-Sleep -Milliseconds 250
     }
    }
    Start-Sleep -Milliseconds 100
   }
   if(!$process.HasExited){$process.Kill();throw 'Pointer preview timed out.'}
   if($process.ExitCode -ne 0){throw "Player exit code $($process.ExitCode); inspect player.log before diagnosing input."}
   if(!$correct){throw 'Actual native answer click did not reach the reward phase.'}
   if(!(Test-Path -LiteralPath $capture) -or !(Test-Path -LiteralPath ($capture+'.json'))){throw 'Actual player screenshot/report missing.'}
   $captureReport=Get-Content -LiteralPath ($capture+'.json') -Raw | ConvertFrom-Json
   if($captureReport.runtimeErrors){throw "Unity runtime errors: $($captureReport.runtimeErrors -join '; ')"}
   if($captureReport.error -or $captureReport.frameCount -lt 30 -or $null -eq $captureReport.activeSeconds -or $captureReport.activeSeconds -lt 17.9){throw 'Player capture has errors, too few frames, or insufficient active gameplay duration.'}
   if((Get-Content -LiteralPath $log -Raw) -match '(?m)^(?:[\w.]*Exception:|Shader error|Could not load file or assembly)'){throw 'Player log contains runtime errors.'}
   $results.Add([pscustomobject]@{case=$name;passed=$true;answerTargets=$clicked.Count;rightIgnored=$rightIgnored;reportLockExercised=$reportLockExercised;rapidSecondPressIgnored=$(if($case.Mode -eq 9){$doubleSuppressed}else{$null});evidence=$folder})
   Write-Output "PASS $name actual native answer; $($clicked.Count) answer targets, right-button rejection checked."
  }catch{
   $results.Add([pscustomobject]@{case=$name;passed=$false;error=$_.Exception.Message;evidence=$folder})
   Write-Output "FAIL $name $($_.Exception.Message)"
  }finally{if(!$process.HasExited){$process.Kill()};$process.Dispose()}
 }
}finally{
 $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output 'results.json')
 foreach($name in $before.Keys){$path=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($before[$name] -ne $after){throw "The real parent save changed: $name"}}
}
Write-Output "Evidence: $Output"
if(@($results | Where-Object {!$_.passed}).Count){throw 'One or more actual native pointer checks failed.'}
