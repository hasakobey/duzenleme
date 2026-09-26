# Uygulama simgesini (Assets/app.ico) çizer: mor degrade zemin üzerinde 2x2 düzen kutucukları.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\Duzenleme\Assets\app.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

function Render([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $bg = New-RoundRect 0 0 ($s - 1) ($s - 1) ($s * 0.23)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s), ([System.Drawing.Color]::FromArgb(255, 99, 102, 241)), ([System.Drawing.Color]::FromArgb(255, 168, 85, 247))
    $g.FillPath($brush, $bg)
    $pad = $s * 0.22; $gap = $s * 0.07; $t = ($s - 2 * $pad - $gap) / 2
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 255, 255, 255))
    $soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 255, 255, 255))
    $amber = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 251, 191, 36))
    $cells = @(@(0, 0, $white), @(1, 0, $soft), @(0, 1, $soft), @(1, 1, $amber))
    foreach ($c in $cells) {
        $x = $pad + $c[0] * ($t + $gap); $y = $pad + $c[1] * ($t + $gap)
        $g.FillPath($c[2], (New-RoundRect $x $y $t $t ($t * 0.28)))
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    if ($s -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Küçük boyutlar klasik 32-bit DIB: her okuyucu (tepsi simgesi dahil) destekler.
        $w = New-Object System.IO.BinaryWriter $ms
        $w.Write([uint32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2)); $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) { $c = $bmp.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) }
        }
        $maskRow = [int]([math]::Ceiling($s / 32.0) * 4)
        $w.Write((New-Object byte[] ($maskRow * $s)))
        $w.Flush()
    }
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 64, 48, 32, 24, 16
$pngs = $sizes | ForEach-Object { , (Render $_) }
$fs = [System.IO.File]::Create($out); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
Write-Output "Wrote $out"
