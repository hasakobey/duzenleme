# NestDesk

**Masaüstün için derli toplu bir yuva. · A tidy home for your desktop.**

Windows 10/11 için ücretsiz ve açık kaynaklı (MIT) masaüstü düzenleyici. · A free, open source (MIT) desktop organizer for Windows 10/11.

[Türkçe](#türkçe) · [English](#english) · [Gizlilik / Privacy](PRIVACY.md) · [Lisans / License](#lisans--license)

**İndir / Download:** [Releases](https://github.com/hasakobey/duzenleme/releases) — `NestDesk-Setup-<sürüm/version>.exe` (kurulum / installer, English + Türkçe) ya da / or `NestDesk-<sürüm/version>-<mimari/arch>-portable.zip`. **Microsoft Store:** [apps.microsoft.com/detail/9PCQ9C4BBWKP](https://apps.microsoft.com/detail/9PCQ9C4BBWKP)

---

## Türkçe

NestDesk masaüstünü **bölmelere** ayırır, istersen masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** taşır, uygulamalarını bir **halka menüde** toplar ve duvar kâğıdının üzerine saat, takvim, not, yapılacaklar, zamanlayıcı gibi **widget'lar** ekler. Arayüz Türkçe ve İngilizcedir; Windows'un diline uyar (**Ayarlar → Genel → Dil / Language**).

### 2.2'deki yenilikler

- **Halka menü:** uygulamaların, dosyaların, web adreslerin, komutların, tuş kısayolların ve metin parçacıkların bir halkada; tıkla ya da `1`–`9`'a bas. İstersen kendi seçtiğin kısayolla imlecin yanında açılır (varsayılan kapalı; **Ayarlar → Masaüstü → Halka menü**).
- **Widget temaları:** Neon, Gece, Kâğıt, Okyanus ve Windows'un açık/koyu ayarına uyan Otomatik; özel vurgu rengi. Yüksek karşıtlıkta widget'lar Windows'un renklerini kullanır.
- **Daha canlı hareket:** widget'lar hafifçe büyüyerek belirir, sayfalar ve bildirimler kayarak gelir; **Ayarlar → Genel → Animasyonlar**: Canlı, Sakin ya da Kapalı. Windows'un animasyon ayarına her zaman uyar.
- **1–9 ve akıllı bırakma:** bölmede ve kısayol kutusunda `1`–`9` ilk dokuz öğeyi açar (**Simgeler ▸ Numara tuşlarıyla aç**); kısayol kutusuna Başlat menüsünden uygulama, tarayıcıdan bağlantı sürükleyebilirsin.
- **Yerel tanılama:** kişisel bilgi içermeyen hata günlüğü, çökmeden sonra "rapor kaydedilsin mi?" sorusu, tanılama sayfası ve paketi. Hiçbir şey gönderilmez.
- **Daha güvenli güncelleme:** sürüm değişince ayarların, dokunulmadan önce `yedekler` klasörüne kopyalanır.
- **Daha az kaynak:** otomatik taşıma kapalıyken masaüstü izlenmez, ağdaki masaüstünde açılış beklemez, paket daha küçük; tepsi menüsü artık Windows'un kendi menüsü.

Önceki sürümlerin notları: [Releases](https://github.com/hasakobey/duzenleme/releases).

### Kurulum

**`NestDesk-Setup-<sürüm>.exe`** — tek kurulum sihirbazı, her bilgisayar için:

- Windows 10 (1607+) ve Windows 11; **x64, ARM64 ve 32-bit** — bilgisayara uygun sürümü kendisi seçer.
- .NET kurulumu gerekmez (çalışma zamanı pakete dahil), **yönetici izni istemez** (kullanıcı başına `%LOCALAPPDATA%\Programs\NestDesk`'e kurulur).
- İngilizce/Türkçe sihirbaz: dil başta sorulur. Başlat menüsü kısayolu, isteğe bağlı masaüstü kısayolu ve "Windows ile başlat".
- Güncellerken çalışan uygulamayı düzgünce kapatır; ayarlar, notlar ve widget düzeni korunur. 2.0'dan güncellerken program dosyasının adı değiştiği için görev çubuğuna sabitlenmiş kısayolu ve tepsi simgesinin görünürlüğünü yeniden ayarlaman gerekebilir. 2.1'den 2.0'a dönmek desteklenmez: 2.0 ayarlarını eski `%AppData%\Duzenleme` klasöründe arar ve yeni kullanıcı gibi açılır; 2.1'in ayarları `%AppData%\NestDesk`'te durur ve 2.1 yeniden kurulunca kaldığı yerden devam eder. 2.2'den itibaren sürüm değişince (güncelleme ya da eski sürüme dönüş) ayar dosyası, dokunulmadan önce `yedekler\before-<yeni>-from-<eski>-<zaman>.json` olarak kopyalanır (en yeni 3 kopya kalır).
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
- Klavye: `Enter` açar, `F2` yeniden adlandırır (diskte; gizli uzantılar korunur, çakışmada "(2)" önerilir), `Delete` Geri Dönüşüm Kutusu'na gönderir, ok tuşlarıyla gezinilir; harf yazınca o harfle başlayan öğeye gidilir. İstersen **Simgeler ▸ Numara tuşlarıyla aç (1–9)** ile `1`–`9` görünen ilk dokuz öğeyi açar (kısayol kutusunda da).

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
| **Kısayol kutusu** | Sekmeli uygulama, dosya ve klasör rafı; sürükle-bırak ya da **Öğe ekle → Uygulama ya da dosya… / Klasör…**. Başlat menüsünden uygulama, tarayıcıdan web ya da e-posta bağlantısı da bırakılabilir (bağlantı yalnızca sen açınca tarayıcıda açılır). Tek tıkla aç, yönetici olarak çalıştır; her öğeye **Yeniden adlandır (F2)** ve **Simgeyi değiştir…** (Windows simgeleri ya da bir resim) |
| **Halka menü** | Aşağıya bak |
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

#### Halka menü

- **Widget ekle → Halka menü** masaüstüne bir halka koyar (içinde Yeni not, Windows masaüstüne göz at, Widget ekle ve NestDesk'i aç). Halkaya uygulama, dosya ya da klasör sürükle; ortasına sağ tık → **Öğe ekle** ile **Web adresi…**, **Komut…**, **Tuş kısayolu gönder…**, **Metin yapıştır…** ya da **Alt halka…** ekle. Tıkla ya da `1`–`9`'a bas; oklar, `Enter`, `Backspace` (geri) ve `Esc` de çalışır.
- **Kısayolla imleçte açılsın** (**Ayarlar → Masaüstü → Halka menü** ya da halkaya sağ tık → **Diğer ▸**): varsayılan kapalıdır; açınca boş bir kısayol (ör. `Ctrl+Alt+R`) önerilir ve yalnızca o kısayol Windows'a kaydedilir. **Tuşu bırakınca seç** açıkken kısayolu basılı tutup imleci bir öğeye doğru götür, bırakınca o öğe açılır.
- Tuş kısayolu ve metin öğeleri yalnızca kısayolla açılan halkada ve yalnızca halkayı açmadan önce öndeki pencereye gider. Yapıştırılan metin pano geçmişine (Win+V) ve bulut panosuna eklenmez, önceki pano içeriği (metin, resim ya da dosya listesi) geri konur. Yönetici olarak çalışan pencerelere tuş ve metin gönderilemez (Windows engeller; NestDesk söyler). Komut yalnızca sen seçince çalışır; "Yönetici olarak çalıştır" işaretliyse Windows her seferinde sorar. Parolaları metin öğesi olarak kaydetme: öğeler `settings.json`'da düz metin durur.

#### Ad, simge ve F2

- "Widget ekle"den eklenen bölme, kutu ve notun başlığı düzenlenir hâlde gelir: adını yaz, `Enter`. Başlıktaki simgeye tıklayınca 64 simgelik seçici açılır (seçerken önizlenir, **Vazgeç** eskiye döner).
- `F2` her yerde yeniden adlandırır: widget'ın başlığı, bölmede seçili dosya ya da klasör (diskte), kısayol kutusunda öğenin yalnızca görünen adı.

#### Sağ tık menüsü ve görünüm

- Menü, seçenek değiştirirken açık kalır ve işaretler yerinde güncellenir; **Arka plan** (Cam/Koyu/Açık), **Vurgu rengi**, **Köşeler**, **Saydamlık** ve not **Renk**'i üzerine gelince önizlenir, tıklayınca uygulanır.
- **Simgeler ▸** (bölme ve kutu): ızgara/liste, 4 boyut, hizalama, aralık, yazı boyutu, adları gizle, tek tıkla aç, numara tuşlarıyla aç (1–9), önizleme. **Görünüm ▸**: göster/gizle (başlık, sayı, arama düğmesi, sekmeler, ×…), tema, arka plan, vurgu rengi, ölçek, saydamlık, köşeler, gölge, "fare üstünde değilken soluk dursun". **Diğer ▸**: başlığa katla, konumu kilitle, mıknatıs, üst üste binmesin, düzenli yerleştir, çoğalt.
- **Tema:** Klasik (Cam/Koyu/Açık arka plan), Windows'un açık/koyu ayarına uyan **Otomatik**, **Neon**, **Gece**, **Kâğıt** ve **Okyanus**; **Vurgu rengi ▸ Özel renk…** widget'ın yanında renk seçici açar (Windows'un vurgu rengi de seçilebilir). Yüksek karşıtlık açıkken widget'lar Windows'un renklerini kullanır. Temalar yalnızca widget'ların görünüşünü değiştirir.
- **Widget'lar** sayfasında bütün widget'ların teması, arka planı ve vurgu rengi tek yerden değişir; düzenleri adıyla kaydedip tek tıkla geri dönersin ("İş", "Oyun"…).
- **Animasyonlar** (**Ayarlar → Genel**): Canlı (widget'lar ve sayfalar hafifçe büyüyerek ya da kayarak açılır), Sakin (yalnızca kısa solmalar) ya da Kapalı. Windows'un "Animasyon efektleri" kapalıysa NestDesk'in hareketlerinin hiçbiri oynamaz; enerji tasarrufu ve uzak masaüstünde Sakin'e iner. Fareyle üstüne gelmek hiçbir şeyi büyütmez ya da kaydırmaz.

#### Yerleşim

- Sürüklerken widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır; bırakılan widget başkasının üstüne binmez, en yakın boş yere kayar. Taşırken `Alt` yapışmayı geçici kapatır, `Shift` ızgaraya hizalar. Kenarlardan büyüt; `Ctrl + tekerlek` bölmede simgeleri, notta yazıyı, diğerlerinde widget'ı büyütür.
- **Yeni widget'ların yeri** (Ayarlar → Masaüstü ya da Widget'lar sayfası): imlecin yanına (varsayılan), etkin ekranın ortasına ya da köşeye (türüne göre).
- Birden çok monitör ve farklı ekran ölçekleri desteklenir; monitör takılıp çıkarılınca ya da uykudan uyanınca widget'lar yerlerine döner, ekrandan taşmaz.

#### Otomatik taşıma (isteğe bağlı)

- Masaüstünde `PDF` adında bir klasör varsa masaüstüne düşen PDF'ler oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için de hazır kurallar var; **Otomatik taşıma** sayfasında her kural bir karttır (`PDF ← .pdf`): **Kural ekle** hazır türlerle (Tasarım, E-kitaplar…) ya da kendi yazdığın uzantıyla iki adımda kural ekler, ⋯ › **Değiştir…** uzantıları ve klasörü değiştirir. Klasörü olmayan kuralın kartında kaç dosyanın beklediği görünür; **Oluştur** ile tek tıkla açılır.
- Klasör masaüstünde yoksa dosyaya dokunulmaz (istersen **Gelişmiş → Klasör yoksa oluştur**). Büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- Kısayollar, gizli ve sistem dosyaları, klasörler ve inmekte olan dosyalar (`.crdownload`, `.part`) asla taşınmaz; aynı adlı dosyanın üzerine yazılmaz (`ad (1).pdf`). Kısayol kutusundaki masaüstü dosyaları da kurallarla taşınmaz.
- Her taşıma geri alınabilir; geri alınan dosya bir daha kendiliğinden taşınmaz.

#### Klasör simgeleri

- 30 sembol × 10 renkli hazır kütüphane; klasör adına göre öneri ("Oyunlarım" → oyun kolu). Kendi `.ico/.png/.svg` dosyanı da yükleyebilirsin.
- Masaüstünde yeni klasör açınca öneri bildirimi gelir. **Ayarlar → Klasör simgeleri → Tüm klasörlere önerilen simgeyi ver** hepsini bir kerede güzelleştirir; **Ayarlar → Gelişmiş → Klasör simgelerinin hepsini kaldır** hepsini geri alır.
- **İsteğe bağlı üretken yapay zekâ:** **Ayarlar → Gelişmiş → Yapay zekâ ile klasör simgesi**'ne kendi Claude (Anthropic) API anahtarını girersen, yazdığın açıklamaya göre ("retro oyun kolu, neon mor") özel simge üretilir. Anahtar DPAPI ile bu Windows hesabına bağlı şifrelenir; zorunlu değildir. Uygunsuz bir sonucu simge penceresindeki **Uygunsuz içeriği bildir** ile bildirebilirsin.

#### Tanılama ve sorun bildirme

- **Hata günlüğü** (**Ayarlar → Gelişmiş → Hata günlüğü**): hatalar bu bilgisayarda, kişisel bilgi olmadan kaydedilir (yollar yer tutucuyla değiştirilir; dosya adların, notların ve yazdıkların yazılmaz); en çok 4 MB, 14 gün. Kapatılabilir; **Günlükleri sil** hepsini siler. **Çökme olunca**: Her seferinde sor (varsayılan), Sormadan kaydet ya da Kaydetme.
- **Ayarlar → Yardım → Sorun bildir:** **Tanılama sayfasını aç** (betiksiz yerel sayfa), **Tanılama paketi oluştur** (içeriği önce listelenir, zip seçtiğin yere kaydedilir) ve **GitHub'da sorun aç**. NestDesk hiçbir şeyi kendiliğinden göndermez; GitHub sorun kayıtları herkese açıktır.

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
| (atanmamış) | Halka menüyü imleçte aç: varsayılan kapalı; **Ayarlar → Masaüstü → Halka menü → Kısayolla imleçte açılsın** boş bir kısayol önerir |

Widget'ın içinde: `F2` yeniden adlandır · `Enter` aç · `Delete` Geri Dönüşüm Kutusu'na gönder (bölme) ya da kutudan kaldır · `Ctrl+F` bölmede ara · `1`–`9` öğeyi aç (bölme ve kutuda seçenek açıksa, halkada her zaman) · `Ctrl + tekerlek` boyut · `Shift+F10` seçili öğenin menüsü · zamanlayıcıda `Boşluk` / `R` · takvimde `Page Up` / `Page Down` / `Home`.

### Gizlilik

Hesap, reklam ve telemetri yok; her şey bilgisayarında kalır. Dosyalar yalnızca sen otomatik taşımayı ya da kutu seçeneğini açarsan taşınır ve her taşıma geri alınır. Klavye kancası yok; halka menü kısayolu varsayılan olarak kapalı. Hata günlüğü kişisel bilgi içermez ve bilgisayarından çıkmaz. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir. Ayrıntılar: [PRIVACY.md](PRIVACY.md#türkçe).

### Kaldırma

- **Store:** Windows uygulamayla birlikte ayarlarını, halka menülerini, ayar yedeklerini ve hata günlüklerini de siler. **Kurulum sürümü:** kaldırma programı gizli kalmış masaüstü simgelerini geri açar ve ayarların (halka menüler, temalar, bırakılan bağlantılar, yedekler ve hata günlükleri dahil) silinip silinmeyeceğini sorar. **Taşınabilir:** klasörü sil.
- Senin içeriğin olarak kalanlar: `NestDesk` kutu klasörü (önce **Ayarlar → Masaüstü → Hepsini masaüstüne geri koy**), klasör simgeleri (önce **Ayarlar → Gelişmiş → Klasör simgelerinin hepsini kaldır**), otomatik taşınan dosyalar (taşındıkları klasörde) ve kaydettiğin tanılama paketleri.

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
powershell -File tools/perf/perf-run.ps1      # başarım ölçümü (test örneğiyle; gerçek masaüstüne dokunmaz; senaryolar ve eşikler: docs/perf.md)
```

Komut satırı: `--minimized` (tepside başla), `--add` ("Widget ekle"; uygulama çalışıyorsa ona iletilir), `--peek` (Windows masaüstüne göz at ya da NestDesk'e dön), `--welcome` (karşılamayı aç; uygulama kapalıyken), `--exit` (çalışan örneği düzgünce kapat), `--restore-desktop` (gizli kalmış Windows masaüstü simgelerini geri açıp çık; kaldırma programı kullanır), `--desktop <klasör> --data <klasör>` (test örneği: gerçek masaüstüne, "Windows ile başlat" kaydına ve Windows simgelerine dokunmaz).

Ortam değişkenleri (hepsi eski `DUZENLEME_*` adlarıyla da okunur): `NESTDESK_LANG=tr|en|pseudo` (arayüz dili; ekran görüntüleri ve testler için), `NESTDESK_DEBUGLOG=<dosya>` (ayrıntılı geliştirme günlüğü; hata günlüğünden ayrı), `NESTDESK_PERF_LOG=<dosya>` (başarım günlüğü), `NESTDESK_WINDOW_AT` / `NESTDESK_QUICKADD_AT` / `NESTDESK_NEWWIDGET_AT` / `NESTDESK_PEEK_AT` / `NESTDESK_RING_AT="x,y"` (fiziksel piksel; pencereleri, göz atma çubuğunu ve kısayolla açılan halkayı o noktanın monitöründe açar), `NESTDESK_TEST_RING=1` (yalnızca `--desktop` ile: halka kısayolu atanmamışken de açılır halkayı dener), `NESTDESK_TEST_HOTKEYS=1` (yalnızca `--desktop` ile: test örneği genel kısayolları da Windows'a kaydeder; yoksa hiçbirini kaydetmez, kısayol iletiyle denenir), `NESTDESK_TEST_CRASH=1` (yalnızca `--desktop` ile: açılıştan birkaç saniye sonra çökme denemesi), `NESTDESK_APPDATA_ROOT` (veri klasörü taşıma denemesi için; yalnızca `--desktop` ile ve `--data` olmadan), `NESTDESK_GPU=1` (widget'larda donanım çizimi). `--desktop` ile `--data` verilmezse veriler `%TEMP%\NestDesk-test-data\<anahtar>` klasöründe tutulur (gerçek ayarlara dokunulmaz). Boş bir `--data` klasörü karşılamayı açar; atlamak için klasöre `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` içerikli bir `settings.json` koy. Geliştirme yardımcıları: `--export-icon-sheet out.png`, `--render-svg in.svg out.png`.

### Yeni sürüm yayınlama

1. `src/Duzenleme/Duzenleme.csproj` → `<Version>` artırılır (ör. 2.2.0). Store aynı sürümü ikinci kez kabul etmez.
2. `tools/store/listing.md` → "Bu sürümdeki yenilikler" (TR/EN) yazılır.
3. `dotnet test` yeşil olmalı. Ayar dosyası geriye uyumlu kalır: yeni ayar = yeni alan; kalıcı enum'lara üye eklenmez. `tools/publish.ps1` ve `package-msix.ps1 -Strict`, `PRIVACY.md`, kurulumun "verileri sil" sorusu ve Store metinleri hata günlüğünü anlatmıyorsa yayın yapmaz (deneme derlemesi: `publish.ps1 -Draft`).
4. Commit → `git tag v2.2.0` → push. GitHub Actions kurulum dosyasını (`NestDesk-Setup-2.2.0.exe`) ve 3 zip'i üretip Releases'ta "NestDesk 2.2.0" sürümünü açar.
5. **Store:** `powershell -File tools/package-msix.ps1 -Strict` → `dist-store\NestDesk-2.2.0.msixbundle`. Partner Center'da **Start update** ile yeni gönderim açılır, yalnızca bu dosya yüklenir ve "What's new" doldurulur (adımlar: [tools/store/listing.md](tools/store/listing.md)).

---

## English

NestDesk splits your desktop into **panels**, can move files that land on the desktop into **folders you created**, gathers your apps in a **ring menu**, and puts **widgets** such as a clock, calendar, notes, to-do lists and timers on your wallpaper. The app is in English and Turkish and follows your Windows display language (**Settings → General → Language / Dil**).

### What's new in 2.2

- **Ring menu:** your apps, files, web addresses, commands, keyboard shortcuts and text snippets in a ring; click or press `1`–`9`. If you like, it opens next to the pointer with a shortcut you choose (off by default; **Settings → Desktop → Ring menu**).
- **Widget themes:** Neon, Night, Paper, Ocean and an Automatic theme that follows Windows light/dark, plus a custom accent color. In high contrast, widgets use your Windows colors.
- **Livelier motion:** widgets pop in gently, pages and notices slide in; **Settings → General → Animations**: Lively, Calm or Off. It always follows the Windows animation setting.
- **1–9 and smart drop:** `1`–`9` opens the first nine items of a panel or shortcut box (**Icons ▸ Open with number keys**); drag apps from the Start menu and links from your browser onto shortcut boxes.
- **Local diagnostics:** an error log without personal information, a "save a report?" prompt after a crash, and a diagnostics page and package. Nothing is sent.
- **Safer updates:** when the version changes, your settings are copied to the `yedekler` folder before they are touched.
- **Lighter:** the desktop isn't watched while auto-move is off, startup no longer waits for a network desktop, the download is smaller, and the tray menu is now the native Windows menu.

Notes for earlier versions: [Releases](https://github.com/hasakobey/duzenleme/releases).

### Installation

**`NestDesk-Setup-<version>.exe`** — one installer for every PC:

- Windows 10 (1607+) and Windows 11; **x64, ARM64 and 32-bit** — it picks the right build automatically.
- No .NET installation needed (the runtime is included) and **no administrator rights** (per-user install to `%LOCALAPPDATA%\Programs\NestDesk`).
- English/Turkish setup wizard: the language is asked first. Start menu shortcut, optional desktop shortcut and "Start with Windows".
- Updating closes the running app cleanly and keeps your settings, notes and widget layout. When updating from 2.0, the program file has a new name, so you may need to pin it to the taskbar again and set the tray icon to always show again. Going back from 2.1 to 2.0 is not supported: 2.0 looks for its settings in the old `%AppData%\Duzenleme` folder and starts like a new user; the 2.1 settings stay in `%AppData%\NestDesk` and are used again when 2.1 is reinstalled. From 2.2 on, whenever the version changes (an update or going back to an older version), the settings file is copied to `yedekler\before-<new>-from-<old>-<time>.json` before it is touched (the 3 newest copies are kept).
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
- Keyboard: `Enter` opens, `F2` renames (on disk; hidden extensions are kept and "(2)" is suggested on a name clash), `Delete` sends to the Recycle Bin, arrow keys move the selection; type a letter to jump to the item that starts with it. Optionally, **Icons ▸ Open with number keys (1–9)** makes `1`–`9` open the first nine visible items (in shortcut boxes too).

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
| **Shortcut box** | A tabbed shelf for apps, files and folders; drag and drop, or **Add item → App or file… / Folder…**. You can also drop apps from the Start menu and web or e-mail links from your browser (a link opens in the browser only when you open it). Open with one click, run as administrator; every item has **Rename (F2)** and **Change icon…** (Windows icons or an image) |
| **Ring menu** | See below |
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

#### Ring menu

- **Add widget → Ring menu** puts a ring on your desktop (with New note, Peek at the Windows desktop, Add widget and Open NestDesk). Drag apps, files or folders onto the ring; right-click its centre → **Add item** to add a **Web address…**, **Command…**, **Send keyboard shortcut…**, **Paste text…** or **Sub-ring…**. Click or press `1`–`9`; the arrow keys, `Enter`, `Backspace` (back) and `Esc` work too.
- **Open at the pointer with a shortcut** (**Settings → Desktop → Ring menu**, or right-click the ring → **More ▸**): off by default; turning it on suggests a free shortcut (e.g. `Ctrl+Alt+R`), and only that shortcut is registered with Windows. With **Select when I let go of the keys** on, hold the shortcut, move the pointer toward an item and let go to open it.
- Keyboard shortcut and text items work only in the ring opened by the shortcut, and go only to the window that was in front before you opened it. Pasted text isn't added to clipboard history (Win+V) or the cloud clipboard, and the previous clipboard content (text, an image or a list of files) is put back. Keys and text can't be sent to windows running as administrator (Windows blocks them; NestDesk tells you). A command runs only when you pick it; if it's marked "Run as administrator", Windows asks every time. Don't save passwords as text items: items are stored as plain text in `settings.json`.

#### Name, icon and F2

- A panel, box or note added from the Add widget window starts with its title in edit mode: type a name and press `Enter`. Click the icon in the title bar to open a picker with 64 icons (previewed as you choose; **Cancel** restores the old one).
- `F2` renames everywhere: a widget's title, the selected file or folder in a panel (on disk), or only the displayed name of a shortcut box item.

#### Right-click menu and look

- The menu stays open while you change options, and its check marks update in place; **Background** (Glass/Dark/Light), **Accent color**, **Corners**, **Transparency** and a note's **Color** are previewed on hover and applied on click.
- **Icons ▸** (panels and boxes): grid/list, 4 sizes, alignment, spacing, text size, hide names, single-click open, open with number keys (1–9), previews. **Appearance ▸**: show/hide parts (title, count, search button, tabs, ×…), theme, background, accent color, scale, transparency, corners, shadow, "fade when the pointer isn't on it". **More ▸**: collapse to title, lock position, snapping, no overlapping, arrange all, duplicate.
- **Theme:** Classic (Glass/Dark/Light background), **Automatic** (follows Windows light/dark), **Neon**, **Night**, **Paper** and **Ocean**; **Accent color ▸ Custom color…** opens a color picker next to the widget (Windows' accent color can be picked too). With high contrast on, widgets use your Windows colors. Themes only change how widgets look.
- On the **Widgets** page you can change the theme, background and accent color of all widgets at once and save named layouts to switch back with one click ("Work", "Games"…).
- **Animations** (**Settings → General**): Lively (widgets and pages open with a gentle zoom or slide), Calm (short fades only) or Off. If Windows "Animation effects" is off, none of NestDesk's own animations play; energy saver and Remote Desktop switch to Calm. Hovering never makes anything grow or slide.

#### Layout

- While dragging, widgets snap to each other and to screen edges like magnets; a dropped widget never lands on top of another but slides to the nearest free spot. While moving, `Alt` turns snapping off temporarily and `Shift` aligns to a grid. Resize from the edges; `Ctrl + wheel` makes icons bigger in panels, text bigger in notes, and other widgets bigger.
- **Where new widgets appear** (Settings → Desktop or the Widgets page): next to the pointer (default), in the center of the active screen, or in a corner (by widget type).
- Multiple monitors with different display scaling are supported; widgets return to their places when monitors are reconnected or the PC wakes up, and never extend off the screen.

#### Auto-move (optional)

- If your desktop has a folder named `PDF`, PDFs saved to the desktop go there. Ready-made rules also cover pictures, documents, archives, videos and music; on the **Auto-move** page each rule is a card (`PDF ← .pdf`): **Add rule** adds one in two steps from a ready-made type (Design, E-books…) or your own extensions, and ⋯ › **Change…** changes its extensions and folder. A rule whose folder doesn't exist shows how many files are waiting, and **Create** creates it with one click.
- If the folder doesn't exist on the desktop, the file is left alone (optionally, turn on creating missing folders under **Advanced**). Matching ignores letter case and Turkish accents: `Arşivler` = `arsivler`. With English as the interface language, the default folders are PDF, Pictures, Documents, Archives, Videos and Music; existing folders in either language are reused.
- Shortcuts, hidden and system files, folders and downloads in progress (`.crdownload`, `.part`) are never moved, and no file is overwritten (`name (1).pdf`). Desktop files that are in a shortcut box are not moved by rules either.
- Every move can be undone; a file you moved back is never moved automatically again.

#### Folder icons

- A library of 30 symbols × 10 colors with suggestions by folder name ("Games" → game controller). You can also use your own `.ico/.png/.svg` files.
- Creating a new folder on the desktop brings up a suggestion. **Settings → Folder icons → Give suggested icons to all folders** styles them all at once; **Settings → Advanced → Remove all folder icons** undoes it.
- **Optional generative AI:** enter your own Claude (Anthropic) API key in **Settings → Advanced → AI folder icon** to generate a custom icon from a description you type ("retro game controller, neon purple"). The key is encrypted with DPAPI for your Windows account only, and it is not required. Report an inappropriate result with **Report inappropriate content** in the icon window.

#### Diagnostics and reporting problems

- **Error log** (**Settings → Advanced → Error log**): errors are recorded on this PC without personal information (paths are replaced with placeholders; your file names, notes and what you type are never recorded); up to 4 MB, 14 days. You can turn it off, and **Delete logs** deletes everything. **When the app crashes**: Ask every time (default), Save without asking or Don't save.
- **Settings → Help → Report a problem:** **Open diagnostics page** (a local page without scripts), **Create diagnostic package** (lists the contents first, then saves a zip where you choose) and **Open an issue on GitHub**. NestDesk never sends anything on its own; GitHub issues are public.

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
| (unassigned) | Open the ring menu at the pointer: off by default; **Settings → Desktop → Ring menu → Open at the pointer with a shortcut** suggests a free shortcut |

Inside a widget: `F2` rename · `Enter` open · `Delete` send to the Recycle Bin (panel) or remove from the box · `Ctrl+F` search a panel · `1`–`9` open an item (in panels and boxes when the option is on, always in a ring) · `Ctrl + wheel` size · `Shift+F10` menu for the selected item · `Space` / `R` in a timer · `Page Up` / `Page Down` / `Home` in the calendar.

### Privacy

No account, no ads, no telemetry; everything stays on your PC. Files are moved only if you turn on auto-move or the box option, and every move can be undone. No keyboard hook; the ring menu shortcut is off by default. The error log contains no personal information and never leaves your PC. The only feature that uses the internet is the optional AI folder icon. Details: [PRIVACY.md](PRIVACY.md#english).

### Uninstalling

- **Store:** Windows removes your settings, ring menus, settings backups and error logs together with the app. **Installer edition:** the uninstaller shows any desktop icons left hidden and asks whether to delete your settings (including ring menus, themes, dropped links, backups and error logs). **Portable:** delete the folder.
- What stays as your own content: the `NestDesk` box folder (first use **Settings → Desktop → Put all back on the desktop**), folder icons (first use **Settings → Advanced → Remove all folder icons**), files moved by auto-move (in the folders they were moved to) and diagnostic packages you saved.

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
powershell -File tools/perf/perf-run.ps1      # performance measurement (test instance; never touches your real desktop; scenarios and thresholds: docs/perf.md)
```

Command line: `--minimized` (start in the tray), `--add` ("Add widget"; forwarded to the running app), `--peek` (peek at the Windows desktop, or return to NestDesk), `--welcome` (open the welcome; while the app is closed), `--exit` (close the running instance cleanly), `--restore-desktop` (show Windows desktop icons that were left hidden and exit; used by the uninstaller), `--desktop <folder> --data <folder>` (test instance: never touches your real desktop, the "Start with Windows" entry or the Windows icons).

Environment variables (the older `DUZENLEME_*` names work too): `NESTDESK_LANG=tr|en|pseudo` (interface language, for screenshots and tests), `NESTDESK_DEBUGLOG=<file>` (detailed developer log; separate from the error log), `NESTDESK_PERF_LOG=<file>` (performance log), `NESTDESK_WINDOW_AT` / `NESTDESK_QUICKADD_AT` / `NESTDESK_NEWWIDGET_AT` / `NESTDESK_PEEK_AT` / `NESTDESK_RING_AT="x,y"` (physical pixels; open windows, the peek bar and the shortcut ring on that point's monitor), `NESTDESK_TEST_RING=1` (only with `--desktop`: try the pop-up ring without assigning its shortcut), `NESTDESK_TEST_HOTKEYS=1` (only with `--desktop`: the test instance also registers its global shortcuts with Windows; otherwise it registers none and shortcuts are tried by message), `NESTDESK_TEST_CRASH=1` (only with `--desktop`: a test crash a few seconds after startup), `NESTDESK_APPDATA_ROOT` (for testing the data folder migration; only with `--desktop` and without `--data`), `NESTDESK_GPU=1` (hardware rendering for widgets). Without `--data`, a `--desktop` test instance keeps its data in `%TEMP%\NestDesk-test-data\<key>` (your real settings are never touched). An empty `--data` folder opens the welcome; to skip it, put a `settings.json` containing `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` in the folder. Developer helpers: `--export-icon-sheet out.png`, `--render-svg in.svg out.png`.

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
