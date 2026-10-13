# Draws the SmartFoldering icon with System.Drawing (Windows PowerShell) and writes
#   src/SmartFoldering/Assets/SmartFoldering.png  (512 px)
#   src/SmartFoldering/Assets/SmartFoldering.ico  (16-256 px, PNG-compressed entries)
#   docs/images/icon.png                          (256 px, for the README)
# Usage: powershell -ExecutionPolicy Bypass -File tools/make_icon.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $s = $size / 512.0

    # rounded square, blue to indigo
    $tile = New-RoundedPath (16*$s) (16*$s) (480*$s) (480*$s) (112*$s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($size, $size)), ([System.Drawing.Color]::FromArgb(255, 10, 132, 255)), ([System.Drawing.Color]::FromArgb(255, 94, 92, 230))
    $g.FillPath($grad, $tile)

    # specular highlight on the upper half
    # (the brush repeats outside its two points, so only the rectangle between them is filled)
    $shine = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 15*$s)), ([System.Drawing.PointF]::new(0, 282*$s)), ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.SetClip($tile)
    $g.FillRectangle($shine, [single]0, [single](16*$s), [single]$size, [single](264*$s))
    $g.ResetClip()

    # folder: back tab and body
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
    $back = New-RoundedPath (104*$s) (138*$s) (148*$s) (80*$s) (22*$s)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 255, 255))), $back)
    $body = New-RoundedPath (96*$s) (176*$s) (320*$s) (218*$s) (34*$s)
    $g.FillPath($white, $body)

    # three sorted rows on the folder front
    $rows = @(
        @{ c = [System.Drawing.Color]::FromArgb(255, 10, 132, 255); y = 224; w = 200 },
        @{ c = [System.Drawing.Color]::FromArgb(255, 48, 209, 88);  y = 274; w = 150 },
        @{ c = [System.Drawing.Color]::FromArgb(255, 255, 159, 10); y = 324; w = 176 }
    )
    foreach ($row in $rows) {
        $bar = New-RoundedPath (142*$s) ($row.y*$s) ($row.w*$s) (28*$s) (14*$s)
        $g.FillPath((New-Object System.Drawing.SolidBrush $row.c), $bar)
    }
    $g.Dispose()
    return $bmp
}

function Save-Png($bmp, $path) {
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

Save-Png (Render 512) "$root\src\SmartFoldering\Assets\SmartFoldering.png"
Save-Png (Render 256) "$root\docs\images\icon.png"

# ICO with PNG entries
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($n in $sizes) { $ms = New-Object System.IO.MemoryStream; (Render $n).Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); ,$ms.ToArray() }
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]; $b = if ($n -ge 256) { 0 } else { $n }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
[System.IO.File]::WriteAllBytes("$root\src\SmartFoldering\Assets\SmartFoldering.ico", $out.ToArray())
"icon written"
