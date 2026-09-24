param(
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='3.1.0',
 [int]$VersionCode=1,
 [string]$Voices='kyutai-Blake',
 [string]$UnityCli='unity'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 & "$PSScriptRoot/build-unity-domain.ps1"
 & python "$PSScriptRoot/stage-unity-android.py" prepare --voices $Voices
 if($LASTEXITCODE -ne 0){throw 'Android staging failed'}
 try {
  & $UnityCli build (Join-Path $root 'UnityPort') --target Android --execute-method KeyLearner.Unity.Editor.AndroidBuild.Build --timeout 3600 --args "-keyLearnerVersion $Version -keyLearnerVersionCode $VersionCode -disableManagedDebugger"
  if($LASTEXITCODE -ne 0){throw 'Android build failed'}
 } finally {
  & python "$PSScriptRoot/stage-unity-android.py" restore
  if($LASTEXITCODE -ne 0){throw 'Android staging restore failed; restore it before any Windows build'}
 }
} finally { Pop-Location }
