param(
 [string]$Executable='',
 [string]$Output='',
 [int[]]$Modes=(0..11),
 [switch]$StaticOnly,
 [string[]]$SelectedCases=@(),
 [switch]$Sound,
 [ValidateRange(0,6500)][float]$Distance=0,
 [ValidateRange(0,120)][float]$CaptureSeconds=0
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-verification/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner'
$parentSnapshot=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $parentSnapshot[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{''}
}
$cases=[Collections.Generic.List[object]]::new()
foreach($mode in $Modes){$cases.Add([pscustomobject]@{Name=('mode-{0:D2}' -f $mode);Mode=$mode;Scenario='';Seconds=8;Expected='';Extra=@()})}
if(!$StaticOnly){
 foreach($spec in @(
  @('rapid-milk',0,3,'recognized=1'),
  @('icons',0,4,'glyphs=[1-9]'),
  @('balloon',0,3,'popped=[1-9]'),
  @('balloon-sequence',0,2,'glyphs=[3-9]'),
  @('cluster',0,3,'paint=[1-9]'),
  @('glass',0,5,'cracks=(?:[2-9]|[1-9]\d)|shattered=[1-9]'),
  @('swipe',0,3,'drops=[1-9]'),
  @('fire',0,4,''),
  @('counting',2,12,'count=25'),
  @('word-balloons',1,3,'balloons=3'),
  @('word-pop',1,6,'score=100'),
  @('guided-mommy',1,3,'completed=1'),
  @('patient-red',1,8,'completed=1'),
  @('cannon-miss',0,3,'blasts=0 shots=0')
 )){$cases.Add([pscustomobject]@{Name=$spec[0];Mode=[int]$spec[1];Scenario=$spec[0];Seconds=[int]$spec[2];Expected=$spec[3];Extra=@()})}
 foreach($scenario in @('dots-correct','dots-retry')){$cases.Add([pscustomobject]@{Name=$scenario;Mode=6;Scenario=$scenario;Seconds=10;Expected='stage=2';Extra=@()})}
 foreach($mode in (7..11)){
  $a=2;$b=1
  if($mode -eq 9){$a=0;$b=3}
  if($mode -eq 10){$a=1;$b=3}
  $cases.Add([pscustomobject]@{Name="math-complete-$mode";Mode=$mode;Scenario='math-complete';Seconds=14;Expected='stage=2';Extra=@('--math-level','1','--math-a',[string]$a,'--math-b',[string]$b,'--math-operation','add')})
 }
}
if($SelectedCases.Count){
 $selected=@($cases | Where-Object {$_.Name -in $SelectedCases})
 if($selected.Count -ne $SelectedCases.Count){throw 'One or more -SelectedCases names did not match an available case.'}
 $cases=$selected
}
if($CaptureSeconds -gt 0){
 if(!$SelectedCases.Count){throw '-CaptureSeconds requires explicit -SelectedCases.'}
 if($CaptureSeconds -lt 2){throw 'The Unity preview requires at least two active seconds.'}
 foreach($case in $cases){if($CaptureSeconds -lt $case.Seconds){$case.Expected=''};$case.Seconds=$CaptureSeconds}
}
if(!("UnityPreviewWindow" -as [type])){Add-Type -Path (Join-Path $root "tools/UnityPreviewWindow.cs")}
$results=[Collections.Generic.List[object]]::new()
foreach($case in $cases){
 $capture=Join-Path $Output ($case.Name+'.png')
 $log=Join-Path $Output ($case.Name+'.log')
 $profile=Join-Path $Output ('profiles/'+$case.Name)
 New-Item -ItemType Directory -Force $profile | Out-Null
 if($case.Name -eq 'balloon-sequence'){
  # This fixture checks separate key balloons. Its Q-to-Z path is also a valid
  # swipe, so isolate it from gesture effects in this disposable profile only.
  $settingsPath=Join-Path $profile 'settings.json'
  $settings=if(Test-Path -LiteralPath $settingsPath){Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json}else{[pscustomobject]@{}}
  $settings | Add-Member -MemberType NoteProperty -Name GestureEffects -Value $false -Force
  $settings | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath -Encoding utf8
 }
 # Explicit disposable profiles, even if Parent Studio arguments are added later.
 $arguments=@('-screen-fullscreen','0','-screen-width','1366','-screen-height','768','--preview','--data',('"'+$profile+'"'),'--mode',[string]$case.Mode,'--seconds',[string]$case.Seconds,'--screenshot',('"'+$capture+'"'),'-logFile',('"'+$log+'"'))
 if(!$Sound){$arguments+='--mute'}
 if($case.Scenario){$arguments+=@('--scenario',$case.Scenario)}
 $arguments+=$case.Extra
 if($Distance -gt 0 -and !$case.Scenario -and $case.Mode -in @(3,4,5)){$arguments+=@("--preview-distance",$Distance.ToString([Globalization.CultureInfo]::InvariantCulture))}
 $process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Normal -PassThru
 try{
  $deadline=[DateTime]::UtcNow.AddSeconds([Math]::Max(45,$case.Seconds+30));$focused=$false
  while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){
   if(!$focused){try{$focused=[UnityPreviewWindow]::Focus($process.Id)}catch{}}
   Start-Sleep -Milliseconds 100
  }
  if(!$process.HasExited){$process.Kill();throw 'Preview did not exit within the bounded capture window.'}
  if($process.ExitCode -ne 0){throw "Player exit code $($process.ExitCode)."}
  if(!(Test-Path -LiteralPath $capture) -or !(Test-Path -LiteralPath ($capture+'.json'))){throw 'Gameplay screenshot/diagnostic report missing.'}
  $report=Get-Content -LiteralPath ($capture+'.json') -Raw | ConvertFrom-Json
  if($report.error){throw "Player reported: $($report.error)"}
  if($report.runtimeErrors){throw "Unity runtime errors: $($report.runtimeErrors -join '; ')"}
  if($null -eq $report.activeSeconds -or [double]$report.activeSeconds -lt $case.Seconds-.1){throw "Preview ended before the requested active gameplay duration: $($report.activeSeconds)s"}
  if($report.mode -ne $case.Mode -or !$report.game){throw 'Requested minigame was not entered.'}
  if($case.Seconds -ge 3 -and $report.frameCount -lt 12){throw 'Too few gameplay frames; check foreground/display availability.'}
  $logText=Get-Content -LiteralPath $log -Raw
  if($logText -match '(?m)^(?:[\w.]*Exception:|Shader error|Could not load file or assembly)'){throw 'Player log contains a runtime/graphics error.'}
  if($case.Expected -and $report.game -notmatch $case.Expected){throw "Expected $($case.Expected), observed $($report.game)."}
  $results.Add([pscustomobject]@{case=$case.Name;passed=$true;diagnostic=$report.game;p95Milliseconds=$report.p95Milliseconds;screenshot=$capture;log=$log})
  Write-Output "PASS $($case.Name): $($report.game); p95=$([Math]::Round($report.p95Milliseconds,2))ms"
 }catch{
  $results.Add([pscustomobject]@{case=$case.Name;passed=$false;error=$_.Exception.Message;screenshot=$capture;log=$log})
  Write-Output "FAIL $($case.Name): $($_.Exception.Message)"
 }finally{$process.Dispose()}
}
foreach($name in $parentSnapshot.Keys){
 $path=Join-Path $parentRoot $name
 $after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{''}
 if($after -ne $parentSnapshot[$name]){throw "The real parent profile changed during verification: $name"}
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'results.json')
Write-Output "Evidence: $Output"
if(@($results | Where-Object {!$_.passed}).Count){throw 'One or more Unity gameplay previews failed. Inspect results.json and individual player logs.'}
Write-Output "PASS: $($results.Count) actual player captures; existing parent save files unchanged. Physical keyboard/touchpad containment is not exercised by this preview runner."









