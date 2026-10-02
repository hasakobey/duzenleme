<#
.SYNOPSIS
  NestDesk başarım ölçümü. Gerçek masaüstüne ve ayarlara dokunmaz. Kullanım, makine kurulumu ve eşikler: docs/perf.md
  (eşikler §4, sonuç tablosu §5).

.DESCRIPTION
  Geçici bir "masaüstü" ve veri klasörüyle bir test örneği açar (--desktop/--data), widget'ları sol test monitörüne
  (fiziksel x -1920..0, %125) yerleştirir, senaryoyu yalnızca pencere iletileriyle sürer ve --exit ile kapatır.
  Gerçek fare/klavye kullanılmaz. Test örneğinin ayarlarında bütün genel kısayollar boştur (PeekDesktop dahil): hiçbir
  gerçek tuş kapılmaz; WM_HOTKEY iletisi yine de işlenir. Kurulu NestDesk'e ve Windows masaüstü simgelerine dokunulmaz
  (başta ve sonda yalnızca okunur).

  Senaryolar:
    startup   açılış: bütün widget'lar görünene ve arayüz iş parçacığı boşalana dek geçen süre, en uzun takılma,
              açılışta ayar dosyası yazma sayısı (varsayılan 600 dosyalık masaüstü).
    clicks    iki widget'a sırayla tıklama (WM_MOUSEACTIVATE, 400 ms'de bir): dakikada ayar yazma sayısı.
    churn     indirme benzeri masaüstü değişikliği (saniyede bir .crdownload'a ekleme, 5 saniyede bir yeni PDF):
              arayüz iş parçacığı işlemcisi, takılmalar, bölme güncellemeleri.
    idle      boşta işlemci kullanımı.
    bigfence  1500 dosyalık masaüstü, tek "Tümü" bölmesi (600x800 DIP, orta simgeler): ilk boşa düşüş ve takılmalar.
    scale     -Widgets 1|5|15 karışık widget (bölme, saat, not, tarih, kutu sırayla): widget başına bellek, GDI/USER ve açılış.
    motion    6 widget (solma sınırının altında); 2 sn'de bir WM_HOTKEY id 1 (masaüstünü gizle/göster), -Seconds boyunca:
              arayüz iş parçacığı yükü, 100 ms'yi aşan takılmalar, sonrasında 5 sn boşta yük, Motion.* sayaçları
              (destekleyen sürümde). Canlı / Sakin / Kapalı: -Set CalmMotion=true ya da -Set AnimationsOff=true.
    ring      standart 9 widget + bir halka menü widget'ı; test örneğine NESTDESK_TEST_RING=1 (yalnızca --desktop ile geçerli:
              halka yolu kısayol kaydedilmeden açılır, gerçek tuş kapılmaz) ve NESTDESK_RING_AT (test monitörünün ortası)
              verilir. -Seconds boyunca (varsayılan 20) 600 ms'de bir WM_HOTKEY id 8 (aç / kapat): kısayol → ilk kare süresi
              ("mark Ring.hotkey" → "mark Ring.firstFrame"; ilk açılış soğuk, sonrakiler sıcak: ortanca ve p95), açılışlar
              boyunca bellek artışı (başta ve sonda). -RingOff: aynı düzen, NESTDESK_TEST_RING verilmez (açılır halka hiç
              kurulmaz; iletiler yok sayılır): iki çalıştırmanın MemoryAtStartupIdle farkı halkanın kalıcı maliyetidir.

  Her senaryoda bellek ölçütleri (PrivateBytes, WorkingSet, tepe WorkingSet, tutamaç, iş parçacığı, GDI ve USER
  nesneleri) açılışta boşa düşünce ve senaryo sonunda alınır. NESTDESK_PERF_LOG'daki açılış işaretleri ("mark ...") ve
  çıkıştaki yönetilen yığın (gcHeapKB...) destekleyen sürümde sonuca eklenir; eski sürümde boş kalır.

  Eşikler: docs/perf.md §4 (boşta < %0,5 çekirdek; churn'de arayüz iş parçacığı < %5 ve 100 ms'yi aşan takılma yok;
  600 ve 1500 dosyalık masaüstünde ilk boşa düşüş < 3 sn ve 250 ms'yi aşan takılma yok; bellek 2.1.1'e göre en çok +%10;
  motion ve ring bütçeleri).

  Sonuç <Out>\<Name>\result.json'a yazılır ve ekrana basılır. Ölçümler sırayla çalıştırılır (aynı anda iki test örneği
  açma); her senaryoyu üç kez çalıştırıp ortancayı al.

.PARAMETER Set
  Test örneğinin settings.json'ına eklenecek ayarlar, "Ad=değer" (değer JSON olarak okunur: true, 5, "metin"); virgülle
  ya da birden çok kez. Ör. -Set CalmMotion=true

.EXAMPLE
  powershell -File tools\perf\perf-run.ps1 -Exe C:\perf\2.2\NestDesk.exe -Scenario startup
  (-Exe: yayın çıktısı ya da taşınabilir zip'ten çıkarılmış klasör; taban 2.1.1 de öyle ölçülür, bkz. docs/perf.md §1)
.EXAMPLE
  powershell -File tools\perf\perf-run.ps1 -Exe <exe> -Scenario scale -Widgets 15 -Name scale-15
.EXAMPLE
  powershell -File tools\perf\perf-run.ps1 -Exe <exe> -Scenario motion -Name motion-sakin -Set CalmMotion=true
#>
param(
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [ValidateSet('startup', 'clicks', 'churn', 'idle', 'bigfence', 'scale', 'motion', 'ring')] [string] $Scenario,
    [string] $Name = $Scenario,
    [int] $Files = -1,
    [int] $Seconds = 40,
    [ValidateSet(1, 5, 15)] [int] $Widgets = 5,
    [string[]] $Set = @(),
    [switch] $RingOff,
    [string] $Out = (Join-Path $env:TEMP 'nestdesk-perf')
)
$ErrorActionPreference = 'Stop'
if (-not ('PerfNative' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'PerfNative.cs') }
$Exe = (Resolve-Path $Exe).Path
# Göreli -Out PowerShell'in klasörüne göre çözülür: .NET çağrıları ([IO.File]) ve exe'ye verilen --desktop/--data
# sürecin kendi klasörünü kullanırdı.
$Out = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
if ($Files -lt 0) {
    $Files = switch ($Scenario) { 'startup' { 600 } 'bigfence' { 1500 } default { 60 } }
}
if ($Scenario -ne 'scale' -and $PSBoundParameters.ContainsKey('Widgets')) { Write-Warning '-Widgets yalnızca scale senaryosunda kullanılır.' }
if ($Scenario -ne 'ring' -and $RingOff) { Write-Warning '-RingOff yalnızca ring senaryosunda kullanılır.' }

$run = Join-Path $Out $Name
if (Test-Path $run) { Remove-Item -Recurse -Force $run }
$desk = Join-Path $run 'desktop'; $data = Join-Path $run 'data'
New-Item -ItemType Directory -Force $desk, $data | Out-Null

# --- Test masaüstü ---
$exts = 'pdf', 'docx', 'txt', 'jpg', 'png', 'zip', 'mp3', 'xlsx', 'pptx', 'mp4'
for ($i = 0; $i -lt $Files; $i++) {
    [IO.File]::WriteAllText((Join-Path $desk ("Dosya {0:D3}.{1}" -f $i, $exts[$i % $exts.Count])), ('x' * (100 + $i)))
}
for ($i = 0; $i -lt 10; $i++) { New-Item -ItemType Directory -Force (Join-Path $desk "Klasor $i") | Out-Null }
New-Item -ItemType Directory -Force (Join-Path $desk 'PDF') | Out-Null
for ($i = 0; $i -lt 20; $i++) { [IO.File]::WriteAllText((Join-Path $desk ("PDF\Belge {0:D2}.pdf" -f $i)), 'pdf') }
for ($i = 0; $i -lt 5; $i++) { [IO.File]::WriteAllText((Join-Path $desk "Site $i.url"), "[InternetShortcut]`r`nURL=https://example.com/$i") }

# --- Widget'lar (sol monitör, fiziksel piksel; DIP = fiziksel / 1,25) ---
# Z sırası sabit (önce eklenen önde ve önce açılır): her çalıştırmada aynı açılış sırası.
$script:order = 0
function W([string]$kind, [int]$px, [int]$py, [hashtable]$extra) {
    $script:order++
    $w = [ordered]@{
        Id = [guid]::NewGuid().ToString('N'); Kind = $kind; PixelLeft = $px; PixelTop = $py
        Left = [math]::Round($px / 1.25, 2); Top = [math]::Round($py / 1.25, 2); Style = 'Glass'; Z = [int64](1000000 - $script:order)
    }
    foreach ($k in $extra.Keys) { $w[$k] = $extra[$k] }
    return $w
}
$launchItems = @(Get-ChildItem $desk -File | Select-Object -First 6 | ForEach-Object FullName)

function StandardSet {
    return @(
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
}

# scale: 5 sütunlu ızgara (383 x 345 fiziksel piksel hücre), türler sırayla; saniyeli saat yok (boşta yükü bozmasın).
function ScaleSet([int]$count) {
    $list = New-Object System.Collections.Generic.List[object]
    $filters = 'Files', 'Folders', 'All'
    for ($i = 0; $i -lt $count; $i++) {
        $row = [math]::Floor($i / 5)
        $px = -1915 + ($i % 5) * 383; $py = 5 + $row * 345
        switch ($i % 5) {
            0 { $list.Add((W 'Fence' $px $py @{ Filter = $filters[$row % 3]; Sort = 'Name'; Width = 280; Height = 240 })) }
            1 { $list.Add((W 'Clock' $px $py @{})) }
            2 { $list.Add((W 'Note' $px $py @{ Width = 260; Height = 200; NoteText = "Not $i" })) }
            3 { $list.Add((W 'Date' $px $py @{})) }
            4 { $list.Add((W 'Launcher' $px $py @{ Width = 280; Height = 220; Tabs = @(@{ Name = 'Uygulamalar'; Items = $launchItems }) })) }
        }
    }
    return $list.ToArray()
}

switch ($Scenario) {
    'bigfence' { $layout = @((W 'Fence' -1915 5 @{ Filter = 'All'; Sort = 'Name'; IconSize = 'Medium'; Width = 600; Height = 800 })) }
    'scale' { $layout = @(ScaleSet $Widgets) }
    'motion' { $layout = @(StandardSet)[0..5] }
    # Halka: öğeler masaüstünün ilk dosyaları ve bir web adresi (yalnızca açılıp kapanır; hiçbir öğe seçilmez).
    'ring' {
        $ringItems = @($launchItems | Select-Object -First 5)
        $layout = @(StandardSet) + @((W 'Launcher' -430 520 @{
                    Variant = 'ring'; Title = 'Halka menü'
                    Tabs = @(@{ Name = 'Uygulamalar'; Items = $ringItems
                            Actions = @(@{ Id = 'a1b2c3d4'; Type = 'url'; Url = 'https://example.com'; PathsBefore = $ringItems.Count }) },
                        @{ Name = 'Belgeler'; Items = @($launchItems | Select-Object -Skip 5 -First 1) })
                }))
    }
    default { $layout = @(StandardSet) }
}

$settings = [ordered]@{
    FirstRunDone = $true; RenameNoticeShown = $true; CloseToTrayHintShown = $true; Paused = $true
    DoubleClickHidesDesktop = $false; SuggestFolderIcons = $false; ShowNotifications = $false
    # Hiçbir genel kısayol kaydedilmez (test örneği kullanıcının Ctrl+Alt+... tuşlarını kapmasın); WM_HOTKEY yine işlenir.
    Hotkeys = [ordered]@{ ToggleDesktop = ''; OrganizeNow = ''; OpenApp = ''; NewNote = ''; PeekWidgets = ''; QuickAdd = ''; PeekDesktop = '' }
    Widgets = $layout
}
foreach ($pair in @($Set | ForEach-Object { $_ -split ',' } | Where-Object { $_ -and $_.Contains('=') })) {
    $key, $text = $pair -split '=', 2
    try { $value = $text | ConvertFrom-Json } catch { $value = $text }
    $settings[$key.Trim()] = $value
}
[IO.File]::WriteAllText((Join-Path $data 'settings.json'), ($settings | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))

$iconsBefore = [PerfNative]::DesktopIconsState()
$psi = New-Object Diagnostics.ProcessStartInfo $Exe
$psi.Arguments = "--desktop `"$desk`" --data `"$data`" --minimized"
$psi.UseShellExecute = $false
# Kabuktan kalmış test değişkenleri ölçümü bozmasın (uygulama önce NESTDESK_, yoksa eski DUZENLEME_ adını okur):
# DEBUGLOG her satırda diske yazar, LANG metinleri değiştirir, TEST_DOUBLECLICK fare girdisi dinler.
foreach ($stale in 'TEST_DOUBLECLICK', 'TEST_RING', 'DEBUGLOG', 'LANG', 'NEWWIDGET_AT', 'PEEK_AT', 'RING_AT', 'APPDATA_ROOT', 'PERF_LOG', 'WINDOW_AT', 'QUICKADD_AT') {
    $psi.EnvironmentVariables.Remove("NESTDESK_$stale")
    $psi.EnvironmentVariables.Remove("DUZENLEME_$stale")
}
$psi.EnvironmentVariables['NESTDESK_WINDOW_AT'] = '-1500,300'
$psi.EnvironmentVariables['NESTDESK_QUICKADD_AT'] = '-1500,300'
$psi.EnvironmentVariables['NESTDESK_PERF_LOG'] = (Join-Path $run 'perf.log')
if ($Scenario -eq 'ring') {
    # Halka test monitörünün ortasında açılır; kısayol kaydedilmez (WM_HOTKEY id 8 yalnızca bu test örneğine gönderilir).
    $psi.EnvironmentVariables['NESTDESK_RING_AT'] = '-960,540'
    if (-not $RingOff) { $psi.EnvironmentVariables['NESTDESK_TEST_RING'] = '1' }
}
$clock = [Diagnostics.Stopwatch]::StartNew()

function Close-Instance($process) {
    $ex = [Diagnostics.Process]::Start($Exe, "--desktop `"$desk`" --exit")
    $ex.WaitForExit(30000) | Out-Null
    if (-not $process.WaitForExit(20000)) { $process.Kill(); return $true }
    return $false
}

# --- Isınma açılışı: uygulama elle yazılmış ayarları bir kez kendi biçimiyle kaydeder (DIP yuvarlama, 2.2'de sürüm
# damgası ve sürüm yedeği). Ölçülen açılış, kullanıcının her günkü açılışı gibi ikinci açılıştır. ---
$prime = [Diagnostics.Process]::Start($psi)
while ($clock.Elapsed.TotalSeconds -lt 90 -and [PerfNative]::WidgetWindows($prime.Id).Count -lt $layout.Count) {
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
    if ($hwnds.Count -ge $layout.Count) { break }
    if ($p.HasExited) { throw "Test örneği erken kapandı (çıkış kodu $($p.ExitCode)); Smart App Control engellemiş olabilir." }
    Start-Sleep -Milliseconds 20
}
$allVisibleMs = $clock.Elapsed.TotalMilliseconds
if ($hwnds.Count -eq 0) { Close-Instance $p | Out-Null; throw '90 saniyede hiçbir widget görünmedi.' }
$ui = [PerfNative]::UiThreadOf($hwnds[0])

function UiCpu {
    $p.Refresh()
    foreach ($t in $p.Threads) { if ($t.Id -eq $ui) { return $t.TotalProcessorTime.TotalMilliseconds } }
    return 0
}

# Süreç belleği ve pencere nesneleri (dışarıdan; her sürümde aynı ölçüm).
function Memory {
    $p.Refresh()
    return [ordered]@{
        PrivateMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
        PeakWorkingSetMB = [math]::Round($p.PeakWorkingSet64 / 1MB, 1)
        Handles = $p.HandleCount
        Threads = $p.Threads.Count
        GdiObjects = [PerfNative]::GuiResources($p.Id, [PerfNative]::GdiObjects)
        UserObjects = [PerfNative]::GuiResources($p.Id, [PerfNative]::UserObjects)
        GdiObjectsPeak = [PerfNative]::GuiResources($p.Id, [PerfNative]::GdiObjectsPeak)
        UserObjectsPeak = [PerfNative]::GuiResources($p.Id, [PerfNative]::UserObjectsPeak)
    }
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
$memoryIdle = Memory

# --- Senaryo ---
$p.Refresh()
$cpu0 = $p.TotalProcessorTime.TotalMilliseconds
$ui0 = UiCpu
$writes0 = Writes
$pings = New-Object System.Collections.Generic.List[double]
$m = [Diagnostics.Stopwatch]::StartNew()
$next = 0; $n = 0; $toggles = 0; $hotkeyMisses = 0; $ringPosts = 0
$scenarioSeconds = switch ($Scenario) {
    'startup' { 5 }
    'bigfence' { 5 }
    'scale' { if ($PSBoundParameters.ContainsKey('Seconds')) { $Seconds } else { 10 } }
    'ring' { if ($PSBoundParameters.ContainsKey('Seconds')) { $Seconds } else { 20 } }
    default { $Seconds }
}
while ($m.Elapsed.TotalSeconds -lt $scenarioSeconds) {
    if ($m.Elapsed.TotalMilliseconds -ge $next) {
        switch ($Scenario) {
            'clicks' { [PerfNative]::MouseActivate($hwnds[$n % 2]); $next += 400 }
            'churn' {
                Add-Content -Path (Join-Path $desk 'indirme.crdownload') -Value ('y' * 4096) -Encoding Ascii
                if ($n % 5 -eq 4) { Set-Content -Path (Join-Path $desk ("Yeni {0:D3}.pdf" -f $n)) -Value 'n' -Encoding Ascii }
                $next += 1000
            }
            'motion' {
                # id 1 = HotkeyAction.ToggleDesktop + 1: widget'lar (simgelerle birlikte; test örneği gerçek simgelere dokunmaz) gizlenir/görünür.
                if ([PerfNative]::PostHotkey($p.Id, 1)) { $toggles++ } else { $hotkeyMisses++ }
                $next += 2000
            }
            'ring' {
                # id 8 = HotkeyAction.Ring + 1: sırayla aç, kapat (açık kalma süresi 600 ms).
                if ([PerfNative]::PostHotkey($p.Id, 8)) { $ringPosts++ } else { $hotkeyMisses++ }
                $next += 600
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

# motion: widget'lar görünür bırakılır, geçişler bittikten sonra 5 sn boşta yük ölçülür.
$afterCpu = $null; $afterUi = $null; $afterPings = New-Object System.Collections.Generic.List[double]
if ($Scenario -eq 'motion') {
    if ($toggles % 2 -eq 1) { [PerfNative]::PostHotkey($p.Id, 1) | Out-Null }
    Start-Sleep -Milliseconds 1000
    $p.Refresh()
    $afterCpu0 = $p.TotalProcessorTime.TotalMilliseconds; $afterUi0 = UiCpu
    $after = [Diagnostics.Stopwatch]::StartNew()
    while ($after.Elapsed.TotalSeconds -lt 5) {
        $afterPings.Add([PerfNative]::Ping($hwnds[0], 10000))
        Start-Sleep -Milliseconds 100
    }
    $afterSeconds = $after.Elapsed.TotalSeconds
    $p.Refresh()
    $afterCpu = [math]::Round(($p.TotalProcessorTime.TotalMilliseconds - $afterCpu0) / ($afterSeconds * 10), 2)
    $afterUi = [math]::Round(((UiCpu) - $afterUi0) / ($afterSeconds * 10), 2)
}
if ($Scenario -eq 'ring') {
    # Açık kaldıysa kapat; bellek kapalı halkayla ölçülür.
    if ($ringPosts % 2 -eq 1) { [PerfNative]::PostHotkey($p.Id, 8) | Out-Null }
    Start-Sleep -Milliseconds 1500
}
$memoryEnd = Memory

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

function Longest($list) {
    $ok = @($list | Where-Object { $_ -ge 0 } | Sort-Object)
    if ($ok.Count -eq 0) { return $null }
    return [math]::Round($ok[-1], 1)
}

# --- Başarım günlüğü (NESTDESK_PERF_LOG; destekleyen sürümde) ---
$perfLog = Join-Path $run 'perf.log'
$fenceUpdates = $null
$marks = [ordered]@{}
$markDetails = [ordered]@{}
$gcAtExit = [ordered]@{}
$motionCounters = [ordered]@{}
$motionEvents = [ordered]@{}
$ringLatencies = New-Object System.Collections.Generic.List[double]
if (Test-Path $perfLog) {
    $fenceUpdates = @(Select-String -Path $perfLog -Pattern 'bölme güncellendi' -Encoding UTF8).Count
    # Her işaretin ilki geçerli (asıl örneğin açılışı); ayrıntı (load=, widgets=, açılıştaki gc...) ayrıca.
    foreach ($hit in @(Select-String -Path $perfLog -Pattern 'mark (\S+) (-?\d+) ms(?: (.+))?$' -Encoding UTF8)) {
        $groups = $hit.Matches[0].Groups
        $markName = $groups[1].Value
        if ($marks.Contains($markName)) { continue }
        $marks[$markName] = [int]$groups[2].Value
        if ($groups[3].Success) { $markDetails[$markName] = $groups[3].Value.Trim() }
    }
    # Çıkıştaki sayaç satırı (PerfLog.Summary): sayaçlar ve yönetilen yığın; son satır geçerli. "mark ..." satırları
    # (RestoreAll.end açılıştaki yığını taşır) sayılmaz: süreç öldürüldüyse (Killed) GcAtExit boş kalır.
    $summary = @(Select-String -Path $perfLog -Pattern 'gcHeapKB=' -Encoding UTF8 |
        Where-Object { $_.Line -notmatch '\bmark \S+ -?\d+ ms' }) | Select-Object -Last 1
    if ($summary) {
        foreach ($kv in [regex]::Matches($summary.Line, '\b(gc\w+|managedKB)=(\d+)')) { $gcAtExit[$kv.Groups[1].Value] = [long]$kv.Groups[2].Value }
    }
    # Halka (2.2): her açılış bir "Ring.hotkey" ve ardından bir "Ring.firstFrame" işareti yazar; sırayla eşlenir.
    if ($Scenario -eq 'ring') {
        $pending = $null
        foreach ($hit in @(Select-String -Path $perfLog -Pattern 'mark (Ring\.hotkey|Ring\.firstFrame) (-?\d+) ms' -Encoding UTF8)) {
            $groups = $hit.Matches[0].Groups
            if ($groups[1].Value -eq 'Ring.hotkey') { $pending = [double]$groups[2].Value }
            elseif ($null -ne $pending) { $ringLatencies.Add([double]$groups[2].Value - $pending); $pending = $null }
        }
    }
    # Hareket (2.2): sayaçlar "Motion.X=n" (son değer geçerli), tek tek yazılan olaylar "Motion.X" (sayılır).
    foreach ($line in [IO.File]::ReadAllLines($perfLog)) {
        if (-not $line.Contains('Motion.')) { continue }
        foreach ($kv in [regex]::Matches($line, '\b(Motion(?:\.\w+)+)=(\d+)')) { $motionCounters[$kv.Groups[1].Value] = [long]$kv.Groups[2].Value }
        foreach ($kv in [regex]::Matches($line, '\b(Motion(?:\.\w+)+)(?![\w.=])')) {
            $key = $kv.Groups[1].Value
            $motionEvents[$key] = 1 + $(if ($motionEvents.Contains($key)) { $motionEvents[$key] } else { 0 })
        }
    }
}
$exeVersion = $null
try { $exeVersion = (Get-Item $Exe).VersionInfo.ProductVersion } catch { }
# Sürüm numarası geliştirme derlemelerinde değişmez (bir dal sürüm numarası artmadan önceki sürümün numarasını taşır): yönetilen DLL'in özeti ve
# yazılma zamanı hangi derlemenin ölçüldüğünü ayırır (docs/perf.md §5'e sürümle birlikte yazılır).
$exeBuild = $null
foreach ($dllName in 'NestDesk.dll', 'Duzenleme.dll') {
    $dll = Join-Path (Split-Path $Exe) $dllName
    if (Test-Path $dll) {
        try {
            $exeBuild = '{0} sha256:{1} {2:yyyy-MM-dd HH:mm}' -f $dllName, (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 12).ToLowerInvariant(),
                (Get-Item $dll).LastWriteTime
        } catch { }
        break
    }
}

# Halka: ilk açılış soğuk (pencere önceden kurulu ama ilk kez gösteriliyor), sonrakiler sıcak.
$ringWarm = @($ringLatencies | Select-Object -Skip 1 | Sort-Object)
function Pct($sorted, [double]$q) { if ($sorted.Count -eq 0) { return $null }; return $sorted[[math]::Min($sorted.Count - 1, [math]::Floor($sorted.Count * $q))] }
$ringMemory = $null
if ($Scenario -eq 'ring') {
    $ringMemory = [ordered]@{
        PrivateMB = [math]::Round($memoryEnd.PrivateMB - $memoryIdle.PrivateMB, 1)
        WorkingSetMB = [math]::Round($memoryEnd.WorkingSetMB - $memoryIdle.WorkingSetMB, 1)
        GdiObjects = $memoryEnd.GdiObjects - $memoryIdle.GdiObjects
        UserObjects = $memoryEnd.UserObjects - $memoryIdle.UserObjects
    }
}

$result = [pscustomobject]@{
    Name = $Name; Scenario = $Scenario; Exe = $Exe; ExeVersion = $exeVersion; ExeBuild = $exeBuild; Settings = ($Set -join ',')
    DesktopFiles = $Files; Widgets = $layout.Count
    FirstWidgetMs = [math]::Round($firstMs); AllWidgetsVisibleMs = [math]::Round($allVisibleMs); StartupIdleMs = [math]::Round($idleMs)
    StartupPings = (Stats $startPings); StartupLongestPingMs = (Longest $startPings); StartupSettingsWrites = $startupWrites
    ScenarioSeconds = [math]::Round($elapsed, 1)
    SettingsWrites = $scenarioWrites; SettingsWritesPerMinute = [math]::Round($scenarioWrites * 60 / $elapsed, 1)
    CpuPercentOfOneCore = [math]::Round($cpu / ($elapsed * 10), 2); UiThreadPercent = [math]::Round($uiMs / ($elapsed * 10), 2)
    Pings = (Stats $pings); LongestPingMs = (Longest $pings)
    MemoryAtStartupIdle = $memoryIdle; MemoryAtEnd = $memoryEnd
    Toggles = $(if ($Scenario -eq 'motion') { $toggles } else { $null })
    UiMsPerToggle = $(if ($Scenario -eq 'motion' -and $toggles -gt 0) { [math]::Round($uiMs / $toggles, 1) } else { $null })
    HotkeyMisses = $(if ($Scenario -in @('motion', 'ring')) { $hotkeyMisses } else { $null })
    RingOff = $(if ($Scenario -eq 'ring') { [bool]$RingOff } else { $null })
    RingOpens = $(if ($Scenario -eq 'ring') { $ringLatencies.Count } else { $null })
    RingColdMs = $(if ($ringLatencies.Count -gt 0) { [math]::Round($ringLatencies[0], 1) } else { $null })
    RingWarmMedianMs = (Pct $ringWarm 0.5); RingWarmP95Ms = (Pct $ringWarm 0.95)
    RingMemoryDelta = $ringMemory
    AfterCpuPercentOfOneCore = $afterCpu; AfterUiThreadPercent = $afterUi
    AfterPings = $(if ($Scenario -eq 'motion') { Stats $afterPings } else { $null })
    MotionCounters = $motionCounters; MotionEvents = $motionEvents
    Marks = $marks; MarkDetails = $markDetails; GcAtExit = $gcAtExit
    FenceUpdatesLogged = $fenceUpdates; TotalSettingsWrites = $totalWrites; Killed = $killed
    RealDesktopIcons = "$iconsBefore -> $([PerfNative]::DesktopIconsState())"
}
$result | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $run 'result.json') -Encoding UTF8
if (($Scenario -in @('motion', 'ring')) -and $hotkeyMisses -gt 0) { Write-Warning "Kısayol penceresi (DuzenlemeHotkeys) $hotkeyMisses kez bulunamadı." }
if ($Scenario -eq 'ring' -and -not $RingOff -and $ringLatencies.Count -eq 0) { Write-Warning 'Halka hiç açılmadı (Ring.hotkey işareti yok): sürüm halka menüyü desteklemiyor olabilir.' }
$result
