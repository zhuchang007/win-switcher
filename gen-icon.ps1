# Generates app.ico matching the in-app button (slate circle, light ring,
# white two-arrow glyph). Small frames use classic BMP/DIB entries so the
# .NET Framework Icon class can read them; the 256px frame is PNG-compressed.
Add-Type -AssemblyName System.Drawing

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sizes = 16, 32, 48, 64, 128, 256

function New-FrameBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $k = $s / 38.0
    $circle = New-Object System.Drawing.Rectangle 1, 1, ($s - 2), ($s - 2)

    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 72, 82, 98))
    $g.FillEllipse($fill, $circle)
    $fill.Dispose()

    $ringW = [Math]::Max(1.0, [single](1.2 * $k))
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 245, 245, 250)), $ringW
    $g.DrawEllipse($ring, $circle)
    $ring.Dispose()

    # Glyph drawn as round-capped shafts + filled triangle heads: deterministic
    # at every size (AdjustableArrowCap would scale again by pen width).
    $glyph = [System.Drawing.Color]::FromArgb(240, 245, 250)
    $penW = [Math]::Max(1.0, [single](2.1 * $k))
    $pen = New-Object System.Drawing.Pen ($glyph, $penW)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $brush = New-Object System.Drawing.SolidBrush $glyph

    # Top arrow, pointing right.
    $g.DrawLine($pen, [single](10 * $k), [single](14 * $k), [single](22.5 * $k), [single](14 * $k))
    $rightHead = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ([single](27 * $k), [single](14 * $k))),
        (New-Object System.Drawing.PointF ([single](22.5 * $k), [single](10.8 * $k))),
        (New-Object System.Drawing.PointF ([single](22.5 * $k), [single](17.2 * $k))))
    $g.FillPolygon($brush, $rightHead)

    # Bottom arrow, pointing left.
    $g.DrawLine($pen, [single](28 * $k), [single](24 * $k), [single](15.5 * $k), [single](24 * $k))
    $leftHead = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ([single](11 * $k), [single](24 * $k))),
        (New-Object System.Drawing.PointF ([single](15.5 * $k), [single](20.8 * $k))),
        (New-Object System.Drawing.PointF ([single](15.5 * $k), [single](27.2 * $k))))
    $g.FillPolygon($brush, $leftHead)

    $pen.Dispose()
    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

# 32bpp DIB (BITMAPINFOHEADER + BGRA bottom-up pixels + zeroed AND mask).
function ConvertTo-DibBytes($bmp) {
    $s = $bmp.Width
    $maskRow = [int]((($s + 31) / 32) * 4)
    $maskSize = $maskRow * $s
    $pixelSize = $s * $s * 4

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40)            # biSize
    $bw.Write([int32]$s)             # biWidth
    $bw.Write([int32]($s * 2))       # biHeight: color + mask halves
    $bw.Write([uint16]1)             # biPlanes
    $bw.Write([uint16]32)            # biBitCount
    $bw.Write([uint32]0)             # biCompression = BI_RGB
    $bw.Write([uint32]($pixelSize + $maskSize))
    $bw.Write([int32]0); $bw.Write([int32]0)  # pels per meter
    $bw.Write([uint32]0); $bw.Write([uint32]0) # palette

    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $bw.Write((New-Object byte[] $maskSize))  # AND mask: alpha channel governs transparency
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return $bytes
}

$entries = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) {
    $bmp = New-FrameBitmap $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entries.Add($ms.ToArray()) | Out-Null
        $ms.Dispose()
    }
    else {
        $entries.Add((ConvertTo-DibBytes $bmp)) | Out-Null
    }
    $bmp.Dispose()
}

$icoPath = Join-Path $dir 'app.ico'
$out = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)                 # reserved
$bw.Write([uint16]1)                 # type: icon
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { [byte]0 } else { [byte]$s }  # 0 means 256
    $bw.Write($dim)                   # width
    $bw.Write($dim)                   # height
    $bw.Write([byte]0)                # palette count
    $bw.Write([byte]0)                # reserved
    $bw.Write([uint16]1)              # color planes
    $bw.Write([uint16]32)             # bits per pixel
    $bw.Write([uint32]$entries[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $entries[$i].Length
}
foreach ($blob in $entries) { $bw.Write($blob) }
$bw.Close()
$out.Close()
Write-Host "wrote $icoPath ($((Get-Item $icoPath).Length) bytes, $($sizes.Count) sizes)"
