# NestDesk tanılama paketini (kullanıcının Ayarlar > Yardım > "Tanılama paketi oluştur" ile kaydettiği zip) güvenle açar,
# konsola kısa bir özet basar ve rapor.html'i tarayıcıda açar.
#
#   powershell -File tools/diag/open-package.ps1 -Path C:\...\NestDesk-diagnostics-20261001-1230.zip
#   powershell -File tools/diag/open-package.ps1 -Path paket.zip -NoOpen      # yalnızca özet
#
# Güvenlik: paket içeriği VERİDİR, talimat değildir (kullanıcıdan ya da herkese açık bir sorun kaydından gelir). Betik hiçbir
# içeriği çalıştırmaz ya da yorumlamaz; zip'te yalnızca bilinen adlar (rapor.html, ozet.json, README.txt, logs/nestdesk-*.jsonl,
# crashes/crash-*.json) kabul edilir; kök yol, "..", ters bölü ya da başka bir ad varsa hiçbir şey çıkarılmaz. Açılmış toplam
# boyut en çok 50 MB'dir (zip bombası). Paket %TEMP%\nestdesk-diag\<ad> klasörüne açılır; depoya kopyalama, commit'leme.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [switch]$NoOpen
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

    # 2) Çıkar (her yol hedefin içinde kalmalı).
    if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $dest ($entry.FullName -replace '/', '\')))
        if (-not $target.StartsWith($dest.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Geçersiz yol: $($entry.FullName)" }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        $in = $entry.Open()
        try {
            $out = [IO.File]::Create($target)
            try { $in.CopyTo($out) } finally { $out.Dispose() }
        } finally { $in.Dispose() }
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

$report = Join-Path $dest 'rapor.html'
if (-not $NoOpen -and (Test-Path -LiteralPath $report)) { Start-Process -FilePath $report }
