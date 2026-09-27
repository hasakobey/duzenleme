# Düzenleme

Windows için modern masaüstü düzenleyici. Masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** kendiliğinden taşır; masaüstüne saat, tarih, not, kısayol kutusu ve klasör bölmesi widget'ları ekler; klasörlerine şık simgeler verir.

Stardock Fences, iTop Easy Desktop, SlideSlide, ViPad ve LaunchBar Commander'ın öne çıkan özelliklerinden esinlenildi.

**Download / İndir:** [Releases](https://github.com/hasakobey/duzenleme/releases) — `Duzenleme-Kurulum-<sürüm>.exe` (installer) or portable zips, free.

> **English:** Düzenleme is a free, open source (MIT) desktop organizer for Windows 10/11, similar to Stardock Fences. It moves files dropped on the desktop into folders the user created (e.g. PDFs into a `PDF` folder, undoable), and adds desktop widgets: fences that show desktop items or folder contents, clock, date, sticky notes and an app launcher. Built with .NET 10 WPF; the installer and portable builds are produced by GitHub Actions from this repository.

## Özellikler

### Otomatik düzenleme
- Masaüstüne bir PDF düştüğünde, masaüstünde `PDF` adında bir klasör varsa dosya oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için de kurallar hazır; klasör adı ↔ uzantı kurallarını istediğin gibi değiştirebilirsin.
- Klasör masaüstünde yoksa dosyaya dokunmaz (istersen "Klasör yoksa oluştur" açılabilir). Büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- Kısayollar, sistem dosyaları ve inmekte olan dosyalar (`.crdownload`, `.part`) asla taşınmaz; aynı adlı dosyanın üzerine yazılmaz (`ad (1).pdf`).
- Her taşıma geri alınabilir; geri alınan dosya bir daha otomatik taşınmaz.

### Widget'lar
| Widget | Ne yapar |
|---|---|
| **Saat** | Büyük dijital saat, günün selamı, isteğe bağlı saniye |
| **Tarih** | Gün/ay/yıl ve bugünün vurgulandığı haftalık şerit |
| **Not** | Yüzen yapışkan not, 6 renk, yazdıkça kaydedilir (SlideSlide) |
| **Bölme** | Masaüstünü bölümlere ayırır: bir klasörün içi (ör. PDF) ya da masaüstündeki **Klasörler / Kısayollar / Dosyalar / Tümü**. Başlıktaki 🔍 ile yazdıkça süzer ve alt klasörlerde de arar (Enter açar). Dosyaları bölmeden bölmeye sürükleyerek taşı. Resim/video/PDF önizlemesi, fare çekilince katlanma (Fences) |
| **Kısayol kutusu** | Sekmeli uygulama/dosya rafı; sürükle-bırakla ya da "Uygulama ekle…" ile ekle, tek tıkla aç, yönetici olarak çalıştır (ViPad + LaunchBar Commander) |

**Widget eklemek:** tepsi simgesine sağ tık → **Widget ekle…**, herhangi bir widget'a sağ tık → **Yeni widget ekle…**, `Ctrl+Alt+B` ya da Başlat menüsündeki **Düzenleme - Widget ekle**. Açılan pencerede Klasörlerim, Kısayollarım, Dosyalarım, PDF, Saat, Not… düğmelerinden birine bas; widget o ekrana eklenir.

**Widget'lar → "Masaüstümü bölümlere ayır"** tek tıkla Klasörler, Kısayollar, Dosyalar ve masaüstündeki PDF/Resimler gibi klasörler için birer bölme kurar ve **masaüstünü bölmelere devreder**: Windows'un kendi masaüstü simgeleri gizlenir, her şey (Bu Bilgisayar, Geri Dönüşüm Kutusu dahil) yalnızca bölmelerde görünür — Fences gibi. Dosyalara dokunulmaz; "Masaüstü simgelerini yalnızca bölmelerde göster" anahtarı (Araçlar, tepsi menüsü ya da bölmeye sağ tık) kapatılınca ya da uygulamadan çıkınca simgeler geri gelir. Bu modda yeni klasör, yeniden adlandırma ve Geri Dönüşüm Kutusu'na taşıma bölme menüsünden yapılır. Masaüstüne çift tık tüm bölmeleri gizler/gösterir.

**Kaldırmak:** widget'ı başlığın sağ ucundaki sönük **×** kaldırır (saat ve tarihte fareyle üstüne gelince sol üst köşede belirir); bildirime ya da tepsi menüsündeki "Geri getir"e tıklayarak geri al. İçindeki bir öğeyi kaldırmak için öğeye sağ tıkla → **Widget'tan kaldır** (bölmede dosyaya dokunmaz, widget'a sağ tık → "Gizlenen öğeler"den geri getirilir; kısayol kutusunda "Geri al" ile döner). Sağ tık → **Göster** ile başlığı, sayıyı, arama düğmesini, sekmeleri, saatin selam satırını, tarihin haftalık şeridini… kapatabilirsin.

**Yerleşim:** sürüklerken widget'lar birbirinin altına/üstüne/yanına ve ekran kenarına mıknatıs gibi yapışır, kenarları hizalanır; bırakılan widget başkasının üstüne binmez, en yakın boş yere kayar; kenardan büyütürken komşusunun kenarında durur. Taşırken `Alt` yapışmayı geçici kapatır, `Shift` ızgaraya hizalar. İkisi de sağ tık → **Yerleşim** menüsünden açılıp kapanır.

Tıkladığın ya da yeni eklediğin widget diğerlerinin önüne geçer (sıra saklanır); "Düzenli yerleştir" hepsini çakışmadan sağdan dizer, önceki düzen "Kayıtlı düzenler"e yedeklenir.

Her widget'a sağ tıkla:
- **Boyut:** kenarlarından ve köşelerinden sürükleyerek büyüt/küçült (saat ve tarih ölçeklenir); `Ctrl + fare tekerleği` bölmede simgeleri, diğerlerinde widget'ı büyütür. Taşırken `Shift` ızgaraya hizalar.
- **Simgeler:** ızgara/liste, 4 boyut, sola/ortaya/sağa hizalama, aralık, yazı boyutu, **adları gizle**, tek tıkla aç, önizleme.
- **Özelleştir:** Cam/Koyu/Açık, 5 vurgu rengi, ölçek, saydamlık, köşeler, gölge, "üzerine gelince belirginleş".
- Konumu kilitle, çoğalt; widget düzenlerini ad vererek kaydedip tek tıkla geri dön ("İş", "Oyun"…).

Widget'lar monitör takılıp çıkarılınca ya da uykudan uyanınca kayıtlı yerlerine döner; yeni widget, düğmeye bastığın monitörde açılır.

### Masaüstü
- **Hızlı gizle:** boş masaüstüne çift tıkla ya da `Ctrl+Alt+H` → simgeler (ve istersen widget'lar) gizlenir (Fences / iTop). Yalnızca masaüstünün kendisine çift tıklama sayılır; Gezgin pencereleri ve dosya seçme kutuları etkilemez.
- **Varsayılan simgeler:** Bu Bilgisayar, Geri Dönüşüm Kutusu, Kullanıcı dosyaları, Ağ ve Denetim Masası'nı tek tıkla aç/kapat.
- **Filtre:** masaüstündeki her şeyi türüne göre (Klasörler, Kısayollar, PDF, Belgeler…) süz ve ara.

### Klasör simgeleri
- 30 sembol × 10 renkli hazır kütüphane; klasör adına göre otomatik öneri ("Oyunlarım" → yeşil oyun kolu).
- Masaüstünde yeni klasör açınca bildirim gelir; tıklayınca simge seçici açılır. "Tüm klasörlere önerilen simgeyi ver" ile hepsini bir kerede güzelleştir.
- Kendi `.ico/.png/.svg` dosyanı yükleyebilirsin.
- **İsteğe bağlı yapay zekâ:** Ayarlar'a Claude API anahtarını girersen, yazdığın açıklamaya göre ("retro oyun kolu, neon mor") özel simge üretilir. Anahtar DPAPI ile yalnızca bu Windows hesabına bağlı şifrelenir. Anahtar zorunlu değildir.

### Klavye kısayolları (değiştirilebilir)
| Kısayol | İşlev |
|---|---|
| `Ctrl+Alt+H` | Masaüstünü gizle / göster |
| `Ctrl+Alt+O` | Masaüstünü şimdi düzenle |
| `Ctrl+Alt+D` | Düzenleme'yi aç |
| `Ctrl+Alt+N` | Yeni not |
| `Ctrl+Alt+W` | Widget'ları pencerelerin önüne getir (5 sn) |
| `Ctrl+Alt+B` | "Widget ekle" penceresi |

## Kurulum

**`Duzenleme-Kurulum-<sürüm>.exe`** — tek kurulum sihirbazı, her bilgisayar için:

- Windows 10 (1607+) ve Windows 11; **x64, ARM64 ve 32-bit** — bilgisayara uygun sürümü kendisi seçer.
- .NET kurulumu gerekmez (çalışma zamanı pakete dahil), **yönetici izni istemez** (kullanıcı başına `%LOCALAPPDATA%\Programs\Duzenleme`).
- Türkçe/İngilizce sihirbaz; Başlat menüsü kısayolu, isteğe bağlı masaüstü kısayolu ve "Windows ile başlat".
- Güncellerken çalışan uygulamayı düzgünce kapatır; ayarlar, notlar ve widget düzeni korunur.
- "Uygulamalar ve özellikler"den kaldırılabilir; kaldırırken ayarların silinip silinmeyeceğini sorar.

**Microsoft Store (MSIX):** Store sürümünü Microsoft imzalar ve günceller; uyarı çıkmaz. Aynı program, yalnızca birkaç fark var: "Windows ile başlat" Windows'un başlangıç görevidir (Ayarlar → Uygulamalar → Başlangıç'ta da görünür) ve ayarlar paketin kendi klasöründe tutulur (önceden kurulum sürümü kullanıldıysa var olan ayarlar okunmaya devam eder). Masaüstünün "Bu Bilgisayar, Geri Dönüşüm Kutusu…" simgeleri Store sürümünde Windows'un Temalar → Masaüstü simgesi ayarlarından açılıp kapatılır.

**Taşınabilir kullanım:** `Duzenleme-<sürüm>-<mimari>-tasinabilir.zip` dosyasını yazılabilir bir klasöre çıkar ve `Duzenleme.exe`'yi çalıştır. İçindeki `portable.txt` sayesinde ayarlar exe'nin yanındaki `data` klasöründe tutulur (USB bellekte taşınabilir).

> **İmza hakkında:** Paket kod imzası olmadan derlenir. Bu yüzden Windows SmartScreen ilk açılışta "Windows bilgisayarınızı korudu" uyarısı gösterebilir (**Ek bilgi → Yine de çalıştır**). **Smart App Control** açık bilgisayarlar imzasız programları engelleyebilir. Kalıcı çözüm bir kod imzalama sertifikasıdır (ör. Microsoft Trusted Signing); `tools/publish.ps1` sertifika verildiğinde tüm dosyaları ve kurulumu otomatik imzalar (dosyanın başındaki açıklamaya bak).

## Geliştirme

Gerekenler: Windows 10/11, **.NET 10 SDK**, paketleme için [Inno Setup 6.3+](https://jrsoftware.org/isdl.php).

```powershell
dotnet test Duzenleme.sln                     # testler
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # test klasörüyle çalıştır
dotnet run --project src/Duzenleme            # gerçek masaüstünü izler!
powershell -File tools/publish.ps1            # dist/: kurulum sihirbazı + 3 taşınabilir zip
powershell -File tools/publish.ps1 -Arch x64  # hızlı deneme (yalnızca x64)
powershell -File tools/package-msix.ps1 -Strict  # dist-store/: Microsoft Store'a yüklenecek .msixbundle (kimlik: tools/store/identity.json)
```

Diğer komut satırı seçenekleri: `--minimized` (tepside başla), `--add` ("Widget ekle" penceresi; uygulama çalışıyorsa ona iletilir), `--exit` (çalışan örneği düzgünce kapat; kurulum programı kullanır). Tanılama günlüğü için `DUZENLEME_DEBUGLOG` ortam değişkenine bir dosya yolu ver.

Geliştirme yardımcıları: `--export-icon-sheet out.png` (simge kütüphanesi önizlemesi), `--render-svg in.svg out.png` (SVG'yi yapay zekâ çıktısıyla aynı temizleme/çizim yolundan geçirir, yanına `.ico` yazar).

Ayarlar ve geçmiş `%AppData%\Duzenleme` altında (`settings.json`, `journal.json`, `ai-icons/`) tutulur; ayarların son 7 günlük kopyası `yedekler/` klasöründedir.

**Performans:** klasör taramaları ve simge/önizleme yüklemeleri arka planda yapılır (büyük klasörde arayüz donmaz); saat yalnızca dakika (ya da saniye) başında çizilir; widget'lar yazılımla çizilir (7 widget'ta ~85 MB daha az bellek). Donanım çizimine dönmek için `DUZENLEME_GPU=1`.

## Code signing policy (kod imzalama)

Sürümler, [SignPath Foundation](https://signpath.org)'ın ücretsiz açık kaynak programıyla imzalanmak üzere başvuru aşamasındadır. Onaylandığında: *Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).*

- İmzalanan dosyalar yalnızca bu depodaki kaynak koddan, [GitHub Actions](.github/workflows/dotnet-desktop.yml) ile derlenir; her sürüm elle onaylanır.
- Committers and reviewers: [hasakobey](https://github.com/hasakobey)
- Approvers: [hasakobey](https://github.com/hasakobey)

**Privacy policy (gizlilik):** This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. — Düzenleme, kullanıcı özellikle istemedikçe hiçbir bilgiyi internete göndermez. Tek istisna isteğe bağlı "yapay zekâ ile klasör simgesi" özelliğidir: kullanıcı kendi API anahtarını girip simge istediğinde yalnızca klasör adı ve yazdığı açıklama Anthropic'e gönderilir ([Anthropic gizlilik politikası](https://www.anthropic.com/legal/privacy)).

## Lisans

[MIT](LICENSE) © 2026 hasakobey — kullanmak, değiştirmek ve dağıtmak serbesttir; telif satırı ve lisans metni korunmalıdır. Kullanılan açık kaynak bileşenler ve lisansları: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) (kurulum ve zip'lerde de programın yanında gelir).
