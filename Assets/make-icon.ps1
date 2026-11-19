$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$size
    $m = $s * 0.035
    $tile = RoundedPath $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.21)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF($s, $s)),
        [System.Drawing.Color]::FromArgb(255, 61, 123, 247),
        [System.Drawing.Color]::FromArgb(255, 13, 71, 190))
    $g.FillPath($brush, $tile)

    $white = [System.Drawing.Color]::White
    $stroke = [Math]::Max($s * 0.052, 1.1)
    $pen = New-Object System.Drawing.Pen($white, $stroke)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $tv = RoundedPath ($s * 0.19) ($s * 0.265) ($s * 0.62) ($s * 0.415) ($s * 0.06)
    $g.DrawPath($pen, $tv)

    $cx = $s * 0.5; $cy = $s * 0.475; $pr = $s * 0.088
    $g.DrawArc($pen, $cx - $pr, $cy - $pr, $pr * 2, $pr * 2, -60, 300)
    $barW = [Math]::Max($s * 0.052, 1.1)
    $barPen = New-Object System.Drawing.Pen($white, $barW)
    $barPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $barPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($barPen, $cx, $cy - $pr * 1.05, $cx, $cy)

    if ($size -ge 32) {
        $standW = [Math]::Max($s * 0.048, 1.1)
        $standPen = New-Object System.Drawing.Pen($white, $standW)
        $standPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $standPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($standPen, $s * 0.5, $s * 0.68, $s * 0.5, $s * 0.755)
        $g.DrawLine($standPen, $s * 0.405, $s * 0.775, $s * 0.595, $s * 0.775)
        $standPen.Dispose()
    }

    $pen.Dispose(); $barPen.Dispose(); $brush.Dispose(); $tile.Dispose(); $tv.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()
foreach ($sz in $sizes) {
    $b = New-IconBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    if ($sz -eq 256) { $b.Save("$PSScriptRoot\icon-preview.png", [System.Drawing.Imaging.ImageFormat]::Png) }
    $ms.Dispose(); $b.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $pngs[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) { $bw.Write($data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes("$PSScriptRoot\app.ico", $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Output "icon written: $((Get-Item "$PSScriptRoot\app.ico").Length) bytes"
