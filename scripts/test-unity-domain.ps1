param([string]$UnityEditor='')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(!$UnityEditor){
    $version=(Get-Content -LiteralPath (Join-Path $repoRoot 'UnityPort/ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value
    $editorRoots=@("$env:ProgramFiles/Unity/Hub/Editor", "$env:ProgramFiles/Unity")
    $UnityEditor=$editorRoots | ForEach-Object {Join-Path $_ "$version/Editor"} | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'Unity.exe')} | Select-Object -First 1
}
if(!$UnityEditor){throw 'Unity Editor was not found for this project version. Pass -UnityEditor with its discovered Editor directory or Unity.exe path.'}
$UnityEditor=(Resolve-Path -LiteralPath $UnityEditor).Path
if(Test-Path -LiteralPath $UnityEditor -PathType Leaf){$UnityEditor=Split-Path $UnityEditor -Parent}
if(!(Test-Path -LiteralPath (Join-Path $UnityEditor 'Unity.exe'))){throw "The selected directory does not contain Unity.exe: $UnityEditor"}
$editorData=Join-Path $UnityEditor 'Data'
$mono=Join-Path $editorData 'MonoBleedingEdge/bin/mono.exe'
$bcl=Join-Path $editorData 'BCLExtensions/runtime/netstandard2.1'
if(-not (Test-Path -LiteralPath $mono)){throw "No bundled Windows Mono runtime found at $mono"}
if(-not (Test-Path -LiteralPath (Join-Path $bcl 'System.Text.Json.dll'))){throw 'This Unity editor does not supply the required .NET Standard 2.1 JSON BCL extension. Re-evaluate plugin packaging before building.'}
& dotnet build (Join-Path $repoRoot 'Shared/Tests/MonoSmoke/KeyLearner.Domain.MonoSmoke.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Runtime probe compilation failed.'}
$testFolder=Join-Path $repoRoot 'artifacts/unity-migration/mono-domain'
New-Item -ItemType Directory -Force $testFolder | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'Shared/Tests/MonoSmoke/bin/Release/netstandard2.1/KeyLearner.Domain.MonoSmoke.dll') -Destination $testFolder -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'Shared/bin/Release/netstandard2.1/KeyLearner.Domain.dll') -Destination $testFolder -Force
$priorMonoPath=$env:MONO_PATH
try {
    # Deliberately load only Unity's runtime BCL dependency closure, not the NuGet
    # copies, so this verifies the same runtime/API identities as the player.
    $env:MONO_PATH="$bcl;$editorData/MonoBleedingEdge/lib/mono/unityjit-win32;$editorData/MonoBleedingEdge/lib/mono/unityjit-win32/Facades"
    & $mono (Join-Path $testFolder 'KeyLearner.Domain.MonoSmoke.dll') (Join-Path $testFolder ('profile-'+[guid]::NewGuid().ToString('N'))) | Tee-Object -FilePath (Join-Path $testFolder 'runtime-smoke.log')
    if($LASTEXITCODE -ne 0){throw 'Unity Mono runtime verification failed.'}
} finally { $env:MONO_PATH=$priorMonoPath }
