param([string]$UnityEditor='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$UnityEditor){
 $version=(Get-Content -LiteralPath (Join-Path $root 'UnityPort/ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value
 $roots=@("$env:ProgramFiles/Unity/Hub/Editor", "$env:ProgramFiles/Unity")
 $UnityEditor=$roots | ForEach-Object {Join-Path $_ "$version/Editor"} | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'Unity.exe')} | Select-Object -First 1
}
if(!$UnityEditor){throw 'Unity Editor was not found for this project version. Pass -UnityEditor with its discovered Editor directory.'}
if(Test-Path -LiteralPath $UnityEditor -PathType Leaf){$UnityEditor=Split-Path $UnityEditor -Parent}
& dotnet build (Join-Path $root 'tools/KeyLearner.PlatformChecks/KeyLearner.PlatformChecks.csproj') -c Release --nologo "-p:UnityEditor=$UnityEditor"
if($LASTEXITCODE -ne 0){throw 'Unity script compilation smoke failed.'}
Write-Output 'PASS: all KeyLearner Unity runtime/Editor scripts compile with C# 9 against this Editor and imported URP references.'
