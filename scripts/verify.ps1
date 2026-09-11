param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet run --project tests/KeyLearner.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
    New-Item -ItemType Directory -Force artifacts | Out-Null
    foreach ($scenario in @('balloon','balloon-sequence','rapid-milk','guided-mommy','milk','mommy','fire','fire-tap','icons','options','extra-key','counting')) {
        $image = Join-Path $repo "artifacts/$scenario.png"
        $profile = Join-Path $repo "artifacts/verification-profile-$scenario"
        $run = Start-Process -FilePath (Join-Path $repo 'bin/Release/net8.0-windows/KeyLearner.exe') -ArgumentList @('--preview','--scenario',$scenario,'--data',('"' + $profile + '"'),'--screenshot',('"' + $image + '"')) -WindowStyle Hidden -PassThru
        if (-not $run.WaitForExit(15000)) { throw "Preview timed out: $scenario" }
        if ($run.ExitCode -ne 0) { throw "Preview failed: $scenario" }
        Get-Content -LiteralPath ($image + '.verified.txt')
    }
} finally { Pop-Location }
