param([string]$Executable='', [string]$Output='', [switch]$SkipNativeFocus, [switch]$VerifyVoices, [string]$ExpectedVoice="", [string]$FinishVoice="")
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-ux/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner'
$before=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name
 $before[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
if(!('UnityPreviewWindow' -as [type])){Add-Type -Path (Join-Path $root 'tools/UnityPreviewWindow.cs')}
$profile=Join-Path $Output 'profile'
$log=Join-Path $Output 'player.log'
$statePath=Join-Path $Output 'ux-state.json'
$arguments=@('-screen-fullscreen','0','-screen-width','1366','-screen-height','768','--preview','--mute','--data',('"'+$profile+'"'),'--ux-verify',('"'+$Output+'"'),'-logFile',('"'+$log+'"'))
if(!$SkipNativeFocus){$arguments+='--native-focus'}
if($VerifyVoices){$arguments=$arguments | Where-Object {$_ -ne '--mute'};$arguments+='--voice-verify'}
if($ExpectedVoice){$arguments+=@('--expect-voice',$ExpectedVoice)}
if($FinishVoice){$arguments+=@('--finish-voice',$FinishVoice)}
$process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Normal -PassThru
$deadline=[DateTime]::UtcNow.AddSeconds($(if($VerifyVoices){180}else{100}))
$minimized=$false;$restored=$false;$focused=$false
try{
 while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){
  if(!$SkipNativeFocus -and !$focused){try{$focused=[UnityPreviewWindow]::Focus($process.Id)}catch{}}
  if(Test-Path -LiteralPath $statePath){
   try{$state=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json}catch{$state=$null}
   if($state -and !$SkipNativeFocus){
    if($state.phase -eq 'await-native-focus-loss' -and !$minimized){[UnityPreviewWindow]::Minimize($process.Id);$minimized=$true}
    if($state.phase -eq 'await-native-focus-return' -and !$restored){[UnityPreviewWindow]::Restore($process.Id);$restored=$true}
   }
  }
  Start-Sleep -Milliseconds 120
 }
 if(!$process.HasExited){$process.Kill();throw 'UX preview exceeded its bounded deadline.'}
 if(!(Test-Path -LiteralPath $statePath)){throw 'UX state report was not written. Inspect player.log.'}
 $state=[UnityPreviewWindow]::ReadSharedReport($statePath) | ConvertFrom-Json
 if($state.phase -ne 'complete'){throw ('UX verification failed: '+$state.error+'; '+$state.phase)}
 if($process.ExitCode -ne 0){throw "Player exit code $($process.ExitCode)"}
 $logText=Get-Content -LiteralPath $log -Raw
 if($logText -match '(?m)^(?:[\w.]*Exception:|Shader error|Could not load file or assembly)'){throw 'Player log contains a runtime/graphics error.'}
 $count=@(Get-ChildItem -LiteralPath $Output -Filter '*.png').Count
 if($count -lt 13){throw "Expected all picker/parent/credit screenshots; found $count."}
 Write-Output "PASS: $($state.passed.Count) UX assertions; $count actual player screenshots. Evidence: $Output"
 if(!$SkipNativeFocus){Write-Output 'PASS: own player minimized/restored and native input ownership transitions canceled its unfinished parent edit.'}
}finally{
 if(!$process.HasExited){$process.Kill()};$process.Dispose()
 foreach($name in $before.Keys){
  $path=Join-Path $parentRoot $name
  $after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
  if($before[$name] -ne $after){throw "The real parent save changed: $name"}
 }
}
