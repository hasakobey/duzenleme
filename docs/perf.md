# NestDesk başarım ölçümü

`tools/perf/perf-run.ps1` NestDesk'in açılışını, boşta yükünü, tepkisini ve belleğini ölçer. Ölçüm yalnızca bir **test
örneğinde** (`--desktop <geçici> --data <geçici>`) yapılır: gerçek masaüstüne, gerçek ayarlara, kayıt defterindeki Run
değerine ve kullanıcının genel kısayollarına dokunulmaz. Betik senaryoyu yalnızca pencere iletileriyle sürer; gerçek
fare ve klavye kullanılmaz.

Bu belge betiğin başlığından (`perf-run.ps1:1`) ve CLAUDE.md'deki "Kayıt ve akıcılık" bölümünden bağlanır. Eşikler
**§4**'te, sonuçlar **§5**'te.

## 1. Nasıl çalıştırılır

```powershell
# Windows PowerShell 5.1 (powershell.exe). PerfNative.cs Add-Type ile derlenir.
powershell -NoProfile -File tools\perf\perf-run.ps1 -Exe <NestDesk.exe> -Scenario startup
powershell -NoProfile -File tools\perf\perf-run.ps1 -Exe <NestDesk.exe> -Scenario scale -Widgets 15 -Name scale-15
powershell -NoProfile -File tools\perf\perf-run.ps1 -Exe <NestDesk.exe> -Scenario motion -Name motion-sakin -Set CalmMotion=true
powershell -NoProfile -File tools\perf\perf-run.ps1 -Exe <NestDesk.exe> -Scenario ring
powershell -NoProfile -File tools\perf\perf-run.ps1 -Exe <NestDesk.exe> -Scenario ring -RingOff -Name ring-off
```

| Parametre | Varsayılan | Anlamı |
|---|---|---|
| `-Exe` | (zorunlu) | Ölçülecek `NestDesk.exe` |
| `-Scenario` | (zorunlu) | `startup`, `clicks`, `churn`, `idle`, `bigfence`, `scale`, `motion`, `ring` (§2) |
| `-Name` | senaryo adı | Çıktı klasörünün adı (`-Out\<Name>`); aynı senaryoyu farklı ayarla ölçerken değiştir |
| `-Files` | 600 (`startup`), 1500 (`bigfence`), 60 (diğerleri) | Test masaüstündeki dosya sayısı |
| `-Seconds` | 40 (`scale`: 10, `ring`: 20) | Senaryo süresi (`startup` ve `bigfence` hep 5 sn) |
| `-Widgets` | 5 | Yalnızca `scale`: 1, 5 ya da 15 widget |
| `-RingOff` | kapalı | Yalnızca `ring`: aynı düzen, `NESTDESK_TEST_RING` verilmez (açılır halka kurulmaz); kalıcı maliyetin tabanı |
| `-Set` | yok | Test örneğinin `settings.json`'ına eklenecek ayarlar: `Ad=değer` (değer JSON: `true`, `5`, `"metin"`), virgülle ayrılır |
| `-Out` | `%TEMP%\nestdesk-perf` | Çıktı kökü (göreli yol PowerShell'in o anki klasörüne göre çözülür) |

**Çıktı:** `<Out>\<Name>\result.json` (ekrana da basılır), `perf.log` (NESTDESK_PERF_LOG), `desktop\` (test masaüstü),
`data\` (test verisi). Klasör her çalıştırmada silinip yeniden kurulur.

**Ne yapar:**
1. Test masaüstünü kurar (dosyalar, 10 klasör, `PDF` klasörü, 5 `.url`) ve senaryonun widget'larıyla `settings.json` yazar.
   Ayarlarda bütün genel kısayollar boştur (`PeekDesktop` dahil): test örneği gerçek tuşları kapmaz. `WM_HOTKEY` iletisi
   kayıtlı olmasa da işlenir (`HotkeyManager.WndProc`).
2. **Isınma açılışı:** örnek bir kez açılıp kapatılır; uygulama elle yazılmış ayarları kendi biçimiyle kaydeder (2.2'de
   sürüm damgası ve sürüm yedeği de burada olur). Ölçülen açılış ikincisidir: kullanıcının her günkü açılışı.
3. Açılışı ölçer: ilk widget, bütün widget'lar görünür, arayüz iş parçacığı boşa düştü (500 ms boyunca 100 ms'de 5 ms'den
   az çalışma). Bu sırada arayüz iş parçacığı `WM_NULL` ile yoklanır (takılmalar).
4. Boşa düşünce bellek ölçütlerini alır, senaryoyu çalıştırır, sonda bellek ölçütlerini yeniden alır.
5. `--desktop <aynı> --exit` ile kapatır; `perf.log`'dan işaretleri, GC yığınını ve Motion sayaçlarını okur.

**Kurallar**
- Ölçümleri **sırayla** çalıştır; aynı anda iki test örneği açma.
- Her senaryoyu **üç kez** çalıştır, **ortancayı** yaz.
- Taban (2.1.1) ve yeni sürüm aynı derleme türüyle ölçülür: ikisi de yayın çıktısı (`publish.ps1`'in R2R, kendi içinde
  taşınabilir zip'i). 2.1.1 tabanı için `dist\NestDesk-2.1.1-x64-portable.zip`'i geçici bir klasöre çıkar. Debug
  derlemesiyle yayın derlemesi karşılaştırılmaz.
- Sonuçtaki `RealDesktopIcons` alanı başta ve sonda aynı olmalı (`VISIBLE -> VISIBLE`): gerçek masaüstüne dokunulmadığının
  denetimi.
- Betik, kabuktan kalmış `NESTDESK_*`/`DUZENLEME_*` test değişkenlerini (`DEBUGLOG`, `LANG`, `TEST_DOUBLECLICK`, `*_AT`,
  `APPDATA_ROOT`) test örneğine geçirmez; ölçüm sırasında DEBUGLOG istiyorsan betiği düzenle (yazma maliyeti sonucu bozar).
- Smart App Control yeni, imzasız bir derlemeyi engelleyebilir: "Test örneği erken kapandı" görürsen yeniden derle
  (`--no-incremental`) ve tekrar dene.

## 2. Senaryolar

| Senaryo | Masaüstü | Widget'lar | Senaryo adımı | Başlıca sonuçlar |
|---|---|---|---|---|
| `startup` | 600 dosya | Standart 9 widget¹ | 5 sn boşta yoklama | `StartupIdleMs`, `StartupLongestPingMs`, `StartupSettingsWrites`, bellek |
| `clicks` | 60 | Standart 9 | İki widget'a sırayla `WM_MOUSEACTIVATE`, 400 ms'de bir | `SettingsWritesPerMinute`, `UiThreadPercent` |
| `churn` | 60 | Standart 9 | Saniyede bir `.crdownload`'a ekleme, 5 sn'de bir yeni PDF | `UiThreadPercent`, `LongestPingMs`, `FenceUpdatesLogged` |
| `idle` | 60 | Standart 9 | Hiçbir şey | `CpuPercentOfOneCore` |
| `bigfence` | 1500 dosya | Tek "Tümü" bölmesi, 600×800 DIP, orta simgeler | 5 sn boşta yoklama | `StartupIdleMs`, `StartupLongestPingMs`, `LongestPingMs`, bellek, GDI/USER |
| `scale` | 60 | `-Widgets` 1/5/15: bölme, saat, not, tarih, kutu sırayla (5 sütunlu ızgara) | 10 sn boşta | Bellek, GDI/USER, `AllWidgetsVisibleMs` (widget başına maliyet §4) |
| `motion` | 60 | Standart 9'un ilk 6'sı (solma sınırı 8'in altında) | 2 sn'de bir `WM_HOTKEY` id 1 (masaüstünü gizle/göster), `-Seconds` boyunca; sonra widget'lar görünür bırakılıp 5 sn boşta | `UiThreadPercent`, `UiMsPerToggle`, `LongestPingMs`, `AfterCpuPercentOfOneCore`, `MotionCounters`, GDI/USER |
| `ring` | 60 | Standart 9 + halka menü widget'ı (5 yol, 1 web adresi, 1 alt halka) | `NESTDESK_TEST_RING=1` ve `NESTDESK_RING_AT=-960,540` ile açılır; 600 ms'de bir `WM_HOTKEY` id 8 (aç / kapat), `-Seconds` boyunca (varsayılan 20); hiçbir öğe seçilmez | `RingColdMs`, `RingWarmMedianMs`, `RingWarmP95Ms`, `RingOpens`, `RingMemoryDelta`; `-RingOff` ile `MemoryAtStartupIdle` farkı = kalıcı maliyet |

¹ Standart 9: "Tümü" bölmesi 400×470, "Dosyalar" ve "Klasörler" bölmeleri, `PDF` klasör bölmesi, iki sekmeli kısayol
kutusu, saniyeli saat, tarih, not ve yapılacaklar listesi.

**Hareket düzeyleri:** `motion` aynı makinede üç kez çalıştırılır:
`-Name motion-canli` (varsayılan), `-Name motion-sakin -Set CalmMotion=true`, `-Name motion-kapali -Set AnimationsOff=true`.
Windows'un "Animasyon efektleri" açık olmalı (kapalıysa üçü de "Kapalı" ölçer).

**Halka:** `NESTDESK_TEST_RING=1` yalnızca test masaüstünde (`--desktop`) geçerlidir: halka kısayolu atanmamışken de
`WM_HOTKEY` id 8 işlenir ve açılır halka boşta önceden kurulur; genel kısayol kaydedilmez (gerçek tuş kapılmaz). Her açılış
`perf.log`'a `mark Ring.hotkey` (iletinin işlenmeye başladığı an) ve `mark Ring.firstFrame` (gösterildikten sonraki ilk kare)
yazar; betik bunları sırayla eşler. Not: `Motion.RingOpen` saydamlıktan başladığından (Canlı/Sakin) ilk kare henüz neredeyse
saydamdır; ölçülen süre "pencere ekranda ve çizildi" anıdır, halkanın tam görünmesi buna en çok açılış hareketinin süresini
(≤ 200 ms) ekler. Kullanıcının gördüğü açılışı karşılaştırmak için `-Set AnimationsOff=true` ile de çalıştır (ilk kare tam görünür).
İlk açılış **soğuk** sayılır (pencere önceden kurulu ama ilk kez gösteriliyor), sonrakiler
**sıcak**. Kalıcı maliyet: `ring` ile `ring -RingOff` çalıştırmalarının `MemoryAtStartupIdle.PrivateMB` farkı (açılır halka
ölçülen açılışta boşa düşülürken kurulur). `RingMemoryDelta`: açılışlar boyunca artış (sızıntı olmamalı). İlk gösterilişte GDI/USER birkaç nesne artar (pencerenin DC'si ve
yüzeyi; bir kez), sonraki açılışlarda düz kalmalı. 2.2'de açılır halka donanımla çizilirken 17 açılışta +130 MB özel bellek
ölçüldü (yönetilen yığın düz: ekran kartı sürücüsünün katmanlı pencere yüzeyleri); yazılımla çizim (`RenderMode.SoftwareOnly`)
sonrası ≤ 1 MB.

## 3. Makine ve kurulum

- **Test monitörü:** solda, fiziksel x **-1920..0**, ölçek **%125** (DIP = fiziksel / 1,25). Widget'lar `PixelLeft/
  PixelTop` ile oraya yerleşir; ana pencere ve "Widget ekle" de oraya açılır (`NESTDESK_WINDOW_AT`,
  `NESTDESK_QUICKADD_AT` = `-1500,300`). Ana monitör kullanıcının gerçek masaüstüdür; oraya hiçbir şey konmaz.
- Test monitörü en az 1920×1080 fiziksel piksel olmalı: `bigfence`'in 600×800 DIP bölmesi (750×1000 fiziksel) ve `scale`'in
  15 widget'lık ızgarası (alt kenar ≈ 995 px) görev çubuğunun üstünde kalır.
- Kurulu NestDesk çalışabilir: test örneği kendi tek örnek adını (`Duzenleme.<kullanıcı>.test.<hash>`) kullanır, pencereler
  süreç kimliğiyle ayırt edilir. En temiz sonuç için ölçüm sırasında ağır işler (derleme, indirme, dizin oluşturma) olmasın.
- Bilgisayar prizde, Windows güç kipi aynı (ör. "En iyi performans"), oturum açıldıktan en az bir dakika sonra.
- Kullanıcı o an bilgisayarı kullanabilir: betik gerçek fare/klavye kullanmaz, odak almaz (widget'lar `WS_EX_NOACTIVATE`).

## 4. Eşikler

Karşılaştırmalar aynı makinede, aynı senaryo ve aynı derleme türüyle yapılır; değerler üç çalıştırmanın ortancasıdır.

| Konu | Senaryo | Eşik | Sonuç alanı |
|---|---|---|---|
| Boşta yük | `idle` | < **%0,5** bir çekirdeğin | `CpuPercentOfOneCore` |
| Değişen masaüstü | `churn` | Arayüz iş parçacığı < **%5**; **100 ms**'yi aşan takılma yok | `UiThreadPercent`, `LongestPingMs` (`Pings` içinde `>100ms=0`) |
| Açılış (600 dosya) | `startup` | İlk boşa düşüş < **3 sn**; **250 ms**'yi aşan takılma yok | `StartupIdleMs`, `StartupLongestPingMs` |
| Büyük bölme (1500 dosya) | `bigfence` | İlk boşa düşüş < **3 sn**; **250 ms**'yi aşan takılma yok (açılışta ve sonrasında) | `StartupIdleMs`, `StartupLongestPingMs`, `LongestPingMs` |
| Bellek | `startup`, `idle`, `bigfence`, `scale` | 2.1.1'e göre en çok **+%10** | `MemoryAtStartupIdle.PrivateMB`, `.WorkingSetMB` |
| Widget başına maliyet | `scale` 1/5/15 | (15 widget − 1 widget) / 14: PrivateMB, GDI, USER ve `AllWidgetsVisibleMs` için 2.1.1'inkinden en çok **+%10** | `MemoryAtStartupIdle.*`, `AllWidgetsVisibleMs` |
| Pencere nesneleri | 50 ekle/kaldır turu | 50. turdan sonra GDI ve USER nesneleri 1. turdakinden **artmaz** (±10 ölçüm payı) | `GdiObjects`, `UserObjects` |
| Hareket | `motion` | Canlı'nın arayüz iş parçacığı yükü Sakin'den en çok **5 puan** fazla; **100 ms**'yi aşan takılma yok; geçişler bitince boşta yük < **%0,5** | `UiThreadPercent` (canlı − sakin), `LongestPingMs`, `AfterCpuPercentOfOneCore` |
| Halka (Dalga 3) | `ring`, `ring -RingOff` | Kısayol → ilk kare p95 ≤ **50 ms** sıcak, ≤ **250 ms** soğuk; halka kısayolu açıkken kalıcı maliyet ≤ **8 MB** PrivateBytes; açılışlar boyunca artış ≤ **1 MB**, GDI/USER artmaz | `RingWarmP95Ms`, `RingColdMs`, `MemoryAtStartupIdle.PrivateMB` (ring − ring-off), `RingMemoryDelta` |

Notlar:
- **Pencere nesneleri:** 50 ekle/kaldır turu henüz betikte değil: arayüz doğrulaması sırasında test örneğinde UIA ile
  yapılır ("Widget ekle" → `Add.<Anahtar>`, widget menüsünden Kaldır; her turdan sonra `PerfNative.GuiResources(pid, 0|1)`).
  `motion` senaryosu da GDI/USER'i başta ve sonda verir: 20 gizle/göster sonrasında artış olmamalı.
- **Bilgi (eşik değil):** `clicks`'te `SettingsWritesPerMinute` ≤ 1 beklenir (Z sırası `SaveSettingsLater` ile en geç
  60 sn'de bir yazılır); `motion`'da `UiMsPerToggle` geçiş başına arayüz iş parçacığı süresidir.
- Eşik aşılırsa önce ölçüm tekrarlanır; aşım sürerse ilgili optimizasyon (plan Dalga 5) devreye alınır ya da değişiklik
  geri çekilir.

## 5. Sonuçlar

Her hücreye üç çalıştırmanın ortancası; tarih ve derleme "Not" sütununa: `ExeVersion`, `ExeBuild` (DLL özeti; geliştirme
derlemeleri sürüm çıkana dek eski sürüm numarasını taşır) ve commit.

> Henüz ölçülmedi: taban (2.1.1) ve 2.2 satırları arayüz doğrulaması sırasında, ölçümler sırayla çalıştırılarak doldurulur.

**Açılış ve boşta**

| Sürüm | startup: boşa düşüş ms / en uzun ping ms | idle: CPU % | churn: UI % / en uzun ping ms | clicks: yazma/dk | bigfence: boşa düşüş ms / en uzun ping ms | Not |
|---|---|---|---|---|---|---|
| 2.1.1 (taban) | | | | | | |
| 2.2 | | | | | | |

**Bellek ve ölçek**

| Sürüm | startup: Private MB / WorkingSet MB | bigfence: Private MB / GDI / USER | scale 1: Private MB / GDI / USER | scale 5 | scale 15 | Widget başına: MB / GDI / USER / ms | Not |
|---|---|---|---|---|---|---|---|
| 2.1.1 (taban) | | | | | | | |
| 2.2 | | | | | | | |

**Hareket ve halka**

| Sürüm | motion Canlı: UI % / en uzun ping ms | motion Sakin: UI % | motion Kapalı: UI % | Sonrası boşta % | GDI/USER başta → sonda | ring: p95 sıcak / soğuk ms, kalıcı MB | Not |
|---|---|---|---|---|---|---|---|
| 2.1.1 (taban) | | | | | | — | |
| 2.2 | | | | | | | |

## 6. Sonuç alanları (`result.json`)

| Alan | Anlamı |
|---|---|
| `ExeVersion`, `ExeBuild`, `Settings` | Ölçülen derlemenin sürümü; yönetilen DLL'in adı, SHA-256 özetinin ilk 12 hanesi ve yazılma zamanı (aynı sürüm numaralı derlemeleri ayırır); `-Set` ile verilen ayarlar |
| `FirstWidgetMs`, `AllWidgetsVisibleMs` | Süreç başlatıldıktan sonra ilk / bütün widget pencerelerinin görünmesi |
| `StartupIdleMs` | Arayüz iş parçacığının ilk boşa düşüşü (-1: 60 sn'de düşmedi) |
| `StartupPings`, `StartupLongestPingMs` | Açılıştaki yoklamalar (n, ortanca, p99, en uzun, >50 ms, >100 ms) |
| `StartupSettingsWrites`, `SettingsWrites`, `SettingsWritesPerMinute`, `TotalSettingsWrites` | `settings.json`'ın yerine konma sayısı (dışarıdan sayılır) |
| `CpuPercentOfOneCore`, `UiThreadPercent` | Senaryo boyunca süreç ve arayüz iş parçacığı işlemcisi (bir çekirdeğin yüzdesi) |
| `Pings`, `LongestPingMs` | Senaryo boyunca 100 ms'de bir `WM_NULL` yoklaması |
| `MemoryAtStartupIdle`, `MemoryAtEnd` | `PrivateMB`, `WorkingSetMB`, `PeakWorkingSetMB`, `Handles`, `Threads`, `GdiObjects`, `UserObjects`, `GdiObjectsPeak`, `UserObjectsPeak` |
| `Toggles`, `UiMsPerToggle`, `HotkeyMisses` | `motion`: gönderilen gizle/göster sayısı, geçiş başına arayüz ms'si, kısayol penceresinin bulunamadığı sayı |
| `AfterCpuPercentOfOneCore`, `AfterUiThreadPercent`, `AfterPings` | `motion`: geçişler bittikten sonraki 5 sn |
| `RingOff`, `RingOpens`, `RingColdMs`, `RingWarmMedianMs`, `RingWarmP95Ms`, `RingMemoryDelta` | `ring`: taban mı, ölçülen açılış sayısı, ilk açılışın ve sonrakilerin kısayol → ilk kare süresi (ms), açılışlar boyunca bellek ve GDI/USER artışı (`MemoryAtEnd − MemoryAtStartupIdle`); `HotkeyMisses` burada da |
| `MotionCounters`, `MotionEvents` | `perf.log`'daki `Motion.X=n` sayaçları ve tek tek yazılan `Motion.X` olayları (2.2 Hareket ile) |
| `Marks`, `MarkDetails` | `perf.log`'daki açılış işaretleri, süreç başından ms (her işaretin ilki; §7) ve ayrıntıları (`load=`, `widgets=`, açılıştaki GC) |
| `GcAtExit` | Çıkıştaki yönetilen bellek: `gcHeapKB`, `gcCommittedKB`, `gcFragmentedKB`, `managedKB`, `gc0..2`, `gcPauseMs` (yalnızca çıkıştaki sayaç satırından; örnek sonlandırıldıysa boş) |
| `FenceUpdatesLogged` | Bölme güncellemesi sayısı |
| `Killed` | Örnek `--exit` ile kapanmadı, sonlandırıldı (sonucu şüpheli say) |
| `RealDesktopIcons` | Gerçek masaüstü simgelerinin başta → sonda durumu (yalnızca okunur) |

## 7. Başarım günlüğü (`NESTDESK_PERF_LOG`)

Betik test örneğine `NESTDESK_PERF_LOG=<Out>\<Name>\perf.log` verir. Değişken yoksa `PerfLog` hiçbir şey yazmaz ve
maliyeti yoktur.

Açılış işaretleri (`PerfLog.Mark`, 2.2), biçim `mark <ad> <süreç başından ms> ms [ayrıntı]`:

| İşaret | Nerede |
|---|---|
| `App.resources` | `App.OnStartup` başı: App.xaml kaynakları (WPF-UI sözlükleri, QuietStyles) yüklendi. Zaman orada alınır, işaret tek örnek denetiminden sonra yazılır: yardımcı süreçler (`--exit`, `--peek`, ikinci açılış) işaret koymaz |
| `Settings.loaded` | `AppHost.Initialize`: `settings.json` okundu (`load=` okuma süresi) |
| `AppHost.initialized` | `AppHost.Initialize` sonu |
| `Tray.created` | Tepsi simgesi kuruldu |
| `Widget.firstRendered` | İlk widget penceresinin `ContentRendered`'ı |
| `RestoreAll.end` | Bütün widget'lar açıldı (`widgets=`, o anki GC yığını) |
| `StartServices.end` | `App.StartServices` sonu |

Çıkışta (`AppHost.FlushAll`) sayaç satırı yazılır: `PerfLog.Count` sayaçları (`SaveSettings`, `FenceUpdate`, 2.2'de
`Motion.*`) ve `PerfLog.Gc()` (yönetilen yığın, toplama sayıları, toplam duraklama).
