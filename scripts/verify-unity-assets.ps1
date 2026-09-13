param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$assetRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'UnityPort/Assets/ThirdParty')) + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -LiteralPath (Join-Path $assetRoot 'ASSET_MANIFEST.json') -Raw | ConvertFrom-Json
if (@($manifest.source_files).Count -eq 0) { throw 'The source asset manifest is empty.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $manifest.source_files) {
    if (-not $seen.Add($entry.path)) { throw "Duplicate asset manifest entry: $($entry.path)" }
    $path = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $entry.path))
    if (-not $path.StartsWith($assetRoot, [StringComparison]::Ordinal)) { throw "Asset manifest path escaped ThirdParty: $($entry.path)" }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing licensed source asset: $($entry.path)" }
    if ((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "Licensed source asset bytes differ from the manifest: $($entry.path)"
    }
}
Write-Output "PASS: $($seen.Count) licensed source asset sizes and SHA-256 hashes match."
