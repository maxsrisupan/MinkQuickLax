# Renders src/MinkQuickLax/Assets/AppIcon.ico at 16-256 px from build/app-icon-small.svg (16-24 px, the tray)
# and build/app-icon.svg (32 px and up). Headless Microsoft Edge rasterizes the SVGs at each exact size, so the
# gradients, glow and clipping match what a browser shows for the same files.
# Usage: powershell -File build/make-app-icon.ps1 [-OutPath src/MinkQuickLax/Assets/AppIcon.ico]
param([string]$OutPath = (Join-Path $PSScriptRoot '..\src\MinkQuickLax\Assets\AppIcon.ico'))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$smallUpTo = 24
$gap = 8

$edge = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge is needed to rasterize the SVGs.' }

function Get-SvgDataUri([string]$name) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $name))
    return 'data:image/svg+xml;base64,' + [Convert]::ToBase64String($bytes)
}

function Get-Bgra($bmp) {
    $rect = New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($bmp.Width * $bmp.Height * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)
    return ,$bytes
}

# Every size side by side in one row at 1 CSS px = 1 pixel, captured with a single screenshot
$largeUri = Get-SvgDataUri 'app-icon.svg'
$smallUri = Get-SvgDataUri 'app-icon-small.svg'
$tags = ''
$offsets = @{}
$x = 0
foreach ($size in $sizes) {
    $uri = if ($size -le $smallUpTo) { $smallUri } else { $largeUri }
    $tags += "<img src=`"$uri`" style=`"position:absolute;left:${x}px;top:0;width:${size}px;height:${size}px`">"
    $offsets[$size] = $x
    $x += $size + $gap
}
$sheetWidth = $x
$sheetHeight = ($sizes | Measure-Object -Maximum).Maximum

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('minkquicklax-icon-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$sheet = $null
try {
    $page = Join-Path $work 'sheet.html'
    $shot = Join-Path $work 'sheet.png'
    [System.IO.File]::WriteAllText($page, "<!doctype html><html><body style=`"margin:0;background:transparent`">$tags</body></html>")
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        --default-background-color=00000000 "--user-data-dir=$(Join-Path $work 'profile')" --virtual-time-budget=2000 `
        "--window-size=$sheetWidth,$sheetHeight" "--screenshot=$shot" ([Uri]$page).AbsoluteUri | Out-Null
    if (-not (Test-Path $shot)) { throw 'Edge did not write the screenshot.' }

    $sheet = [System.Drawing.Bitmap]::FromFile($shot)
    if ($sheet.GetPixel(0, 0).A -ne 0) { throw 'The screenshot background is not transparent.' }

    $images = @()
    foreach ($size in $sizes) {
        $rect = New-Object System.Drawing.Rectangle($offsets[$size], 0, $size, $size)
        $bmp = $sheet.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $ms = New-Object System.IO.MemoryStream
        if ($size -eq 256) {
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        } else {
            # Classic DIB entry: BITMAPINFOHEADER + bottom-up BGRA + empty AND mask
            $w = New-Object System.IO.BinaryWriter($ms)
            $maskRow = [int][Math]::Floor(($size + 31) / 32) * 4
            $w.Write([int]40); $w.Write([int]$size); $w.Write([int]($size * 2))
            $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]0)
            $w.Write([int]($size * $size * 4 + $maskRow * $size))
            $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
            $px = Get-Bgra $bmp
            for ($row = $size - 1; $row -ge 0; $row--) { $w.Write($px, $row * $size * 4, $size * 4) }
            $w.Write((New-Object byte[] ($maskRow * $size)))
            $w.Flush()
        }
        $images += ,@($size, $ms.ToArray())
        $bmp.Dispose()
    }
} finally {
    if ($sheet) { $sheet.Dispose() }
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $s = $img[0]; $len = $img[1].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$len); $bw.Write([int]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img[1]) }
$bw.Flush()
$OutPath = [System.IO.Path]::GetFullPath($OutPath)
[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())

"wrote $OutPath ($((Get-Item $OutPath).Length) bytes)"
