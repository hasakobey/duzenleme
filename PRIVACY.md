# NestDesk — Gizlilik Politikası / Privacy Policy

[Türkçe](#türkçe) · [English](#english)

## Türkçe

**Son güncelleme:** 27 Eylül 2026

Bu politika, Windows için masaüstü düzenleyici **NestDesk**'in (Microsoft Store, kurulum sihirbazı ve taşınabilir sürümler) verileri nasıl işlediğini anlatır. NestDesk açık kaynaklıdır; aşağıda yazılanların hepsi [kaynak kodda](https://github.com/hasakobey/duzenleme) doğrulanabilir.

### Kısaca

- NestDesk **kişisel veri toplamaz** ve geliştiriciye hiçbir şey göndermez. Hesap, reklam, kullanım istatistiği (telemetri) ya da çökme raporu gönderimi yoktur.
- Ayarların, notların ve taşıma geçmişin **yalnızca bilgisayarında** tutulur.
- İnternete çıkan tek özellik isteğe bağlı **"yapay zekâ ile klasör simgesi"**dir: yalnızca kendi Anthropic API anahtarını girip bir simge istediğinde, klasörün adı ve yazdığın açıklama Anthropic'e gönderilir.

### 1. Toplanan veriler

Yok. NestDesk'in bir sunucusu yoktur; uygulama kullanım bilgisi, cihaz bilgisi, dosya adı ya da başka herhangi bir bilgiyi geliştiriciye göndermez. İsteğe bağlı yapay zekâ özelliği dışında (bkz. 4. bölüm) üçüncü kişilere de bir şey göndermez. Uygulama güncelleme denetimi de yapmaz (Store sürümünü Microsoft Store günceller).

NestDesk'in eski adı Düzenleme'dir; veri klasörü ve bazı iç adlar bu yüzden hâlâ "Duzenleme" adını taşır.

### 2. Bilgisayarında tutulanlar

NestDesk'in çalışması için gereken her şey yerel olarak şu klasörde saklanır:

- Kurulum sihirbazıyla kurulan sürüm: `%AppData%\Duzenleme`
- Taşınabilir sürüm: programın yanındaki `data` klasörü
- Microsoft Store sürümü: Windows, uygulamanın `%AppData%\Duzenleme`'de yeni oluşturduğu dosyaları paketine ayrılmış klasöre (`%LocalAppData%\Packages\…\LocalCache\Roaming\Duzenleme`) yönlendirir. Daha önce kurulum sihirbazı sürümü kullanıldıysa Store sürümü `%AppData%\Duzenleme`'deki mevcut ayarları ve geçmişi yerinde okuyup kullanmaya devam eder; bu dosyalar Store sürümü kaldırılınca silinmez. Uygulamanın kullandığı klasörü Ayarlar → **Gelişmiş** → **Dosya konumları** → **Ayar ve geçmiş dosyaları** → **Aç** ile görebilirsin.

| Dosya | İçerik |
|---|---|
| `settings.json` | Kurallar, widget'ların yeri ve görünümü, **notlarının metni**, kısayol kutusuna eklediğin uygulama ve dosya yolları, bölmelerde gizlediğin öğeler, klavye kısayolları, kayıtlı düzenler ve (girdiysen) şifrelenmiş API anahtarı |
| `yedekler/` | Uygulama açılırken günde en fazla bir kez alınan `settings.json` kopyaları; en yeni 7 tanesi tutulur |
| `journal.json` | Son 500 taşımanın kaydı: zaman, dosyanın eski ve yeni yolu (geri alabilmen için) |
| `settings.json.bozuk-<zaman>`, `journal.json.bozuk-<zaman>` | Yalnızca bu dosyalardan biri okunamayacak kadar bozulursa, varsayılan ayarlarla değiştirilmeden önce alınan kopyası. Kendiliğinden silinmez (elle silebilirsin); notlarını ve şifreli API anahtarını içerebilir |
| `ai-icons/` | Yapay zekâ ile ürettiğin simgeler (SVG); dosya adı, üretim zamanı ve açıklamanın (boşsa klasör adının) ilk 40 karakteridir |

Ayrıca:

- Bir klasöre simge verdiğinde simge, o klasörün içine gizli bir `.ico` dosyası olarak yazılır ve klasörün `desktop.ini` dosyası güncellenir (Windows'un standart klasör simgesi yöntemi). Simge penceresindeki **Varsayılana dön** bunları kaldırır.
- "Windows ile başlat" gibi seçenekler Windows'un kendi kullanıcı ayarlarına yazılır.
- Tanılama günlüğü yalnızca `DUZENLEME_DEBUGLOG` ortam değişkeniyle açıkça istenirse, senin belirttiğin dosyaya yazılır.

Bunların hiçbiri bilgisayarından dışarı gönderilmez.

### 3. Uygulamanın bilgisayarında yaptıkları

- **Masaüstü dosyaları:** NestDesk masaüstü klasörünü izler ve senin kurallarına göre dosyaları masaüstündeki klasörlere taşır. Bölmeler masaüstündeki (ve Genel Masaüstü'ndeki) öğeleri listeler. Dosyaların içeriği incelenmez; simgeler ve önizlemeler Windows'un kendi küçük resim hizmetiyle bilgisayarında oluşturulur.
- **Fare:** Boş masaüstüne çift tıklamayı algılamak için Windows'un ham fare girişi (Raw Input) dinlenir; fare kancası kullanılmaz, uygulama farenin hareketini hiçbir zaman geciktirmez. Yalnızca son sol tıklamanın zamanı ve ekrandaki konumu bellekte tutulur; kaydedilmez, gönderilmez. Ayarlar'daki "Boş masaüstüne çift tıklayınca gizle/göster" kapatılınca dinleme durur.
- **Klavye:** Klavye kancası kullanılmaz. Kısayollar (ör. `Ctrl+Alt+H`) Windows'un genel kısayol kaydıyla çalışır; uygulama kendi pencereleri dışında klavyeni izlemez.

### 4. İsteğe bağlı: yapay zekâ ile klasör simgesi (Anthropic)

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

**API anahtarının saklanması:** Anahtar, Windows'un veri koruma arayüzüyle (DPAPI, "geçerli kullanıcı" kapsamı) şifrelenir; `settings.json` içinde yalnızca şifreli hali tutulur. Şifreli anahtar genellikle yalnızca aynı bilgisayardaki aynı Windows hesabında çözülebilir: ayar dosyası ya da bir kopyası başka bir hesaba ya da başka bir bilgisayara taşınırsa okunamaz. (İstisna: kurumsal ağlarda dolaşım profili kullanan bir hesap, anahtarı ağdaki başka bir bilgisayarda da çözebilir.) DPAPI'nin doğası gereği aynı Windows hesabıyla çalışan programlar anahtarı çözebilir; bu yüzden anahtarı yalnızca güvendiğin bir bilgisayarda kaydet. Ayarlar'daki **Sil** düğmesi anahtarı `settings.json`'dan kaldırır; şifreli hali `yedekler/` klasöründeki eski kopyalarda bu kopyalar yenileriyle değişene kadar, varsa `settings.json.bozuk-<zaman>` dosyalarında ise sen silene kadar kalır (hemen kaldırmak için bunları silebilirsin). Anahtarı tamamen geçersiz kılmak için Anthropic Console'dan iptal edebilirsin.

### 5. Senin başlattığın diğer bağlantılar

- Ayarlar'daki bağlantılar (ör. Anthropic Console, indirme sayfası) yalnızca sen tıklayınca varsayılan tarayıcında açılır.
- Bölmelerden ve kısayol kutusundan açtığın öğeler (internet kısayolları dahil) Windows tarafından ilgili programla açılır.
- Masaüstün OneDrive'a ya da bir ağ klasörüne yönlendirilmişse dosya işlemleri Windows üzerinden o konumda yapılır; NestDesk bu hizmetlere kendisi bağlanmaz.

### 6. Microsoft Store

NestDesk'i Microsoft Store'dan yüklediysen, indirme bilgileri ve (Windows tanılama ayarlarına bağlı olarak) çökme bilgileri gibi veriler Microsoft tarafından [Microsoft Gizlilik Bildirimi](https://privacy.microsoft.com/privacystatement)'ne göre işlenebilir; geliştirici Partner Center'da bunlara dayanan raporları (ör. indirme sayısı, çökme istatistikleri) görebilir. Bu veriler NestDesk'in kodu tarafından toplanmaz.

### 7. Verilerini silmek

- **Microsoft Store sürümü:** Uygulamayı kaldırınca Windows paket klasörünü de siler. (Store sürümünden önce kurulum sihirbazı sürümü kullanıldıysa Store sürümünün de kullandığı `%AppData%\Duzenleme` klasörü kalır; elle silebilirsin.)
- **Kurulum sihirbazı sürümü:** Kaldırma programı ayarların silinip silinmeyeceğini sorar; istersen `%AppData%\Duzenleme` klasörünü elle de silebilirsin.
- **Taşınabilir sürüm:** Programın yanındaki `data` klasörünü sil.
- Klasör simgeleri klasörlerin içinde kalır; simge penceresindeki **Varsayılana dön** ile kaldırılır. Taşınan dosyalar taşındıkları yerde kalır; **Otomatik taşıma** sayfasından geri alınabilir.

### 8. Çocuklar

NestDesk, çocuklar dahil kimseden kişisel veri toplamaz.

### 9. Değişiklikler

Bu politika değişirse güncel hali bu dosyada yayımlanır ve yukarıdaki tarih güncellenir. Önceki sürümler GitHub deposunun geçmişinde görülebilir.

### 10. İletişim

Sorular ve talepler için: [GitHub Issues](https://github.com/hasakobey/duzenleme/issues). Herkese açık bir kayda kişisel bilgi ya da API anahtarı yazma.

---

## English

**Last updated:** September 27, 2026

This policy explains how **NestDesk**, a desktop organizer for Windows, handles data in all of its editions (Microsoft Store, installer and portable). NestDesk is open source; everything below can be verified in the [source code](https://github.com/hasakobey/duzenleme).

### Summary

- NestDesk **does not collect personal data** and sends nothing to the developer. There are no accounts, ads, usage analytics (telemetry) or crash-report uploads.
- Your settings, notes and move history are stored **only on your PC**.
- The only feature that uses the internet is the optional **AI folder icon** generator: only when you enter your own Anthropic API key and request an icon, the folder's name and the description you typed are sent to Anthropic.

### 1. Data we collect

None. NestDesk has no server of its own; the app does not send usage data, device information, file names or any other information to the developer. Apart from the optional AI feature (see section 4), it sends nothing to third parties either. The app does not check for updates either (the Store edition is updated by the Microsoft Store).

NestDesk was formerly called Düzenleme; that is why its data folder and some internal names still use "Duzenleme".

### 2. Data stored on your PC

Everything NestDesk needs is stored locally in this folder:

- Installer edition: `%AppData%\Duzenleme`
- Portable edition: the `data` folder next to the program
- Microsoft Store edition: Windows redirects files the app newly creates in `%AppData%\Duzenleme` to the folder reserved for its package (`%LocalAppData%\Packages\…\LocalCache\Roaming\Duzenleme`). If you used the installer edition before, the Store edition keeps reading and using your existing settings and history in `%AppData%\Duzenleme` in place; those files are not deleted when the Store edition is uninstalled. To see the folder the app uses, go to Settings → **Gelişmiş** (Advanced) → **Dosya konumları** (File locations) → **Ayar ve geçmiş dosyaları** (Settings and history files) → **Aç** (Open).

| File | Contents |
|---|---|
| `settings.json` | Rules, widget positions and appearance, **the text of your notes**, paths of apps and files you added to launchers, items you hid in fences, keyboard shortcuts, saved layouts and (if you entered one) the encrypted API key |
| `yedekler/` | Copies of `settings.json`, taken at most once a day when the app starts; the 7 newest are kept |
| `journal.json` | A record of the last 500 moves: time, the file's old and new path (so you can undo them) |
| `settings.json.bozuk-<time>`, `journal.json.bozuk-<time>` | Only if one of these files becomes too damaged to read: a copy taken before it is replaced with defaults. Never deleted automatically (you can delete them manually); may contain your notes and the encrypted API key |
| `ai-icons/` | Icons you generated with AI (SVG); the file name is the time of creation and the first 40 characters of your description (or of the folder name if the description is empty) |

In addition:

- When you give a folder an icon, the icon is written into that folder as a hidden `.ico` file and the folder's `desktop.ini` is updated (the standard Windows folder icon mechanism). **Varsayılana dön** (Reset to default) in the icon window removes them.
- Options such as "start with Windows" are written to Windows' own user settings.
- A diagnostic log is written only if you explicitly request it with the `DUZENLEME_DEBUGLOG` environment variable, to the file you specify.

None of this ever leaves your PC.

### 3. What the app does on your PC

- **Desktop files:** NestDesk watches your desktop folder and moves files into folders on your desktop according to your rules. Fences list the items on your desktop (and the Public Desktop). File contents are not inspected; icons and previews are generated on your PC by Windows' own thumbnail service.
- **Mouse:** Windows' raw mouse input (Raw Input) is read to detect a double-click on an empty area of the desktop; no mouse hook is used and the app never delays the mouse pointer. Only the time and screen position of the last left click are kept in memory; they are never saved or sent. Turning off "Boş masaüstüne çift tıklayınca gizle/göster" (hide/show on double-click) in Settings stops listening.
- **Keyboard:** No keyboard hook is used. Shortcuts (e.g. `Ctrl+Alt+H`) are registered with Windows' global hotkey system; the app does not monitor your keyboard outside its own windows.

### 4. Optional: AI folder icons (Anthropic)

This feature is off by default and works only if you enter your own Claude API key in Settings. Without a key, NestDesk itself never connects to the internet.

- **When you save the key,** a single verification request is sent to the Anthropic API to check that the key is valid. It contains only the key and the standard technical details listed below.
- **When you press "Üret" (Generate),** the following is sent to the Anthropic API:
  - the **name** of the folder you are styling (not its full path or contents),
  - the **description** you typed for the icon,
  - a fixed instruction text describing how to draw the icon,
  - your API key, for authentication,
  - technical details that Anthropic's official software library sends with every request (library version, operating system, processor architecture, .NET version) and, as with any internet connection, your IP address.
- Nothing else is sent: your files, other file names, notes, settings and history stay on your PC.
- The returned SVG drawing is stripped of anything executable or externally linked and saved in the `ai-icons/` folder.
- Requests are made with your own Anthropic account, and Anthropic bills the usage to that account. How Anthropic processes this data is governed by Anthropic's terms and [privacy policy](https://www.anthropic.com/legal/privacy).

**How the API key is stored:** The key is encrypted with the Windows Data Protection API (DPAPI, current-user scope); only the encrypted form is kept in `settings.json`. It can usually be decrypted only by the same Windows account on the same PC: if the settings file or a copy of it is moved to another account or another PC, the key cannot be read. (Exception: an account with a roaming profile on a corporate network can also decrypt it on another PC on that network.) By the nature of DPAPI, programs running under the same Windows account can decrypt it, so save the key only on a PC you trust. The **Sil** (Delete) button in Settings removes the key from `settings.json`; the encrypted key stays in older copies in `yedekler/` until they are replaced by newer ones, and in any `settings.json.bozuk-<time>` files until you delete them (delete these to remove it immediately). To invalidate the key completely, revoke it in the Anthropic Console.

### 5. Other connections you start

- Links in Settings (e.g. the Anthropic Console, the download page) open in your default browser only when you click them.
- Items you open from fences and launchers (including internet shortcuts) are opened by Windows with the associated program.
- If your desktop is redirected to OneDrive or a network folder, file operations happen there through Windows; NestDesk does not connect to those services itself.

### 6. Microsoft Store

If you installed NestDesk from the Microsoft Store, data such as download information and (depending on your Windows diagnostic settings) crash information may be processed by Microsoft under the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement); the developer can see reports based on it in Partner Center (e.g. download counts, crash statistics). This data is not collected by NestDesk's code.

### 7. Deleting your data

- **Microsoft Store edition:** Uninstalling the app also makes Windows delete the package folder. (If you used the installer edition before, the `%AppData%\Duzenleme` folder, which the Store edition also uses, remains; you can delete it manually.)
- **Installer edition:** The uninstaller asks whether to delete your settings; you can also delete `%AppData%\Duzenleme` manually.
- **Portable edition:** Delete the `data` folder next to the program.
- Folder icons stay inside the folders; remove them with **Varsayılana dön** (Reset to default) in the icon window. Moved files stay where they were moved; you can undo moves on the **Otomatik taşıma** (Auto-move) page.

### 8. Children

NestDesk does not collect personal data from anyone, including children.

### 9. Changes

If this policy changes, the updated version is published in this file and the date above is updated. Earlier versions are visible in the GitHub repository history.

### 10. Contact

For questions and requests: [GitHub Issues](https://github.com/hasakobey/duzenleme/issues). Please do not post personal information or API keys in public issues.
