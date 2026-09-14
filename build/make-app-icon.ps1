# Renders src/MinkQuickLax/Assets/AppIcon.ico: three glass orbs (#2FD9BE, #FFC15A, #FF7250) at 16-256 px.
# Usage: powershell -File build/make-app-icon.ps1 -OutPath src/MinkQuickLax/Assets/AppIcon.ico
param([string]$OutPath)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-Color([string]$hex, [int]$alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}

function Draw-Orb($g, [double]$s, [double]$cx, [double]$cy, [double]$r, [string]$light, [string]$dark) {
    $x = ($cx - $r) * $s; $y = ($cy - $r) * $s; $d = 2 * $r * $s
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([single]$x, [single]$y, [single]$d, [single]$d)
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $brush.CenterPoint = New-Object System.Drawing.PointF([single](($cx - $r * 0.35) * $s), [single](($cy - $r * 0.35) * $s))
    $brush.CenterColor = New-Color $light
    $brush.SurroundColors = [System.Drawing.Color[]]@(New-Color $dark)
    $g.FillEllipse($brush, [single]$x, [single]$y, [single]$d, [single]$d)
    # Top-left specular highlight
    $hw = $d * 0.42; $hh = $d * 0.26
    $hx = $x + $d * 0.2; $hy = $y + $d * 0.12
    $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF([single]$hx, [single]$hy, [single]$hw, [single]$hh)),
        (New-Color '#FFFFFF' 150), (New-Color '#FFFFFF' 0), [single]90)
    $g.FillEllipse($hl, [single]$hx, [single]$hy, [single]$hw, [single]$hh)
    # Thin inner edge
    if ($s -ge 32) {
        $pen = New-Object System.Drawing.Pen((New-Color '#FFFFFF' 60), [single]([Math]::Max(1, $s / 64)))
        $g.DrawEllipse($pen, [single]($x + 0.5), [single]($y + 0.5), [single]($d - 1), [single]($d - 1))
        $pen.Dispose()
    }
    $hl.Dispose(); $brush.Dispose(); $path.Dispose()
}

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-Orb $g $size 0.68 0.34 0.24 '#FFD98C' '#E08A1E'
    Draw-Orb $g $size 0.64 0.68 0.26 '#FF9C80' '#C73E2A'
    Draw-Orb $g $size 0.37 0.47 0.32 '#8FF0DF' '#1E8F9E'
    $g.Dispose()
    return $bmp
}

function Get-Bgra($bmp) {
    $rect = New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($bmp.Width * $bmp.Height * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)
    return ,$bytes
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = @()
foreach ($size in $sizes) {
    $bmp = Render $size
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
[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())

"wrote $OutPath ($((Get-Item $OutPath).Length) bytes)"
