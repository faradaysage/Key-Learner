param([Parameter(Mandatory)][string]$Installer,[string]$UpgradeInstaller='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$registry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D5C654D0-1B14-4479-B771-20BD264731A8}_is1'
if(Test-Path $registry){throw 'An existing KeyLearner installation is registered; refusing to replace it for a smoke test.'}
$destination=Join-Path $root 'artifacts/installed-smoke'
$expected=[IO.Path]::GetFullPath($destination)
if(!$expected.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar)){throw 'Unexpected test installation directory'}
$profile=Join-Path $env:LOCALAPPDATA 'KeyLearner/settings.json'
$profileHash=if(Test-Path $profile){(Get-FileHash $profile).Hash}else{''}
function Install-TestPackage([string]$file) {
 $process=Start-Process -FilePath (Resolve-Path $file).Path -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=',('/DIR="'+$destination+'"')) -WindowStyle Hidden -PassThru
 if(!$process.WaitForExit(60000) -or $process.ExitCode -ne 0){throw 'Installer smoke test failed'}
 $entry=Get-ItemProperty $registry
 if($entry.InstallLocation.TrimEnd('\') -ne $destination){throw 'Installer changed the destination'}
 if(!(Test-Path (Join-Path $destination 'KeyLearner.exe'))){throw 'Game was not installed'}
 return $entry.DisplayVersion
}
try {
 $first=Install-TestPackage $Installer
 $second=Install-TestPackage $(if($UpgradeInstaller){$UpgradeInstaller}else{$Installer})
 if(@(Get-ChildItem (Split-Path $registry) | Where-Object {$_.PSChildName -eq (Split-Path $registry -Leaf)}).Count -ne 1){throw 'Duplicate installation identity'}
 if($profileHash -and (Get-FileHash $profile).Hash -ne $profileHash){throw 'Upgrade modified the parent profile'}
 "PASS install $first -> ${second}: one registration, same directory, parent settings preserved."
} finally {
 if(Test-Path $registry){
  $entry=Get-ItemProperty $registry
  if($entry.InstallLocation.TrimEnd('\') -eq $destination){
   $uninstaller=Join-Path $destination 'unins000.exe'
   $process=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru
   if(!$process.WaitForExit(60000) -or $process.ExitCode -ne 0){throw 'Test uninstall failed'}
  }
 }
}
