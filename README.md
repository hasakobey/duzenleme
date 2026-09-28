# NestDesk

**Masaüstün için derli toplu bir yuva.** Windows için modern masaüstü düzenleyici. Masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** kendiliğinden taşır; masaüstüne saat, tarih, not, yapılacaklar listesi, kısayol kutusu ve klasör bölmesi widget'ları ekler; klasörlerine şık simgeler verir.

Stardock Fences, iTop Easy Desktop, SlideSlide, ViPad ve LaunchBar Commander'ın öne çıkan özelliklerinden esinlenildi.

**Download / İndir:** [Releases](https://github.com/hasakobey/duzenleme/releases) — `NestDesk-Setup-<version>.exe` (installer, English/Türkçe) or `NestDesk-<version>-<arch>-portable.zip`, free.

> **English:** NestDesk is a free, open source (MIT) desktop organizer for Windows 10/11, similar to Stardock Fences — a tidy home for your desktop. It moves files dropped on the desktop into folders the user created (e.g. PDFs into a `PDF` folder, undoable, and only after the user agrees), and adds desktop widgets: fences that show desktop items or folder contents, clock, date, sticky notes, to-do lists and an app launcher. Built with .NET 10 WPF; the installer and portable builds are produced by GitHub Actions from this repository.

## İlk açılış

- NestDesk ilk açılışta üç adımlık kısa bir **karşılama** gösterir: bölmeler, dosya taşıma ve araçlar (saat, tarih, not, yapılacaklar, kısayol kutusu).
- **Onay verilmeden hiçbir dosya taşınmaz.** 2. adım masaüstünde duran dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir; "Evet, dosyalarımı yerine taşı" seçilmezse otomatik taşıma kapalı kalır (sonra **Otomatik taşıma** sayfasından açılır).
- "Şimdilik atla" masaüstünde hiçbir şeyi değiştirmez ve ana pencereyi açar.
- Karşılamayı yeniden açmak için: **Ayarlar → Yardım → Karşılama**. Var olan widget'lar ve kurallar silinmez.

## Ana pencere

Tepsi simgesine tıkla ya da `Ctrl+Alt+D`. Dört sayfa vardır:

- **Ana sayfa:** Şimdi düzenle, Widget ekle, Son taşımayı geri al; otomatik taşımanın durumu (anahtarıyla buradan da açılıp kapanır) ve son taşınanlar.
- **Widget'lar:** ekleme kutucukları, masaüstündekiler (**Bul** / **Kaldır**), "Masaüstü simgelerini yalnızca bölmelerde göster" anahtarı, tüm widget'ların görünümü, düzenli yerleştirme ve kayıtlı düzenler.
- **Otomatik taşıma:** aç/kapat, kurallar ("Hangi dosya nereye gitsin?"), son taşınanlar ve geri alma; nadiren gereken ayarlar **Gelişmiş** altında.
- **Ayarlar:** Genel, Masaüstü, Klasör simgeleri, Gelişmiş (kısayollar, yapay zekâ, dosya konumları), Yardım, Hakkında.

Geri bildirimler pencerenin altındaki şeritte kalır (kendiliğinden kaybolmaz); kaldırma ve silme gibi işlemler oradan **Geri al** ile geri alınır. Sayfa geçişlerinde ve widget'larda animasyon yoktur. Pencereyi kapatınca NestDesk tepside çalışmaya devam eder.

## Özellikler

### Otomatik taşıma
- Masaüstüne bir PDF düştüğünde, masaüstünde `PDF` adında bir klasör varsa dosya oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için de kurallar hazır; klasör adı ↔ uzantı kurallarını **Otomatik taşıma** sayfasında istediğin gibi değiştirebilirsin. Klasörü olmayan kuralın satırında kaç dosyanın beklediği görünür; "Klasörü oluştur" ile tek tıkla açılır.
- Klasör masaüstünde yoksa dosyaya dokunmaz (istersen **Gelişmiş → Klasör yoksa oluştur** açılabilir). Büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- Kısayollar, sistem dosyaları ve inmekte olan dosyalar (`.crdownload`, `.part`) asla taşınmaz; aynı adlı dosyanın üzerine yazılmaz (`ad (1).pdf`).
- Her taşıma geri alınabilir; geri alınan dosya bir daha otomatik taşınmaz.

### Widget'lar
| Widget | Ne yapar |
|---|---|
| **Saat** | Büyük dijital saat, günün selamı, isteğe bağlı saniye |
| **Tarih** | Gün/ay/yıl ve bugünün vurgulandığı haftalık şerit |
| **Not** | Yüzen yapışkan not, 6 renk, yazdıkça kaydedilir (SlideSlide) |
| **Yapılacaklar** | Onay kutulu liste; işaretledikçe kaydedilir, biten maddeler üstü çizili olarak yerinde kalır. "Bitenleri temizle" geri alınabilir; sağ tıkla düz nota (ya da düz not listeye) çevrilir |
| **Bölme** | Masaüstünü bölmelere ayırır: bir klasörün içi (ör. PDF) ya da masaüstündeki **Klasörler / Kısayollar / Dosyalar / Tümü**. Başlıktaki 🔍 ile yazdıkça süzer ve alt klasörlerde de arar (Enter açar). Dosyaları bölmeden bölmeye sürükleyerek taşı. Resim/video/PDF önizlemesi, başlığa çift tıklayınca katlanma (Fences) |
| **Kısayol kutusu** | Sekmeli uygulama/dosya rafı; sürükle-bırakla ya da "Uygulama ya da dosya ekle…" ile ekle, tek tıkla aç, yönetici olarak çalıştır (ViPad + LaunchBar Commander) |

**Widget eklemek:** tepsi simgesine sağ tık → **Widget ekle…**, herhangi bir widget'a sağ tık → **Yeni widget ekle…**, `Ctrl+Alt+B`, Başlat menüsündeki **NestDesk - Widget ekle** ya da ana pencerede **Widget'lar** sayfası. Klasörler, Kısayollar, Dosyalar, PDF, Saat, Not, Yapılacaklar… kutucuklarından birine bas; widget o ekrana eklenir.

**"Masaüstümü bölmelere ayır"** ("Widget ekle" penceresi) tek tıkla Klasörler, Kısayollar, Dosyalar ve masaüstündeki PDF/Resimler gibi klasörler için birer bölme kurar ve **masaüstünü bölmelere devreder**: Windows'un kendi masaüstü simgeleri gizlenir, her şey (Bu Bilgisayar, Geri Dönüşüm Kutusu dahil) yalnızca bölmelerde görünür — Fences gibi. Bildirimdeki **Geri al** eklenen bölmeleri kaldırıp modu kapatır. Dosyalara dokunulmaz; "Masaüstü simgelerini yalnızca bölmelerde göster" anahtarı (Widget'lar sayfası, tepsi menüsü ya da bölmeye sağ tık → **Diğer**) kapatılınca ya da uygulamadan çıkınca simgeler geri gelir. Bu modda yeni klasör, yeniden adlandırma ve Geri Dönüşüm Kutusu'na taşıma bölme menüsünden yapılır. Masaüstüne çift tık tüm bölmeleri gizler/gösterir.

**Kaldırmak:** her widget'ın sağ üst köşesindeki sönük **×** hep yerindedir ve widget'ı kaldırır; bildirimdeki **Geri al** ya da tepsi menüsündeki "Geri getir" ile geri gelir. × istenirse sağ tık → **Görünüm ▸ Göster ▸ Kaldır düğmesi (×)** ile gizlenir (konum kilitliyken de gizlenir). İçindeki bir öğeyi kaldırmak için öğeye sağ tıkla → **Widget'tan kaldır** (bölmede dosyaya dokunmaz, widget'a sağ tık → "Gizlenen öğeler"den geri getirilir; kısayol kutusunda "Geri al" ile döner). **Görünüm ▸ Göster** ile başlığı, sayıyı, arama düğmesini, sekmeleri, saatin selam satırını, tarihin haftalık şeridini… kapatabilirsin.

**Yerleşim:** sürüklerken widget'lar birbirinin altına/üstüne/yanına ve ekran kenarına mıknatıs gibi yapışır, kenarları hizalanır; bırakılan widget başkasının üstüne binmez, en yakın boş yere kayar; kenardan büyütürken komşusunun kenarında durur. Taşırken `Alt` yapışmayı geçici kapatır, `Shift` ızgaraya hizalar. İkisi de sağ tık → **Diğer** menüsünden ya da Widget'lar sayfasından açılıp kapanır.

Tıkladığın ya da yeni eklediğin widget diğerlerinin önüne geçer (sıra saklanır). **Düzenli yerleştir** (Widget'lar sayfası ya da sağ tık → Diğer) hepsini çakışmadan sağdan dizer; önceki düzen "Kayıtlı düzenler"e yedeklenir. Kayıtlı bir düzen uygulanmadan önce de o anki yerleşim "Düzen uygulanmadan önce" adıyla yedeklenir.

Her widget'a sağ tıkla:
- **Boyut:** kenarlarından ve köşelerinden sürükleyerek büyüt/küçült (saat ve tarih ölçeklenir); `Ctrl + fare tekerleği` bölmede simgeleri, diğerlerinde widget'ı büyütür. Taşırken `Shift` ızgaraya hizalar.
- **Simgeler ▸:** sıralama (bölmede), ızgara/liste düzeni, 4 boyut, sola/ortaya/sağa hizalama, aralık, yazı boyutu, **adları gizle**, tek tıkla aç, önizleme.
- **Görünüm ▸:** Göster (parçalar), Arka plan (Cam/Koyu/Açık), 5 vurgu rengi, ölçek, saydamlık, köşeler, gölge, "Fare üstünde değilken soluk dursun". Notlar kendi kağıt rengini kullanır.
- **Diğer ▸:** Başlığa katla, "Fare üstünde değilken başlığa katla", konumu kilitle, mıknatıs, üst üste binmesin, düzenli yerleştir, çoğalt.
- Widget'lar sayfasında bütün widget'ların arka planı ve vurgu rengi tek yerden değişir; widget düzenlerini ad vererek kaydedip tek tıkla geri dönersin ("İş", "Oyun"…).

Widget'lar monitör takılıp çıkarılınca ya da uykudan uyanınca kayıtlı yerlerine döner; yeni widget, düğmeye bastığın monitörde açılır.

### Masaüstü
- **Hızlı gizle:** boş masaüstüne çift tıkla ya da `Ctrl+Alt+H` → simgeler (ve istersen widget'lar) gizlenir (Fences / iTop). Yalnızca masaüstünün kendisine çift tıklama sayılır; Gezgin pencereleri ve dosya seçme kutuları etkilemez. Ayarı **Ayarlar → Masaüstü**'nde.
- **Varsayılan simgeler:** Bu Bilgisayar, Geri Dönüşüm Kutusu, Kullanıcı dosyaları, Ağ ve Denetim Masası'nı **Ayarlar → Masaüstü → Sistem simgeleri**'nden tek tıkla aç/kapat.

### Klasör simgeleri
- 30 sembol × 10 renkli hazır kütüphane; klasör adına göre otomatik öneri ("Oyunlarım" → yeşil oyun kolu).
- Masaüstünde yeni klasör açınca bildirim gelir; tıklayınca simge seçici açılır. **Ayarlar → Klasör simgeleri → Tüm klasörlere önerilen simgeyi ver** ile hepsini bir kerede güzelleştir.
- Kendi `.ico/.png/.svg` dosyanı yükleyebilirsin.
- **İsteğe bağlı yapay zekâ:** **Ayarlar → Gelişmiş → Yapay zekâ ile klasör simgesi**'ne Claude API anahtarını girersen, yazdığın açıklamaya göre ("retro oyun kolu, neon mor") özel simge üretilir. Anahtar DPAPI ile yalnızca bu Windows hesabına bağlı şifrelenir. Anahtar zorunlu değildir.

### Klavye kısayolları (değiştirilebilir)
| Kısayol | İşlev |
|---|---|
| `Ctrl+Alt+H` | Masaüstünü gizle / göster |
| `Ctrl+Alt+O` | Masaüstünü şimdi düzenle |
| `Ctrl+Alt+D` | NestDesk'i aç |
| `Ctrl+Alt+N` | Yeni not |
| `Ctrl+Alt+W` | Widget'ları pencerelerin önüne getir (5 sn) |
| `Ctrl+Alt+B` | "Widget ekle" penceresi |

## Kurulum

**`NestDesk-Setup-<sürüm>.exe`** — tek kurulum sihirbazı, her bilgisayar için:

- Windows 10 (1607+) ve Windows 11; **x64, ARM64 ve 32-bit** — bilgisayara uygun sürümü kendisi seçer.
- .NET kurulumu gerekmez (çalışma zamanı pakete dahil), **yönetici izni istemez** (kullanıcı başına; `%LOCALAPPDATA%\Programs\NestDesk`'e kurulur, güncellemeler önceki kurulumun klasöründe kalır).
- İngilizce/Türkçe sihirbaz: dil başta sorulur (Windows'un dili seçili gelir; başka dillerde İngilizce). Açık/koyu Windows temasına uyar. Başlat menüsü kısayolu, isteğe bağlı masaüstü kısayolu ve "Windows ile başlat".
- Güncellerken çalışan uygulamayı düzgünce kapatır; ayarlar, notlar ve widget düzeni korunur.
- "Uygulamalar ve özellikler"den kaldırılabilir; kaldırırken gizli kalmış masaüstü simgelerini geri açar ve ayarların silinip silinmeyeceğini sorar. Klasörlere verilen simgeler klasörlerin içinde kalır; istersen kaldırmadan önce **Ayarlar → Gelişmiş → Klasör simgelerinin hepsini kaldır**.

**Microsoft Store (MSIX):** Store sürümünü Microsoft imzalar ve günceller; uyarı çıkmaz. Aynı program, yalnızca birkaç fark var: "Windows ile başlat" Windows'un başlangıç görevidir (Ayarlar → Uygulamalar → Başlangıç'ta da görünür) ve ayarlar paketin kendi klasöründe tutulur (önceden kurulum sürümü kullanıldıysa var olan ayarlar okunmaya devam eder). Masaüstünün "Bu Bilgisayar, Geri Dönüşüm Kutusu…" simgeleri Store sürümünde Windows'un Temalar → Masaüstü simgesi ayarlarından açılıp kapatılır.

**Taşınabilir kullanım:** `NestDesk-<sürüm>-<mimari>-portable.zip` dosyasını yazılabilir bir klasöre çıkar ve `NestDesk.exe`'yi çalıştır. İçindeki `portable.txt` sayesinde ayarlar exe'nin yanındaki `data` klasöründe tutulur (USB bellekte taşınabilir).

**Güncelleme:** NestDesk gizlilik sözü gereği kendisi güncelleme denetlemez. Store sürümü kendiliğinden güncellenir; kurulum sürümünde yeni kurulum dosyasını çalıştırman yeterli (yeni sürümü **Ayarlar → Hakkında → İndirme sayfasını aç**'tan görürsün); taşınabilir sürümde yeni zip'i aynı klasörün üzerine çıkar (`data` korunur).

> **İmza hakkında:** Paket kod imzası olmadan derlenir. Bu yüzden Windows SmartScreen ilk açılışta "Windows bilgisayarınızı korudu" uyarısı gösterebilir (**Ek bilgi → Yine de çalıştır**). **Smart App Control** açık bilgisayarlar imzasız programları engelleyebilir. Kalıcı çözüm bir kod imzalama sertifikasıdır (ör. Microsoft Trusted Signing); `tools/publish.ps1` sertifika verildiğinde tüm dosyaları ve kurulumu otomatik imzalar (dosyanın başındaki açıklamaya bak).

## Geliştirme

Gerekenler: Windows 10/11, **.NET 10 SDK**, paketleme için [Inno Setup 6.6+](https://jrsoftware.org/isdl.php).

```powershell
dotnet test Duzenleme.sln                     # testler
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # test klasörüyle çalıştır
dotnet run --project src/Duzenleme            # gerçek masaüstünü izler!
powershell -File tools/publish.ps1            # dist/: kurulum sihirbazı + 3 taşınabilir zip
powershell -File tools/publish.ps1 -Arch x64  # hızlı deneme (yalnızca x64)
powershell -File tools/package-msix.ps1 -Strict  # dist-store/: Microsoft Store'a yüklenecek .msixbundle (kimlik: tools/store/identity.json)
```

Diğer komut satırı seçenekleri: `--minimized` (tepside başla), `--add` ("Widget ekle" penceresi; uygulama çalışıyorsa ona iletilir), `--welcome` (karşılamayı aç; ilk açılış tamamlandıysa var olanları silmeden yeniden kurulum. Yalnızca uygulama kapalıyken çalışır: çalışan bir örnek varsa yalnızca ana penceresi öne gelir, karşılamayı oradan **Ayarlar → Yardım → Karşılama** açar), `--exit` (çalışan örneği düzgünce kapat; kurulum programı kullanır), `--restore-desktop` (uygulama zorla kapatılıp Windows masaüstü simgelerini gizli bıraktıysa geri açıp çıkar; kaldırma programı kullanır, çalışan örneğe dokunmaz). Tanılama günlüğü için `NESTDESK_DEBUGLOG` ortam değişkenine bir dosya yolu ver.

Test ortam değişkenleri (hepsi eski `DUZENLEME_*` adlarıyla da okunur): `NESTDESK_WINDOW_AT="x,y"` (fiziksel piksel) verilirse ana pencere ve karşılama bu noktanın monitöründe açılır; `NESTDESK_QUICKADD_AT` aynısını "Widget ekle" penceresi için yapar. `NESTDESK_APPDATA_ROOT` yalnızca `--desktop` ile ve `--data` verilmeden çalışan test örneğinde `%AppData%` yerine geçer (eski `Duzenleme` veri klasörünün taşınmasını gerçek `%AppData%`'ya dokunmadan denemek için). Boş bir `--data` klasörü karşılamayı açar; atlamak için klasöre `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` içerikli bir `settings.json` koy. `--desktop` ile açılan test örneği gerçek masaüstü simgelerine ve "Windows ile başlat" kaydına dokunmaz.

Geliştirme yardımcıları: `--export-icon-sheet out.png` (simge kütüphanesi önizlemesi), `--render-svg in.svg out.png` (SVG'yi yapay zekâ çıktısıyla aynı temizleme/çizim yolundan geçirir, yanına `.ico` yazar).

Ayarlar ve geçmiş `%AppData%\NestDesk` altında (`settings.json`, `journal.json`, `ai-icons/`) tutulur. Ayarların son 7 günlük kopyası `yedekler/` klasöründedir.

**Performans:** klasör taramaları ve simge/önizleme yüklemeleri arka planda yapılır (büyük klasörde arayüz donmaz); saat yalnızca dakika (ya da saniye) başında çizilir; widget'lar yazılımla çizilir (7 widget'ta ~85 MB daha az bellek). Donanım çizimine dönmek için `NESTDESK_GPU=1`.

Proje, ad alanı ve çözüm adları (`Duzenleme.sln`, `src/Duzenleme`, `Duzenleme.*`) ile tek örnek kilidinin adı (`Duzenleme.<kullanıcı>`) bilerek eski addır: kullanıcıya görünmezler ve 2.0 ile 2.1'in birbirini görmesini sağlarlar.

## Yeni sürüm yayınlama

1. `src/Duzenleme/Duzenleme.csproj` → `<Version>` bir artırılır (ör. 2.1.1). Store aynı sürümü ikinci kez kabul etmez.
2. `tools/store/listing.md` → "Bu sürümdeki yenilikler" (TR/EN) yazılır.
3. `dotnet test` yeşil olmalı. Ayar dosyası geriye uyumlu kalır: yeni ayar = yeni `bool` alan; kalıcı enum'lara üye eklenmez (eski sürüm bütün ayarları okunamaz sayar).
4. Commit → `git tag v2.1.1` → push. GitHub Actions kurulum dosyasını (`NestDesk-Setup-2.1.1.exe`) ve 3 zip'i üretip Releases'ta "NestDesk 2.1.1" sürümünü açar.
5. **Store:** `powershell -File tools/package-msix.ps1 -Strict` → `dist-store\NestDesk-2.1.1.msixbundle`. Partner Center'da **yeni gönderim** açılır, yalnızca bu dosya yüklenir ve "What's new" doldurulur. Onay genelde 1–3 gün sürer.
6. **İmza:** SignPath onaylanana dek kurulum dosyası imzasızdır ve her yeni sürümde SmartScreen uyarısı çıkabilir. Store sürümünü Microsoft imzalar.

## Code signing policy (kod imzalama)

Sürümler, [SignPath Foundation](https://signpath.org)'ın ücretsiz açık kaynak programıyla imzalanmak üzere başvuru aşamasındadır. Onaylandığında: *Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).*

- İmzalanan dosyalar yalnızca bu depodaki kaynak koddan, [GitHub Actions](.github/workflows/dotnet-desktop.yml) ile derlenir; her sürüm elle onaylanır.
- Committers and reviewers: [hasakobey](https://github.com/hasakobey)
- Approvers: [hasakobey](https://github.com/hasakobey)

**Privacy policy (gizlilik):** This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. — NestDesk, kullanıcı özellikle istemedikçe hiçbir bilgiyi internete göndermez. Tek istisna isteğe bağlı "yapay zekâ ile klasör simgesi" özelliğidir: kullanıcı kendi API anahtarını girip simge istediğinde yalnızca klasör adı ve yazdığı açıklama Anthropic'e gönderilir ([Anthropic gizlilik politikası](https://www.anthropic.com/legal/privacy)). Ayrıntılar: [PRIVACY.md](PRIVACY.md).

## Geçmiş

NestDesk'in 2.0.0'dan önceki adı **Düzenleme**'ydi. 2.1.0'da eski ad görünen her yerden kalktı: program dosyası `Duzenleme.exe` → `NestDesk.exe`, ayar klasörü `%AppData%\Duzenleme` → `%AppData%\NestDesk`, "Windows ile başlat" kaydı ve klasör simgesi dosyaları (`.duzenleme-*.ico` → `.nestdesk-*.ico`). Güncellemede hepsi kendiliğinden taşınır; ayarlar, notlar, widget'lar ve kurallar olduğu gibi kalır. Windows bazı tercihleri program dosyasının yoluna göre tuttuğu için görev çubuğuna sabitlenmiş eski kısayolu yeniden sabitlemen, tepsi simgesini de (saatin yanındaki ^ altındaysa) yeniden görev çubuğuna sürüklemen gerekebilir. 2.1'den 2.0'a geri dönmek desteklenmez (2.0 taşınan ayar klasörünü bulamaz).

*English:* NestDesk was called Düzenleme before 2.0.0. In 2.1.0 the program file, data folder (`%AppData%\NestDesk`), startup entry and folder-icon files moved to the new name; updating migrates everything automatically. Taskbar pins and the "always show" tray setting may need to be set again.

## Lisans

[MIT](LICENSE) © 2026 hasakobey — kullanmak, değiştirmek ve dağıtmak serbesttir; telif satırı ve lisans metni korunmalıdır. Kullanılan açık kaynak bileşenler ve lisansları: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) (kurulum ve zip'lerde de programın yanında gelir).
