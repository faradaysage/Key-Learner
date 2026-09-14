param(
    [string]$Configuration = 'Release',
    [string]$UnityProject,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $UnityProject) { $UnityProject = Join-Path $repoRoot 'UnityPort' }
$project = Join-Path $repoRoot 'Shared/KeyLearner.Domain.csproj'
& dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw 'Shared domain compilation failed.' }
if (-not $SkipTests) {
    $logFolder = Join-Path $repoRoot 'artifacts'
    New-Item -ItemType Directory -Force $logFolder | Out-Null
    $regressionLog = Join-Path $logFolder 'domain-regression.log'
    & dotnet run --project (Join-Path $repoRoot 'Shared/Tests/KeyLearner.Domain.Regression.csproj') -c $Configuration *> $regressionLog
    if ($LASTEXITCODE -ne 0) { Get-Content $regressionLog -Tail 30; throw 'Shared domain regression failed.' }
    Get-Content $regressionLog -Tail 1
}
$compiled = Join-Path $repoRoot "Shared/bin/$Configuration/netstandard2.1"
$destination = Join-Path $UnityProject 'Assets/Plugins/KeyLearner'
New-Item -ItemType Directory -Force $destination | Out-Null
# Unity 6000.6's .NET Standard 2.1 BCL extensions already provide System.Text.Json
# and dependency versions 8.0.0.0/Unsafe 6.0.0.0. Do not duplicate framework DLLs.
foreach ($name in @('KeyLearner.Domain.dll','KeyLearner.Domain.pdb')) {
    Copy-Item -LiteralPath (Join-Path $compiled $name) -Destination (Join-Path $destination $name) -Force
}
Write-Output "Unity domain published to $destination"
