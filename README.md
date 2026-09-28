# NestDesk

**Masaüstün için derli toplu bir yuva. · A tidy home for your desktop.**

Windows 10/11 için ücretsiz ve açık kaynaklı (MIT) masaüstü düzenleyici. · A free, open source (MIT) desktop organizer for Windows 10/11.

[Türkçe](#türkçe) · [English](#english) · [Gizlilik / Privacy](PRIVACY.md) · [Lisans / License](#lisans--license)

**İndir / Download:** [Releases](https://github.com/hasakobey/duzenleme/releases) — `NestDesk-Setup-<sürüm/version>.exe` (kurulum / installer, English + Türkçe) ya da / or `NestDesk-<sürüm/version>-<mimari/arch>-portable.zip`. Microsoft Store sürümü yayımlanınca bağlantısı buraya eklenecek. · The Microsoft Store link will be added here once it is published.

---

## Türkçe

NestDesk masaüstünü **bölmelere** ayırır, istersen masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** taşır ve duvar kâğıdının üzerine saat, takvim, not, yapılacaklar, zamanlayıcı gibi **widget'lar** ekler. Arayüz Türkçe ve İngilizcedir; Windows'un diline uyar (**Ayarlar → Genel → Dil / Language**).

### 2.1'deki yenilikler

- **Daha hızlı ve akıcı:** ayarlar arka planda kaydedilir, bölmeler yalnızca değişeni günceller; yavaş bilgisayarda ve ağ klasörlerinde donma yok.
- **Keskin görüntü:** her ekran ölçeğinde net yazı ve simgeler; widget'lar ekrana sığar, dar başlıklar kendini toplar.
- **İngilizce arayüz:** NestDesk artık Türkçe ve İngilizce.
- **Canlı menüler:** sağ tık menüsü seçenek değiştirirken açık kalır, değişiklik hemen görünür; arka planı, vurgu rengini, köşeleri, saydamlığı ve not rengini üzerine gelerek önizle.
- **Ad ve simge:** eklerken adını yaz, başlıktaki simgeye tıklayıp simge seç; kısayol kutusundaki öğelere kendi adını ve simgesini ver. **F2** ile widget'ı, bölmedeki dosyayı ya da kutudaki öğeyi yerinde yeniden adlandır.
- **Windows masaüstüne göz at** (`Ctrl+Alt+G`): Windows'un kendi simgeleri görünür, widget'lar çekilir, birkaç dakika sonra kendiliğinden geri dönülür.
- **Yeni widget'ların yeri:** imlecin yanı, ekranın ortası ya da köşe; yeni widget birkaç saniye vurgulu durur.
- **Kutulara eklediklerim masaüstünden kalksın:** kısayol kutusuna eklediğin masaüstü öğesi görünür bir `NestDesk` klasörüne taşınır, kutuda durur; tek tıkla geri konur.
- **Yeni widget'lar:** aylık takvim, geri sayım, zamanlayıcı / Pomodoro / kronometre, dünya saati, sistem durumu, Geri Dönüşüm Kutusu ve herhangi bir klasörü (İndirilenler, Belgeler…) gösteren bölme.
- Program dosyası `NestDesk.exe`, ayar klasörü `%AppData%\NestDesk` oldu; güncellemede her şey kendiliğinden taşınır.

### Kurulum

**`NestDesk-Setup-<sürüm>.exe`** — tek kurulum sihirbazı, her bilgisayar için:

- Windows 10 (1607+) ve Windows 11; **x64, ARM64 ve 32-bit** — bilgisayara uygun sürümü kendisi seçer.
- .NET kurulumu gerekmez (çalışma zamanı pakete dahil), **yönetici izni istemez** (kullanıcı başına `%LOCALAPPDATA%\Programs\NestDesk`'e kurulur).
- İngilizce/Türkçe sihirbaz: dil başta sorulur. Başlat menüsü kısayolu, isteğe bağlı masaüstü kısayolu ve "Windows ile başlat".
- Güncellerken çalışan uygulamayı düzgünce kapatır; ayarlar, notlar ve widget düzeni korunur. 2.0'dan güncellerken program dosyasının adı değiştiği için görev çubuğuna sabitlenmiş kısayolu ve tepsi simgesinin görünürlüğünü yeniden ayarlaman gerekebilir. 2.1'den 2.0'a dönmek desteklenmez: 2.0 ayarlarını eski `%AppData%\Duzenleme` klasöründe arar ve yeni kullanıcı gibi açılır; 2.1'in ayarları `%AppData%\NestDesk`'te durur ve 2.1 yeniden kurulunca kaldığı yerden devam eder.
- "Uygulamalar ve özellikler"den kaldırılır; kaldırırken gizli kalmış masaüstü simgelerini geri açar ve ayarların silinip silinmeyeceğini sorar (bkz. [Kaldırma](#kaldırma)).

**Microsoft Store (MSIX):** Store sürümünü Microsoft imzalar ve günceller; uyarı çıkmaz. Aynı programdır; farklar: "Windows ile başlat" Windows'un başlangıç görevidir (Ayarlar → Uygulamalar → Başlangıç'ta da görünür), ayarlar paketin kendi klasöründe tutulur ve masaüstünün "Bu Bilgisayar, Geri Dönüşüm Kutusu…" simgeleri Windows'un Temalar → Masaüstü simgesi ayarlarından açılıp kapatılır. Windows 10 2004 ve üstü gerekir.

**Taşınabilir kullanım:** `NestDesk-<sürüm>-<mimari>-portable.zip` dosyasını yazılabilir bir klasöre çıkar ve `NestDesk.exe`'yi çalıştır. İçindeki `portable.txt` sayesinde ayarlar exe'nin yanındaki `data` klasöründe tutulur (USB bellekte taşınabilir).

**Güncelleme:** NestDesk gizlilik sözü gereği kendisi güncelleme denetlemez. Store sürümü kendiliğinden güncellenir; kurulum sürümünde yeni kurulum dosyasını çalıştırman yeterli (**Ayarlar → Hakkında → İndirme sayfasını aç**); taşınabilir sürümde yeni zip'i aynı klasörün üzerine çıkar (`data` korunur).

> **İmza hakkında:** GitHub'daki kurulum dosyası ve zip'ler şimdilik kod imzası olmadan derlenir. Windows SmartScreen ilk açılışta "Windows bilgisayarınızı korudu" uyarısı gösterebilir (**Ek bilgi → Yine de çalıştır**); **Akıllı Uygulama Denetimi** (Smart App Control) açık bilgisayarlar imzasız programları engelleyebilir. Store sürümünde bu sorun yoktur.

### İlk açılış

- Üç adımlık kısa bir **karşılama** masaüstünü seninle birlikte kurar: bölmeler, dosya taşıma ve araçlar.
- **Onay verilmeden hiçbir dosya taşınmaz.** Otomatik taşıma sen açana kadar kapalıdır; 2. adım masaüstündeki dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir.
- "Şimdilik atla" masaüstünde hiçbir şeyi değiştirmez. Karşılamayı yeniden açmak için: **Ayarlar → Yardım → Karşılama** (var olanlar silinmez).
- NestDesk bildirim alanında (sistem tepsisi) çalışır; ana pencereyi kapatmak uygulamayı kapatmaz. Çıkmak için tepsi simgesine sağ tık → **Çıkış**.

### Ana pencere

Tepsi simgesine tıkla ya da `Ctrl+Alt+D`. Dört sayfa: **Ana sayfa** (Widget ekle, Şimdi düzenle, Son taşımayı geri al, son taşınanlar), **Widget'lar** (ekleme kutucukları, masaüstündekiler, Windows masaüstü simgeleri seçeneği, tüm widget'ların görünümü, yeni widget'ların yeri, kayıtlı düzenler), **Otomatik taşıma** (aç/kapat, kurallar, geçmiş ve geri alma) ve **Ayarlar** (Genel, Masaüstü, Klasör simgeleri, Gelişmiş, Yardım, Hakkında). Geri bildirimler pencerenin altındaki şeritte kalır; kaldırma gibi işlemler oradan **Geri al** ile geri alınır.

### Özellikler

#### Bölmeler

- **Masaüstünden:** Klasörler, Kısayollar (Bu Bilgisayar, Geri Dönüşüm Kutusu dahil), Dosyalar ya da Tüm masaüstü.
- **Bir klasörün içi:** masaüstündeki bir klasör (ör. PDF) ya da masaüstünde olmayan herhangi bir klasör: İndirilenler, Belgeler, Resimler ya da **Başka klasör…** (en yeni üstte, en çok 300 öğe; fazlası "Klasörde aç").
- **Yeni bölme…** kutucuğu adı, ne göstereceğini ve simgesini tek pencerede sorar.
- Başlıktaki 🔍 ya da `Ctrl+F` ile yazdıkça ara (alt klasörler dahil); dosyaları bölmeden bölmeye sürükle; resim/video/PDF önizlemesi; en yeni, en eski, ad, tür, boyut ya da elle (sürükleyerek) sıralama; başlığa katlanma.
- Klavye: `Enter` açar, `F2` yeniden adlandırır (diskte; gizli uzantılar korunur, çakışmada "(2)" önerilir), `Delete` Geri Dönüşüm Kutusu'na gönderir, ok tuşlarıyla gezinilir.

#### Windows masaüstü simgeleri (üç seçenek)

**Ayarlar → Masaüstü**, **Widget'lar** sayfası ve tepsi menüsünde aynı seçim:

1. **Hepsi masaüstünde görünsün** (varsayılan): Windows simgeleri her zamanki gibi; kutular ve bölmeler dosyalara dokunmadan kısayol gösterir.
2. **Kutulara eklediklerim masaüstünden kalksın:** Windows simgeleri görünür; kısayol kutusuna eklediğin (sürükleyerek ya da **Öğe ekle**) masaüstü öğesi masaüstü klasörünün yanındaki görünür `NestDesk\<kutunun adı>` klasörüne taşınır ("Oyunlar" adlı bir kutu için ör. `C:\Users\<ad>\NestDesk\Oyunlar`; masaüstün OneDrive'daysa OneDrive'ın içinde) ve kutuda durur. Masaüstüne sonradan gelenler görünür kalır. Öğeye sağ tık → **Masaüstüne geri koy**, kutudan çıkarmak ya da kutuyu kaldırmak öğeyi masaüstüne döndürür; Ayarlar'da **Klasörü aç** ve **Hepsini masaüstüne geri koy**. Ortak Masaüstü'ndeki (tüm hesapların gördüğü) öğeler ayrıca izin verilmedikçe taşınmaz. Dosyaların gizlenmez, öznitelikleri değişmez.
3. **Yalnızca bölmelerde göster:** Windows'un kendi simgeleri gizlenir, her şey bölmelerde görünür. Dosyalara dokunulmaz; başka seçeneğe geçince ya da uygulamadan çıkınca simgeler geri gelir. **"Masaüstümü bölmelere ayır"** ("Widget ekle" penceresi) gereken bölmeleri kurup bu seçeneğe geçer; bildirimdeki **Geri al** hepsini geri alır.

#### Windows masaüstüne göz at

`Ctrl+Alt+G`, tepsi menüsü, widget'a sağ tık ya da **Ayarlar → Masaüstü**: Windows'un kendi masaüstü simgeleri görünür, widget'lar kenara çekilir (ayarlanabilir). Ekranın üst ortasındaki küçük çubuk kalan süreyi gösterir: **+5 dk** ya da **NestDesk'e dön**. Varsayılan olarak 2 dakika sonra kendiliğinden dönülür (1/2/5/10 dakika ya da "ben dönene dek"); sen masaüstünde çalışırken beklenir. İstersen göz atarken açık pencereler de küçültülür (Win+D gibi; varsayılan kapalı). Hiçbir dosya değişmez.

**Boş masaüstüne çift tıklayınca:** Otomatik (önerilen; bölmeler masaüstünü yönetirken göz atar, yoksa simgeleri ve widget'ları gizler/gösterir), gizle/göster, göz at ya da hiçbir şey. Yalnızca masaüstünün kendisine çift tıklama sayılır; Gezgin pencereleri ve dosya seçme kutuları etkilemez.

#### Widget'lar

| Widget | Ne yapar |
|---|---|
| **Bölme** | Yukarıya bak |
| **Kısayol kutusu** | Sekmeli uygulama, dosya ve klasör rafı; sürükle-bırak ya da **Öğe ekle → Uygulama ya da dosya… / Klasör…**. Tek tıkla aç, yönetici olarak çalıştır; her öğeye **Yeniden adlandır (F2)** ve **Simgeyi değiştir…** (Windows simgeleri ya da bir resim) |
| **Saat** | Büyük dijital saat, 12/24 saat, isteğe bağlı saniye ve günün selamı |
| **Tarih** | Gün, ay, yıl ve bugünün vurgulandığı haftalık şerit |
| **Takvim** | Aylık takvim, hafta numaraları, haftanın ilk günü; `Page Up`/`Page Down` ay değiştirir, `Home` bugüne döner |
| **Dünya saati** | Seçtiğin şehirlerde saat kaç, kaç saat fark var, dün/yarın; Windows'un saat dilimleriyle (yaz saati dahil) |
| **Geri sayım** | Bir güne kaç gün kaldı (tatil, doğum günü…); her yıl yinelenebilir |
| **Zamanlayıcı** | Geri sayan zamanlayıcı; `Boşluk` başlat/duraklat, `R` sıfırla, **+1 dk**; süre dolunca bildirim ve vurgu çerçevesi. Sağ tık → **Tür** ile kronometre olur |
| **Pomodoro** | 25 dakika odak, 5 dakika mola, dört turda bir uzun mola; süreler ayarlanır, sonraki aşama kendiliğinden başlayabilir |
| **Not** | Yapışkan not, 6 renk, yazdıkça kaydedilir; `Ctrl + fare tekerleği` yazı boyutu |
| **Yapılacaklar** | Onay kutulu liste; biten maddeler üstü çizili kalır ya da istersen alta iner; "Bitenleri temizle" geri alınabilir |
| **Sistem durumu** | İşlemci, bellek, disk ve pil, açık kalma süresi; 2–10 saniyede bir, yalnızca görünürken; Görev Yöneticisi'ni açar |
| **Geri Dönüşüm Kutusu** | Kaç öğe var, ne kadar yer tutuyor; çift tık açar, üstüne bırakılan dosyayı kutuya gönderir, **Boşalt…** önce sorar |

**Widget eklemek:** `Ctrl+Alt+B`, tepsi simgesine sağ tık → **Widget ekle…**, bir widget'a sağ tık → **Yeni widget ekle…**, Başlat menüsündeki **NestDesk – Widget ekle** ya da ana pencerede **Widget'lar** sayfası. Kutucuklar üç bölümde: **Bölmeler**, **Araçlar**, **Saat ve bilgi**.

**Kaldırmak:** her widget'ın sağ üst köşesindeki sönük **×** hep yerindedir; bildirimdeki **Geri al** ya da tepsideki "Geri getir" ile geri gelir. × istenirse sağ tık → **Görünüm ▸ Göster ▸ Kaldır düğmesi (×)** ile gizlenir. Bölme ve kutudaki bir öğeyi kaldırmak için öğeye sağ tık → **Widget'tan kaldır** (bölmede dosyaya dokunmaz).

#### Ad, simge ve F2

- "Widget ekle"den eklenen bölme, kutu ve notun başlığı düzenlenir hâlde gelir: adını yaz, `Enter`. Başlıktaki simgeye tıklayınca 64 simgelik seçici açılır (seçerken önizlenir, **Vazgeç** eskiye döner).
- `F2` her yerde yeniden adlandırır: widget'ın başlığı, bölmede seçili dosya ya da klasör (diskte), kısayol kutusunda öğenin yalnızca görünen adı.

#### Sağ tık menüsü ve görünüm

- Menü, seçenek değiştirirken açık kalır ve işaretler yerinde güncellenir; **Arka plan** (Cam/Koyu/Açık), **Vurgu rengi**, **Köşeler**, **Saydamlık** ve not **Renk**'i üzerine gelince önizlenir, tıklayınca uygulanır.
- **Simgeler ▸** (bölme ve kutu): ızgara/liste, 4 boyut, hizalama, aralık, yazı boyutu, adları gizle, tek tıkla aç, önizleme. **Görünüm ▸**: göster/gizle (başlık, sayı, arama düğmesi, sekmeler, ×…), arka plan, vurgu rengi, ölçek, saydamlık, köşeler, gölge, "fare üstünde değilken soluk dursun". **Diğer ▸**: başlığa katla, konumu kilitle, mıknatıs, üst üste binmesin, düzenli yerleştir, çoğalt.
- **Widget'lar** sayfasında bütün widget'ların arka planı ve vurgu rengi tek yerden değişir; düzenleri adıyla kaydedip tek tıkla geri dönersin ("İş", "Oyun"…).

#### Yerleşim

- Sürüklerken widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır; bırakılan widget başkasının üstüne binmez, en yakın boş yere kayar. Taşırken `Alt` yapışmayı geçici kapatır, `Shift` ızgaraya hizalar. Kenarlardan büyüt; `Ctrl + tekerlek` bölmede simgeleri, notta yazıyı, diğerlerinde widget'ı büyütür.
- **Yeni widget'ların yeri** (Ayarlar → Masaüstü ya da Widget'lar sayfası): imlecin yanına (varsayılan), etkin ekranın ortasına ya da köşeye (türüne göre).
- Birden çok monitör ve farklı ekran ölçekleri desteklenir; monitör takılıp çıkarılınca ya da uykudan uyanınca widget'lar yerlerine döner, ekrandan taşmaz.

#### Otomatik taşıma (isteğe bağlı)

- Masaüstünde `PDF` adında bir klasör varsa masaüstüne düşen PDF'ler oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için de hazır kurallar var; **Otomatik taşıma** sayfasında klasör adı ↔ uzantı kurallarını değiştir. Klasörü olmayan kuralın satırında kaç dosyanın beklediği görünür; **Klasörü oluştur** ile tek tıkla açılır.
- Klasör masaüstünde yoksa dosyaya dokunulmaz (istersen **Gelişmiş → Klasör yoksa oluştur**). Büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- Kısayollar, gizli ve sistem dosyaları, klasörler ve inmekte olan dosyalar (`.crdownload`, `.part`) asla taşınmaz; aynı adlı dosyanın üzerine yazılmaz (`ad (1).pdf`). Kısayol kutusundaki masaüstü dosyaları da kurallarla taşınmaz.
- Her taşıma geri alınabilir; geri alınan dosya bir daha kendiliğinden taşınmaz.

#### Klasör simgeleri

- 30 sembol × 10 renkli hazır kütüphane; klasör adına göre öneri ("Oyunlarım" → oyun kolu). Kendi `.ico/.png/.svg` dosyanı da yükleyebilirsin.
- Masaüstünde yeni klasör açınca öneri bildirimi gelir. **Ayarlar → Klasör simgeleri → Tüm klasörlere önerilen simgeyi ver** hepsini bir kerede güzelleştirir; **Ayarlar → Gelişmiş → Klasör simgelerinin hepsini kaldır** hepsini geri alır.
- **İsteğe bağlı üretken yapay zekâ:** **Ayarlar → Gelişmiş → Yapay zekâ ile klasör simgesi**'ne kendi Claude (Anthropic) API anahtarını girersen, yazdığın açıklamaya göre ("retro oyun kolu, neon mor") özel simge üretilir. Anahtar DPAPI ile bu Windows hesabına bağlı şifrelenir; zorunlu değildir. Uygunsuz bir sonucu simge penceresindeki **Uygunsuz içeriği bildir** ile bildirebilirsin.

### Klavye kısayolları

Genel kısayollar (**Ayarlar → Gelişmiş → Klavye kısayolları**'ndan değiştirilebilir):

| Kısayol | İşlev |
|---|---|
| `Ctrl+Alt+B` | "Widget ekle" penceresi |
| `Ctrl+Alt+G` | Windows masaüstüne göz at / NestDesk'e dön |
| `Ctrl+Alt+H` | Masaüstünü gizle / göster |
| `Ctrl+Alt+N` | Yeni not |
| `Ctrl+Alt+O` | Masaüstünü şimdi düzenle |
| `Ctrl+Alt+D` | NestDesk'i aç |
| `Ctrl+Alt+W` | Widget'ları pencerelerin önüne getir (5 sn) |

Widget'ın içinde: `F2` yeniden adlandır · `Enter` aç · `Delete` Geri Dönüşüm Kutusu'na gönder (bölme) ya da kutudan kaldır · `Ctrl+F` bölmede ara · `Ctrl + tekerlek` boyut · `Shift+F10` seçili öğenin menüsü · zamanlayıcıda `Boşluk` / `R` · takvimde `Page Up` / `Page Down` / `Home`.

### Gizlilik

Hesap, reklam ve telemetri yok; her şey bilgisayarında kalır. Dosyalar yalnızca sen otomatik taşımayı ya da kutu seçeneğini açarsan taşınır ve her taşıma geri alınır. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir. Ayrıntılar: [PRIVACY.md](PRIVACY.md#türkçe).

### Kaldırma

- **Store:** Windows uygulamayla birlikte ayarlarını da siler. **Kurulum sürümü:** kaldırma programı gizli kalmış masaüstü simgelerini geri açar ve ayarların silinip silinmeyeceğini sorar. **Taşınabilir:** klasörü sil.
- Senin içeriğin olarak kalanlar: `NestDesk` kutu klasörü (önce **Ayarlar → Masaüstü → Hepsini masaüstüne geri koy**), klasör simgeleri (önce **Ayarlar → Gelişmiş → Klasör simgelerinin hepsini kaldır**) ve otomatik taşınan dosyalar (taşındıkları klasörde).

### Derleme

Gerekenler: Windows 10/11, **.NET 10 SDK**, kurulum dosyası için [Inno Setup 6.6+](https://jrsoftware.org/isdl.php). Kaynak koddaki `Duzenleme.*` adları (çözüm, proje, ad alanı, tek örnek kilidi) bilerek korunur: kullanıcıya görünmezler ve sürümlerin birbirini tanımasını sağlarlar.

```powershell
dotnet build Duzenleme.sln                    # derle (0 uyarı beklenir)
dotnet test Duzenleme.sln                     # testler
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # test klasörüyle çalıştır
dotnet run --project src/Duzenleme            # dikkat: gerçek masaüstünü izler!
powershell -File tools/publish.ps1            # dist/: kurulum sihirbazı + 3 taşınabilir zip
powershell -File tools/publish.ps1 -Arch x64  # hızlı deneme (yalnızca x64)
powershell -File tools/package-msix.ps1 -Strict  # dist-store/: Microsoft Store'a yüklenecek .msixbundle (kimlik: tools/store/identity.json)
powershell -File tools/perf/perf-run.ps1      # başarım ölçümü (test örneğiyle; gerçek masaüstüne dokunmaz)
```

Komut satırı: `--minimized` (tepside başla), `--add` ("Widget ekle"; uygulama çalışıyorsa ona iletilir), `--peek` (Windows masaüstüne göz at ya da NestDesk'e dön), `--welcome` (karşılamayı aç; uygulama kapalıyken), `--exit` (çalışan örneği düzgünce kapat), `--restore-desktop` (gizli kalmış Windows masaüstü simgelerini geri açıp çık; kaldırma programı kullanır), `--desktop <klasör> --data <klasör>` (test örneği: gerçek masaüstüne, "Windows ile başlat" kaydına ve Windows simgelerine dokunmaz).

Ortam değişkenleri (hepsi eski `DUZENLEME_*` adlarıyla da okunur): `NESTDESK_LANG=tr|en|pseudo` (arayüz dili; ekran görüntüleri ve testler için), `NESTDESK_DEBUGLOG=<dosya>` (tanılama günlüğü), `NESTDESK_WINDOW_AT` / `NESTDESK_QUICKADD_AT` / `NESTDESK_NEWWIDGET_AT` / `NESTDESK_PEEK_AT="x,y"` (fiziksel piksel; pencereleri o noktanın monitöründe açar), `NESTDESK_APPDATA_ROOT` (veri klasörü taşıma denemesi için; yalnızca `--desktop` ile ve `--data` olmadan), `NESTDESK_GPU=1` (widget'larda donanım çizimi). `--desktop` ile `--data` verilmezse veriler `%TEMP%\NestDesk-test-data\<anahtar>` klasöründe tutulur (gerçek ayarlara dokunulmaz). Boş bir `--data` klasörü karşılamayı açar; atlamak için klasöre `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` içerikli bir `settings.json` koy. Geliştirme yardımcıları: `--export-icon-sheet out.png`, `--render-svg in.svg out.png`.

### Yeni sürüm yayınlama

1. `src/Duzenleme/Duzenleme.csproj` → `<Version>` artırılır (ör. 2.1.1). Store aynı sürümü ikinci kez kabul etmez.
2. `tools/store/listing.md` → "Bu sürümdeki yenilikler" (TR/EN) yazılır.
3. `dotnet test` yeşil olmalı. Ayar dosyası geriye uyumlu kalır: yeni ayar = yeni alan; kalıcı enum'lara üye eklenmez.
4. Commit → `git tag v2.1.1` → push. GitHub Actions kurulum dosyasını (`NestDesk-Setup-2.1.1.exe`) ve 3 zip'i üretip Releases'ta "NestDesk 2.1.1" sürümünü açar.
5. **Store:** `powershell -File tools/package-msix.ps1 -Strict` → `dist-store\NestDesk-2.1.1.msixbundle`. Partner Center'da **yeni gönderim** açılır, yalnızca bu dosya yüklenir ve "What's new" doldurulur (adımlar: [tools/store/listing.md](tools/store/listing.md)).

---

## English

NestDesk splits your desktop into **panels**, can move files that land on the desktop into **folders you created**, and puts **widgets** such as a clock, calendar, notes, to-do lists and timers on your wallpaper. The app is in English and Turkish and follows your Windows display language (**Settings → General → Language / Dil**).

### What's new in 2.1

- **Faster and smoother:** settings are saved in the background and panels update only what changed, so slow PCs and network folders no longer freeze the app.
- **Crisp at any scaling:** sharp text and icons at every display scale; widgets always fit on the screen and narrow title bars tidy themselves up.
- **English interface:** NestDesk now speaks English and Turkish.
- **Live menus:** right-click menus stay open while you change options and show changes right away; hover to preview background, accent color, corners, transparency and note color.
- **Name and icon:** type a name as you add, click the icon in the title bar to pick one, and give shortcut box items their own names and icons. Press **F2** to rename a widget, a file in a panel or a box item in place.
- **Peek at the Windows desktop** (`Ctrl+Alt+G`): the Windows icons appear, widgets step aside, and NestDesk comes back on its own after a few minutes.
- **Where new widgets appear:** next to the pointer, in the center of the screen or in a corner; a new widget stays highlighted for a few seconds.
- **Items I add to boxes leave the desktop:** a desktop item you add to a shortcut box moves into a visible `NestDesk` folder and stays in the box; put it back with one click.
- **New widgets:** monthly calendar, countdown, timer / Pomodoro / stopwatch, world clock, system status, Recycle Bin, and panels that show any folder (Downloads, Documents…).
- The program file is now `NestDesk.exe` and the settings folder `%AppData%\NestDesk`; updating moves everything automatically.

### Installation

**`NestDesk-Setup-<version>.exe`** — one installer for every PC:

- Windows 10 (1607+) and Windows 11; **x64, ARM64 and 32-bit** — it picks the right build automatically.
- No .NET installation needed (the runtime is included) and **no administrator rights** (per-user install to `%LOCALAPPDATA%\Programs\NestDesk`).
- English/Turkish setup wizard: the language is asked first. Start menu shortcut, optional desktop shortcut and "Start with Windows".
- Updating closes the running app cleanly and keeps your settings, notes and widget layout. When updating from 2.0, the program file has a new name, so you may need to pin it to the taskbar again and set the tray icon to always show again. Going back from 2.1 to 2.0 is not supported: 2.0 looks for its settings in the old `%AppData%\Duzenleme` folder and starts like a new user; the 2.1 settings stay in `%AppData%\NestDesk` and are used again when 2.1 is reinstalled.
- Uninstall from "Apps & features"; the uninstaller shows any desktop icons left hidden and asks whether to delete your settings (see [Uninstalling](#uninstalling)).

**Microsoft Store (MSIX):** the Store edition is signed and updated by Microsoft, so there are no warnings. It is the same app, with these differences: "Start with Windows" is a Windows startup task (also listed in Settings → Apps → Startup), settings are kept in the package's own folder, and the "This PC, Recycle Bin…" desktop icons are turned on and off in Windows' Themes → Desktop icon settings. Requires Windows 10 version 2004 or later.

**Portable use:** extract `NestDesk-<version>-<arch>-portable.zip` to a writable folder and run `NestDesk.exe`. Thanks to the included `portable.txt`, settings are kept in the `data` folder next to the exe (works from a USB drive).

**Updates:** as part of its privacy promise, NestDesk never checks for updates by itself. The Store edition updates automatically; for the installer edition, run the new installer (**Settings → About → Open download page**); for the portable edition, extract the new zip over the same folder (`data` is kept).

> **About signing:** the installer and zips on GitHub are currently built without a code signature. Windows SmartScreen may show "Windows protected your PC" on first launch (**More info → Run anyway**), and PCs with **Smart App Control** turned on may block unsigned programs. The Store edition doesn't have this problem.

### First launch

- A short three-step **welcome** sets up your desktop with you: panels, moving files and tools.
- **No file is moved before you agree.** Auto-move stays off until you turn it on; step 2 shows in advance which of the files on your desktop would go to which folder.
- "Skip for now" changes nothing on your desktop. To open the welcome again: **Settings → Help → Welcome** (nothing you already have is deleted).
- NestDesk runs in the notification area (system tray); closing the main window keeps it running. To quit, right-click the tray icon → **Exit**.

### Main window

Click the tray icon or press `Ctrl+Alt+D`. Four pages: **Home** (Add widget, Tidy up now, Undo last move, recent moves), **Widgets** (add tiles, widgets on your desktop, the Windows desktop icons choice, the look of all widgets, where new widgets appear, saved layouts), **Auto-move** (on/off, rules, history and undo) and **Settings** (General, Desktop, Folder icons, Advanced, Help, About). Feedback stays in a strip at the bottom of the window; actions such as removing a widget can be undone there with **Undo**.

### Features

#### Panels

- **From the desktop:** Folders, Shortcuts (including This PC and Recycle Bin), Files, or the whole desktop.
- **Inside a folder:** a folder on the desktop (e.g. PDF) or any folder that isn't on the desktop: Downloads, Documents, Pictures or **Other folder…** (newest first, up to 300 items; the rest via "Open folder").
- The **New panel…** tile asks for the name, what to show and the icon in one window.
- Search as you type with the 🔍 button or `Ctrl+F` (including subfolders); drag files between panels; preview pictures, videos and PDFs; sort newest or oldest first, by name, type, size or by hand (drag to reorder); collapse to the title bar.
- Keyboard: `Enter` opens, `F2` renames (on disk; hidden extensions are kept and "(2)" is suggested on a name clash), `Delete` sends to the Recycle Bin, arrow keys move the selection.

#### Windows desktop icons (three choices)

The same choice is in **Settings → Desktop**, on the **Widgets** page and in the tray menu:

1. **Show everything on the desktop** (default): the Windows icons look as usual; boxes and panels show shortcuts without touching your files.
2. **Items I add to boxes leave the desktop:** the Windows icons stay visible; a desktop item you add to a shortcut box (by dragging or with **Add item**) is moved into a visible `NestDesk\<box name>` folder next to your Desktop folder (for a box named "Games", e.g. `C:\Users\<name>\NestDesk\Games`; inside OneDrive if your desktop is in OneDrive) and stays in the box. Items that land on the desktop later stay visible. Right-click the item → **Put back on the desktop**, removing it from the box, or removing the box returns it to the desktop; Settings offers **Open folder** and **Put all back on the desktop**. Items on the Public Desktop (seen by all accounts) are not moved unless you give separate permission. Your files are never hidden and their attributes never change.
3. **Show only in panels:** Windows' own icons are hidden and everything appears in panels. Your files are not touched; the icons come back when you pick another choice or quit the app. **"Sort my desktop into panels"** (in the Add widget window) creates the panels you need and switches to this choice; **Undo** in the notice reverts everything.

#### Peek at the Windows desktop

`Ctrl+Alt+G`, the tray menu, right-click a widget, or **Settings → Desktop**: Windows' own desktop icons appear and your widgets step aside (configurable). A small bar at the top center of the screen shows the time left, with **+5 min** and **Back to NestDesk**. By default NestDesk comes back after 2 minutes (1/2/5/10 minutes or "until I return"), and it waits while you are working on the desktop. Optionally, open windows are minimized while peeking (like Win+D; off by default). No file is changed.

**Double-click on the empty desktop:** Automatic (recommended; peeks while panels manage the desktop, otherwise hides/shows icons and widgets), hide/show, peek, or do nothing. Only double-clicks on the desktop itself count; File Explorer windows and file dialogs are not affected.

#### Widgets

| Widget | What it does |
|---|---|
| **Panel** | See above |
| **Shortcut box** | A tabbed shelf for apps, files and folders; drag and drop, or **Add item → App or file… / Folder…**. Open with one click, run as administrator; every item has **Rename (F2)** and **Change icon…** (Windows icons or an image) |
| **Clock** | Large digital clock, 12/24-hour, optional seconds and a greeting |
| **Date** | Day, month, year and a weekly strip that highlights today |
| **Calendar** | Monthly calendar with week numbers and first day of the week; `Page Up`/`Page Down` change the month, `Home` returns to today |
| **World clock** | The time in cities you choose, the time difference and yesterday/tomorrow, using Windows time zones (including daylight saving time) |
| **Countdown** | Days left until a date (vacation, birthday…); can repeat every year |
| **Timer** | A countdown timer; `Space` starts/pauses, `R` resets, **+1 min**; a notification and a highlight frame when time is up. Right-click → **Mode** turns it into a stopwatch |
| **Pomodoro** | 25 minutes of focus, 5-minute breaks, a long break every four rounds; lengths are adjustable and the next phase can start automatically |
| **Note** | Sticky note in 6 colors, saved as you type; `Ctrl + mouse wheel` changes the text size |
| **To-do** | Checklist; finished items stay crossed out in place or move to the bottom; "clear finished" can be undone |
| **System status** | Processor, memory, disk and battery, plus uptime; every 2–10 seconds, only while visible; opens Task Manager |
| **Recycle Bin** | How many items and how much space; double-click opens it, files dropped on it are recycled, **Empty…** asks first |

**Adding widgets:** `Ctrl+Alt+B`, right-click the tray icon → **Add widget…**, right-click a widget → **Add widget…**, **NestDesk – Add widget** in the Start menu, or the **Widgets** page in the main window. The tiles come in three groups: **Panels**, **Tools**, **Clocks and info**.

**Removing:** the dimmed **×** in each widget's top-right corner is always there; **Undo** in the notice or "Bring back" in the tray menu restores it. You can hide the × with right-click → **Appearance ▸ Show ▸ Remove button (×)**. To remove an item from a panel or box, right-click it → **Hide in this panel** / **Remove from box** (in a panel this never touches the file).

#### Name, icon and F2

- A panel, box or note added from the Add widget window starts with its title in edit mode: type a name and press `Enter`. Click the icon in the title bar to open a picker with 64 icons (previewed as you choose; **Cancel** restores the old one).
- `F2` renames everywhere: a widget's title, the selected file or folder in a panel (on disk), or only the displayed name of a shortcut box item.

#### Right-click menu and look

- The menu stays open while you change options, and its check marks update in place; **Background** (Glass/Dark/Light), **Accent color**, **Corners**, **Transparency** and a note's **Color** are previewed on hover and applied on click.
- **Icons ▸** (panels and boxes): grid/list, 4 sizes, alignment, spacing, text size, hide names, single-click open, previews. **Appearance ▸**: show/hide parts (title, count, search button, tabs, ×…), background, accent color, scale, transparency, corners, shadow, "fade when the pointer isn't on it". **More ▸**: collapse to title, lock position, snapping, no overlapping, arrange all, duplicate.
- On the **Widgets** page you can change the background and accent color of all widgets at once and save named layouts to switch back with one click ("Work", "Games"…).

#### Layout

- While dragging, widgets snap to each other and to screen edges like magnets; a dropped widget never lands on top of another but slides to the nearest free spot. While moving, `Alt` turns snapping off temporarily and `Shift` aligns to a grid. Resize from the edges; `Ctrl + wheel` makes icons bigger in panels, text bigger in notes, and other widgets bigger.
- **Where new widgets appear** (Settings → Desktop or the Widgets page): next to the pointer (default), in the center of the active screen, or in a corner (by widget type).
- Multiple monitors with different display scaling are supported; widgets return to their places when monitors are reconnected or the PC wakes up, and never extend off the screen.

#### Auto-move (optional)

- If your desktop has a folder named `PDF`, PDFs saved to the desktop go there. Ready-made rules also cover pictures, documents, archives, videos and music; change the folder name ↔ extension rules on the **Auto-move** page. A rule whose folder doesn't exist shows how many files are waiting, and **Create folder** creates it with one click.
- If the folder doesn't exist on the desktop, the file is left alone (optionally, turn on creating missing folders under **Advanced**). Matching ignores letter case and Turkish accents: `Arşivler` = `arsivler`. With English as the interface language, the default folders are PDF, Pictures, Documents, Archives, Videos and Music; existing folders in either language are reused.
- Shortcuts, hidden and system files, folders and downloads in progress (`.crdownload`, `.part`) are never moved, and no file is overwritten (`name (1).pdf`). Desktop files that are in a shortcut box are not moved by rules either.
- Every move can be undone; a file you moved back is never moved automatically again.

#### Folder icons

- A library of 30 symbols × 10 colors with suggestions by folder name ("Games" → game controller). You can also use your own `.ico/.png/.svg` files.
- Creating a new folder on the desktop brings up a suggestion. **Settings → Folder icons → Give suggested icons to all folders** styles them all at once; **Settings → Advanced → Remove all folder icons** undoes it.
- **Optional generative AI:** enter your own Claude (Anthropic) API key in **Settings → Advanced → AI folder icon** to generate a custom icon from a description you type ("retro game controller, neon purple"). The key is encrypted with DPAPI for your Windows account only, and it is not required. Report an inappropriate result with **Report inappropriate content** in the icon window.

### Keyboard shortcuts

Global shortcuts (customizable in **Settings → Advanced → Keyboard shortcuts**):

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+B` | "Add widget" window |
| `Ctrl+Alt+G` | Peek at the Windows desktop / back to NestDesk |
| `Ctrl+Alt+H` | Hide / show the desktop |
| `Ctrl+Alt+N` | New note |
| `Ctrl+Alt+O` | Tidy up the desktop now |
| `Ctrl+Alt+D` | Open NestDesk |
| `Ctrl+Alt+W` | Bring widgets in front of windows (5 s) |

Inside a widget: `F2` rename · `Enter` open · `Delete` send to the Recycle Bin (panel) or remove from the box · `Ctrl+F` search a panel · `Ctrl + wheel` size · `Shift+F10` menu for the selected item · `Space` / `R` in a timer · `Page Up` / `Page Down` / `Home` in the calendar.

### Privacy

No account, no ads, no telemetry; everything stays on your PC. Files are moved only if you turn on auto-move or the box option, and every move can be undone. The only feature that uses the internet is the optional AI folder icon. Details: [PRIVACY.md](PRIVACY.md#english).

### Uninstalling

- **Store:** Windows removes your settings together with the app. **Installer edition:** the uninstaller shows any desktop icons left hidden and asks whether to delete your settings. **Portable:** delete the folder.
- What stays as your own content: the `NestDesk` box folder (first use **Settings → Desktop → Put all back on the desktop**), folder icons (first use **Settings → Advanced → Remove all folder icons**) and files moved by auto-move (in the folders they were moved to).

### Building

Requirements: Windows 10/11, the **.NET 10 SDK**, and [Inno Setup 6.6+](https://jrsoftware.org/isdl.php) for the installer. The `Duzenleme.*` names in the source code (solution, project, namespace, single-instance lock) are kept on purpose: users never see them, and they let versions recognize each other.

```powershell
dotnet build Duzenleme.sln                    # build (expects 0 warnings)
dotnet test Duzenleme.sln                     # tests
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # run against a test folder
dotnet run --project src/Duzenleme            # careful: watches your real desktop!
powershell -File tools/publish.ps1            # dist/: installer + 3 portable zips
powershell -File tools/publish.ps1 -Arch x64  # quick build (x64 only)
powershell -File tools/package-msix.ps1 -Strict  # dist-store/: .msixbundle for the Microsoft Store (identity: tools/store/identity.json)
powershell -File tools/perf/perf-run.ps1      # performance measurement (test instance; never touches your real desktop)
```

Command line: `--minimized` (start in the tray), `--add` ("Add widget"; forwarded to the running app), `--peek` (peek at the Windows desktop, or return to NestDesk), `--welcome` (open the welcome; while the app is closed), `--exit` (close the running instance cleanly), `--restore-desktop` (show Windows desktop icons that were left hidden and exit; used by the uninstaller), `--desktop <folder> --data <folder>` (test instance: never touches your real desktop, the "Start with Windows" entry or the Windows icons).

Environment variables (the older `DUZENLEME_*` names work too): `NESTDESK_LANG=tr|en|pseudo` (interface language, for screenshots and tests), `NESTDESK_DEBUGLOG=<file>` (diagnostic log), `NESTDESK_WINDOW_AT` / `NESTDESK_QUICKADD_AT` / `NESTDESK_NEWWIDGET_AT` / `NESTDESK_PEEK_AT="x,y"` (physical pixels; open windows on that point's monitor), `NESTDESK_APPDATA_ROOT` (for testing the data folder migration; only with `--desktop` and without `--data`), `NESTDESK_GPU=1` (hardware rendering for widgets). Without `--data`, a `--desktop` test instance keeps its data in `%TEMP%\NestDesk-test-data\<key>` (your real settings are never touched). An empty `--data` folder opens the welcome; to skip it, put a `settings.json` containing `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` in the folder. Developer helpers: `--export-icon-sheet out.png`, `--render-svg in.svg out.png`.

---

## Code signing policy

Releases are in the application process for free code signing through the [SignPath Foundation](https://signpath.org) open source program. Once approved: *Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).*

- Signed files are built only from the source code in this repository with [GitHub Actions](.github/workflows/dotnet-desktop.yml); every release is approved manually.
- Committers and reviewers: [hasakobey](https://github.com/hasakobey)
- Approvers: [hasakobey](https://github.com/hasakobey)

**Privacy policy:** This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. The only exception is the optional AI folder icon feature: when the user enters their own API key and requests an icon, only the folder name and the user's description are sent to Anthropic ([Anthropic privacy policy](https://www.anthropic.com/legal/privacy)). Details: [PRIVACY.md](PRIVACY.md).

## Lisans / License

[MIT](LICENSE) © 2026 hasakobey. Kullanılan açık kaynak bileşenler / third-party components: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) (kurulum ve zip'lerde programın yanında gelir / shipped next to the program).

<sub>NestDesk'in 2.0.0'dan önceki adı Düzenleme'ydi. · NestDesk was called Düzenleme before 2.0.0.</sub>
