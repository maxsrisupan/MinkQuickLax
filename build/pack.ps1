# Packs the published app into a Velopack release, and optionally uploads it to GitHub Releases (PLAN.md 9, SPEC 4.11).
# Stable versions use Velopack's default channel "win" (installer: MinkQuickLax-win-Setup.exe); versions with a
# pre-release part (0.2.0-beta.1) go to the "beta" channel as a GitHub pre-release.
# Usage: pwsh build/pack.ps1 -Version 0.1.0-beta.1 [-Publish artifacts/publish] [-Output artifacts/releases] [-Upload]
# Upload needs $env:GITHUB_TOKEN with permission to create releases.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Publish = 'artifacts/publish',
    [string]$Output = 'artifacts/releases',
    [switch]$Upload
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = 'https://github.com/maxsrisupan/MinkQuickLax'
$prerelease = $Version.Contains('-')
$channel = if ($prerelease) { 'beta' } else { 'win' }
$publishPath = Join-Path $root $Publish
$outputPath = Join-Path $root $Output
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

# The release page (PLAN.md 9): how to install, how to get past SmartScreen, then this version's CHANGELOG.md section.
function Write-ReleaseNotes([string]$Path) {
    $changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Encoding utf8
    $start = [Array]::IndexOf($changelog, "## $Version")
    if ($start -lt 0) { throw "CHANGELOG.md has no '## $Version' section" }
    $end = $start + 1
    while ($end -lt $changelog.Count -and -not $changelog[$end].StartsWith('## ')) { $end++ }
    $changes = ($changelog[($start + 1)..($end - 1)] -join "`n").Trim()
    $setup = "MinkQuickLax-$channel-Setup.exe"
    $header = @"
## ติดตั้ง

1. ดาวน์โหลด ``$setup`` ด้านล่าง แล้วดับเบิลคลิก ติดตั้งแบบรายผู้ใช้ ไม่ต้องใช้สิทธิ์ผู้ดูแลระบบ
2. ถ้า Windows ขึ้น "Windows protected your PC" ให้กด **More info** แล้วกด **Run anyway** (โปรแกรมยังไม่ได้เซ็นดิจิทัล)

## Install

1. Download ``$setup`` below and run it. It installs for your user only and needs no administrator rights.
2. If Windows shows "Windows protected your PC", choose **More info**, then **Run anyway** (the app is not code-signed yet).

## สิ่งที่เปลี่ยน · Changes

"@
    # No byte order mark: it would end up in front of the first heading on the release page.
    [IO.File]::WriteAllText($Path, $header + "`n" + $changes + "`n", (New-Object Text.UTF8Encoding $false))
}

function Invoke-Vpk([string[]]$Arguments) {
    dotnet vpk @Arguments
    if ($LASTEXITCODE -ne 0) { throw "vpk $($Arguments[0]) failed ($LASTEXITCODE)" }
}

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

    # An empty value would vanish from the command line and shift every argument after it.
    $tokenArgs = if ($env:GITHUB_TOKEN) { @('--token', $env:GITHUB_TOKEN) } else { @() }
    if ($Upload) {
        # The previous release of this channel lets vpk build a small delta package.
        $downloadArgs = @('download', 'github', '--repoUrl', $repo, '--channel', $channel, '-o', $outputPath) + $tokenArgs
        if ($prerelease) { $downloadArgs += '--pre' }
        dotnet vpk @downloadArgs
        if ($LASTEXITCODE -ne 0) { Write-Warning 'No earlier release to download; packing without a delta.' }
    }

    $notes = Join-Path $outputPath "release-notes-$Version.md"
    Write-ReleaseNotes -Path $notes
    $packArgs = @(
        'pack',
        '--packId', 'MinkQuickLax',
        '--packVersion', $Version,
        '--packDir', $publishPath,
        '--mainExe', 'MinkQuickLax.exe',
        '--packTitle', 'MinkQuickLax',
        '--packAuthors', 'maxsrisupan',
        '--icon', (Join-Path $root 'src/MinkQuickLax/Assets/AppIcon.ico'),
        '--channel', $channel,
        # SPEC 4.11: a Start Menu shortcut only.
        '--shortcuts', 'StartMenuRoot',
        '--releaseNotes', $notes,
        '-o', $outputPath
    )
    Invoke-Vpk $packArgs

    if ($Upload) {
        # Not $upload: PowerShell names are case-insensitive, so that would overwrite the -Upload switch.
        $uploadArgs = @(
            'upload', 'github',
            '--repoUrl', $repo,
            '--channel', $channel,
            '--tag', "v$Version",
            '--releaseName', "MinkQuickLax $Version",
            # Boolean options are bare flags in vpk 1.2 ("--publish true" is rejected).
            '--publish',
            '--merge',
            '-o', $outputPath
        )
        $uploadArgs += $tokenArgs
        if ($prerelease) { $uploadArgs += '--pre' }
        Invoke-Vpk $uploadArgs
    }
}
finally {
    Pop-Location
}
Write-Host "Packed $Version ($channel) into $outputPath"
