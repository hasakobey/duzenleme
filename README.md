# NestDesk

**Masaüstün için derli toplu bir yuva. · A tidy home for your desktop.**

Windows 10/11 için ücretsiz ve açık kaynaklı (MIT) masaüstü düzenleyici. · A free, open source (MIT) desktop organizer for Windows 10/11.

[Türkçe](#türkçe) · [English](#english) · [Gizlilik / Privacy](PRIVACY.md) · [Lisans / License](#lisans--license)

**İndir / Download:** [Releases](https://github.com/hasakobey/duzenleme/releases) — `NestDesk-Setup-<sürüm/version>.exe` (kurulum / installer, English + Türkçe) ya da / or `NestDesk-<sürüm/version>-<mimari/arch>-portable.zip`. **Microsoft Store:** [apps.microsoft.com/detail/9PCQ9C4BBWKP](https://apps.microsoft.com/detail/9PCQ9C4BBWKP)

---

## Türkçe

NestDesk masaüstünü **bölmelere** ayırır, istersen masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** taşır, uygulamalarını bir **halka menüde** toplar ve duvar kâğıdının üzerine saat, takvim, not, yapılacaklar, zamanlayıcı gibi **widget'lar** ekler. Arayüz Türkçe ve İngilizcedir; Windows'un diline uyar (**Ayarlar → Dil / Language**).

### 2.2'deki yenilikler

- **Halka menü:** boş masaüstüne çift tıkla (ya da tepsiden **Halkayı aç**): NestDesk halkası imlecin yanında açılır; uygulamaların, klasörlerin, web adreslerin, komutların, tuş kısayolların ve metin parçacıkların bir halkada, tıkla ya da `1`–`9`'a bas. Halkanın altındaki **Halkayı düzenle** canlı önizlemeli düzenleyiciyi açar; masaüstündeki halka widget'ı dört biçimden birini alır (Klasik, Daire, Etiketli, Işıltı). İstersen kendi seçtiğin kısayolla da açılır (varsayılan kapalı).
- **Daha sade (Kolay NestDesk):** her widget'ın tek **Düzenle** paneli (köşedeki ⋯), en çok beş satırlık sağ tık menüleri, widget'ları yöneten **Widget'lar** sayfası, yedi satırlık **Ayarlar** (+ **Gelişmiş ayarlar**) ve üç adımlık karşılama.
- **Widget temaları:** Neon, Gece, Kâğıt, Okyanus ve Windows'un açık/koyu ayarına uyan Otomatik; özel vurgu rengi. Yüksek karşıtlıkta widget'lar Windows'un renklerini kullanır.
- **Daha canlı hareket:** widget'lar hafifçe büyüyerek belirir, sayfalar ve bildirimler kayarak gelir; **Ayarlar → Gelişmiş ayarlar → Görünüm → Animasyonlar**: Canlı, Sakin ya da Kapalı. Windows'un animasyon ayarına her zaman uyar.
- **1–9 ve akıllı bırakma:** bölmede ve kısayol kutusunda `1`–`9` ilk dokuz öğeyi açar (**Düzenle → Daha fazla → Numara tuşlarıyla aç**); kısayol kutusuna Başlat menüsünden uygulama, tarayıcıdan bağlantı sürükleyebilirsin.
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

- Üç adımlık kısa bir **karşılama** masaüstünü seninle birlikte kurar: bölmeler, dosya taşıma ve NestDesk halkan (boş masaüstüne çift tıklayınca açılsın mı, bir tuşla da açılsın mı) ile masaüstüne eklenecek araçlar.
- **Onay verilmeden hiçbir dosya taşınmaz.** Otomatik taşıma sen açana kadar kapalıdır; 2. adım masaüstündeki dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir.
- "Şimdilik atla" masaüstünde hiçbir şeyi değiştirmez. Karşılamayı yeniden açmak için: **Ayarlar → Yardım → Karşılamayı yeniden başlat** (var olanlar silinmez).
- NestDesk bildirim alanında (sistem tepsisi) çalışır; ana pencereyi kapatmak uygulamayı kapatmaz. Çıkmak için tepsi simgesine sağ tık → **Çıkış**.

### Ana pencere

Tepsi simgesine tıkla ya da `Ctrl+Alt+D`. Dört sayfa: **Ana sayfa** (Widget ekle, Halkayı düzenle, ilk ziyarette "Nasıl kullanılır" kartları, tek satırlık otomatik taşıma: Geri al, Dosyaları şimdi taşı ve aç/kapat, son taşınanlar), **Widget'lar** (Widget ekle, "Tüm widget'lar": bir görünümü ya da rengi hepsine uygula, "Masaüstündekiler" listesi ve seçilenin yanında Düzenle paneli; "Daha fazla"da hizala, öne getir ve kayıtlı düzenler), **Otomatik taşıma** (aç/kapat, kurallar, geçmiş ve geri alma) ve **Ayarlar** (Dil, Windows ile başlat, Simgeleri bölmelerde topla, Halka menü, Yardım, Hakkında ve **Gelişmiş ayarlar**: Masaüstü, Widget yerleşimi, Görünüm, Klavye kısayolları, Klasör simgeleri, Kutular, Sorun giderme). Geri bildirimler pencerenin altındaki şeritte kalır; kaldırma gibi işlemler oradan **Geri al** ile geri alınır.

### Özellikler

#### Bölmeler

- **Masaüstünden:** Klasörler, Kısayollar (Bu Bilgisayar, Geri Dönüşüm Kutusu dahil), Dosyalar ya da Tüm masaüstü.
- **Bir klasörün içi:** masaüstündeki bir klasör (ör. PDF) ya da masaüstünde olmayan herhangi bir klasör: İndirilenler, Belgeler, Resimler ya da **Başka klasör…** (en yeni üstte, en çok 300 öğe; fazlası "Klasörde aç").
- **Yeni bölme…** kutucuğu adı, ne göstereceğini ve simgesini tek pencerede sorar.
- Başlıktaki 🔍 ya da `Ctrl+F` ile yazdıkça ara (alt klasörler dahil); dosyaları bölmeden bölmeye sürükle; resim/video/PDF önizlemesi; en yeni, en eski, ad, tür, boyut ya da elle (sürükleyerek) sıralama; başlığa katlanma.
- Klavye: `Enter` açar, `F2` yeniden adlandırır (diskte; gizli uzantılar korunur, çakışmada "(2)" önerilir), `Delete` Geri Dönüşüm Kutusu'na gönderir, ok tuşlarıyla gezinilir; harf yazınca o harfle başlayan öğeye gidilir. İstersen **Düzenle → Daha fazla → Numara tuşlarıyla aç (1–9)** ile `1`–`9` görünen ilk dokuz öğeyi açar (kısayol kutusunda da).

#### Windows masaüstü simgeleri

**Ayarlar → Simgeleri bölmelerde topla** (karşılamanın 1. adımında **Evet, bölmelere ayır** / **Hayır, simgeler kalsın**):

1. **Kapalı:** Windows simgeleri her zamanki gibi; bölmeler dosyalara dokunmadan kısayol gösterir.
2. **Açık:** Windows'un kendi simgeleri gizlenir, her şey bölmelerde görünür. Dosyalara dokunulmaz; anahtarı kapatınca ya da uygulamadan çıkınca simgeler geri gelir. **"Masaüstümü bölmelere ayır"** ("Widget ekle" penceresi) gereken bölmeleri kurup bunu açar; bildirimdeki **Geri al** hepsini geri alır.

#### Kutuya koyduğum simgeler masaüstünden kalksın

"Kutuya ya da halkaya koyduğun masaüstü simgesi masaüstünden kalkar; çıkarınca geri gelir." Kapalıyken kutu ve halka yalnızca bağlantı tutar. İlk masaüstü simgesini kutuya koyduğunda kutunun yanında bir kez sorulur (**Evet, kalksın** / **Hayır, kalsın**); sonra **Ayarlar → Gelişmiş ayarlar → Kutular**'dan açılıp kapanır.

- **Windows simgeleri görünürken** dosya masaüstü klasörünün yanındaki görünür `NestDesk\<kutunun adı>` klasörüne taşınır ("Oyunlar" adlı bir kutu için ör. `C:\Users\<ad>\NestDesk\Oyunlar`; masaüstün OneDrive'daysa OneDrive'ın içinde) ve kutuda durur. Dosyaların gizlenmez, öznitelikleri değişmez; kökteki `Bu klasör nedir.txt` her dosyanın eski yerini yazar.
- **"Simgeleri bölmelerde topla" açıkken** hiçbir dosya taşınmaz: kutudaki simgeler masaüstü bölmelerinde görünmez.
- **Çıkarınca geri gelir:** **Kutudan kaldır** / **Halkadan kaldır** (`Delete`), masaüstüne ya da bir bölmeye sürüklemek, sekmeyi, alt halkayı ya da kutunun kendisini kaldırmak; bildirimdeki **Geri al** yeniden kutuya koyar. Başka bir kutuda da duruyorsa yalnızca oradan kalkar. Ayarlar'da **Klasörü aç** ve **Hepsini masaüstüne geri koy**; seçeneği kapatmak da hepsini geri koyar (**Geri al** ile).
- Seçenek açıkken masaüstünde kalmış bir simgede (önceden eklenen, "Şimdi değil", açık dosya) öğe menüsü **Masaüstünden kaldır** sunar.
- **Klasörler taşınmaz** (kutuda bağlantı olarak durur). **Herkesin masaüstündeki** simgeler (çoğu uygulamanın kısayolu) bir kez ayrıca sorulur ve Windows'un yönetici onayıyla taşınır; Store sürümü onları taşımaz.
- **Uygulama eklenince masaüstündeki kısayolu da kalksın:** Başlat menüsünden eklediğin uygulamanın masaüstünde birebir aynı uygulamayı gösteren kısayolu varsa ilk seferde sorulur (**Evet, hep böyle yap** / **Hayır**).

#### Windows masaüstüne göz at

`Ctrl+Alt+G`, tepsi menüsü, halkadaki **Windows masaüstüne göz at** ya da **Ayarlar → Gelişmiş ayarlar → Masaüstü**: Windows'un kendi masaüstü simgeleri görünür, widget'lar kenara çekilir (ayarlanabilir). Ekranın üst ortasındaki küçük çubuk kalan süreyi gösterir: **+5 dk** ya da **NestDesk'e dön**. Varsayılan olarak 2 dakika sonra kendiliğinden dönülür (1/2/5/10 dakika ya da "ben dönene dek"); sen masaüstünde çalışırken beklenir. İstersen göz atarken açık pencereler de küçültülür (Win+D gibi; varsayılan kapalı). Hiçbir dosya değişmez.

**Boş masaüstüne çift tıklayınca** (**Ayarlar → Gelişmiş ayarlar → Masaüstü**): halkayı aç (karşılamada yeni kullanıcıya önerilir; **Ayarlar → Halka menü**'deki anahtar da bunu seçer), Otomatik (önerilen; bölmeler masaüstünü yönetirken göz atar, yoksa simgeleri ve widget'ları gizler/gösterir), gizle/göster, göz at ya da hiçbir şey. Yalnızca masaüstünün kendisine çift tıklama sayılır; Gezgin pencereleri ve dosya seçme kutuları etkilemez.

#### Widget'lar

| Widget | Ne yapar |
|---|---|
| **Bölme** | Yukarıya bak |
| **Kısayol kutusu** | Sekmeli uygulama, dosya ve klasör rafı; sürükle-bırak, sağ tık → **Uygulama ekle…** ya da Düzenle panelindeki ekleme düğmeleri. Başlat menüsünden uygulama, tarayıcıdan web ya da e-posta bağlantısı da bırakılabilir (bağlantı yalnızca sen açınca tarayıcıda açılır). Tek tıkla aç, yönetici olarak çalıştır; her öğeye **Yeniden adlandır (F2)** ve **Simgeyi değiştir…** (Windows simgeleri ya da bir resim) |
| **Halka menü** | Aşağıya bak |
| **Saat** | Büyük dijital saat, 12/24 saat, isteğe bağlı saniye ve günün selamı |
| **Tarih** | Gün, ay, yıl ve bugünün vurgulandığı haftalık şerit |
| **Takvim** | Aylık takvim, hafta numaraları, haftanın ilk günü; `Page Up`/`Page Down` ay değiştirir, `Home` bugüne döner |
| **Dünya saati** | Seçtiğin şehirlerde saat kaç, kaç saat fark var, dün/yarın; Windows'un saat dilimleriyle (yaz saati dahil) |
| **Geri sayım** | Bir güne kaç gün kaldı (tatil, doğum günü…); her yıl yinelenebilir |
| **Zamanlayıcı** | Geri sayan zamanlayıcı; `Boşluk` başlat/duraklat, `R` sıfırla, **+1 dk**; süre dolunca bildirim ve vurgu çerçevesi. Düzenle → **Tür** ile kronometre olur |
| **Pomodoro** | 25 dakika odak, 5 dakika mola, dört turda bir uzun mola; süreler ayarlanır, sonraki aşama kendiliğinden başlayabilir |
| **Not** | Yapışkan not, 6 renk, yazdıkça kaydedilir; `Ctrl + fare tekerleği` yazı boyutu |
| **Yapılacaklar** | Onay kutulu liste; biten maddeler üstü çizili kalır ya da istersen alta iner; "Bitenleri temizle" geri alınabilir |
| **Sistem durumu** | İşlemci, bellek, disk ve pil, açık kalma süresi; 2–10 saniyede bir, yalnızca görünürken; Görev Yöneticisi'ni açar |
| **Geri Dönüşüm Kutusu** | Kaç öğe var, ne kadar yer tutuyor; çift tık açar, üstüne bırakılan dosyayı kutuya gönderir, **Boşalt…** önce sorar |

**Widget eklemek:** `Ctrl+Alt+B`, tepsi simgesine sağ tık → **Widget ekle…**, halkadaki **Widget ekle**, Başlat menüsündeki **NestDesk – Widget ekle** ya da ana pencerede **Ana sayfa** / **Widget'lar** sayfasındaki **Widget ekle**. İlk sayfada sık kullanılan sekiz kutucuk, **Hepsini göster**'de öbürleri (halka menü, Pomodoro, dünya saati…), **Bölme ›**'de bölmeler ve **Yeni bölme…**.

**Kaldırmak:** her widget'ın sağ üst köşesindeki sönük **×** hep yerindedir (sağ tık → **Kaldır** ya da Düzenle panelindeki **Kaldır** da olur); bildirimdeki **Geri al** ya da tepsideki "Geri getir" ile geri gelir. × istenirse **Düzenle → Daha fazla → Kaldır düğmesi (×)** ile gizlenir. Bölmedeki bir öğeyi gizlemek için öğeye sağ tık → **Bölmeden gizle** (dosyaya dokunmaz), kutudakini çıkarmak için **Kutudan kaldır**.

#### Halka menü

- **NestDesk halkası** hazır gelir (içinde Yeni not, Windows masaüstüne göz at, Widget ekle ve NestDesk'i aç): boş masaüstüne çift tıkla (karşılamada seçilir; sonra **Ayarlar → Halka menü → Boş masaüstüne çift tıklayınca açılsın**) ya da tepsi simgesine sağ tık → **Halkayı aç**; halka imlecin yanında açılır. Tıkla ya da `1`–`9`'a bas; oklar, `Enter`, `Backspace` (geri) ve `Esc` de çalışır.
- **Halkayı düzenle** (halkanın altında, Ana sayfada ve Ayarlar'da): canlı önizlemeli düzenleyici. Halkada bir yere tıklayıp seç, **◀ / ▶** (`Alt+←/→`) ile taşı, **Değiştir…** ya da **Kaldır** (bildirimde **Geri al**); ekleme çubuğunda **+ Uygulama**, **+ Klasör**, **+ Web adresi**, **+ NestDesk eylemi** ve **Daha fazla ▾** (**Komut…**, **Tuşlara bas…**, **Metin yapıştır…**, **Alt halka…**).
- **Halka widget'ı:** **Widget ekle → Hepsini göster → Halka menü** masaüstüne bir halka koyar; uygulama, dosya ya da klasör sürükle ya da sondaki **+** yerine tıkla. Düzenle panelinde aynı düzenleyici ve **Halka biçimi**: Klasik, Daire (yörünge), Etiketli (adlar halkanın çevresinde) ya da Işıltı. Biçim yalnızca çizimi değiştirir; yüksek karşıtlıkta hepsi Windows renkleriyle Klasik çizilir.
- **Tuşla imlecin yanında açılsın** (**Ayarlar → Halka menü**, halka düzenleyicisinin altı ya da halka widget'ına sağ tık): varsayılan kapalıdır; açınca boş bir kısayol (ör. `Ctrl+Alt+R`) önerilir ve yalnızca o kısayol Windows'a kaydedilir. **Tuşu bırakınca seç** açıkken kısayolu basılı tutup imleci bir öğeye doğru götür, bırakınca o öğe açılır.
- Tuş kısayolu ve metin öğeleri yalnızca kısayolla açılan halkada ve yalnızca halkayı açmadan önce öndeki pencereye gider (çift tıklamayla ya da tepsiden açılan halkada soluk durur ve çalışmaz). Yapıştırılan metin pano geçmişine (Win+V) ve bulut panosuna eklenmez, önceki pano içeriği (metin, resim ya da dosya listesi) geri konur. Yönetici olarak çalışan pencerelere tuş ve metin gönderilemez (Windows engeller; NestDesk söyler). Komut yalnızca sen seçince çalışır; "Yönetici olarak çalıştır" işaretliyse Windows her seferinde sorar. Parolaları metin öğesi olarak kaydetme: öğeler `settings.json`'da düz metin durur.

#### Düzenle paneli, ad ve simge

- Köşedeki **⋯** ya da sağ tık → **Düzenle…** widget'ın yanında tek bir panel açar: simge (64 simgelik seçici) ve ad, widget'ın kendi ayarları (bölmede ne gösterileceği ve görünüm, kutuda öğeler ve sekmeler, notta renk ve liste, zamanlayıcıda tür ve süre…), **Görünüm**, **Göster** (başlık, sayı, arama düğmesi, sekmeler…) ve kapalı **Daha fazla** (saydamlık, köşeler, gölge, konumu kilitle, başlığa katla, çoğalt, numara tuşları, "fare üstünde değilken soluk dursun"…). Her seçim hemen uygulanır; **Geri al** paneli açtığın hâle döndürür, **Bitti** ya da `Esc` kapatır.
- Sağ tık menüsü en çok beş satırdır: **Düzenle…**, widget'ın en sık iki işi (ör. bölmede "Bu bölmede ara", zamanlayıcıda Başlat · Sıfırla) ve **Kaldır**.
- Yeni widget adlandırmaya açılmaz (not yazı alanıyla, yapılacaklar ilk satırla açılır); ad Düzenle panelinden ya da `F2` ile verilir. `F2` her yerde yeniden adlandırır: widget'ın başlığı, bölmede seçili dosya ya da klasör (diskte), kısayol kutusunda öğenin yalnızca görünen adı.

#### Görünüm

- **Görünüm** galerisi gerçek widget gibi çizilir: **Otomatik** (Windows'un açık/koyu ayarına uyar), **Cam**, **Koyu**, **Açık** ve **Neon**, **Gece**, **Kâğıt**, **Okyanus** temaları; **Renk** beş nokta ya da **Özel…** (widget'ın yanında canlı önizlemeli renk seçici; Windows'un vurgu rengi de seçilebilir); **Boyut** Küçük/Orta/Büyük. **Bu görünümü tüm widget'lara uygula** hepsine geçirir (**Geri al** ile). Yüksek karşıtlık açıkken widget'lar Windows'un renklerini kullanır. Temalar yalnızca widget'ların görünüşünü değiştirir.
- **Widget'lar** sayfasında **Tüm widget'lar** şeridi bir görünümü ya da rengi bütün widget'lara tek yerden verir; düzenleri adıyla kaydedip tek tıkla geri dönersin ("İş", "Oyun"…).
- **Animasyonlar** (**Ayarlar → Gelişmiş ayarlar → Görünüm**): Canlı (widget'lar ve sayfalar hafifçe büyüyerek ya da kayarak açılır), Sakin (yalnızca kısa solmalar) ya da Kapalı. Windows'un "Animasyon efektleri" kapalıysa NestDesk'in hareketlerinin hiçbiri oynamaz; enerji tasarrufu ve uzak masaüstünde Sakin'e iner. Fareyle üstüne gelmek hiçbir şeyi büyütmez ya da kaydırmaz.

#### Yerleşim

- Sürüklerken widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır; bırakılan widget başkasının üstüne binmez, en yakın boş yere kayar. Taşırken `Alt` yapışmayı geçici kapatır, `Shift` ızgaraya hizalar. Kenarlardan büyüt; `Ctrl + tekerlek` bölmede simgeleri, notta yazıyı, diğerlerinde widget'ı büyütür.
- **Yeni widget'ların yeri** (Ayarlar → Gelişmiş ayarlar → Masaüstü): imlecin yanına (varsayılan), etkin ekranın ortasına ya da köşeye (türüne göre).
- Birden çok monitör ve farklı ekran ölçekleri desteklenir; monitör takılıp çıkarılınca ya da uykudan uyanınca widget'lar yerlerine döner, ekrandan taşmaz.

#### Otomatik taşıma (isteğe bağlı)

- Masaüstünde `PDF` adında bir klasör varsa masaüstüne düşen PDF'ler oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için de hazır kurallar var; **Otomatik taşıma** sayfasında her kural bir karttır (`PDF ← .pdf`): **Kural ekle** hazır türlerle (Tasarım, E-kitaplar…) ya da kendi yazdığın uzantıyla iki adımda kural ekler, ⋯ › **Değiştir…** uzantıları ve klasörü değiştirir. Klasörü olmayan kuralın kartında kaç dosyanın beklediği görünür; **Oluştur** ile tek tıkla açılır.
- Klasör masaüstünde yoksa dosyaya dokunulmaz (istersen **Gelişmiş → Klasör yoksa oluştur**). Büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- Kısayollar, gizli ve sistem dosyaları, klasörler ve inmekte olan dosyalar (`.crdownload`, `.part`) asla taşınmaz; aynı adlı dosyanın üzerine yazılmaz (`ad (1).pdf`). Kısayol kutusundaki masaüstü dosyaları da kurallarla taşınmaz.
- Her taşıma geri alınabilir; geri alınan dosya bir daha kendiliğinden taşınmaz.

#### Klasör simgeleri

- 30 sembol × 10 renkli hazır kütüphane; klasör adına göre öneri ("Oyunlarım" → oyun kolu). Kendi `.ico/.png/.svg` dosyanı da yükleyebilirsin.
- Masaüstünde yeni klasör açınca öneri bildirimi gelir. **Ayarlar → Gelişmiş ayarlar → Klasör simgeleri → Tüm klasörlere önerilen simgeyi ver** hepsini bir kerede güzelleştirir; aynı yerdeki **Klasör simgelerinin hepsini kaldır** hepsini geri alır.
- **İsteğe bağlı üretken yapay zekâ:** **Ayarlar → Gelişmiş ayarlar → Klasör simgeleri → Yapay zekâ ile klasör simgesi**'ne kendi Claude (Anthropic) API anahtarını girersen, yazdığın açıklamaya göre ("retro oyun kolu, neon mor") özel simge üretilir. Anahtar DPAPI ile bu Windows hesabına bağlı şifrelenir; zorunlu değildir. Uygunsuz bir sonucu simge penceresindeki **Uygunsuz içeriği bildir** ile bildirebilirsin.

#### Tanılama ve sorun bildirme

- **Hata günlüğü** (**Ayarlar → Gelişmiş ayarlar → Sorun giderme → Hata günlüğü**): hatalar bu bilgisayarda, kişisel bilgi olmadan kaydedilir (yollar yer tutucuyla değiştirilir; dosya adların, notların ve yazdıkların yazılmaz); en çok 4 MB, 14 gün. Kapatılabilir; **Günlükleri sil** hepsini siler. **Çökme olunca**: Her seferinde sor (varsayılan), Sormadan kaydet ya da Kaydetme.
- **Ayarlar → Gelişmiş ayarlar → Sorun giderme:** **Tanılama sayfasını aç** (betiksiz yerel sayfa) ve **Tanılama paketi oluştur** (içeriği önce listelenir, zip seçtiğin yere kaydedilir); **Ayarlar → Yardım → GitHub'da sorun aç**. NestDesk hiçbir şeyi kendiliğinden göndermez; GitHub sorun kayıtları herkese açıktır.

### Klavye kısayolları

Genel kısayollar (**Ayarlar → Gelişmiş ayarlar → Klavye kısayolları**'ndan değiştirilebilir):

| Kısayol | İşlev |
|---|---|
| `Ctrl+Alt+B` | "Widget ekle" penceresi |
| `Ctrl+Alt+G` | Windows masaüstüne göz at / NestDesk'e dön |
| `Ctrl+Alt+H` | Masaüstünü gizle / göster |
| `Ctrl+Alt+N` | Yeni not |
| `Ctrl+Alt+O` | Dosyaları şimdi taşı |
| `Ctrl+Alt+D` | NestDesk'i aç |
| `Ctrl+Alt+W` | Widget'ları pencerelerin önüne getir (5 sn) |
| (atanmamış) | Halka menüyü imleçte aç: varsayılan kapalı; **Ayarlar → Halka menü → Tuşla imlecin yanında açılsın** boş bir kısayol önerir |

Widget'ın içinde: `F2` yeniden adlandır · `Enter` aç · `Delete` Geri Dönüşüm Kutusu'na gönder (bölme) ya da kutudan kaldır · `Ctrl+F` bölmede ara · `1`–`9` öğeyi aç (bölme ve kutuda seçenek açıksa, halkada her zaman) · `Ctrl + tekerlek` boyut · `Shift+F10` seçili öğenin menüsü · zamanlayıcıda `Boşluk` / `R` · takvimde `Page Up` / `Page Down` / `Home`.

### Gizlilik

Hesap, reklam ve telemetri yok; her şey bilgisayarında kalır. Dosyalar yalnızca sen otomatik taşımayı ya da kutu seçeneğini açarsan taşınır ve her taşıma geri alınır. Klavye kancası yok; halka menü kısayolu varsayılan olarak kapalı. Hata günlüğü kişisel bilgi içermez ve bilgisayarından çıkmaz. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir. Ayrıntılar: [PRIVACY.md](PRIVACY.md#türkçe).

### Kaldırma

- **Store:** Windows uygulamayla birlikte ayarlarını, halka menülerini, ayar yedeklerini ve hata günlüklerini de siler. **Kurulum sürümü:** kaldırma programı gizli kalmış masaüstü simgelerini geri açar, kutulara taşınan simgeleri masaüstüne geri koyar ve ayarların (halka menüler, temalar, bırakılan bağlantılar, yedekler ve hata günlükleri dahil) silinip silinmeyeceğini sorar. **Taşınabilir:** klasörü sil.
- Senin içeriğin olarak kalanlar: `NestDesk` kutu klasörü (Store ve taşınabilir sürümde önce **Ayarlar → Gelişmiş ayarlar → Kutular → Hepsini masaüstüne geri koy**; kaldırdıktan sonra `Bu klasör nedir.txt` eski yerleri yazar), klasör simgeleri (önce **Ayarlar → Gelişmiş ayarlar → Klasör simgeleri → Klasör simgelerinin hepsini kaldır**), otomatik taşınan dosyalar (taşındıkları klasörde) ve kaydettiğin tanılama paketleri.

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

Komut satırı: `--minimized` (tepside başla), `--add` ("Widget ekle"; uygulama çalışıyorsa ona iletilir), `--peek` (Windows masaüstüne göz at ya da NestDesk'e dön), `--welcome` (karşılamayı aç; uygulama kapalıyken), `--exit` (çalışan örneği düzgünce kapat), `--restore-desktop` (gizli kalmış Windows masaüstü simgelerini geri açar, kutulara taşınan simgeleri geri koyar ve çıkar; kaldırma programı kullanır, sessiz kaldırmada `--quiet`), `--desktop <klasör> --data <klasör>` (test örneği: gerçek masaüstüne, "Windows ile başlat" kaydına ve Windows simgelerine dokunmaz).

Ortam değişkenleri (hepsi eski `DUZENLEME_*` adlarıyla da okunur): `NESTDESK_LANG=tr|en|pseudo` (arayüz dili; ekran görüntüleri ve testler için), `NESTDESK_DEBUGLOG=<dosya>` (ayrıntılı geliştirme günlüğü; hata günlüğünden ayrı), `NESTDESK_PERF_LOG=<dosya>` (başarım günlüğü), `NESTDESK_WINDOW_AT` / `NESTDESK_QUICKADD_AT` / `NESTDESK_NEWWIDGET_AT` / `NESTDESK_PEEK_AT` / `NESTDESK_RING_AT="x,y"` (fiziksel piksel; pencereleri, göz atma çubuğunu ve kısayolla açılan halkayı o noktanın monitöründe açar), `NESTDESK_TEST_RING=1` (yalnızca `--desktop` ile: halka kısayolu atanmamışken de açılır halkayı dener), `NESTDESK_TEST_HOTKEYS=1` (yalnızca `--desktop` ile: test örneği genel kısayolları da Windows'a kaydeder; yoksa hiçbirini kaydetmez, kısayol iletiyle denenir), `NESTDESK_TEST_CRASH=1` (yalnızca `--desktop` ile: açılıştan birkaç saniye sonra çökme denemesi), `NESTDESK_APPDATA_ROOT` (veri klasörü taşıma denemesi için; yalnızca `--desktop` ile ve `--data` olmadan), `NESTDESK_GPU=1` (widget'larda donanım çizimi). `--desktop` ile `--data` verilmezse veriler `%TEMP%\NestDesk-test-data\<anahtar>` klasöründe tutulur (gerçek ayarlara dokunulmaz). Boş bir `--data` klasörü karşılamayı açar; atlamak için klasöre `{ "FirstRunDone": true, "RenameNoticeShown": true, "CloseToTrayHintShown": true }` içerikli bir `settings.json` koy. Geliştirme yardımcıları: `--export-icon-sheet out.png`, `--render-svg in.svg out.png`.

### Yeni sürüm yayınlama

1. `src/Duzenleme/Duzenleme.csproj` → `<Version>` artırılır (ör. 2.2.0). Store aynı sürümü ikinci kez kabul etmez.
2. `tools/store/listing.md` → "Bu sürümdeki yenilikler" (TR/EN) yazılır.
3. `dotnet test` yeşil olmalı. Ayar dosyası geriye uyumlu kalır: yeni ayar = yeni alan; kalıcı enum'lara üye eklenmez. `tools/publish.ps1` ve `package-msix.ps1 -Strict`, `PRIVACY.md`, kurulumun "verileri sil" sorusu ve Store metinleri hata günlüğünü anlatmıyorsa yayın yapmaz (deneme derlemesi: `publish.ps1 -Draft`).
4. Commit → `git tag v2.2.0` → push. GitHub Actions kurulum dosyasını (`NestDesk-Setup-2.2.0.exe`) ve 3 zip'i üretip Releases'ta "NestDesk 2.2.0" sürümünü açar.
5. **Store:** `powershell -File tools/package-msix.ps1 -Strict` → `dist-store\NestDesk-2.2.0.msixbundle`. Partner Center'da **Start update** ile yeni gönderim açılır, yalnızca bu dosya yüklenir ve "What's new" doldurulur (adımlar: [tools/store/listing.md](tools/store/listing.md)).

---

## English

NestDesk splits your desktop into **panels**, can move files that land on the desktop into **folders you created**, gathers your apps in a **ring menu**, and puts **widgets** such as a clock, calendar, notes, to-do lists and timers on your wallpaper. The app is in English and Turkish and follows your Windows display language (**Settings → Dil / Language**).

### What's new in 2.2

- **Ring menu:** double-click the empty desktop (or select **Open the ring** in the tray menu): your NestDesk ring opens next to the pointer with your apps, folders, web addresses, commands, keyboard shortcuts and text snippets; click or press `1`–`9`. **Edit ring** below the ring opens an editor with a live preview; a ring widget on the desktop can use one of four styles (Classic, Orbit, Labeled, Glow). If you like, it also opens with a shortcut you choose (off by default).
- **Simpler (Easy NestDesk):** one **Edit** panel per widget (the ⋯ in its corner), right-click menus with at most five rows, a **Widgets** page that manages all your widgets, a seven-row **Settings** page (+ **Advanced settings**) and a three-step welcome.
- **Widget themes:** Neon, Night, Paper, Ocean and an Automatic theme that follows Windows light/dark, plus a custom accent color. In high contrast, widgets use your Windows colors.
- **Livelier motion:** widgets pop in gently, pages and notices slide in; **Settings → Advanced settings → Appearance → Animations**: Lively, Calm or Off. It always follows the Windows animation setting.
- **1–9 and smart drop:** `1`–`9` opens the first nine items of a panel or shortcut box (**Edit → More → Open with number keys**); drag apps from the Start menu and links from your browser onto shortcut boxes.
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

- A short three-step **welcome** sets up your desktop with you: panels, moving files, and your NestDesk ring (open it with a double-click on the empty desktop, and optionally with a key) plus the tools to put on your desktop.
- **No file is moved before you agree.** Auto-move stays off until you turn it on; step 2 shows in advance which of the files on your desktop would go to which folder.
- "Skip for now" changes nothing on your desktop. To open the welcome again: **Settings → Help → Restart the setup guide** (nothing you already have is deleted).
- NestDesk runs in the notification area (system tray); closing the main window keeps it running. To quit, right-click the tray icon → **Exit**.

### Main window

Click the tray icon or press `Ctrl+Alt+D`. Four pages: **Home** (Add widget, Edit ring, "How it works" cards on your first visit, a one-line auto-move card with Undo, Move files now and on/off, recent moves), **Widgets** (Add widget; "All widgets" to apply a look or color to every widget; the "On your desktop" list with the Edit panel of the selected widget next to it; "More" for aligning, bringing to front and saved layouts), **Auto-move** (on/off, rules, history and undo) and **Settings** (Language, Start with Windows, Gather icons into panels, Ring menu, Help, About and **Advanced settings**: Desktop, Widget placement, Appearance, Keyboard shortcuts, Folder icons, Shortcut boxes, Troubleshooting). Feedback stays in a strip at the bottom of the window; actions such as removing a widget can be undone there with **Undo**.

### Features

#### Panels

- **From the desktop:** Folders, Shortcuts (including This PC and Recycle Bin), Files, or the whole desktop.
- **Inside a folder:** a folder on the desktop (e.g. PDF) or any folder that isn't on the desktop: Downloads, Documents, Pictures or **Other folder…** (newest first, up to 300 items; the rest via "Open folder").
- The **New panel…** tile asks for the name, what to show and the icon in one window.
- Search as you type with the 🔍 button or `Ctrl+F` (including subfolders); drag files between panels; preview pictures, videos and PDFs; sort newest or oldest first, by name, type, size or by hand (drag to reorder); collapse to the title bar.
- Keyboard: `Enter` opens, `F2` renames (on disk; hidden extensions are kept and "(2)" is suggested on a name clash), `Delete` sends to the Recycle Bin, arrow keys move the selection; type a letter to jump to the item that starts with it. Optionally, **Edit → More → Open with number keys (1–9)** makes `1`–`9` open the first nine visible items (in shortcut boxes too).

#### Windows desktop icons

**Settings → Gather icons into panels** (in step 1 of the welcome: **Yes, sort into panels** / **No, keep my icons**):

1. **Off:** the Windows icons look as usual; panels show shortcuts without touching your files.
2. **On:** Windows' own icons are hidden and everything appears in panels. Your files are not touched; the icons come back when you turn it off or quit the app. **"Sort my desktop into panels"** (in the Add widget window) creates the panels you need and turns this on; **Undo** in the notice reverts everything.

#### Icons I put in boxes leave the desktop

"A desktop icon you put in a box or ring leaves the desktop; take it out and it comes back." While this is off, boxes and rings only hold links. The first time you put a desktop icon in a box you are asked once, next to the box (**Yes, remove it** / **No, keep it**); after that it is a switch in **Settings → Advanced settings → Shortcut boxes**.

- **With Windows icons visible**, the file is moved into a visible `NestDesk\<box name>` folder next to your Desktop folder (for a box named "Games", e.g. `C:\Users\<name>\NestDesk\Games`; inside OneDrive if your desktop is in OneDrive) and stays in the box. Your files are never hidden and their attributes never change; `What is this folder.txt` at the top of that folder lists each file's former location.
- **While "Gather icons into panels" is on**, no file is moved: icons in boxes simply don't show in the desktop panels.
- **Take it out and it comes back:** **Remove from box** / **Remove from ring** (`Delete`), dragging it to the desktop or onto a panel, or removing its tab, sub-ring or the box itself; **Undo** in the notice puts it back in the box. If it is also in another box, it only leaves that one. Settings offers **Open folder** and **Put all back on the desktop**; turning the option off puts everything back too (with **Undo**).
- If an icon is still on the desktop while the option is on (added earlier, "Not now", the file was open), its item menu offers **Remove from desktop**.
- **Folders are never moved** (they stay in the box as links). Icons on **everyone's desktop** (where most apps put their shortcuts) are asked about once more and moved with Windows' administrator approval; the Store edition doesn't move them.
- **When I add an app, its desktop shortcut leaves too:** if an app you add from the Start menu has a desktop shortcut to exactly the same app, you are asked the first time (**Yes, always do this** / **No**).

#### Peek at the Windows desktop

`Ctrl+Alt+G`, the tray menu, **Peek at the Windows desktop** in the ring, or **Settings → Advanced settings → Desktop**: Windows' own desktop icons appear and your widgets step aside (configurable). A small bar at the top center of the screen shows the time left, with **+5 min** and **Back to NestDesk**. By default NestDesk comes back after 2 minutes (1/2/5/10 minutes or "until I return"), and it waits while you are working on the desktop. Optionally, open windows are minimized while peeking (like Win+D; off by default). No file is changed.

**Double-click on the empty desktop** (**Settings → Advanced settings → Desktop**): open the ring (suggested to new users in the welcome; the switch in **Settings → Ring menu** chooses it too), Automatic (recommended; peeks while panels manage the desktop, otherwise hides/shows icons and widgets), hide/show, peek, or do nothing. Only double-clicks on the desktop itself count; File Explorer windows and file dialogs are not affected.

#### Widgets

| Widget | What it does |
|---|---|
| **Panel** | See above |
| **Shortcut box** | A tabbed shelf for apps, files and folders; drag and drop, right-click → **Add app…**, or the add buttons in its Edit panel. You can also drop apps from the Start menu and web or e-mail links from your browser (a link opens in the browser only when you open it). Open with one click, run as administrator; every item has **Rename (F2)** and **Change icon…** (Windows icons or an image) |
| **Ring menu** | See below |
| **Clock** | Large digital clock, 12/24-hour, optional seconds and a greeting |
| **Date** | Day, month, year and a weekly strip that highlights today |
| **Calendar** | Monthly calendar with week numbers and first day of the week; `Page Up`/`Page Down` change the month, `Home` returns to today |
| **World clock** | The time in cities you choose, the time difference and yesterday/tomorrow, using Windows time zones (including daylight saving time) |
| **Countdown** | Days left until a date (vacation, birthday…); can repeat every year |
| **Timer** | A countdown timer; `Space` starts/pauses, `R` resets, **+1 min**; a notification and a highlight frame when time is up. Edit → **Mode** turns it into a stopwatch |
| **Pomodoro** | 25 minutes of focus, 5-minute breaks, a long break every four rounds; lengths are adjustable and the next phase can start automatically |
| **Note** | Sticky note in 6 colors, saved as you type; `Ctrl + mouse wheel` changes the text size |
| **To-do** | Checklist; finished items stay crossed out in place or move to the bottom; "clear finished" can be undone |
| **System status** | Processor, memory, disk and battery, plus uptime; every 2–10 seconds, only while visible; opens Task Manager |
| **Recycle Bin** | How many items and how much space; double-click opens it, files dropped on it are recycled, **Empty…** asks first |

**Adding widgets:** `Ctrl+Alt+B`, right-click the tray icon → **Add widget…**, **Add widget** in the ring, **NestDesk – Add widget** in the Start menu, or **Add widget** on the **Home** or **Widgets** page of the main window. The first page has eight common tiles, **Show all** has the rest (ring menu, Pomodoro, world clock…), and **Panel ›** has the panels and **New panel…**.

**Removing:** the dimmed **×** in each widget's top-right corner is always there (right-click → **Remove** or **Remove** in the Edit panel work too); **Undo** in the notice or "Bring back" in the tray menu restores it. You can hide the × with **Edit → More → Remove button (×)**. To remove an item from a panel or box, right-click it → **Hide in this panel** / **Remove from box** (in a panel this never touches the file).

#### Ring menu

- Your **NestDesk ring** is ready from the start (with New note, Peek at the Windows desktop, Add widget and Open NestDesk): double-click the empty desktop (chosen in the welcome; later **Settings → Ring menu → Open when I double-click the empty desktop**) or right-click the tray icon → **Open the ring**, and it opens next to the pointer. Click or press `1`–`9`; the arrow keys, `Enter`, `Backspace` (back) and `Esc` work too.
- **Edit ring** (below the ring, on the Home page and in Settings): an editor with a live preview. Click a slot to select it, move it with **◀ / ▶** (`Alt+←/→`), **Change…** or **Remove** it (**Undo** in the notice); the add bar has **+ App**, **+ Folder**, **+ Web address**, **+ NestDesk action** and **More ▾** (**Command…**, **Press keys…**, **Paste text…**, **Sub-ring…**).
- **Ring widget:** **Add widget → Show all → Ring menu** puts a ring on your desktop; drag apps, files or folders onto it or click the **+** slot at the end. Its Edit panel has the same editor and **Ring style**: Classic, Orbit, Labeled (names around the ring) or Glow. A style only changes how the ring is drawn; in high contrast every style is drawn as Classic with your Windows colors.
- **Open next to the pointer with a key** (**Settings → Ring menu**, the bottom of the ring editor, or right-click a ring widget): off by default; turning it on suggests a free shortcut (e.g. `Ctrl+Alt+R`), and only that shortcut is registered with Windows. With **Select when I let go of the keys** on, hold the shortcut, move the pointer toward an item and let go to open it.
- Keyboard shortcut and text items work only in the ring opened by the shortcut, and go only to the window that was in front before you opened it (in a ring opened by a double-click or from the tray they are dimmed and do nothing). Pasted text isn't added to clipboard history (Win+V) or the cloud clipboard, and the previous clipboard content (text, an image or a list of files) is put back. Keys and text can't be sent to windows running as administrator (Windows blocks them; NestDesk tells you). A command runs only when you pick it; if it's marked "Run as administrator", Windows asks every time. Don't save passwords as text items: items are stored as plain text in `settings.json`.

#### Edit panel, name and icon

- The **⋯** in a widget's corner, or right-click → **Edit…**, opens one panel next to the widget: icon (a picker with 64 icons) and name, the widget's own settings (what a panel shows and how, a box's items and tabs, a note's color and checklist, a timer's mode and duration…), **Appearance**, **Show** (title, count, search button, tabs…) and a collapsed **More** (transparency, corners, shadow, lock position, collapse to title, duplicate, number keys, "fade when the pointer isn't on it"…). Every choice applies at once; **Undo** returns to how it was when you opened the panel, and **Done** or `Esc` closes it.
- Right-click menus have at most five rows: **Edit…**, the widget's two most common actions (e.g. "Search this panel" in a panel, Start · Reset in a timer) and **Remove**.
- A new widget doesn't open in rename mode (a note opens with its text area, a to-do list with its first line); name it in the Edit panel or with `F2`. `F2` renames everywhere: a widget's title, the selected file or folder in a panel (on disk), or only the displayed name of a shortcut box item.

#### Look

- The **Appearance** gallery is drawn like the real widget: **Auto** (follows Windows light/dark), **Glass**, **Dark**, **Light** and the **Neon**, **Night**, **Paper** and **Ocean** themes; **Color** has five dots or **Custom…** (a color picker next to the widget with a live preview; Windows' accent color can be picked too); **Size** is Small/Medium/Large. **Use this look for all widgets** copies it to every widget (with **Undo**). With high contrast on, widgets use your Windows colors. Themes only change how widgets look.
- On the **Widgets** page, the **All widgets** strip gives one look or color to every widget at once, and you can save named layouts to switch back with one click ("Work", "Games"…).
- **Animations** (**Settings → Advanced settings → Appearance**): Lively (widgets and pages open with a gentle zoom or slide), Calm (short fades only) or Off. If Windows "Animation effects" is off, none of NestDesk's own animations play; energy saver and Remote Desktop switch to Calm. Hovering never makes anything grow or slide.

#### Layout

- While dragging, widgets snap to each other and to screen edges like magnets; a dropped widget never lands on top of another but slides to the nearest free spot. While moving, `Alt` turns snapping off temporarily and `Shift` aligns to a grid. Resize from the edges; `Ctrl + wheel` makes icons bigger in panels, text bigger in notes, and other widgets bigger.
- **Where new widgets appear** (Settings → Advanced settings → Desktop): next to the pointer (default), in the center of the active screen, or in a corner (by widget type).
- Multiple monitors with different display scaling are supported; widgets return to their places when monitors are reconnected or the PC wakes up, and never extend off the screen.

#### Auto-move (optional)

- If your desktop has a folder named `PDF`, PDFs saved to the desktop go there. Ready-made rules also cover pictures, documents, archives, videos and music; on the **Auto-move** page each rule is a card (`PDF ← .pdf`): **Add rule** adds one in two steps from a ready-made type (Design, E-books…) or your own extensions, and ⋯ › **Change…** changes its extensions and folder. A rule whose folder doesn't exist shows how many files are waiting, and **Create** creates it with one click.
- If the folder doesn't exist on the desktop, the file is left alone (optionally, turn on creating missing folders under **Advanced**). Matching ignores letter case and Turkish accents: `Arşivler` = `arsivler`. With English as the interface language, the default folders are PDF, Pictures, Documents, Archives, Videos and Music; existing folders in either language are reused.
- Shortcuts, hidden and system files, folders and downloads in progress (`.crdownload`, `.part`) are never moved, and no file is overwritten (`name (1).pdf`). Desktop files that are in a shortcut box are not moved by rules either.
- Every move can be undone; a file you moved back is never moved automatically again.

#### Folder icons

- A library of 30 symbols × 10 colors with suggestions by folder name ("Games" → game controller). You can also use your own `.ico/.png/.svg` files.
- Creating a new folder on the desktop brings up a suggestion. **Settings → Advanced settings → Folder icons → Give every folder its suggested icon** styles them all at once; **Remove all folder icons** in the same place undoes it.
- **Optional generative AI:** enter your own Claude (Anthropic) API key in **Settings → Advanced settings → Folder icons → AI folder icons** to generate a custom icon from a description you type ("retro game controller, neon purple"). The key is encrypted with DPAPI for your Windows account only, and it is not required. Report an inappropriate result with **Report inappropriate content** in the icon window.

#### Diagnostics and reporting problems

- **Error log** (**Settings → Advanced settings → Troubleshooting → Error log**): errors are recorded on this PC without personal information (paths are replaced with placeholders; your file names, notes and what you type are never recorded); up to 4 MB, 14 days. You can turn it off, and **Delete logs** deletes everything. **When the app crashes**: Ask every time (default), Save without asking or Don't save.
- **Settings → Advanced settings → Troubleshooting:** **Open diagnostics page** (a local page without scripts) and **Create diagnostic package** (lists the contents first, then saves a zip where you choose); **Settings → Help → Open an issue on GitHub**. NestDesk never sends anything on its own; GitHub issues are public.

### Keyboard shortcuts

Global shortcuts (customizable in **Settings → Advanced settings → Keyboard shortcuts**):

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+B` | "Add widget" window |
| `Ctrl+Alt+G` | Peek at the Windows desktop / back to NestDesk |
| `Ctrl+Alt+H` | Hide / show the desktop |
| `Ctrl+Alt+N` | New note |
| `Ctrl+Alt+O` | Move files now |
| `Ctrl+Alt+D` | Open NestDesk |
| `Ctrl+Alt+W` | Bring widgets in front of windows (5 s) |
| (unassigned) | Open the ring menu at the pointer: off by default; **Settings → Ring menu → Open next to the pointer with a key** suggests a free shortcut |

Inside a widget: `F2` rename · `Enter` open · `Delete` send to the Recycle Bin (panel) or remove from the box · `Ctrl+F` search a panel · `1`–`9` open an item (in panels and boxes when the option is on, always in a ring) · `Ctrl + wheel` size · `Shift+F10` menu for the selected item · `Space` / `R` in a timer · `Page Up` / `Page Down` / `Home` in the calendar.

### Privacy

No account, no ads, no telemetry; everything stays on your PC. Files are moved only if you turn on auto-move or the box option, and every move can be undone. No keyboard hook; the ring menu shortcut is off by default. The error log contains no personal information and never leaves your PC. The only feature that uses the internet is the optional AI folder icon. Details: [PRIVACY.md](PRIVACY.md#english).

### Uninstalling

- **Store:** Windows removes your settings, ring menus, settings backups and error logs together with the app. **Installer edition:** the uninstaller shows any desktop icons left hidden, puts icons that were moved to boxes back on the desktop and asks whether to delete your settings (including ring menus, themes, dropped links, backups and error logs). **Portable:** delete the folder.
- What stays as your own content: the `NestDesk` box folder (in the Store and portable editions first use **Settings → Advanced settings → Shortcut boxes → Put all back on the desktop**; afterwards `What is this folder.txt` lists the former locations), folder icons (first use **Settings → Advanced settings → Folder icons → Remove all folder icons**), files moved by auto-move (in the folders they were moved to) and diagnostic packages you saved.

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

Command line: `--minimized` (start in the tray), `--add` ("Add widget"; forwarded to the running app), `--peek` (peek at the Windows desktop, or return to NestDesk), `--welcome` (open the welcome; while the app is closed), `--exit` (close the running instance cleanly), `--restore-desktop` (shows Windows desktop icons that were left hidden, puts icons moved to boxes back and exits; used by the uninstaller, with `--quiet` for a silent uninstall), `--desktop <folder> --data <folder>` (test instance: never touches your real desktop, the "Start with Windows" entry or the Windows icons).

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
