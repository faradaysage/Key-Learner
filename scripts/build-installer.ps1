param(
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='2.0.1',
 [string]$Compiler=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Compiler){
 $candidates=@((Join-Path $root '.local/installer-tools/compiler/ISCC.exe'),"${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe","$env:ProgramFiles\Inno Setup 7\ISCC.exe")
 $Compiler=$candidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
}
if(!$Compiler){throw 'Inno Setup compiler not found. Pass -Compiler <path-to-ISCC.exe>.'}
$publish=Join-Path $root "artifacts/publish-$Version"
$output=Join-Path $root 'artifacts/installer'
Push-Location $root
try {
 $runtime=(Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json').'latest-runtime'
 if($runtime -notmatch '^8\.0\.\d+$'){throw 'Unexpected .NET runtime version metadata'}
 dotnet publish KeyLearner.csproj -c Release -r win-x64 --self-contained true -o $publish "-p:Version=$Version" "-p:FileVersion=$Version.0" -p:PublishSingleFile=false "-p:RuntimeFrameworkVersion=$runtime"
 if($LASTEXITCODE -ne 0){throw 'Publish failed'}
 if(Test-Path (Join-Path $publish 'data/private_dictionary.csv')){throw 'Private dictionary must never be packaged.'}
 foreach($required in @('KeyLearner.exe','coreclr.dll','Content/Branding/KeyLearner.ico','Content/Voice/a.wav','Content/Fonts/StudioRounded.xnb','Content/Effects/fire-atlas.png')){
  if(!(Test-Path (Join-Path $publish $required))){throw "Missing packaged dependency: $required"}
 }
 & $Compiler "/DAppVersion=$Version" "/DPublishDir=$publish" "/DInstallerDir=$output" installer/KeyLearner.iss
 if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
 $setup=Join-Path $output "KeyLearner-$Version-win-x64-setup.exe"
 $hash=Get-FileHash $setup -Algorithm SHA256
 "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($setup))" | Set-Content "$setup.sha256" -Encoding ascii
 Write-Output "Installer: $setup"
} finally {Pop-Location}
