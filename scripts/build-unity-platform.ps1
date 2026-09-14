param([string]$RuntimeVersion='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'UnityPort/Assets/StreamingAssets/Platform'
if(!$RuntimeVersion){
 $RuntimeVersion=dotnet --list-runtimes | Select-String '^Microsoft.NETCore.App (8\.0\.\d+)' | ForEach-Object {$_.Matches.Groups[1].Value} | Sort-Object {[version]$_} -Descending | Select-Object -First 1
}
if(!$RuntimeVersion){throw 'Install a .NET 8 SDK/runtime, or supply -RuntimeVersion with an available .NET 8 runtime patch.'}
dotnet publish (Join-Path $root 'tools/KeyLearner.PlatformHelper/KeyLearner.PlatformHelper.csproj') -c Release -r win-x64 --self-contained true -o $output "-p:RuntimeFrameworkVersion=$RuntimeVersion" -p:PublishSingleFile=false
if($LASTEXITCODE -ne 0){throw 'Windows platform helper publish failed.'}
# dotnet publish does not copy the redistributable packages' license/notice text.
$assets=Get-Content -LiteralPath (Join-Path $root 'tools/KeyLearner.PlatformHelper/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoots=@($assets.packageFolders.PSObject.Properties.Name)
$speechVersion=($assets.libraries.PSObject.Properties.Name | Where-Object {$_ -like "System.Speech/*"} | Select-Object -First 1).Split("/")[1]
$licenseFolder=Join-Path $output 'Licenses';New-Item -ItemType Directory -Force $licenseFolder | Out-Null
foreach($package in @(@{Id='microsoft.netcore.app.runtime.win-x64';Version=$RuntimeVersion;Name='dotnet-runtime'},@{Id='system.speech';Version=$speechVersion;Name='system-speech'})){
 foreach($file in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')){
  $relative=$package.Id+'/'+$package.Version+'/'+$file
  $sourceNotice=$packageRoots | ForEach-Object {Join-Path $_ $relative} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
  if(!$sourceNotice){throw "Redistributed package notice missing: $relative"}
  Copy-Item -LiteralPath $sourceNotice -Destination (Join-Path $licenseFolder ($package.Name+'-'+$file)) -Force
 }
}
$sourceContent=Join-Path $root 'Content'
foreach($folder in @('Voice','Sounds','Branding','Icons')){
 $destination=Join-Path $root "UnityPort/Assets/StreamingAssets/Content/$folder"
 New-Item -ItemType Directory -Force $destination | Out-Null
 $sourceFolder=Join-Path $sourceContent $folder
 if($folder -eq 'Voice'){
  $stagedPacks=[IO.Path]::GetFullPath((Join-Path $destination 'Packs'))
  $allowed=[IO.Path]::GetFullPath((Join-Path $root 'UnityPort/Assets/StreamingAssets/Content/Voice'))+[IO.Path]::DirectorySeparatorChar
  if(!$stagedPacks.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe staged voice-pack path'}
  if(Test-Path -LiteralPath $stagedPacks){
   $voiceItems=@(Get-Item -LiteralPath $destination)+@(Get-Item -LiteralPath $stagedPacks)+@(Get-ChildItem -LiteralPath $stagedPacks -Recurse -Force)
   if($voiceItems | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Refusing to clean voice staging through reparse points'}
   Remove-Item -LiteralPath $stagedPacks -Recurse -Force
  }
  $stagedManifest=Join-Path $destination 'voice-packs.json'
  if(!(Test-Path -LiteralPath (Join-Path $sourceFolder 'voice-packs.json')) -and (Test-Path -LiteralPath $stagedManifest)){Remove-Item -LiteralPath $stagedManifest -Force}
 }
 foreach($sourceFile in Get-ChildItem -LiteralPath $sourceFolder -Recurse -File | Where-Object {$_.Extension -notin @('.xnb','.meta')}){
  $relative=$sourceFile.FullName.Substring($sourceFolder.Length).TrimStart('\','/')
  $target=Join-Path $destination $relative
  New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
  Copy-Item -LiteralPath $sourceFile.FullName -Destination $target -Force
 }
}
Write-Output "Windows speech/accessibility helper and licensed offline audio staged at $output"
