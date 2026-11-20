<#
.SYNOPSIS
    Génère l'icône de Phone Mirror (Assets\PhoneMirror.ico) : un téléphone et des ondes de
    diffusion sur un fond en dégradé. Toutes les tailles utilisées par Windows (16 à 256 px).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Assets\make-icon.ps1
    powershell -ExecutionPolicy Bypass -File .\Assets\make-icon.ps1 -PreviewPng C:\temp\apercu.png
#>
param(
    [string] $OutFile = (Join-Path $PSScriptRoot 'PhoneMirror.ico'),
    [string] $PreviewPng
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([single] $x, [single] $y, [single] $w, [single] $h, [single] $r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconImage([int] $size) {
    $s = [single] $size
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Fond : carré arrondi en dégradé indigo -> turquoise.
    $bg = New-RoundedRect 0 0 $s $s ($s * 0.22)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s),
        [System.Drawing.Color]::FromArgb(255, 79, 70, 229), [System.Drawing.Color]::FromArgb(255, 6, 182, 212))
    $g.FillPath($brush, $bg)

    # Aux petites tailles, les ondes deviendraient illisibles : téléphone seul, plus grand et centré.
    $small = $size -le 24
    $phoneW = $s * ($(if ($small) { 0.46 } else { 0.38 }))
    $phoneH = $s * ($(if ($small) { 0.74 } else { 0.62 }))
    $phoneX = $(if ($small) { ($s - $phoneW) / 2 } else { $s * 0.18 })
    $phoneY = ($s - $phoneH) / 2

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillPath($white, (New-RoundedRect $phoneX $phoneY $phoneW $phoneH ($phoneW * 0.2)))

    # Écran du téléphone.
    $border = [Math]::Max(1.0, $phoneW * 0.1)
    $screenTop = [Math]::Max(1.0, $phoneH * 0.1)
    $screen = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, $phoneY), (New-Object System.Drawing.PointF 0, ($phoneY + $phoneH)),
        [System.Drawing.Color]::FromArgb(255, 49, 46, 129), [System.Drawing.Color]::FromArgb(255, 30, 27, 75))
    $g.FillPath($screen, (New-RoundedRect ($phoneX + $border) ($phoneY + $screenTop) ($phoneW - 2 * $border) ($phoneH - 2 * $screenTop) ($phoneW * 0.08)))

    if (-not $small) {
        # Ondes de diffusion vers la droite (l'écran part vers le PC).
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ($s * 0.055)
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $cx = $phoneX + $phoneW + $s * 0.02
        $cy = $s / 2
        foreach ($r in ($s * 0.14), ($s * 0.26)) {
            $g.DrawArc($pen, $cx - $r, $cy - $r, 2 * $r, 2 * $r, -45, 90)
        }
        $pen.Dispose()
    }

    $g.Dispose()
    return $bmp
}


# Image au format bitmap d'icône (DIB 32 bits avec transparence), lisible par tous les composants
# Windows, y compris les plus anciens qui ne décodent pas le PNG dans un ICO.
function ConvertTo-IconDib([System.Drawing.Bitmap] $bmp) {
    $size = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    # BITMAPINFOHEADER : la hauteur compte l'image et le masque, d'où le double.
    $w.Write([uint32] 40); $w.Write([int32] $size); $w.Write([int32] (2 * $size))
    $w.Write([uint16] 1); $w.Write([uint16] 32); $w.Write([uint32] 0)
    $w.Write([uint32] ($size * $size * 4 + $maskStride * $size))
    $w.Write([int32] 0); $w.Write([int32] 0); $w.Write([uint32] 0); $w.Write([uint32] 0)
    # Pixels BGRA, lignes de bas en haut.
    for ($y = $size - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $data.Stride, $size * 4) }
    # Masque ET vide : la transparence vient du canal alpha.
    $w.Write((New-Object byte[] ($maskStride * $size)))
    $w.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($size in $sizes) {
    $bmp = New-IconImage $size
    if ($PreviewPng -and $size -eq 256) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    if ($size -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png) # PNG : l'usage pour la taille 256
        $bytes = $ms.ToArray()
    }
    else {
        $bytes = ConvertTo-IconDib $bmp
    }
    $bmp.Dispose()
    , $bytes
}

# Format ICO : en-tête, une entrée de 16 octets par taille, puis les images.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16] 0); $w.Write([uint16] 1); $w.Write([uint16] $sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $(if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] })
    $w.Write([byte] $dim); $w.Write([byte] $dim); $w.Write([byte] 0); $w.Write([byte] 0)
    $w.Write([uint16] 1); $w.Write([uint16] 32)
    $w.Write([uint32] $pngs[$i].Length); $w.Write([uint32] $offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutFile, $out.ToArray())
Write-Host "Icône écrite : $OutFile ($($sizes -join ', ') px)"
