<#
.SYNOPSIS
  NestDesk başarım ölçümü (2.1 "Akıcılık" eşikleri). Gerçek masaüstüne ve ayarlara dokunmaz.

.DESCRIPTION
  Geçici bir "masaüstü" ve veri klasörüyle bir test örneği açar (--desktop/--data), widget'ları sol test monitörüne
  (fiziksel x -1920..0, %125) yerleştirir, senaryoyu yalnızca pencere iletileriyle sürer ve --exit ile kapatır.
  Gerçek fare/klavye kullanılmaz; kurulu NestDesk'e ve Windows masaüstü simgelerine dokunulmaz (başta ve sonda
  yalnızca okunur).

  Senaryolar:
    startup  açılış: bütün widget'lar görünene ve arayüz iş parçacığı boşalana dek geçen süre, en uzun takılma,
             açılışta ayar dosyası yazma sayısı (varsayılan 600 dosyalık masaüstü).
    clicks   iki widget'a sırayla tıklama (WM_MOUSEACTIVATE, 400 ms'de bir): dakikada ayar yazma sayısı.
    churn    indirme benzeri masaüstü değişikliği (saniyede bir .crdownload'a ekleme, 5 saniyede bir yeni PDF):
             arayüz iş parçacığı işlemcisi, takılmalar, bölme güncellemeleri (DUZENLEME_PERF_LOG destekleyen sürümde).
    idle     boşta işlemci kullanımı.

  Eşikler (perf.md §4): boşta < %0,5 çekirdek; churn'de arayüz iş parçacığı < %5 ve 100 ms'yi aşan takılma yok;
  600 dosyalık masaüstünde ilk boşa düşüş < 3 sn ve 250 ms'yi aşan tek takılma yok.

.EXAMPLE
  powershell -File tools\perf\perf-run.ps1 -Exe src\Duzenleme\bin\Debug\net10.0-windows\Duzenleme.exe -Scenario clicks
#>
param(
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [ValidateSet('startup', 'clicks', 'churn', 'idle')] [string] $Scenario,
    [string] $Name = $Scenario,
    [int] $Files = -1,
    [int] $Seconds = 40,
    [string] $Out = (Join-Path $env:TEMP 'nestdesk-perf')
)
$ErrorActionPreference = 'Stop'
if (-not ('PerfNative' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'PerfNative.cs') }
$Exe = (Resolve-Path $Exe).Path
if ($Files -lt 0) { $Files = if ($Scenario -eq 'startup') { 600 } else { 60 } }

$run = Join-Path $Out $Name
if (Test-Path $run) { Remove-Item -Recurse -Force $run }
$desk = Join-Path $run 'desktop'; $data = Join-Path $run 'data'
New-Item -ItemType Directory -Force $desk, $data | Out-Null

# --- Test masaüstü ---
$exts = 'pdf', 'docx', 'txt', 'jpg', 'png', 'zip', 'mp3', 'xlsx', 'pptx', 'mp4'
for ($i = 0; $i -lt $Files; $i++) {
    Set-Content -Path (Join-Path $desk ("Dosya {0:D3}.{1}" -f $i, $exts[$i % $exts.Count])) -Value ('x' * (100 + $i)) -Encoding Ascii
}
for ($i = 0; $i -lt 10; $i++) { New-Item -ItemType Directory -Force (Join-Path $desk "Klasor $i") | Out-Null }
New-Item -ItemType Directory -Force (Join-Path $desk 'PDF') | Out-Null
for ($i = 0; $i -lt 20; $i++) { Set-Content -Path (Join-Path $desk ("PDF\Belge {0:D2}.pdf" -f $i)) -Value 'pdf' -Encoding Ascii }
for ($i = 0; $i -lt 5; $i++) { Set-Content -Path (Join-Path $desk "Site $i.url") -Value "[InternetShortcut]`r`nURL=https://example.com/$i" -Encoding Ascii }

# --- Widget'lar (sol monitör, fiziksel piksel; DIP = fiziksel / 1,25) ---
function W([string]$kind, [int]$px, [int]$py, [hashtable]$extra) {
    $w = [ordered]@{
        Id = [guid]::NewGuid().ToString('N'); Kind = $kind; PixelLeft = $px; PixelTop = $py
        Left = [math]::Round($px / 1.25, 2); Top = [math]::Round($py / 1.25, 2); Style = 'Glass'; Z = [int64](Get-Random -Minimum 1 -Maximum 1000000)
    }
    foreach ($k in $extra.Keys) { $w[$k] = $extra[$k] }
    return $w
}
$launchItems = @(Get-ChildItem $desk -File | Select-Object -First 6 | ForEach-Object FullName)
$widgets = @(
    (W 'Fence' -1915 5 @{ Filter = 'All'; Sort = 'Name'; Width = 400; Height = 470 }),
    (W 'Fence' -1370 5 @{ Filter = 'Files'; Sort = 'Name'; Width = 340; Height = 250 }),
    (W 'Fence' -1370 360 @{ Filter = 'Folders'; Sort = 'Name'; Width = 340; Height = 250 }),
    (W 'Fence' -900 5 @{ Filter = 'None'; FolderName = 'PDF'; Sort = 'Name'; Width = 340; Height = 250 }),
    (W 'Launcher' -900 360 @{ Width = 340; Height = 230; Tabs = @(@{ Name = 'Uygulamalar'; Items = $launchItems }, @{ Name = 'Dosyalar'; Items = @() }) }),
    (W 'Clock' -430 5 @{ ShowSeconds = $true }),
    (W 'Date' -430 260 @{}),
    (W 'Note' -1915 640 @{ Width = 300; Height = 220; NoteText = "Alisveris`r`n- sut`r`n- ekmek" }),
    (W 'Note' -1370 715 @{ Width = 300; Height = 200; NoteChecklist = $true; NoteColor = 'Green'; NoteText = "☐ bir`n☑ iki" })
)
$settings = [ordered]@{
    FirstRunDone = $true; RenameNoticeShown = $true; CloseToTrayHintShown = $true; Paused = $true
    DoubleClickHidesDesktop = $false; SuggestFolderIcons = $false; ShowNotifications = $false
    Hotkeys = @{ ToggleDesktop = ''; OrganizeNow = ''; OpenApp = ''; NewNote = ''; PeekWidgets = ''; QuickAdd = '' }
    Widgets = $widgets
}
[IO.File]::WriteAllText((Join-Path $data 'settings.json'), ($settings | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))

$iconsBefore = [PerfNative]::DesktopIconsState()
$psi = New-Object Diagnostics.ProcessStartInfo $Exe
$psi.Arguments = "--desktop `"$desk`" --data `"$data`" --minimized"
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['DUZENLEME_WINDOW_AT'] = '-1500,300'
$psi.EnvironmentVariables['DUZENLEME_QUICKADD_AT'] = '-1500,300'
$psi.EnvironmentVariables['DUZENLEME_PERF_LOG'] = (Join-Path $run 'perf.log')
$clock = [Diagnostics.Stopwatch]::StartNew()

function Close-Instance($process) {
    $ex = [Diagnostics.Process]::Start($Exe, "--desktop `"$desk`" --exit")
    $ex.WaitForExit(30000) | Out-Null
    if (-not $process.WaitForExit(20000)) { $process.Kill(); return $true }
    return $false
}

# --- Isınma açılışı: uygulama elle yazılmış ayarları bir kez kendi biçimiyle kaydeder (DIP yuvarlama vb.). Ölçülen
# açılış, kullanıcının her günkü açılışı gibi ikinci açılıştır. ---
$prime = [Diagnostics.Process]::Start($psi)
while ($clock.Elapsed.TotalSeconds -lt 90 -and [PerfNative]::WidgetWindows($prime.Id).Count -lt $widgets.Count) {
    if ($prime.HasExited) { throw "Test örneği erken kapandı (çıkış kodu $($prime.ExitCode)); Smart App Control engellemiş olabilir." }
    Start-Sleep -Milliseconds 50
}
Start-Sleep -Seconds 4
Close-Instance $prime | Out-Null
Remove-Item (Join-Path $run 'perf.log') -ErrorAction SilentlyContinue

# --- Ayar dosyası yazmalarını dışarıdan say (her sürümde aynı ölçüm: yerine konan settings.json) ---
$counter = New-Object WriteCounter $data, 'settings.json'
function Writes { return $counter.Count }

# --- Aç ---
$clock.Restart()
$p = [Diagnostics.Process]::Start($psi)
$hwnds = @()
$firstMs = -1
# İlk widget görünür görünmez arayüz iş parçacığı yoklanır: açılıştaki takılmalar da ölçülür.
$startPings = New-Object System.Collections.Generic.List[double]
while ($clock.Elapsed.TotalSeconds -lt 90) {
    $hwnds = [PerfNative]::WidgetWindows($p.Id)
    if ($hwnds.Count -gt 0) {
        if ($firstMs -lt 0) { $firstMs = $clock.Elapsed.TotalMilliseconds }
        $startPings.Add([PerfNative]::Ping($hwnds[0], 10000))
    }
    if ($hwnds.Count -ge $widgets.Count) { break }
    if ($p.HasExited) { throw "Test örneği erken kapandı (çıkış kodu $($p.ExitCode)); Smart App Control engellemiş olabilir." }
    Start-Sleep -Milliseconds 20
}
$allVisibleMs = $clock.Elapsed.TotalMilliseconds
$ui = [PerfNative]::UiThreadOf($hwnds[0])

function UiCpu {
    $p.Refresh()
    foreach ($t in $p.Threads) { if ($t.Id -eq $ui) { return $t.TotalProcessorTime.TotalMilliseconds } }
    return 0
}

# Açılış: arayüz iş parçacığı 500 ms boyunca (5 örnek) 100 ms'de 5 ms'den az çalışınca "boşta" sayılır.
$quiet = 0; $last = UiCpu; $idleMs = -1
while ($clock.Elapsed.TotalSeconds -lt 60) {
    $startPings.Add([PerfNative]::Ping($hwnds[0], 10000))
    Start-Sleep -Milliseconds 100
    $now = UiCpu
    if ($now - $last -lt 5) { $quiet++ } else { $quiet = 0 }
    $last = $now
    if ($quiet -ge 5) { $idleMs = $clock.Elapsed.TotalMilliseconds - 500; break }
}
Start-Sleep -Seconds 2
$startupWrites = Writes

# --- Senaryo ---
$p.Refresh()
$cpu0 = $p.TotalProcessorTime.TotalMilliseconds
$ui0 = UiCpu
$writes0 = Writes
$pings = New-Object System.Collections.Generic.List[double]
$m = [Diagnostics.Stopwatch]::StartNew()
$next = 0; $n = 0
$scenarioSeconds = if ($Scenario -eq 'startup') { 5 } else { $Seconds }
while ($m.Elapsed.TotalSeconds -lt $scenarioSeconds) {
    if ($m.Elapsed.TotalMilliseconds -ge $next) {
        switch ($Scenario) {
            'clicks' { [PerfNative]::MouseActivate($hwnds[$n % 2]); $next += 400 }
            'churn' {
                Add-Content -Path (Join-Path $desk 'indirme.crdownload') -Value ('y' * 4096) -Encoding Ascii
                if ($n % 5 -eq 4) { Set-Content -Path (Join-Path $desk ("Yeni {0:D3}.pdf" -f $n)) -Value 'n' -Encoding Ascii }
                $next += 1000
            }
            default { $next += 1000 }
        }
        $n++
    }
    $pings.Add([PerfNative]::Ping($hwnds[0], 10000))
    Start-Sleep -Milliseconds 100
}
$elapsed = $m.Elapsed.TotalSeconds
$p.Refresh()
$cpu = $p.TotalProcessorTime.TotalMilliseconds - $cpu0
$uiMs = (UiCpu) - $ui0
$scenarioWrites = (Writes) - $writes0

# --- Kapat ---
$killed = Close-Instance $p
Start-Sleep -Milliseconds 300
$totalWrites = Writes
$counter.Dispose()

function Stats($list) {
    $ok = @($list | Where-Object { $_ -ge 0 } | Sort-Object)
    if ($ok.Count -eq 0) { return 'yok' }
    $p99 = $ok[[math]::Min($ok.Count - 1, [math]::Floor($ok.Count * 0.99))]
    return ('n={0} medyan={1:0.0}ms p99={2:0.0}ms en_uzun={3:0.0}ms >50ms={4} >100ms={5}' -f $ok.Count, $ok[[math]::Floor($ok.Count / 2)], $p99, $ok[-1],
        @($ok | Where-Object { $_ -gt 50 }).Count, @($ok | Where-Object { $_ -gt 100 }).Count)
}
$perfLog = Join-Path $run 'perf.log'
$fenceUpdates = if (Test-Path $perfLog) { @(Select-String -Path $perfLog -Pattern 'bölme güncellendi' -Encoding UTF8).Count } else { $null }

$result = [pscustomobject]@{
    Name = $Name; Scenario = $Scenario; Exe = $Exe; DesktopFiles = $Files; Widgets = $widgets.Count
    FirstWidgetMs = [math]::Round($firstMs); AllWidgetsVisibleMs = [math]::Round($allVisibleMs); StartupIdleMs = [math]::Round($idleMs)
    StartupPings = (Stats $startPings); StartupSettingsWrites = $startupWrites
    ScenarioSeconds = [math]::Round($elapsed, 1)
    SettingsWrites = $scenarioWrites; SettingsWritesPerMinute = [math]::Round($scenarioWrites * 60 / $elapsed, 1)
    CpuPercentOfOneCore = [math]::Round($cpu / ($elapsed * 10), 2); UiThreadPercent = [math]::Round($uiMs / ($elapsed * 10), 2)
    Pings = (Stats $pings); FenceUpdatesLogged = $fenceUpdates; TotalSettingsWrites = $totalWrites; Killed = $killed
    RealDesktopIcons = "$iconsBefore -> $([PerfNative]::DesktopIconsState())"
}
$result | ConvertTo-Json | Set-Content (Join-Path $run 'result.json') -Encoding UTF8
$result
