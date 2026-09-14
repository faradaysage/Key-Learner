param([string]$Executable='', [string]$Output='', [switch]$Sound, [string[]]$Cases=@('pearl-paint','hop-ten','bubble','treasure','obstacle','steering','breach','dinosaur-roar','dinosaur-motion','dinosaur-camera','ocean-visitor','bird-companion'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-immersion/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
Add-Type -Path (Join-Path $root 'tools/UnityPreviewWindow.cs')
$before=@{};$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner'
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $before[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
$results=[Collections.Generic.List[object]]::new()
try{
 foreach($case in $Cases){
  if($case -notin @('pearl-paint','hop-ten','bubble','treasure','obstacle','steering','breach','dinosaur-roar','dinosaur-motion','dinosaur-camera','ocean-visitor','bird-companion')){throw "Unknown case $case"}
  $folder=Join-Path $Output $case;New-Item -ItemType Directory -Force $folder | Out-Null
  $statePath=Join-Path $folder 'interaction.json';$capture=Join-Path $folder 'gameplay.png';$log=Join-Path $folder 'player.log'
  $mode=switch($case){'pearl-paint'{6};'hop-ten'{11};'dinosaur-roar'{12};'dinosaur-motion'{12};'dinosaur-camera'{12};'bird-companion'{3};{$_ -in @('bubble','breach','ocean-visitor')}{5};default{4}}
  $scenario=switch($case){'pearl-paint'{'dots-bonus'};'hop-ten'{'math-pointer'};'treasure'{'racer-treasure'};'obstacle'{'racer-obstacle'};'ocean-visitor'{'ocean-visitor'};'bird-companion'{'bird-companion'};default{''}}
  $profile=Join-Path $folder 'profile';New-Item -ItemType Directory -Force $profile | Out-Null
  if($case -eq 'breach'){'{"FlightAssist":false}' | Set-Content -LiteralPath (Join-Path $profile 'settings.json')}
  $arguments=@('-screen-fullscreen','0','-screen-width','1366','-screen-height','768','--preview','--data',('"'+$profile+'"'),'--mode',[string]$mode,'--scenario',$scenario,'--interaction-report',('"'+$statePath+'"'),'--seconds','18','--screenshot',('"'+$capture+'"'),'-logFile',('"'+$log+'"'))
  if($case -eq 'dinosaur-camera'){$arguments+=@('--camera-screenshots',('"'+$folder+'"'))}
  if($case -eq 'dinosaur-motion'){$arguments+=@('--motion-screenshots',('"'+$folder+'"'))}
  if(!$Sound){$arguments+='--mute'}
  if($case -in @('breach','steering','ocean-visitor','bird-companion')){$arguments+=@('--event-screenshot',('"'+(Join-Path $folder 'action.png')+'"'))}
  if($case -eq 'breach'){$arguments+=@('--entry-screenshot',('"'+(Join-Path $folder 'entry.png')+'"'))}
  if($case -eq 'hop-ten'){$arguments+=@('--math-level','6','--math-a','8','--math-b','2','--math-operation','add')}
  $process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Normal -PassThru
  $deadline=[DateTime]::UtcNow.AddSeconds(55);$focused=$false;$acted=$false;$success=$false;$task=$null;$maxY=-1000;$released=$false;$heldUntil=0;$evidence=@{}
  try{
   while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){
    if(!$focused){try{$focused=[UnityPreviewWindow]::Focus($process.Id)}catch{}}
    $state=$null;if(Test-Path -LiteralPath $statePath){try{$state=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json}catch{}}
    if($state -and $focused -and ($null -eq $state.time -or [double]$state.time -ge .5)){
     switch($case){
      'pearl-paint'{
       if($state.CanAnswer -and !$acted -and $state.Balls.Count){
        if(!$state.Bonus){throw 'Expected a real earned bonus round.'}
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        [UnityPreviewWindow]::Click($process.Id,[int]$state.Balls[0].X,[int]$state.Balls[0].Y,$false);$acted=$true
       }
       if($acted -and $state.PaintedCount -eq 1 -and $state.CanAnswer -and !$evidence.painted){
        $evidence.painted=$state
        $target=$state.Targets | Where-Object Value -eq $state.Answer | Select-Object -First 1
        Start-Sleep -Milliseconds 200
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        [UnityPreviewWindow]::Click($process.Id,[int]$target.X,[int]$target.Y,$false)
       }
       if($state.Correct -and $state.Points -eq 80){$success=$true}
      }
      'hop-ten'{
       if($state.CanAnswer -and !$acted){
        if($state.Answer -ne 10){throw 'Cannon Hop fixture did not produce ten.'}
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        if($state.HopSelection -lt 10){
         [UnityPreviewWindow]::HoldGameKey($process.Id,39,100)
         Start-Sleep -Milliseconds 350
        }else{
         $evidence.selectedTen=$state
         [UnityPreviewWindow]::HoldGameKey($process.Id,13,100);$acted=$true
        }
       }
       if($state.Correct){$success=$true}
      }
      'bubble'{
       if(!$acted){
        $target=$state.pickupTargets | Where-Object visible | Select-Object -First 1
        if($target){
         if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
         [UnityPreviewWindow]::Click($process.Id,[int]$target.x,[int]$target.y,$false);$acted=$true
        }
       }
       if($acted -and $state.bubbles -gt 0 -and $state.bonusScore -ge 5){$success=$true}
      }
      'treasure'{if($state.treasures -gt 0 -and $state.vehicleIndex -gt 0 -and $state.bonusScore -ge 25){$success=$true}}
      'obstacle'{if($state.obstacles -gt 0 -and $state.obstacleRemaining -gt 0){$evidence.hit=$state};if($evidence.hit -and $state.obstacleRemaining -eq 0 -and $state.speed -gt $evidence.hit.speed){$success=$true}}
      'steering'{
       if(!$acted){
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        $task=[UnityPreviewWindow]::HoldGameKeyAsync($process.Id,39,1400);$acted=$true;$evidence.before=$state
       }
       if($state.rightHeld -and [Math]::Abs($state.wheelSteering) -gt 15 -and $state.frontWheels -ge 2 -and [Math]::Abs($state.heading-$evidence.before.heading) -gt .05){$success=$true}
      }
      'dinosaur-roar'{
       if(!$acted){
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        [UnityPreviewWindow]::HoldGameKey($process.Id,162,100);$acted=$true
       }
       if($state.roarActive -and $state.roars -gt 0){$evidence.roar=$state}
       if($evidence.roar -and $state.footfalls -gt 2 -and $state.bodyVisible -and $state.pterosaurs -gt 0){$success=$true}
      }
      'dinosaur-motion'{
       if(!$acted){
        if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
        $task=[UnityPreviewWindow]::HoldGameKeyAsync($process.Id,32,1600);$acted=$true
       }
       if($state.heroMotion -eq 'Run' -and $state.speed -gt 17){$evidence.run=$state}
       if($evidence.run -and !$evidence.braking -and $task.IsCompleted){
        $task.GetAwaiter().GetResult();$task=[UnityPreviewWindow]::HoldGameKeyAsync($process.Id,40,1900);$evidence.braking=$true
       }
       if($evidence.braking -and $state.heroMotion -eq 'Idle' -and $state.speed -lt .7){$evidence.idle=$state}
       if($evidence.idle -and !$evidence.roaring -and $task.IsCompleted){
        $task.GetAwaiter().GetResult();[UnityPreviewWindow]::HoldGameKey($process.Id,162,100);$evidence.roaring=$true
       }
       if($evidence.roaring -and $state.heroMotion -eq 'Roar' -and $state.heroGait -eq 'Walk' -and $state.roarActive){$success=$true}
      }
      'dinosaur-camera'{
       if($state.cameraHeight -lt 3.5){throw 'Camera entered terrain.'}
       if(!$evidence.close -and $state.cameraView -eq 'Close' -and $state.bodyVisible -and $state.time -gt 1){
        $evidence.close=$state;[UnityPreviewWindow]::HoldGameKey($process.Id,112,900)
       }elseif($evidence.close -and !$evidence.far -and $state.cameraView -eq 'Far' -and $state.bodyVisible -and $state.time -gt 3){
        $evidence.far=$state;[UnityPreviewWindow]::HoldGameKey($process.Id,112,100)
       }elseif($evidence.far -and !$evidence.first -and $state.firstPerson -and !$state.bodyVisible -and $state.time -gt 5){
        $evidence.first=$state;[UnityPreviewWindow]::HoldGameKey($process.Id,112,100)
       }elseif($evidence.first -and $state.cameraView -eq 'Close' -and $state.bodyVisible -and $state.time -gt 7){$success=$true}
      }
      'bird-companion'{if($state.companionActive -and $state.companionVisits -eq 1){$success=$true}}
      'ocean-visitor'{
       if($state.sharkWarning){$evidence.warning=$state}
       if($evidence.warning -and $state.sharkActive -and !$state.sharkWarning -and $state.sharkVisits -eq 1 -and $state.predatorScatters -gt 0){$success=$true}
      }
      'breach'{
       $maxY=[Math]::Max($maxY,[double]$state.y)
       if($state.surfaceVisible -and $state.y -gt 0){$evidence.above=$state}
       if(!$task -or $task.IsCompleted){
        if($task){$task.GetAwaiter().GetResult();$task=$null}
        if(!$evidence.above -and $state.time -lt 13){
         if(![UnityPreviewWindow]::Focus($process.Id)){throw 'Owned player not foreground.'}
         $task=[UnityPreviewWindow]::HoldGameKeyAsync($process.Id,40,1800)
        }
       }
       if($evidence.above -and $state.y -lt -5 -and !$state.surfaceVisible -and $state.surfaceExits -gt 0 -and $state.surfaceEntries -gt 0){$success=$true}
      }
     }
     if($success -and !$evidence.accepted){$evidence.accepted=$state}
    }
    Start-Sleep -Milliseconds 75
   }
   if($task){$task.GetAwaiter().GetResult()}
   if(!$process.HasExited){$process.Kill();throw 'Immersion acceptance preview timed out.'}
   if($process.ExitCode -ne 0){throw "Player exited $($process.ExitCode)"}
   if(!$success){throw "No verified $case result (max height $maxY)."}
   if($case -eq 'dinosaur-motion'){foreach($motion in @('Idle','Walk','Run','Roar')){if(!(Test-Path -LiteralPath (Join-Path $folder ('motion-'+$motion+'.png')))){throw 'Motion screenshot missing.'}}}
   if($case -eq 'breach' -and !(Test-Path -LiteralPath (Join-Path $folder 'entry.png'))){throw 'Re-entry splash screenshot missing.'}
   if($case -eq 'dinosaur-camera'){foreach($view in 0..2){if(!(Test-Path -LiteralPath (Join-Path $folder ('camera-'+$view+'.png')))){throw 'Camera screenshot missing.'}}}
   $report=Get-Content -LiteralPath ($capture+'.json') -Raw | ConvertFrom-Json
   if($report.error -or $report.runtimeErrors){throw 'Player reported runtime errors.'}
   if($case -in @('breach','steering','ocean-visitor','bird-companion') -and !(Test-Path -LiteralPath (Join-Path $folder 'action.png'))){throw 'Action screenshot missing.'}
   if($Sound -and !$report.audio.SoundEnabled){throw 'Expected sound-enabled acceptance.'}
   if($Sound -and $case -eq 'pearl-paint' -and $report.audio.MusicFrames -le 0){throw 'Bonus music never reached the Unity mixer.'}
   if($Sound -and $case -eq 'hop-ten' -and $report.audio.PreparedStarted -le 0){throw 'Prepared narration did not play.'}
   $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $folder 'accepted.json')
   $results.Add([pscustomobject]@{case=$case;passed=$true;evidence=$folder})
   Write-Output "PASS $case"
  }catch{$results.Add([pscustomobject]@{case=$case;passed=$false;error=$_.Exception.Message;evidence=$folder});Write-Output "FAIL $case $($_.Exception.Message)"}
  finally{$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $folder 'observed.json');if(!$process.HasExited){$process.Kill()};$process.Dispose()}
 }
}finally{
 $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'results.json')
 foreach($name in $before.Keys){$path=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($before[$name] -ne $after){throw "Parent save changed: $name"}}
}
if(@($results | Where-Object {!$_.passed}).Count){throw 'An immersion acceptance check failed.'}
