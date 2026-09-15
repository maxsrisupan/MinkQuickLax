# Publishes the app for the installer: self-contained win-x64 with ReadyToRun, the manual next to the exe (PLAN.md 9).
# Usage: pwsh build/publish.ps1 -Version 0.1.0-beta.1 [-Output artifacts/publish]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Output = 'artifacts/publish'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/MinkQuickLax/MinkQuickLax.csproj'
$outputPath = Join-Path $root $Output

if (Test-Path $outputPath) {
    Get-ChildItem $outputPath -Force | Remove-Item -Recurse -Force
}

# The version comes from the tag; Directory.Build.props holds the default for local builds.
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:Version=$Version -o $outputPath
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

foreach ($required in 'MinkQuickLax.exe', 'Manual/th.html', 'Manual/en.html') {
    if (-not (Test-Path (Join-Path $outputPath $required))) { throw "Published output is missing $required" }
}
Write-Host "Published $Version to $outputPath"
