param([string]$Installer='', [string]$UpgradeInstaller='', [string]$Output='', [switch]$PreflightOnly, [switch]$BaselineIsMonoGame)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Output){$Output=Join-Path $root ('artifacts/unity-installer-test/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $Output | Out-Null
$identity='{D5C654D0-1B14-4479-B771-20BD264731A8}_is1'
$registry='HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/'+$identity
$keys=@($registry,('HKLM:/Software/Microsoft/Windows/CurrentVersion/Uninstall/'+$identity),('HKLM:/Software/WOW6432Node/Microsoft/Windows/CurrentVersion/Uninstall/'+$identity))
$existing=@($keys | Where-Object {Test-Path -LiteralPath $_})
$defaultInstall=Join-Path $env:LOCALAPPDATA 'Programs/KeyLearner'
$defaultMenu=Join-Path $env:APPDATA 'Microsoft/Windows/Start Menu/Programs/KeyLearner'
$reason=if($existing.Count){'An existing KeyLearner registration must be preserved.'}elseif(Test-Path -LiteralPath $defaultInstall){'An existing default application directory must be preserved.'}elseif(Test-Path -LiteralPath $defaultMenu){'Existing KeyLearner Start menu content must be preserved.'}else{''}
$preflight=[pscustomobject]@{eligible=(!$reason);reason=$reason;registrations=$existing;defaultInstallExists=(Test-Path -LiteralPath $defaultInstall);defaultStartMenuExists=(Test-Path -LiteralPath $defaultMenu);checkedAt=(Get-Date).ToUniversalTime().ToString('o')}
$preflight | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Output 'preflight.json')
if($reason){Write-Output "SKIPPED installation test: $reason Read-only evidence: $Output";return}
if($PreflightOnly){Write-Output "PASS read-only installer preflight; no existing installation or shortcuts. Evidence: $Output";return}
if(!$Installer){throw 'Pass the final release installer path, or use -PreflightOnly.'}
if($BaselineIsMonoGame -and !$UpgradeInstaller){throw '-BaselineIsMonoGame requires the distinct Unity -UpgradeInstaller.'}
$script:installerStillRunning=$false
$Installer=(Resolve-Path -LiteralPath $Installer).Path
if(!$UpgradeInstaller){$UpgradeInstaller=$Installer}else{$UpgradeInstaller=(Resolve-Path -LiteralPath $UpgradeInstaller).Path}
$destination=Join-Path $Output 'installed'
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if(!$destination.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Test installation must remain within this repository artifacts directory.'}
if(Test-Path -LiteralPath $destination){throw 'Test installation directory already exists; choose a new output directory.'}
function Assert-ScopedInstallation([string]$target){
 $resolved=[IO.Path]::GetFullPath($target).TrimEnd('\')
 if(!$resolved.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation target escaped the repository artifact directory.'}
 $ancestor=$resolved
 while($true){
  if(Test-Path -LiteralPath $ancestor){$entry=Get-Item -LiteralPath $ancestor -Force;if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing an installer test through a junction or symbolic link.'}}
  if($ancestor -eq $artifactRoot.TrimEnd('\')){break}
  $ancestor=Split-Path $ancestor -Parent
  if(!$ancestor){throw 'Could not verify the installation ancestry.'}
 }
}
Assert-ScopedInstallation $destination
$group='KeyLearner QA '+[Guid]::NewGuid().ToString('N')
$menu=Join-Path $env:APPDATA ('Microsoft/Windows/Start Menu/Programs/'+$group)
$allowedMenus=@($menu,$defaultMenu)
$parentRoot=Join-Path $env:LOCALAPPDATA 'KeyLearner';$before=@{}
foreach($name in @('settings.json','words.json','profile.json','gesture-training.json','math-progress.json')){
 $path=Join-Path $parentRoot $name;$before[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
}
function Install-Package([string]$file,[string]$label,[bool]$legacy=$false){
 $log=Join-Path $Output ($label+'.log')
 $arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOCLOSEAPPLICATIONS','/TASKS=',('/DIR="'+$destination+'"'),('/GROUP="'+$group+'"'),('/LOG="'+$log+'"'))
 $process=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru
 try{$script:installerStillRunning=$true;if(!$process.WaitForExit(300000)){throw 'Installer exceeded its 300-second deadline; cleanup is deferred until that process exits.'};$script:installerStillRunning=$false;if($process.ExitCode -ne 0){throw "Installer exit code $($process.ExitCode)"}}finally{$process.Dispose()}
 $entry=Get-ItemProperty -LiteralPath $registry
 if([IO.Path]::GetFullPath($entry.InstallLocation).TrimEnd('\') -ne $destination){throw 'Installer registered an unexpected directory.'}
 $dependencies=if($legacy){@('KeyLearner.exe','KeyLearner.dll')}else{@('KeyLearner.exe','UnityPlayer.dll','KeyLearner_Data/globalgamemanagers','KeyLearner_Data/StreamingAssets/Platform/KeyLearner.PlatformHelper.exe','KeyLearner_Data/StreamingAssets/Platform/Licenses/dotnet-runtime-LICENSE.TXT','KeyLearner_Data/StreamingAssets/Platform/Licenses/system-speech-LICENSE.TXT')}
 foreach($relative in $dependencies){
  if(!(Test-Path -LiteralPath (Join-Path $destination $relative))){throw "Installed payload missing: $relative"}
 }
 # The preserved installer disables its group page and may ignore /GROUP.
 # Both candidates were absent before testing; the shortcut target is checked below.
 $shortcut=@($allowedMenus | ForEach-Object {Join-Path $_ 'KeyLearner Parent Studio.lnk'} | Where-Object {Test-Path -LiteralPath $_}) | Select-Object -First 1
 if(!$shortcut){throw 'Parent Studio shortcut missing from the preflight-approved Start menu locations.'}
 $shell=New-Object -ComObject WScript.Shell
 try{$link=$shell.CreateShortcut($shortcut);if(!$legacy -and $link.Arguments -ne ('--preview --studio --data "'+$parentRoot+'"')){throw 'Parent shortcut does not explicitly select the real profile.'};if($link.TargetPath -ne (Join-Path $destination 'KeyLearner.exe')){throw 'Parent shortcut targets an unexpected executable.'}}
 finally{if($link){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
 return $entry.DisplayVersion
}
$first='';$second='';$failure='';$uninstalled=$false;$profilesUnchanged=$false;$obsoleteRemoved=0;$customPreserved=$false
try{
 $first=Install-Package $Installer 'install' $BaselineIsMonoGame
 $customSentinel=Join-Path $destination 'qa-custom-content.txt'
 [IO.File]::WriteAllText($customSentinel,'Isolated upgrade preservation check.')
 $legacyPaths=@()
 if($BaselineIsMonoGame){
  $legacyPaths=@(Get-Content -LiteralPath (Join-Path $root 'installer/LegacyMonoGameFiles.iss') | ForEach-Object {if($_ -match '^Type: files; Name: "\{app\}\\([^"*?]+)"$'){$matches[1]}} | Where-Object {Test-Path -LiteralPath (Join-Path $destination $_)})
  if(!$legacyPaths.Count){throw 'Baseline legacy payload did not match the audited migration manifest.'}
 }
 $second=Install-Package $UpgradeInstaller 'upgrade'
 if(Get-ChildItem -LiteralPath $destination -Recurse | Where-Object {$_.Name -match 'BackUpThisFolder_ButDontShipItWithYourGame|BurstDebugInformation_DoNotShip' -or $_.Extension -eq '.pdb'}){throw 'Unity installer included a build backup/debug artifact.'}
 foreach($relative in $legacyPaths){if(Test-Path -LiteralPath (Join-Path $destination $relative)){throw "Obsolete MonoGame payload remained after upgrade: $relative"};$obsoleteRemoved++}
 if((Get-Content -LiteralPath $customSentinel -Raw) -ne 'Isolated upgrade preservation check.'){throw 'Upgrade did not preserve an unlisted custom file.'}
 $customPreserved=$true
 Remove-Item -LiteralPath $customSentinel
 if(@($keys | Where-Object {Test-Path -LiteralPath $_}).Count -ne 1){throw 'Upgrade created more than one installation registration.'}
 Write-Output "PASS install/upgrade $first -> ${second}: one identity, same temporary directory, explicit real-profile Parent Studio shortcut."
}catch{$failure=$_.Exception.Message;throw}
finally{
 try{
  if($script:installerStillRunning){throw 'Refusing to uninstall while the installer is still running; inspect the bounded test log and process first.'}
  if(Test-Path -LiteralPath $registry){
   $entry=Get-ItemProperty -LiteralPath $registry
   $registered=[IO.Path]::GetFullPath($entry.InstallLocation).TrimEnd('\')
   if($registered -ne $destination -or !$registered.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to uninstall a registration outside the verified temporary directory.'}
   Assert-ScopedInstallation $registered
   $uninstaller=Join-Path $destination 'unins000.exe'
   $process=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $Output 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru
   try{if(!$process.WaitForExit(300000) -or $process.ExitCode -ne 0){throw 'Temporary uninstall failed.'}}finally{$process.Dispose()}
   if(Test-Path -LiteralPath $registry){throw 'Temporary uninstall left the app registration.'}
   foreach($menuPath in $allowedMenus){if(Test-Path -LiteralPath $menuPath){throw 'Temporary uninstall left a Start menu group created during this test.'}}
   $uninstalled=$true
  }
  foreach($name in $before.Keys){$path=Join-Path $parentRoot $name;$after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''};if($before[$name] -ne $after){throw "Installer test modified parent data: $name"}}
  $profilesUnchanged=$true
 }catch{if(!$failure){$failure=$_.Exception.Message};throw}
 finally{[pscustomobject]@{passed=(!$failure);installVersion=$first;upgradeVersion=$second;uninstalled=$uninstalled;error=$failure;profileFilesUnchanged=$profilesUnchanged;obsoleteFilesRemoved=$obsoleteRemoved;unlistedCustomFilePreserved=$customPreserved;destination=$destination} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'result.json')}
}
Write-Output "PASS temporary installation removed; all five real parent profile files unchanged. Evidence: $Output"


