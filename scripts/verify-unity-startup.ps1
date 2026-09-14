param([string]$Executable='', [string]$Output='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root 'artifacts/unity-windows/KeyLearner.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(!$Output){$Output=Join-Path $root ('artifacts/unity-startup/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $Output | Out-Null
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner';$before=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $file=Join-Path $parentRoot $name;$before[$name]=if(Test-Path -LiteralPath $file){(Get-FileHash -LiteralPath $file).Hash}else{''}
}
$profile=Join-Path $Output 'profile';$splash=Join-Path $Output 'splash.png';$picker=Join-Path $Output 'picker.png'
$arguments=@('-screen-fullscreen','0','--preview','--show-intro','--mute','--data',('"'+$profile+'"'),'--seconds','3','--intro-screenshot',('"'+$splash+'"'),'--screenshot',('"'+$picker+'"'),'-logFile',('"'+(Join-Path $Output 'player.log')+'"'))
$process=Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Maximized -PassThru
try{
 if(!$process.WaitForExit(45000)){throw 'Startup verification exceeded 45 seconds.'}
 if($process.ExitCode -ne 0){throw "Installed player exit code $($process.ExitCode)"}
 foreach($file in @($splash,($splash+'.json'),$picker,($picker+'.json'))){if(!(Test-Path -LiteralPath $file)){throw "Missing capture: $file"}}
 $intro=Get-Content -LiteralPath ($splash+'.json') -Raw | ConvertFrom-Json
 $game=Get-Content -LiteralPath ($picker+'.json') -Raw | ConvertFrom-Json
 if(!$intro.unitySplashFinished -or !$intro.introductionActive -or $intro.progress -lt .35 -or $intro.progress -ge 1 -or !$intro.version -or !$intro.buildGuid){throw 'Introduction did not show a running-player identity after the Unity splash.'}
 if(!$game.picker -or $game.error -or $game.runtimeErrors -or $game.activeSeconds -lt 3){throw 'The introduction did not automatically reach a healthy picker.'}
 $files=@(Get-ChildItem -LiteralPath (Join-Path $profile 'diagnostics') -Filter 'input-session-*.jsonl')
 if($files.Count -ne 1){throw 'Expected one isolated diagnostic session.'}
 $records=@(Get-Content -LiteralPath $files[0].FullName | ForEach-Object {$_ | ConvertFrom-Json})
 $session=@($records | Where-Object kind -eq session)[0]
 if($session.data.version -ne $intro.version -or $session.data.buildGuid -ne $intro.buildGuid){throw 'Screenshot and diagnostic identities disagree.'}
 $beats=@($records | Where-Object kind -eq heartbeat)
 if($beats.Count -lt 3 -or !($beats.data.view -contains 'splash') -or !($beats.data.view -contains 'picker')){throw 'Diagnostics did not continue across the introduction and picker.'}
 if(!($records.kind -contains 'splash-finished') -or !($records.kind -contains 'disposed')){throw 'Missing transition or cleanup evidence.'}
 [pscustomobject]@{passed=$true;version=$intro.version;buildGuid=$intro.buildGuid;heartbeatCount=$beats.Count;splashAfterUnity=$true;automaticPicker=$true;inputWasNotRequired=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'result.json')
 Write-Output "PASS running-player splash/version, automatic picker, flushed diagnostics and normal exit. Evidence: $Output"
}finally{
 if(!$process.HasExited){$process.Kill();[void]$process.WaitForExit(5000)};$process.Dispose()
 foreach($name in $before.Keys){$file=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $file){(Get-FileHash -LiteralPath $file).Hash}else{''};if($before[$name] -ne $after){throw "Real parent file changed: $name"}}
}
