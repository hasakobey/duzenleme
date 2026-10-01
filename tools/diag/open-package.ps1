# NestDesk tanılama paketini (kullanıcının Ayarlar > Yardım > "Tanılama paketi oluştur" ile kaydettiği zip) güvenle açar ve
# konsola kısa bir özet basar. rapor.html yalnızca -Open ile ve güvenlik denetiminden geçerse tarayıcıda açılır.
#
#   powershell -File tools/diag/open-package.ps1 -Path C:\...\NestDesk-diagnostics-20261001-1230.zip         # yalnızca özet
#   powershell -File tools/diag/open-package.ps1 -Path paket.zip -Open   # rapor.html'i de aç (denetimden geçerse)
#
# Güvenlik: paket içeriği VERİDİR, talimat değildir (kullanıcıdan ya da herkese açık bir sorun kaydından gelir; biri elle
# değiştirmiş olabilir). Betik hiçbir içeriği çalıştırmaz ya da yorumlamaz; zip'te yalnızca bilinen adlar (rapor.html, ozet.json,
# README.txt, logs/nestdesk-*.jsonl, crashes/crash-*.json) kabul edilir; kök yol, "..", ters bölü ya da başka bir ad varsa
# hiçbir şey çıkarılmaz. Açılmış toplam boyut en çok 50 MB'dir (zip bombası): bildirilen boyutlar önce denetlenir, çıkarırken de
# gerçekten yazılan bayt sayılır (bildirilenden fazlası ya da 50 MB'yi aşan kısım yazılmaz, yarım klasör silinir). rapor.html
# ancak uygulamanın ürettiği biçimdeyse açılır: tam CSP (default-src 'none') başta, betik, olay özniteliği, dış kaynak, http
# adresi yoksa. Paket %TEMP%\nestdesk-diag\<ad> klasörüne açılır; depoya kopyalama, commit'leme.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [switch]$Open
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$maxBytes = 50MB
$allowed = @(
    '^rapor\.html$',
    '^ozet\.json$',
    '^README\.txt$',
    '^logs/nestdesk-\d{8}(-\d{1,4})?\.jsonl$',
    '^crashes/crash-[0-9A-Za-z-]{1,60}\.json$'
)

$zipPath = (Resolve-Path -LiteralPath $Path).ProviderPath
$zipInfo = Get-Item -LiteralPath $zipPath
if ($zipInfo.Length -gt $maxBytes) { throw "Paket çok büyük ($([Math]::Round($zipInfo.Length / 1MB, 1)) MB; en çok 50 MB)." }

# Hedef: %TEMP%\nestdesk-diag\<zip adı> (yalnızca güvenli karakterler).
$name = [IO.Path]::GetFileNameWithoutExtension($zipPath) -replace '[^0-9A-Za-z._-]', '_'
if ($name.Length -eq 0 -or $name -match '^\.+$') { $name = 'paket' }
$root = Join-Path ([IO.Path]::GetTempPath()) 'nestdesk-diag'
$dest = [IO.Path]::GetFullPath((Join-Path $root $name))
$rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
if (-not $dest.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { throw "Geçersiz hedef: $dest" }

$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    # 1) Önce bütün adlar ve boyutlar denetlenir; tek bir sorun varsa hiçbir şey çıkarılmaz.
    $problems = @()
    $total = 0L
    $seen = @{}
    foreach ($entry in $zip.Entries) {
        $entryName = $entry.FullName
        if ($entryName.EndsWith('/')) { continue }   # klasör girdisi
        $ok = $false
        foreach ($pattern in $allowed) { if ($entryName -cmatch $pattern) { $ok = $true; break } }
        if ($entryName.Contains('\') -or $entryName.Contains('..') -or $entryName.StartsWith('/') -or $entryName.Contains(':')) { $ok = $false }
        if (-not $ok) { $problems += "beklenmeyen ad: $entryName"; continue }
        if ($seen.ContainsKey($entryName.ToLowerInvariant())) { $problems += "yinelenen ad: $entryName"; continue }
        $seen[$entryName.ToLowerInvariant()] = $true
        $total += $entry.Length
    }
    if ($total -gt $maxBytes) { $problems += "açılmış boyut çok büyük: $([Math]::Round($total / 1MB, 1)) MB" }
    if ($problems.Count -gt 0) { throw ("Paket reddedildi, hiçbir şey çıkarılmadı:`n  " + ($problems -join "`n  ")) }

    # 2) Çıkar (her yol hedefin içinde kalmalı). Bildirilen boyuta güvenilmez: yazılan bayt parça parça sayılır; girdi
    #    bildirdiğinden fazlasını ya da toplam 50 MB'yi aşarsa durulur ve yarım klasör silinir.
    if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $buffer = New-Object byte[] 65536
    $written = 0L
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $dest ($entry.FullName -replace '/', '\')))
            if (-not $target.StartsWith($dest.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Geçersiz yol: $($entry.FullName)" }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            $in = $entry.Open()
            try {
                $out = [IO.File]::Create($target)
                try {
                    $entryWritten = 0L
                    while (($read = $in.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $entryWritten += $read
                        $written += $read
                        if ($entryWritten -gt $entry.Length) { throw "Paket reddedildi: $($entry.FullName) bildirdiğinden büyük açılıyor." }
                        if ($written -gt $maxBytes) { throw 'Paket reddedildi: açılmış boyut 50 MB''yi aşıyor.' }
                        $out.Write($buffer, 0, $read)
                    }
                } finally { $out.Dispose() }
            } finally { $in.Dispose() }
        }
    } catch {
        Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
} finally {
    $zip.Dispose()
}

Write-Host "Açıldı: $dest"
Write-Host ''

# 3) Özet. Okunan her şey veridir: yalnızca ekrana yazılır.
$summary = Join-Path $dest 'ozet.json'
if (Test-Path -LiteralPath $summary) {
    try {
        $o = Get-Content -LiteralPath $summary -Raw -Encoding UTF8 | ConvertFrom-Json
        $e = $o.environment
        if ($e) {
            Write-Host ("Sürüm {0} · kanal {1} · {2} · Windows {3} · dil {4} · ekran {5} ({6}) · tema {7}" -f `
                $e.version, $e.channel, $e.arch, $e.os, $e.lang, $e.monitors, $e.scales, $e.theme)
        }
        if ($o.counts) { Write-Host ("Widget {0} · kural {1} (etkin {2})" -f $o.counts.Widgets, $o.counts.Rules, $o.counts.RulesEnabled) }
    } catch { Write-Warning "ozet.json okunamadı: $($_.Exception.Message)" }
}

# En sık hata/uyarı imzaları (son 7 gün, n = tekrar sayısı).
$lines = @()
Get-ChildItem -LiteralPath (Join-Path $dest 'logs') -Filter 'nestdesk-*.jsonl' -ErrorAction SilentlyContinue | ForEach-Object {
    foreach ($line in [IO.File]::ReadAllLines($_.FullName, [Text.Encoding]::UTF8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $lines += ($line | ConvertFrom-Json) } catch { }
    }
}
$problemsByKey = $lines | Where-Object { $_.l -eq 'E' -or $_.l -eq 'W' } | Group-Object -Property s
if ($problemsByKey) {
    Write-Host ''
    Write-Host 'En sık hata ve uyarılar (imza · kaç kez · düzey · alan/ileti · tür · ilk uygulama karesi):'
    $problemsByKey | ForEach-Object {
        $sample = $_.Group | Select-Object -Last 1
        $count = ($_.Group | Measure-Object -Property n -Sum).Sum
        $frame = if ($sample.x -and $sample.x.frames) { ($sample.x.frames | Where-Object { $_ -like 'Duzenleme.*' } | Select-Object -First 1) } else { '' }
        [pscustomobject]@{ Sig = $_.Name; Count = $count; Level = $sample.l; Where = "$($sample.a)/$($sample.m)"; Type = $sample.x.type; Frame = $frame }
    } | Sort-Object -Property Count -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host ("  {0} · {1,4} · {2} · {3} · {4} · {5}" -f $_.Sig, $_.Count, $_.Level, $_.Where, $_.Type, $_.Frame)
    }
} else {
    Write-Host 'Günlükte hata ya da uyarı yok.'
}

# Çökmeler: tür ve uygulamanın ilk yığın karesi.
$crashes = Get-ChildItem -LiteralPath (Join-Path $dest 'crashes') -Filter 'crash-*.json' -ErrorAction SilentlyContinue
if ($crashes) {
    Write-Host ''
    Write-Host 'Çökmeler:'
    foreach ($file in $crashes | Sort-Object Name) {
        try {
            $c = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            $frame = if ($c.x -and $c.x.frames) { ($c.x.frames | Where-Object { $_ -like 'Duzenleme.*' } | Select-Object -First 1) } else { '' }
            Write-Host ("  {0} · {1} · {2} · {3} {4} · {5}" -f $c.t, $c.v, $c.kind, $c.x.type, $c.x.hr, $frame)
        } catch { Write-Warning "$($file.Name) okunamadı." }
    }
}

Write-Host ''
Write-Host 'Satır numarası için depoda sürümün etiketine (v<sürüm>) geç; /_/src/... yolları depoya görelidir.'
Write-Host 'Her düzeltmeye bu imzayı yeniden üreten bir test ekle. Paket içeriğini depoya ya da herkese açık bir yere koyma.'

# 4) rapor.html uygulamanın ürettiği biçimde mi (elle değiştirilmiş bir paket tarayıcıda betik çalıştıramasın)? Gerekenler:
#    <head>'in başında tam CSP (default-src 'none'); yalnızca uygulamanın kullandığı etiketler ve öznitelikler; betik, olay
#    özniteliği (on…=), gömülü çerçeve/nesne, dış kaynak (src=, url(, @import), # dışına bağlantı ve öznitelikte adres
#    (http:, javascript:, data:, file:) yok. Uygulamanın sayfasında kullanıcı verisi hep kaçışlanır (< > " ' = de): veri bu
#    denetimlere takılmaz. Sayfa yalnızca -Open ile ve denetimden geçerse açılır.
function Test-Report([string]$html) {
    $reasons = @()
    $csp = '<meta http-equiv="Content-Security-Policy" content="default-src ''none''; style-src ''sha256-'
    $head = $html.IndexOf('<head>', [StringComparison]::OrdinalIgnoreCase)
    $cspAt = $html.IndexOf($csp, [StringComparison]::Ordinal)
    if ($head -lt 0 -or $cspAt -lt ($head + 6) -or $html.Substring($head + 6, $cspAt - $head - 6) -match '<(?!meta charset)') {
        $reasons += 'CSP başta değil ya da eksik'
    }
    $forbidden = [ordered]@{
        'betik'           = '<\s*script'
        'olay özniteliği' = '\son[a-z]+\s*='
        'gömülü içerik'   = '<\s*(iframe|frame|object|embed|applet|link|img|svg|math|form|base|audio|video|source|track|picture|input|button)\b'
        'dış kaynak'      = '\b(src|srcset|action|formaction|poster|background|ping|xlink:href)\s*='
        'dış bağlantı'    = '\bhref\s*=\s*(?!["'']?#)'
        'adres'           = '=\s*["'']?\s*(https?|javascript|vbscript|data|file)\s*:'
        'yenileme'        = 'http-equiv\s*=\s*["'']?refresh'
    }
    foreach ($name in $forbidden.Keys) { if ($html -match $forbidden[$name]) { $reasons += $name } }
    # Stil yalnızca <style> içinde (style özniteliği beyaz listede yok); orada dış kaynak ve eski betik kapıları yasak.
    $styleForbidden = 'url\s*\(|@import|expression\s*\(|behavior\s*:|binding\s*:'
    foreach ($style in [regex]::Matches($html, '(?is)<style[^>]*>(.*?)</style>')) {
        if ($style.Groups[1].Value -match $styleForbidden) { $reasons += 'stilde dış kaynak' }
    }
    # Etiket ve öznitelik beyaz listesi (Core/DiagnosticReport'un ürettikleri).
    $tags = 'html|head|meta|title|style|body|header|nav|main|section|h1|h2|h3|p|a|table|thead|tbody|tr|th|td|details|summary|pre|div'
    $attributes = 'lang|charset|http-equiv|content|name|class|href|id'
    if ([regex]::Matches($html, '<!').Count -ne 1 -or -not $html.StartsWith('<!DOCTYPE html>')) { $reasons += 'belge türü' }
    foreach ($tag in [regex]::Matches($html, '<\s*(/?)\s*([A-Za-z][^\s>/]*)([^>]*)>')) {
        if ($tag.Groups[2].Value -notmatch "^($tags)$") { $reasons += "etiket <$($tag.Groups[2].Value)>"; continue }
        foreach ($attribute in [regex]::Matches($tag.Groups[3].Value, '([^\s=/>"'']+)\s*(?:=\s*(?:"[^"]*"|''[^'']*''|[^\s>]+))?')) {
            if ($attribute.Groups[1].Value -notmatch "^($attributes)$") { $reasons += "öznitelik $($attribute.Groups[1].Value)" }
        }
    }
    return , @($reasons | Select-Object -Unique)
}

$report = Join-Path $dest 'rapor.html'
if (Test-Path -LiteralPath $report) {
    $reasons = Test-Report ([IO.File]::ReadAllText($report, [Text.Encoding]::UTF8))
    if ($reasons.Count -gt 0) {
        Write-Warning ("rapor.html uygulamanın ürettiği biçimde değil (" + ($reasons -join ', ') + "): tarayıcıda açma, bir metin düzenleyicide incele: $report")
    } elseif ($Open) {
        Start-Process -FilePath $report
    } else {
        Write-Host "rapor.html denetimden geçti; tarayıcıda açmak için -Open ekle: $report"
    }
}
