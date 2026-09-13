param(
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='3.0.0',
 [string]$WindowsBuild='',
 [string]$Compiler=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WindowsBuild){$WindowsBuild=Join-Path $root 'artifacts/unity-windows'}
$WindowsBuild=[IO.Path]::GetFullPath($WindowsBuild)
if(!$Compiler){$Compiler=@((Join-Path $root '.local/installer-tools/compiler/ISCC.exe'),"${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe","$env:ProgramFiles\Inno Setup 7\ISCC.exe") | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1}
if(!$Compiler){throw 'Inno Setup compiler not found. Pass -Compiler <path-to-ISCC.exe>.'}
foreach($dependency in @('KeyLearner.exe','LICENSE','THIRD_PARTY_ASSETS.md','UnityPlayer.dll','KeyLearner_Data/globalgamemanagers','KeyLearner_Data/StreamingAssets/Platform/KeyLearner.PlatformHelper.exe','KeyLearner_Data/StreamingAssets/Content/Voice/a.wav','KeyLearner_Data/StreamingAssets/Content/Sounds/ATTRIBUTION.md','KeyLearner_Data/StreamingAssets/Platform/Licenses/dotnet-runtime-LICENSE.TXT','KeyLearner_Data/StreamingAssets/Platform/Licenses/dotnet-runtime-THIRD-PARTY-NOTICES.TXT','KeyLearner_Data/StreamingAssets/Platform/Licenses/system-speech-LICENSE.TXT','KeyLearner_Data/StreamingAssets/Platform/Licenses/system-speech-THIRD-PARTY-NOTICES.TXT')){
 if(!(Test-Path -LiteralPath (Join-Path $WindowsBuild $dependency))){throw "Missing Windows build dependency: $dependency. Build using KeyLearner.Unity.Editor.ProjectSetup.BuildWindows first."}
}
if(Get-ChildItem -LiteralPath $WindowsBuild -Recurse -File -Filter private_dictionary.csv){throw 'Private dictionary must never be packaged.'}
if(Get-ChildItem -LiteralPath $WindowsBuild -Recurse -Directory | Where-Object {$_.Name -in @('.local','voice-cache')}){throw 'Local tools and profile voice caches must not be packaged.'}
$output=Join-Path $root 'artifacts/installer'
New-Item -ItemType Directory -Force $output | Out-Null
& $Compiler "/DUnityPort=1" "/DAppVersion=$Version" "/DPublishDir=$WindowsBuild" "/DInstallerDir=$output" (Join-Path $root 'installer/KeyLearner.iss')
if($LASTEXITCODE -ne 0){throw 'Unity installer compilation failed.'}
$setup=Join-Path $output "KeyLearner-$Version-win-x64-setup.exe"
$hash=Get-FileHash -LiteralPath $setup -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($setup))" | Set-Content -LiteralPath "$setup.sha256" -Encoding ascii
Write-Output "Unity installer: $setup"
