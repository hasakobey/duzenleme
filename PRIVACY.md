# NestDesk — Gizlilik Politikası / Privacy Policy

[Türkçe](#türkçe) · [English](#english)

## Türkçe

**Son güncelleme:** 28 Eylül 2026 (NestDesk 2.1)

Bu politika, Windows için masaüstü düzenleyici **NestDesk**'in (Microsoft Store, kurulum sihirbazı ve taşınabilir sürümler) verileri nasıl işlediğini anlatır. NestDesk açık kaynaklıdır; aşağıda yazılanların hepsi [kaynak kodda](https://github.com/hasakobey/duzenleme) doğrulanabilir.

### Kısaca

- NestDesk **kişisel veri toplamaz** ve geliştiriciye hiçbir şey göndermez. Hesap, reklam, kullanım istatistiği (telemetri) ya da çökme raporu gönderimi yoktur.
- Her şey **bilgisayarında** işlenir ve saklanır: ayarların, notların, taşıma geçmişin ve kutu kayıtların.
- Dosyaların **yalnızca senin izninle** taşınır: otomatik taşıma ve kısayol kutusunun "masaüstünden kalksın" seçeneği sen açana kadar kapalıdır. Her taşıma kaydedilir ve geri alınabilir.
- İnternete çıkan tek özellik isteğe bağlı **"yapay zekâ ile klasör simgesi"**dir: yalnızca kendi Anthropic API anahtarını girip bir simge istediğinde, klasörün adı ve yazdığın açıklama Anthropic'e gönderilir.

### 1. Toplanan veriler

Yok. NestDesk'in bir sunucusu yoktur; uygulama kullanım bilgisi, cihaz bilgisi, dosya adı ya da başka herhangi bir bilgiyi geliştiriciye göndermez. İsteğe bağlı yapay zekâ özelliği dışında (bkz. 5. bölüm) üçüncü kişilere de bir şey göndermez. Uygulama güncelleme denetimi de yapmaz (Store sürümünü Microsoft Store günceller).

### 2. Bilgisayarında tutulanlar

NestDesk'in çalışması için gereken her şey yerel olarak şu klasörde saklanır:

- **Kurulum sihirbazıyla kurulan sürüm:** `%AppData%\NestDesk`. (2.1'den önceki sürümler `%AppData%\Duzenleme` kullanıyordu; 2.1 bu klasörü ilk açılışta, bilgisayarının içinde yeniden adlandırır. Klasör o an kullanımdaysa geçici olarak eski klasör kullanılır.)
- **Taşınabilir sürüm:** programın yanındaki `data` klasörü.
- **Microsoft Store sürümü:** Windows, uygulamanın `%AppData%\NestDesk`'te yeni oluşturduğu dosyaları paketine ayrılmış klasöre (`%LocalAppData%\Packages\…\LocalCache\Roaming\NestDesk`) yönlendirir. Daha önce kurulum sihirbazı sürümü kullanıldıysa Store sürümü o sürümün klasöründeki (`%AppData%\NestDesk` ya da `%AppData%\Duzenleme`) ayarları ve geçmişi yerinde okuyup kullanmaya devam eder; paket dışındaki bu klasörü taşımaz, yeniden adlandırmaz ve kaldırılınca silmez.

Uygulamanın kullandığı klasörü Ayarlar → **Gelişmiş** → **Dosya konumları** → **Ayar ve geçmiş dosyaları** → **Aç** ile görebilirsin.

| Dosya | İçerik |
|---|---|
| `settings.json` | Otomatik taşıma kuralları; widget'ların yeri, adı, simgesi ve görünümü; **notlarının ve yapılacaklar listelerinin metni**; geri sayımların adı ve günü; dünya saatindeki şehirler; zamanlayıcıların durumu (ör. bitiş zamanı; çalışan bir zamanlayıcı yeniden başlatmadan sonra da doğru süreyi göstersin diye); kısayol kutusuna eklediğin uygulama, dosya ve klasör yolları ile onlara verdiğin adlar ve simgeler; bölmelerin gösterdiği klasörlerin yolları ve bölmelerde gizlediğin öğeler; klavye kısayolları; kayıtlı düzenler; dil seçimi ve (girdiysen) şifrelenmiş API anahtarı |
| `journal.json` | Son 500 otomatik taşımanın kaydı: zaman, dosyanın eski ve yeni yolu (geri alabilmen için) |
| `box-moves.json` | Kısayol kutusu seçeneğiyle taşınan öğelerin kaydı: masaüstündeki eski yolu, `NestDesk` klasöründeki yeni yolu, hangi kutuya eklendiği ve zaman (masaüstüne geri koyabilmen için) |
| `*.bak` (ör. `settings.json.bak`) | Yukarıdaki üç dosyanın bir önceki hâli. Her kayıtta yenilenir; yazma yarıda kesilirse (elektrik kesintisi gibi) ayarların kaybolmasın diye tutulur ve asıl dosyayla aynı türde bilgi içerir |
| `yedekler/` | Uygulama açılırken günde en fazla bir kez alınan `settings.json` kopyaları; en yeni 7 tanesi tutulur |
| `settings.json.bozuk-<zaman>`, `journal.json.bozuk-<zaman>` | Yalnızca bu dosyalardan biri okunamayacak kadar bozulursa, varsayılan ayarlarla değiştirilmeden önce alınan kopyası. Kendiliğinden silinmez (elle silebilirsin); notlarını ve şifreli API anahtarını içerebilir |
| `ai-icons/` | Yapay zekâ ile ürettiğin simgeler (SVG); dosya adı, üretim zamanı ve açıklamanın (boşsa klasör adının) ilk 40 karakteridir |
| `icons/` | Kısayol kutusu öğesine "Resim dosyasından…" ile simge olarak seçtiğin resimlerin kopyaları (asıl dosya silinse de simge kalsın diye); dosya adı içeriğin özetidir |

Ayrıca:

- **`NestDesk` klasörü (kutu klasörü):** Kısayol kutusu seçeneğini açarsan kutuya eklediğin masaüstü öğeleri, masaüstü klasörünün bulunduğu yerdeki görünür `NestDesk` klasörüne (genellikle `C:\Users\<ad>\NestDesk\<kutunun adı>`) taşınır. Masaüstün OneDrive'daysa bu klasör de OneDrive'ın içindedir (`…\OneDrive\NestDesk`) ve OneDrive onu masaüstün gibi eşitler. İçindekiler senin dosyalarındır: sıradan bir klasördür, gizlenmez, dosyaların öznitelikleri değiştirilmez.
- **Klasör simgeleri:** Bir klasöre simge verdiğinde simge, o klasörün içine gizli bir `.nestdesk-<zaman>.ico` dosyası (2.1 öncesinde `.duzenleme-<zaman>.ico`) olarak yazılır ve klasörün `desktop.ini` dosyası güncellenir (Windows'un standart klasör simgesi yöntemi). Simge penceresindeki **Varsayılana dön** bunları kaldırır; Ayarlar → **Gelişmiş** → **Klasör simgelerinin hepsini kaldır** masaüstündeki klasörlerde (ve içlerindeki klasörlerde) NestDesk'in verdiği bütün simgeleri bir kerede kaldırır.
- **Windows ayarları:** "Windows ile başlat" ve Ayarlar → Masaüstü → **Sistem simgeleri** (Bu Bilgisayar, Geri Dönüşüm Kutusu…) gibi seçenekler Windows'un kendi kullanıcı ayarlarına yazılır (Store sürümünde başlangıç Windows'un başlangıç görevidir).
- **Tanılama günlüğü** yalnızca `NESTDESK_DEBUGLOG` (ya da eski adı `DUZENLEME_DEBUGLOG`) ortam değişkeniyle açıkça istenirse, senin belirttiğin dosyaya yazılır.

Bunların hiçbiri bilgisayarından dışarı gönderilmez.

### 3. Dosyalarına ne yapar

NestDesk dosyaları yalnızca aşağıdaki durumlarda taşır, adlandırır ya da siler. Dosyaların içeriğini okumaz; kararlar dosyanın adına, uzantısına ve konumuna göre verilir.

- **Otomatik taşıma (isteğe bağlı):** Yeni kullanıcıda kapalıdır. Karşılamada "Evet, dosyalarımı yerine taşı"yı seçersen ya da **Otomatik taşıma** sayfasından açarsan, masaüstüne düşen dosyalar kurallarına göre **masaüstünde zaten bulunan** klasörlere taşınır ("Klasör yoksa oluştur" varsayılan olarak kapalıdır). Kısayollar, gizli ve sistem dosyaları, klasörler ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz. Her taşıma `journal.json`'a yazılır ve **Otomatik taşıma** sayfasından geri alınır; geri aldığın dosya bir daha kendiliğinden taşınmaz.
- **Kısayol kutusu seçeneği (isteğe bağlı):** Yalnızca Ayarlar → Masaüstü → **Windows masaüstü simgeleri** → **Kutulara eklediklerim masaüstünden kalksın** seçiliyken, bir kısayol kutusuna eklediğin masaüstü öğesi yukarıdaki `NestDesk` klasörüne taşınır ve kutuda durur. Taşıma `box-moves.json`'a yazılır; öğeye sağ tık → **Masaüstüne geri koy**, Ayarlar'daki **Hepsini masaüstüne geri koy** ya da kutudan çıkarmak öğeyi masaüstüne geri getirir; seçeneği kapatırken de geri konmaları önerilir. Bilgisayardaki tüm hesaplarda görünen Ortak Masaüstü'ndeki öğeler, ayrıca izin vermedikçe taşınmaz. Kuralların ve bölmelerin kullandığı klasörler taşınmaz.
- **Senin komutların:** Bölmede F2 ile yeniden adlandırma, "Yeni klasör", Delete ile Geri Dönüşüm Kutusu'na gönderme ve bölmeler arasında sürükleyip bırakma yalnızca sen istediğinde, Windows'un kendi dosya işlemleriyle yapılır (Windows'un silme onayı ayarına uyulur; Ortak Masaüstü'nde Windows yönetici izni sorar). Kısayol kutusunda F2 yalnızca kutuda görünen adı değiştirir, dosyaya dokunmaz.
- **Geri Dönüşüm Kutusu widget'ı:** Kutudaki öğe sayısını ve kapladığı yeri Windows'tan okur. **Boşalt…** her zaman önce onay ister; onaylarsan Windows kutuyu boşaltır (bu kalıcıdır). Widget'a bırakılan dosyalar Geri Dönüşüm Kutusu'na gönderilir.
- **Masaüstü simgelerini gizleme ve göz atma:** "Yalnızca bölmelerde göster", masaüstünü gizleme (çift tıklama, `Ctrl+Alt+H`) ve "Windows masaüstüne göz at" (`Ctrl+Alt+G`) yalnızca Windows'un masaüstü simgelerinin görünürlüğünü değiştirir; dosyalara dokunmaz. Simgeler uygulamadan çıkınca, oturum kapanırken ve uygulama çökerse yeniden gösterilir; zorla kapatılırsa bir sonraki açılışta ya da oturumu kapatıp açınca geri gelir.

### 4. Okuduğu ve gösterdiği bilgiler

- **Bölmeler ve klasör bölmeleri:** Masaüstündeki (ve Ortak Masaüstü'ndeki) öğeleri ve bölmede göstermeyi seçtiğin klasörleri (İndirilenler, Belgeler, Resimler ya da başka bir klasör) listeler: ad, boyut, tarih ve simge. Simgeler ve önizlemeler Windows'un kendi küçük resim hizmetiyle bilgisayarında oluşturulur. Yalnızca çevrimiçi olan bulut dosyaları (ör. OneDrive'da "isteğe bağlı" dosyalar) için önizleme oluşturulmaz, böylece listelemek onları indirtmez; dosya yalnızca sen açınca iner.
- **Sistem durumu widget'ı:** İşlemci kullanımını, belleği, diskteki boş alanı ve pil durumunu Windows'tan okur; yalnızca widget görünürken, birkaç saniyede bir. Değerler yalnızca gösterilir: kaydedilmez, gönderilmez.
- **Saat, tarih, takvim, dünya saati ve geri sayım:** Bilgisayarın saatini ve Windows'un saat dilimi bilgisini kullanır. İnternete bağlanmaz; takvim hesaplarına, kişilere ya da konumuna erişmez. Dünya saatindeki şehirleri sen seçersin.
- **Zamanlayıcı ve Pomodoro bildirimleri:** Süre dolunca bildirim alanı (sistem tepsisi) üzerinden Windows bildirimi gösterilir; Windows'un bildirim ve "Rahatsız etmeyin" ayarları geçerlidir. Her zamanlayıcıda sağ tık → **Süre dolunca bildir** ile kapatılabilir. Taşıma bildirimleri Ayarlar → Genel'den kapatılır.
- **Fare:** Boş masaüstüne çift tıklamayı algılamak için Windows'un ham fare girişi (Raw Input) dinlenir; fare kancası kullanılmaz, uygulama farenin hareketini hiçbir zaman geciktirmez. Yalnızca son sol tıklamanın zamanı ve ekrandaki konumu bellekte tutulur; kaydedilmez, gönderilmez. Ayarlar → Masaüstü → **Boş masaüstüne çift tıklayınca** → **Hiçbir şey yapma** seçilince dinleme durur.
- **Klavye:** Klavye kancası kullanılmaz. Genel kısayollar (ör. `Ctrl+Alt+G`) Windows'un genel kısayol kaydıyla çalışır; F2, Enter, Delete gibi tuşlar yalnızca bir widget etkinken o widget'ta çalışır. Uygulama kendi pencereleri dışında klavyeni izlemez.

### 5. İsteğe bağlı: yapay zekâ ile klasör simgesi (Anthropic)

Bu özellik varsayılan olarak kapalıdır ve yalnızca Ayarlar'a kendi Claude API anahtarını girersen çalışır. Anahtar yoksa NestDesk'in kendisi internete hiç bağlanmaz.

- **Anahtarı kaydederken** anahtarın geçerli olup olmadığı Anthropic API'sine yapılan tek bir doğrulama isteğiyle denetlenir. Bu istek yalnızca anahtarı ve aşağıda sayılan standart teknik bilgileri içerir.
- **"Üret"e bastığında** Anthropic API'sine gönderilenler:
  - simge verdiğin klasörün **adı** (tam yolu ya da içindekiler değil),
  - simge için **yazdığın açıklama**,
  - simgenin nasıl çizileceğini anlatan sabit bir talimat metni,
  - kimlik doğrulama için API anahtarın,
  - Anthropic'in resmi yazılım kitaplığının her istekle gönderdiği teknik bilgiler (kitaplığın sürümü, işletim sistemi, işlemci mimarisi, .NET sürümü) ve her internet bağlantısında olduğu gibi IP adresin.
- Başka hiçbir şey gönderilmez: dosyaların, diğer dosya adların, notların, ayarların ve geçmişin bilgisayarında kalır.
- Gelen SVG çizimi, çalıştırılabilir ya da dışarı bağlanan her şeyden temizlenir ve `ai-icons/` klasörüne kaydedilir.
- İstekler senin Anthropic hesabın üzerinden yapılır ve kullanım ücretini Anthropic senin hesabına yansıtır. Anthropic'in bu verileri nasıl işlediği Anthropic'in koşullarına ve [gizlilik politikasına](https://www.anthropic.com/legal/privacy) tabidir.
- Üretilen bir simge uygunsuzsa klasör simgesi penceresindeki **Uygunsuz içeriği bildir** bağlantısı tarayıcında önceden doldurulmuş bir GitHub bildirim formu açar. Kendiliğinden hiçbir şey gönderilmez: formda yalnızca başlık, boş sorular ve NestDesk'in sürüm numarası bulunur; açıklaman, klasör adı ya da simge forma konmaz. Neyi yazacağına ve formu gönderip göndermeyeceğine sen karar verirsin (GitHub hesabı gerekir, bildirim herkese açıktır).

**API anahtarının saklanması:** Anahtar, Windows'un veri koruma arayüzüyle (DPAPI, "geçerli kullanıcı" kapsamı) şifrelenir; `settings.json` içinde yalnızca şifreli hali tutulur. Şifreli anahtar genellikle yalnızca aynı bilgisayardaki aynı Windows hesabında çözülebilir: ayar dosyası ya da bir kopyası başka bir hesaba ya da başka bir bilgisayara taşınırsa okunamaz. (İstisna: kurumsal ağlarda dolaşım profili kullanan bir hesap, anahtarı ağdaki başka bir bilgisayarda da çözebilir.) DPAPI'nin doğası gereği aynı Windows hesabıyla çalışan programlar anahtarı çözebilir; bu yüzden anahtarı yalnızca güvendiğin bir bilgisayarda kaydet. Ayarlar'daki **Sil** düğmesi anahtarı `settings.json`'dan kaldırır; şifreli hali `settings.json.bak`'ta bir sonraki kayda kadar, `yedekler/` klasöründeki eski kopyalarda bu kopyalar yenileriyle değişene kadar, varsa `settings.json.bozuk-<zaman>` dosyalarında ise sen silene kadar kalır (hemen kaldırmak için bunları silebilirsin). Anahtarı tamamen geçersiz kılmak için Anthropic Console'dan iptal edebilirsin.

### 6. Senin başlattığın diğer bağlantılar

- Uygulamadaki bağlantılar (ör. Anthropic Console, indirme sayfası, bu gizlilik politikası, bildirim formu) yalnızca sen tıklayınca varsayılan tarayıcında açılır.
- Bölmelerden ve kısayol kutusundan açtığın öğeler (internet kısayolları dahil) Windows tarafından ilgili programla açılır.
- Masaüstün ya da bölmede gösterdiğin bir klasör OneDrive'a ya da bir ağ klasörüne bağlıysa dosya işlemleri Windows üzerinden o konumda yapılır; NestDesk bu hizmetlere kendisi bağlanmaz.

### 7. Microsoft Store

NestDesk'i Microsoft Store'dan yüklediysen, indirme bilgileri ve (Windows tanılama ayarlarına bağlı olarak) çökme bilgileri gibi veriler Microsoft tarafından [Microsoft Gizlilik Bildirimi](https://privacy.microsoft.com/privacystatement)'ne göre işlenebilir; geliştirici Partner Center'da bunlara dayanan raporları (ör. indirme sayısı, çökme istatistikleri) görebilir. Bu veriler NestDesk'in kodu tarafından toplanmaz.

### 8. Kaldırma ve verilerini silmek

- **Microsoft Store sürümü:** Uygulamayı kaldırınca Windows paket klasörünü de siler (ayarlar, notlar, taşıma geçmişi ve kutu kayıtları). Store sürümünden önce kurulum sihirbazı sürümü kullanıldıysa iki sürümün de kullandığı `%AppData%\NestDesk` ya da `%AppData%\Duzenleme` klasörü kalır; elle silebilirsin.
- **Kurulum sihirbazı sürümü:** Kaldırma programı gizli kalmış masaüstü simgelerini yeniden gösterir ve ayarların silinip silinmeyeceğini sorar (`%AppData%\NestDesk` ve varsa `%AppData%\Duzenleme`); istersen bu klasörleri elle de silebilirsin.
- **Taşınabilir sürüm:** Programın yanındaki `data` klasörünü sil.
- **Her sürümde senin içeriğin olarak kalanlar** (uygulama bunları kaldırılırken silmez, çünkü senin dosyaların ve klasörlerindir):
  - `NestDesk` kutu klasörü ve içindeki dosyaların. Kaldırmadan önce Ayarlar → **Masaüstü** → **Hepsini masaüstüne geri koy** ile hepsini masaüstüne döndürebilir ya da klasörü elle taşıyabilirsin.
  - Klasör simgeleri. Kaldırmadan önce Ayarlar → **Gelişmiş** → **Klasör simgelerinin hepsini kaldır** ile hepsini, simge penceresindeki **Varsayılana dön** ile tek tek kaldırabilirsin.
  - Otomatik taşınan dosyalar taşındıkları klasörde kalır; kaldırmadan önce **Otomatik taşıma** sayfasından geri alabilirsin.

### 9. Çocuklar

NestDesk, çocuklar dahil kimseden kişisel veri toplamaz.

### 10. Değişiklikler

Bu politika değişirse güncel hali bu dosyada yayımlanır ve yukarıdaki tarih güncellenir. Önceki sürümler GitHub deposunun geçmişinde görülebilir.

### 11. İletişim

Sorular ve talepler için: [GitHub Issues](https://github.com/hasakobey/duzenleme/issues). Herkese açık bir kayda kişisel bilgi ya da API anahtarı yazma.

---

## English

**Last updated:** September 28, 2026 (NestDesk 2.1)

This policy explains how **NestDesk**, a desktop organizer for Windows, handles data in all of its editions (Microsoft Store, installer and portable). NestDesk is open source; everything below can be verified in the [source code](https://github.com/hasakobey/duzenleme).

### Summary

- NestDesk **does not collect personal data** and sends nothing to the developer. There are no accounts, ads, usage analytics (telemetry) or crash-report uploads.
- Everything is processed and stored **on your PC**: your settings, notes, move history and box records.
- Your files are moved **only with your permission**: auto-move and the shortcut box option "leave the desktop" stay off until you turn them on. Every move is recorded and can be undone.
- The only feature that uses the internet is the optional **AI folder icon** generator: only when you enter your own Anthropic API key and request an icon, the folder's name and the description you typed are sent to Anthropic.

### 1. Data we collect

None. NestDesk has no server of its own; the app does not send usage data, device information, file names or any other information to the developer. Apart from the optional AI feature (see section 5), it sends nothing to third parties either. The app does not check for updates either (the Store edition is updated by the Microsoft Store).

### 2. Data stored on your PC

Everything NestDesk needs is stored locally in this folder:

- **Installer edition:** `%AppData%\NestDesk`. (Versions before 2.1 used `%AppData%\Duzenleme`; on first start, 2.1 renames that folder locally on your PC. If the folder is in use at that moment, the old folder is used temporarily.)
- **Portable edition:** the `data` folder next to the program.
- **Microsoft Store edition:** Windows redirects files the app newly creates in `%AppData%\NestDesk` to the folder reserved for its package (`%LocalAppData%\Packages\…\LocalCache\Roaming\NestDesk`). If you used the installer edition before, the Store edition keeps reading and using your existing settings and history in that edition's folder (`%AppData%\NestDesk` or `%AppData%\Duzenleme`) in place; it never moves or renames that folder outside its package, and does not delete it when uninstalled.

To see the folder the app uses, go to Settings → **Advanced** → **File locations** → **Settings and history files** → **Open**.

| File | Contents |
|---|---|
| `settings.json` | Auto-move rules; widget positions, names, icons and appearance; **the text of your notes and to-do lists**; countdown names and dates; world clock cities; timer state (e.g. the end time, so a running timer still shows the right time after a restart); paths of the apps, files and folders you added to shortcut boxes and the names and icons you gave them; paths of the folders your panels show and items you hid in panels; keyboard shortcuts; saved layouts; your language choice and (if you entered one) the encrypted API key |
| `journal.json` | A record of the last 500 automatic moves: time, the file's old and new path (so you can undo them) |
| `box-moves.json` | A record of items moved by the shortcut box option: the old path on the desktop, the new path in the `NestDesk` folder, which box it was added to, and the time (so you can put it back) |
| `*.bak` (e.g. `settings.json.bak`) | The previous version of the three files above. Refreshed with every save and kept so your settings survive an interrupted write (e.g. a power cut); contains the same kind of information as the main file |
| `yedekler/` | Copies of `settings.json`, taken at most once a day when the app starts; the 7 newest are kept |
| `settings.json.bozuk-<time>`, `journal.json.bozuk-<time>` | Only if one of these files becomes too damaged to read: a copy taken before it is replaced with defaults. Never deleted automatically (you can delete them manually); may contain your notes and the encrypted API key |
| `ai-icons/` | Icons you generated with AI (SVG); the file name is the time of creation and the first 40 characters of your description (or of the folder name if the description is empty) |
| `icons/` | Copies of images you chose as a shortcut box item's icon with "From an image file…", so the icon stays even if the original file is deleted; the file name is a hash of the content |

In addition:

- **The `NestDesk` folder (box folder):** if you turn on the shortcut box option, desktop items you add to a box are moved into a visible `NestDesk` folder located where your Desktop folder is (usually `C:\Users\<name>\NestDesk\<box name>`). If your desktop is in OneDrive, this folder is inside OneDrive too (`…\OneDrive\NestDesk`), and OneDrive syncs it just like your desktop. Its contents are your own files: it is an ordinary folder, nothing is hidden and no file attributes are changed.
- **Folder icons:** when you give a folder an icon, the icon is written into that folder as a hidden `.nestdesk-<time>.ico` file (`.duzenleme-<time>.ico` before 2.1) and the folder's `desktop.ini` is updated (the standard Windows folder icon mechanism). **Reset to default** in the icon window removes them; Settings → **Advanced** → **Remove all folder icons** removes every icon NestDesk gave to folders on your desktop (and the folders inside them) at once.
- **Windows settings:** options such as "Start with Windows" and Settings → Desktop → **System icons** (This PC, Recycle Bin…) are written to Windows' own user settings (in the Store edition, starting with Windows is a Windows startup task).
- A **diagnostic log** is written only if you explicitly request it with the `NESTDESK_DEBUGLOG` (or the older `DUZENLEME_DEBUGLOG`) environment variable, to the file you specify.

None of this ever leaves your PC.

### 3. What it does with your files

NestDesk moves, renames or deletes files only in the cases below. It does not read file contents; decisions are based on a file's name, extension and location.

- **Auto-move (optional):** off for new users. If you choose "Yes, move my files" in the welcome or turn it on on the **Auto-move** page, files that land on the desktop are moved by your rules into folders that **already exist on the desktop** (the option to create missing folders is off by default). Shortcuts, hidden and system files, folders and downloads in progress are never moved, and no file is ever overwritten. Every move is written to `journal.json` and can be undone on the **Auto-move** page; a file you moved back is never moved automatically again.
- **Shortcut box option (optional):** only while Settings → Desktop → **Windows desktop icons** → **Items I add to boxes leave the desktop** is selected, a desktop item you add to a shortcut box is moved into the `NestDesk` folder described above and stays in the box. The move is written to `box-moves.json`; right-click the item → **Put back on the desktop**, **Put everything back on the desktop** in Settings, or removing it from the box returns it to the desktop, and turning the option off offers to put everything back. Items on the Public Desktop, which all accounts on the PC see, are not moved unless you give separate permission. Folders used by rules and panels are not moved.
- **Your commands:** renaming with F2 in a panel, "New folder", sending items to the Recycle Bin with Delete, and dragging between panels happen only when you do them, using Windows' own file operations (Windows' delete confirmation setting applies; for the Public Desktop, Windows asks for administrator permission). In a shortcut box, F2 changes only the name shown in the box, not the file.
- **Recycle Bin widget:** reads the number of items and the space they use from Windows. **Empty…** always asks for confirmation first; if you confirm, Windows empties the Recycle Bin (this is permanent). Files dropped on the widget are sent to the Recycle Bin.
- **Hiding desktop icons and peeking:** "Show only in panels", hiding the desktop (double-click, `Ctrl+Alt+H`) and "Peek at the Windows desktop" (`Ctrl+Alt+G`) only change whether Windows' desktop icons are visible; they don't touch your files. The icons are shown again when you quit the app, when you sign out, and if the app crashes; if it is force-closed, they come back the next time it starts or when you sign out and back in.

### 4. What it reads and shows

- **Panels and folder panels:** list the items on your desktop (and the Public Desktop) and in folders you choose to show in a panel (Downloads, Documents, Pictures or another folder): name, size, date and icon. Icons and previews are generated on your PC by Windows' own thumbnail service. No previews are made for online-only cloud files (e.g. OneDrive "files on demand"), so listing them does not download them; a file is downloaded only when you open it.
- **System status widget:** reads processor usage, memory, free disk space and battery status from Windows, only while the widget is visible and every few seconds. The values are only displayed; they are never stored or sent.
- **Clock, date, calendar, world clock and countdown:** use your PC's clock and Windows' time zone data. They don't connect to the internet or access calendar accounts, contacts or your location. You choose the world clock cities yourself.
- **Timer and Pomodoro notifications:** when time is up, a Windows notification is shown through the notification area (system tray); Windows' notification and "Do not disturb" settings apply. Each timer can turn it off with right-click → **Notify me when time is up**. Move notifications can be turned off in Settings → General.
- **Mouse:** Windows' raw mouse input (Raw Input) is read to detect a double-click on an empty area of the desktop; no mouse hook is used and the app never delays the mouse pointer. Only the time and screen position of the last left click are kept in memory; they are never saved or sent. Choosing Settings → Desktop → **When you double-click the empty desktop** → **Do nothing** stops listening.
- **Keyboard:** no keyboard hook is used. Global shortcuts (e.g. `Ctrl+Alt+G`) are registered with Windows' global hotkey system; keys such as F2, Enter and Delete work only in a widget while it is active. The app does not monitor your keyboard outside its own windows.

### 5. Optional: AI folder icons (Anthropic)

This feature is off by default and works only if you enter your own Claude API key in Settings. Without a key, NestDesk itself never connects to the internet.

- **When you save the key,** a single verification request is sent to the Anthropic API to check that the key is valid. It contains only the key and the standard technical details listed below.
- **When you press "Generate",** the following is sent to the Anthropic API:
  - the **name** of the folder you are styling (not its full path or contents),
  - the **description** you typed for the icon,
  - a fixed instruction text describing how to draw the icon,
  - your API key, for authentication,
  - technical details that Anthropic's official software library sends with every request (library version, operating system, processor architecture, .NET version) and, as with any internet connection, your IP address.
- Nothing else is sent: your files, other file names, notes, settings and history stay on your PC.
- The returned SVG drawing is stripped of anything executable or externally linked and saved in the `ai-icons/` folder.
- Requests are made with your own Anthropic account, and Anthropic bills the usage to that account. How Anthropic processes this data is governed by Anthropic's terms and [privacy policy](https://www.anthropic.com/legal/privacy).
- If a generated icon is inappropriate, the **Report inappropriate content** link in the folder icon window opens a pre-filled GitHub issue form in your browser. Nothing is sent automatically: the form contains only a title, empty questions and NestDesk's version number; your description, the folder name and the icon are not put in it. You decide what to write and whether to submit it (a GitHub account is required and reports are public).

**How the API key is stored:** The key is encrypted with the Windows Data Protection API (DPAPI, current-user scope); only the encrypted form is kept in `settings.json`. It can usually be decrypted only by the same Windows account on the same PC: if the settings file or a copy of it is moved to another account or another PC, the key cannot be read. (Exception: an account with a roaming profile on a corporate network can also decrypt it on another PC on that network.) By the nature of DPAPI, programs running under the same Windows account can decrypt it, so save the key only on a PC you trust. The **Delete** button in Settings removes the key from `settings.json`; the encrypted key stays in `settings.json.bak` until the next save, in older copies in `yedekler/` until they are replaced by newer ones, and in any `settings.json.bozuk-<time>` files until you delete them (delete these to remove it immediately). To invalidate the key completely, revoke it in the Anthropic Console.

### 6. Other connections you start

- Links in the app (e.g. the Anthropic Console, the download page, this privacy policy, the report form) open in your default browser only when you click them.
- Items you open from panels and shortcut boxes (including internet shortcuts) are opened by Windows with the associated program.
- If your desktop or a folder shown in a panel is in OneDrive or on a network folder, file operations happen there through Windows; NestDesk does not connect to those services itself.

### 7. Microsoft Store

If you installed NestDesk from the Microsoft Store, data such as download information and (depending on your Windows diagnostic settings) crash information may be processed by Microsoft under the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement); the developer can see reports based on it in Partner Center (e.g. download counts, crash statistics). This data is not collected by NestDesk's code.

### 8. Uninstalling and deleting your data

- **Microsoft Store edition:** uninstalling the app also makes Windows delete the package folder (settings, notes, move history and box records). If you used the installer edition before, the `%AppData%\NestDesk` or `%AppData%\Duzenleme` folder used by both editions remains; you can delete it manually.
- **Installer edition:** the uninstaller shows any desktop icons that were left hidden and asks whether to delete your settings (`%AppData%\NestDesk` and, if present, `%AppData%\Duzenleme`); you can also delete these folders manually.
- **Portable edition:** delete the `data` folder next to the program.
- **What stays as your own content in every edition** (the app does not delete these when it is uninstalled, because they are your files and folders):
  - The `NestDesk` box folder and the files in it. Before uninstalling, you can return everything to the desktop with Settings → **Desktop** → **Put everything back on the desktop**, or move the folder yourself.
  - Folder icons. Before uninstalling, remove all of them with Settings → **Advanced** → **Remove all folder icons**, or one by one with **Reset to default** in the icon window.
  - Files moved by auto-move stay in the folder they were moved to; you can undo moves on the **Auto-move** page before uninstalling.

### 9. Children

NestDesk does not collect personal data from anyone, including children.

### 10. Changes

If this policy changes, the updated version is published in this file and the date above is updated. Earlier versions are visible in the GitHub repository history.

### 11. Contact

For questions and requests: [GitHub Issues](https://github.com/hasakobey/duzenleme/issues). Please do not post personal information or API keys in public issues.
