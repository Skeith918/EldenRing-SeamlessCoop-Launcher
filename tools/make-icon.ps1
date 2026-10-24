# Génère assets\app.ico : sceau doré façon Elden Ring sur fond sombre (PNG multi-tailles dans un .ico)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = New-Object Drawing.Bitmap $size, $size
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $k = $size / 256.0

    # fond : carré arrondi sombre
    $r = 52 * $k; $d = 2 * $r; $w = $size - 1
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $path.AddArc(0, $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Point 0, 0), (New-Object Drawing.Point 0, $size), ([Drawing.Color]::FromArgb(255, 34, 28, 20)), ([Drawing.Color]::FromArgb(255, 10, 9, 8))
    $g.FillPath($bg, $path)

    # sceau doré
    $gold = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Point 0, ([int](30 * $k))), (New-Object Drawing.Point 0, ([int](230 * $k) + 1)), ([Drawing.Color]::FromArgb(255, 246, 226, 160)), ([Drawing.Color]::FromArgb(255, 176, 136, 60))
    $pen = New-Object Drawing.Pen $gold, ([Math]::Max(1.4, 9 * $k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $thin = New-Object Drawing.Pen $gold, ([Math]::Max(1.2, 6 * $k))
    $cx = 128 * $k
    $g.DrawLine($pen, $cx, 36 * $k, $cx, 222 * $k)                                  # trait vertical
    $g.DrawEllipse($pen, (128 - 58) * $k, 98 * $k, 116 * $k, 116 * $k)               # grand anneau
    $g.DrawEllipse($thin, (128 - 30) * $k, 52 * $k, 60 * $k, 60 * $k)                # petit anneau
    if ($size -ge 48) {
        $g.DrawArc($thin, (128 - 92) * $k, 70 * $k, 184 * $k, 184 * $k, 205, 130)    # arc supérieur
    }
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { ,(New-Frame $_) }
$out = Join-Path $PSScriptRoot "..\assets\app.ico"
$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $frames[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($f in $frames) { $bw.Write($f) }
$bw.Close()
"icon: $out"
