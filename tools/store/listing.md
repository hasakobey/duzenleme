# Microsoft Store listesi — NestDesk 2.0.0

Partner Center'a kopyalanacak hazır metinler. Her alan ayrı bir kod bloğunda; GitHub'da bloğun sağ üstündeki kopyala düğmesiyle al. Açıklama alanları düz metindir: içine HTML ya da bağlantı koyma (Microsoft'un önerisi; bağlantılar kendi alanlarına girer).

Sınırlar Microsoft Learn'deki MSIX sayfalarından ("Add and edit Store listing info", "App screenshots, images, and trailers") alındı (Eylül 2026). Keywords alanı MSIX sayfalarında yalnızca "Create an app submission" sayfasının Store listings tablosunda isteğe bağlı alan olarak geçiyor, sınırı yazmıyor; anahtar kelime sınırı Microsoft Learn'ün "Store listing for titles on XBOX devices" sayfasındaki (oyun yayımlama rehberi, Additional information tablosu) değerdir:

| Alan (Partner Center) | Sınır | Bu dosyada |
|---|---|---|
| Description | zorunlu, en fazla 10.000 karakter | ~3.900 karakter |
| Short description | en fazla 1.000; bazı görünümlerde yalnızca ilk 270 karakter görünür | < 270 |
| Product features | en fazla 20 madde, her biri en fazla 200 karakter; madde işareti koyma | 18 madde |
| Keywords | en fazla 7 terim, tüm terimlerde toplam en fazla 21 kelime; terim başına en fazla 40 karakter (Partner Center daha kısa bir sınır gösterirse o geçerli) | 7 terim, en uzunu 20 karakter, 14–15 kelime |
| What's new in this version | en fazla 1.500 karakter; **ilk gönderimde boş bırak** | 2.0.0 ilk gönderim: boş; sonraki sürümde doldurulur |
| Screenshot caption | en fazla 200 karakter | 6 açıklama (baştaki "1." numarasını yapıştırma) |

## Ortak değerler (iki dil için aynı)

| Alan | Değer |
|---|---|
| Privacy policy URL | `https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md` |
| Website | `https://github.com/hasakobey/duzenleme` |
| Support contact info | `https://github.com/hasakobey/duzenleme/issues` |
| Copyright and trademark info | `© 2026 hasakobey` |
| Developed by | `hasakobey` |
| Base price | Free (Ücretsiz) |
| Category | **Productivity**; Secondary category: **Utilities + tools** |
| Sistem gereksinimi | Windows 10 sürüm 2004 (19041) veya üstü, Windows 11 — paket bildirimindeki `MinVersion` |

Depo adresi şimdilik `hasakobey/duzenleme` kalır (uygulamanın eski adı; depo yeniden adlandırılınca bu bağlantılar ve `Core/AppInfo.cs` → `ReleasesUrl` birlikte güncellenir).

**Kategori gerekçesi:** Microsoft'un tablosunda *Productivity* "bir işi daha verimli bitirmeye yardım eden uygulamalar" (örnekler: not alma, verileri görüntüleme ve sıralama) olarak tanımlanıyor; NestDesk'in asıl işi masaüstünü düzenli tutmak ve dosyaları kendiliğinden yerine koymak, not ve yapılacaklar widget'ları da bu tanıma giriyor. *Utilities + tools* (örnek: dosya yöneticisi) ikincil kategori olarak dosya taşıma ve bölme tarafını kapsar. Productivity'nin alt kategorisi yok. *Personalization* (tema, duvar kâğıdı) widget'lara ve klasör simgelerine uyardı ama uygulamanın ana değerini anlatmıyor.

**Ek lisans koşulları (Additional license terms, isteğe bağlı):**

```text
NestDesk açık kaynaklıdır ve MIT Lisansı ile dağıtılır. Kullanılan açık kaynak bileşenlerin lisansları THIRD-PARTY-NOTICES.txt dosyasında listelenir.
```

```text
NestDesk is open source and distributed under the MIT License. Licenses of the open source components it uses are listed in THIRD-PARTY-NOTICES.txt.
```

---

## Türkçe (tr-TR)

### Ürün adı

```text
NestDesk
```

### Kısa açıklama (Short description)

```text
Masaüstüne düşen dosyaları senin oluşturduğun klasörlere kendiliğinden taşır, masaüstünü bölmelere ayırır; saat, tarih, not, yapılacaklar ve kısayol kutusu widget'ları ekler. Her taşıma geri alınabilir. Ücretsiz, açık kaynak, reklamsız.
```

### Açıklama (Description)

```text
NestDesk, Windows masaüstünü derli toplu tutan ücretsiz ve açık kaynaklı bir düzenleyicidir. Masaüstüne indirdiğin ya da kaydettiğin dosyaları, masaüstünde kendi oluşturduğun klasörlere kendiliğinden taşır; masaüstünü bölmelere ayırır ve duvar kâğıdının üzerine saat, tarih, not ve yapılacaklar listesi gibi şık widget'lar ekler. Masaüstün için derli toplu bir yuva.

İLK AÇILIŞ
• Üç adımlık kısa bir karşılama masaüstünü seninle birlikte kurar: bölmeler, dosya taşıma ve küçük araçlar. İstersen atlayabilirsin.
• Sen onay vermeden hiçbir dosya taşınmaz. Karşılama, şu an masaüstünde duran dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir.

OTOMATİK TAŞIMA
• Masaüstünde "PDF" adında bir klasörün varsa masaüstüne düşen her PDF oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için hazır kurallar var; klasör adlarını ve uzantıları istediğin gibi değiştirebilirsin.
• Klasör masaüstünde yoksa dosyaya dokunulmaz (istersen "Klasör yoksa oluştur" seçeneğini açabilirsin). Büyük/küçük harf ve Türkçe karakter fark etmez: "Arşivler" ile "arsivler" aynı klasördür.
• Kısayollar, gizli ve sistem dosyaları ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz.
• Her taşıma "Otomatik taşıma" sayfasında listelenir ve tek tıkla geri alınır; geri aldığın dosya bir daha kendiliğinden taşınmaz.

BÖLMELER
• Masaüstünü bölmelere ayır: klasörler, kısayollar, dosyalar ya da bir klasörün içi (ör. PDF) ayrı panellerde.
• "Masaüstümü bölmelere ayır" tek tıkla hepsini kurar. İstersen Windows'un masaüstü simgeleri gizlenir, her şey yalnızca bölmelerde görünür. Dosyalara dokunulmaz; modu kapatınca ya da uygulamadan çıkınca simgeler geri gelir.
• Bölmede yazdıkça ara (alt klasörler dahil), dosyaları bölmeden bölmeye sürükleyerek taşı, resim, video ve PDF önizlemelerini gör; istersen bölme, fare üzerinden çekilince başlığına katlansın.
• Boş masaüstüne çift tıkla ya da Ctrl+Alt+H'ye bas: simgeler ve widget'lar gizlenir, tekrarlayınca geri gelir.

WIDGET'LAR
• Saat: büyük dijital saat, günün selamı, isteğe bağlı saniye.
• Tarih: gün, ay, yıl ve bugünün vurgulandığı haftalık şerit.
• Not: altı renkli yapışkan not; yazdıkça kaydedilir.
• Yapılacaklar: onay kutulu liste; biten maddeler üstü çizili olarak yerinde kalır.
• Kısayol kutusu: sekmeli uygulama ve dosya rafı; tek tıkla aç, istersen yönetici olarak çalıştır.
• Sürüklerken widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır, üst üste binmez. Cam, koyu ya da açık arka plan, vurgu rengi, saydamlık ve ölçek ayarlanır; widget düzenlerini adıyla kaydedip tek tıkla geri dönebilirsin.
• Kaldırma düğmesi (×) her widget'ta hep yerinde durur; kaldırdığın widget'ı tek tıkla geri getirebilirsin.
• Birden çok monitör ve farklı ekran ölçekleri desteklenir; monitör takılıp çıkarılınca widget'lar yerlerine döner.

KLASÖR SİMGELERİ
• 30 sembol ve 10 renkten oluşan hazır kütüphane; klasör adına göre otomatik öneri ("Oyunlarım" için oyun kolu gibi). Kendi .ico, .png ya da .svg dosyanı da kullanabilirsin.
• Masaüstünde yeni klasör açtığında simge önerisi gelir.
• İsteğe bağlı: kendi Claude (Anthropic) API anahtarını girersen yazdığın kısa açıklamaya göre yapay zekâ ile özel simge üretebilirsin. Bu özellik zorunlu değildir; kullanım ücretini Anthropic senin hesabına yansıtır.

GİZLİLİK
Hesap, reklam ve telemetri yok. Ayarların, notların ve geçmişin yalnızca bilgisayarında tutulur. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir: klasörün adını ve yazdığın açıklamayı Anthropic'e gönderir (ayrıntılar gizlilik politikasında).

KLAVYE KISAYOLLARI (değiştirilebilir)
Ctrl+Alt+B widget ekle · Ctrl+Alt+H masaüstünü gizle/göster · Ctrl+Alt+O şimdi düzenle · Ctrl+Alt+D NestDesk'i aç · Ctrl+Alt+N yeni not · Ctrl+Alt+W widget'ları öne getir

NestDesk açık kaynaklıdır (MIT lisansı). Bildirim alanında (sistem tepsisi) çalışır; ana pencereyi kapatmak uygulamayı kapatmaz.
```

### Ürün özellikleri (Product features) — her satır ayrı bir kutuya

```text
Masaüstüne düşen dosyaları masaüstünde oluşturduğun klasörlere kendiliğinden taşır (ör. PDF'ler "PDF" klasörüne)
İlk açılışta kısa bir karşılama; sen onay vermeden hiçbir dosya taşınmaz
PDF, resim, belge, arşiv, video ve müzik için hazır kurallar; klasör adları ve uzantılar değiştirilebilir
Her taşıma geri alınabilir; geri alınan dosya bir daha taşınmaz
Kısayollar, sistem dosyaları ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz
Bölmeler: klasörler, kısayollar, dosyalar ya da bir klasörün içi masaüstünde ayrı panellerde
İsteğe bağlı: Windows masaüstü simgelerini gizle, her şey yalnızca bölmelerde görünsün; çıkınca simgeler geri gelir
Bölmede anında arama, sürükle-bırakla taşıma, resim/video/PDF önizlemesi, kendiliğinden katlanma
Boş masaüstüne çift tıkla ya da Ctrl+Alt+H: masaüstünü gizle ve göster
Saat, tarih, yapışkan not, onay kutulu yapılacaklar listesi ve sekmeli kısayol kutusu widget'ları
Mıknatıslı yerleşim, çakışma engeli ve kayıtlı widget düzenleri
Cam, koyu ve açık arka plan; vurgu rengi, saydamlık ve ölçek ayarı
Birden çok monitör ve farklı ekran ölçekleri desteği
300 hazır klasör simgesi (30 sembol × 10 renk), ada göre öneri ya da kendi .ico/.png/.svg dosyan
İsteğe bağlı: kendi Anthropic API anahtarınla yapay zekâ ile özel klasör simgesi
Değiştirilebilir genel klavye kısayolları
Hesap, reklam ve telemetri yok; verilerin bilgisayarında kalır
Ücretsiz ve açık kaynak (MIT)
```

### Anahtar kelimeler (Keywords)

```text
masaüstü düzenleyici
dosya düzenleme
masaüstü bölmeleri
masaüstü widget
klasör simgesi
yapışkan not
masaüstü saati
```

### Bu sürümdeki yenilikler (What's new in this version) — 2.0.0

NestDesk'in Store'daki ilk sürümü 2.0.0 olduğu için bu alan **boş bırakılır** (Microsoft ilk gönderimde boş bırakmanı istiyor). Sonraki her sürümde (ör. 2.0.1) buraya o sürümün yenilikleri kısa maddeler hâlinde (`• ` ile başlayan satırlar, en fazla 1.500 karakter) Türkçe ve İngilizce yazılır.

### Ekran görüntüsü açıklamaları (caption)

```text
1. Bölmelere ayrılmış, derli toplu masaüstü: klasörler, kısayollar, dosyalar ve PDF bölmeleri; saat, tarih ve yapılacaklar widget'ları
2. Ctrl+Alt+B ile açılan "Widget ekle" penceresi: bölme, saat, tarih, not, yapılacaklar ya da kısayol kutusu tek tıkla masaüstünde
3. Otomatik taşıma: hangi dosya türünün hangi klasöre gideceğini sen belirlersin; her taşıma tek tıkla geri alınır
4. İlk açılışta kısa karşılama: bölmeler, dosya taşıma ve araçlar; dosyaların sen onay vermeden taşınmaz
5. Klasör simgeleri: 300 hazır simge ve klasör adına göre öneri
6. Widget'ları özelleştir: cam, koyu ya da açık arka plan, vurgu rengi, saydamlık, simge boyutu
```

---

## English (en-US)

Paket bildirimi `tr-TR` ve `en-US` dillerini bildirdiği için Partner Center iki dili de listeler. Arayüz şimdilik yalnızca Türkçe olduğundan İngilizce açıklama bunu açıkça söylüyor; bu satırı silme (liste uygulamayı doğru anlatmazsa sertifikasyon reddedebilir).

### Product name

```text
NestDesk
```

### Short description

```text
Automatically moves files from your desktop into the folders you created, splits your desktop into fences and adds clock, date, note, to-do and launcher widgets. Every move can be undone. Free, open source, no ads.
```

### Description

```text
NestDesk keeps your Windows desktop tidy. Files you download or save to the desktop are moved automatically into folders you created there, your desktop can be split into neat fences, and stylish clock, date, note, to-do and launcher widgets sit right on your wallpaper. A tidy home for your desktop. It is free, open source and has no ads.

Please note: the app's interface is currently in Turkish.

FIRST LAUNCH
• A short three-step welcome sets up your desktop with you: fences, moving files and small tools. You can skip it.
• No file is moved before you agree. The welcome shows in advance which of the files currently on your desktop would go to which folder.

AUTOMATIC MOVING
• If your desktop has a folder named "PDF", every PDF saved to the desktop goes there. Ready-made rules cover PDFs, pictures, documents, archives, videos and music; change folder names and extensions as you like (default folder names are Turkish, e.g. "Resimler" for pictures).
• If the target folder does not exist, the file is left alone (optionally, missing folders can be created). Matching ignores letter case and Turkish accents: "Arşivler" and "arsivler" are the same folder.
• Shortcuts, hidden and system files and downloads in progress are never moved, and no file is ever overwritten.
• Every move is listed on the Auto-move page and can be undone with one click; a file you moved back is never moved automatically again.

FENCES
• Split your desktop into panels: folders, shortcuts, files, or the contents of a folder (e.g. PDF).
• One click sets it all up. Optionally the Windows desktop icons are hidden so everything appears only in fences. Your files are not touched, and the icons come back when you turn the mode off or quit the app.
• Search as you type (including subfolders), drag files between fences, preview images, videos and PDFs, and let a fence roll up to its title bar when the mouse leaves.
• Double-click the empty desktop or press Ctrl+Alt+H to hide icons and widgets; do it again to bring them back.

WIDGETS
• Clock: large digital clock with a greeting and optional seconds.
• Date: day, month and year with a week strip that highlights today.
• Note: sticky notes in six colors, saved as you type.
• To-do: a checklist; finished items stay in place, crossed out.
• Launcher: tabbed shelf for apps and files; open with one click or run as administrator.
• Widgets snap to each other and to screen edges like magnets and never overlap. Choose a glass, dark or light background, accent color, opacity and scale, and save named layouts to switch between them with one click.
• The remove button (×) is always in place on every widget, and a removed widget can be brought back with one click.
• Multiple monitors with different display scaling are supported; widgets return to their places when monitors are reconnected.

FOLDER ICONS
• A library of 30 symbols in 10 colors with automatic suggestions based on the folder name. You can also use your own .ico, .png or .svg files.
• When you create a new folder on the desktop, an icon is suggested.
• Optional: enter your own Claude (Anthropic) API key to generate a custom icon from a short description with AI. This is not required; usage is billed by Anthropic to your own account.

PRIVACY
No account, no ads, no telemetry. Your settings, notes and history stay on your PC. The only feature that uses the internet is the optional AI icon generator, which sends the folder name and your description to Anthropic (details in the privacy policy).

KEYBOARD SHORTCUTS (customizable)
Ctrl+Alt+B add widget · Ctrl+Alt+H hide/show desktop · Ctrl+Alt+O organize now · Ctrl+Alt+D open NestDesk · Ctrl+Alt+N new note · Ctrl+Alt+W bring widgets to front

NestDesk is open source under the MIT License. It runs in the notification area (system tray); closing the main window keeps it running.
```

### Product features — one line per box

```text
Automatically moves files dropped on the desktop into folders you created there (e.g. PDFs into a "PDF" folder)
A short welcome on first launch; your files are never moved before you agree
Ready-made rules for PDFs, pictures, documents, archives, videos and music; edit folder names and extensions
Every move can be undone; files you move back are never moved again
Never moves shortcuts, system files or downloads in progress, and never overwrites a file
Fences: show folders, shortcuts, files or a folder's contents in separate desktop panels
Optional: hide the Windows desktop icons and show everything only in fences; icons return when you quit
Instant search in fences, drag and drop between fences, image/video/PDF previews, auto roll-up
Double-click the empty desktop or press Ctrl+Alt+H to hide or show the desktop
Clock, date, sticky note, to-do checklist and tabbed launcher widgets
Magnetic snapping, no overlapping and saved widget layouts
Glass, dark and light backgrounds with accent colors, opacity and scale
Works with multiple monitors and different display scaling
300 ready-made folder icons (30 symbols × 10 colors), suggestions by folder name, or your own .ico/.png/.svg
Optional AI-generated folder icons with your own Anthropic API key
Customizable global keyboard shortcuts
No account, no ads, no telemetry; your data stays on your PC
Free and open source (MIT)
```

### Keywords

```text
desktop organizer
auto file organizer
desktop widgets
desktop icons
folder icons
sticky notes
desktop clock
```

### What's new in this version — 2.0.0

Leave blank: 2.0.0 is NestDesk's first Store submission (see the Turkish note above).

### Screenshot captions

```text
1. A tidy desktop split into fences for folders, shortcuts, files and PDFs, with clock, date and to-do widgets
2. The "Widget ekle" (Add widget) window, opened with Ctrl+Alt+B: add a fence, clock, date, note, to-do list or launcher with one click
3. Auto-move: you decide which file types go to which folder, and every move can be undone with one click
4. A short welcome on first launch: fences, moving files and tools; your files are never moved before you agree
5. Folder icons: 300 ready-made icons with suggestions based on the folder name
6. Customize widgets: glass, dark or light background, accent color, opacity and icon size
```

---

## Yaş derecelendirmesi (Age ratings) — IARC anketi için yanıt önerileri

Anket soruları IARC tarafından zaman zaman değiştirilir; aşağıdakiler yanıtların **dayanağıdır**, soru metinleri formda farklı olabilir. Her soruyu uygulamanın gerçekte yaptığına göre yanıtla.

- **İlk soru (uygulama türü):** oyun olmayan, yardımcı program / üretkenlik türünü seç (formda "Utility", "Productivity" ya da "Other" gibi geçer).
- **Şiddet, korku, cinsellik/çıplaklık, küfür, uyuşturucu/alkol/tütün, kumar, ayrımcı içerik:** Hayır. Uygulamada böyle bir içerik yok.
- **Kullanıcılar birbiriyle etkileşiyor ya da içerik paylaşıyor mu (sohbet, mesaj, paylaşım):** Hayır. Uygulamada kullanıcılar arası hiçbir iletişim yok.
- **Kişisel bilgiler üçüncü kişilerle paylaşılıyor mu:** Hayır. Uygulama kişisel bilgi toplamaz. (İsteğe bağlı yapay zekâ özelliği, kullanıcı istediğinde klasörün adını ve kullanıcının yazdığı açıklamayı kullanıcının kendi Anthropic API anahtarıyla Anthropic'e gönderir; ayrıntılar gizlilik politikasının 4. bölümünde.)
- **Kullanıcının konumu paylaşılıyor mu:** Hayır.
- **Dijital ürün satın alma / uygulama içi satın alma:** Hayır. (Anthropic API ücreti kullanıcının Anthropic ile kendi hesabıdır; uygulama bir şey satmaz.)
- **Reklam:** Hayır.
- **Sınırsız internet erişimi (web tarayıcı, arama motoru):** Hayır. Uygulama web içeriği göstermez; internet yalnızca isteğe bağlı yapay zekâ simgesi için ve yalnızca Anthropic API'sine kullanılır.
- **Üretken yapay zekâ ile ilgili bir soru çıkarsa:** Evet, isteğe bağlı — kullanıcı kendi API anahtarını girerse metin açıklamasından metin içermeyen, basit şekillerden oluşan bir klasör simgesi (SVG) üretilir; çıktı yalnızca o kullanıcının bilgisayarında kalır, kimseyle paylaşılmaz.

Beklenen sonuç en düşük yaş derecesidir (ör. PEGI 3 / ESRB Everyone); formu doldurunca **Save and generate** ile derecelendirmeleri gör.

Partner Center'daki gizlilik sorusu (**Properties** → uygulama kişisel bilgiye erişiyor, topluyor ya da iletiyor mu?): uygulama kişisel veri toplamasa da kullanıcının yazdığı metni Anthropic'e iletebildiği ve `runFullTrust` bildirdiği için **Yes** seç ve gizlilik politikası URL'sini gir. Microsoft da bu durumda URL'yi zaten isteyebiliyor.

---

## Kısıtlı yetenek gerekçesi: runFullTrust (Submission options → Restricted capabilities)

İngilizce yaz; kutu kısaysa aşağıdaki kısa sürümü kullan.

```text
NestDesk is a Win32 desktop application (WPF, .NET 10) packaged as MSIX with the Windows.FullTrustApplication entry point. It needs runFullTrust because its core features rely on desktop and shell APIs that are not available to app-container apps:

1. Desktop icon visibility: when the user turns on "show desktop icons only in fences", double-clicks the empty desktop or presses Ctrl+Alt+H, the app finds Explorer's desktop icon view (Progman/WorkerW > SHELLDLL_DefView > SysListView32) and hides or shows it with ShowWindow. No files are changed. Icons are restored when the mode is turned off, when the app exits and when the user signs out.
2. Desktop-attached widgets: clock, date, note, to-do, launcher and fence windows are owned by the desktop window and kept at the bottom of the z-order (SetWindowPos, WM_WINDOWPOSCHANGING, WS_EX_NOACTIVATE) so they stay on the desktop and survive Win+D. Monitor and per-monitor DPI APIs place them on the right screen.
3. Double-click detection: a low-level mouse hook (WH_MOUSE_LL) records only the time and position of left-button presses to detect a double-click on an empty area of the desktop. Nothing is logged or transmitted, and the hook is removed when the user turns this option off in Settings.
4. Global hotkeys: six user-configurable shortcuts (e.g. Ctrl+Alt+H, Ctrl+Alt+B) are registered with RegisterHotKey. No keyboard hook is used.
5. File system monitoring and moving: a FileSystemWatcher monitors the user's Desktop folder, and, once the user has turned automatic moving on, new files are moved into folders the user created on the desktop according to user-defined rules. Every move is journaled and can be undone. On explicit user request, fences also rename items, create folders and send items to the Recycle Bin.
6. Shell integration: folder icons are applied with SHGetSetFolderCustomSettings (desktop.ini plus a hidden .ico inside the folder); icons and thumbnails come from SHGetFileInfo and IShellItemImageFactory; items are opened with ShellExecute, including "Run as administrator" for launcher items the user added, and Explorer shell locations such as This PC and Recycle Bin.
7. A notification area (tray) icon is the app's main entry point.

The app does not install drivers or services and does not require administrator rights. Its only network use is optional and goes only to the Anthropic API: when the user saves their own Anthropic API key, the key is verified with one request, and when the user requests an AI-generated folder icon, the folder name and the user's description are sent.
```

Kısa sürüm:

```text
Win32 WPF (.NET 10) desktop app packaged with the Windows.FullTrustApplication entry point. runFullTrust is required to: hide/show Explorer's desktop icon view (SHELLDLL_DefView/SysListView32) on user request and restore it on exit; keep widget windows owned by the desktop at the bottom of the z-order; detect double-clicks on the empty desktop with a low-level mouse hook (time/position only, nothing stored or sent, can be turned off); register user-configurable global hotkeys (RegisterHotKey); watch the user's Desktop folder and move files into user-created folders by user rules (undoable); apply folder icons with SHGetSetFolderCustomSettings; and launch user-chosen items via ShellExecute. No drivers, services or admin rights.
```

---

## Sertifikasyon test notları (Submission options → Notes for certification)

```text
Thank you for testing NestDesk. No account or sign-in is needed, and the app does not require administrator rights. The UI is in Turkish; English meanings of the labels are given in parentheses.

GETTING STARTED
- On first launch the app adds an icon to the notification area (system tray) and opens a short welcome window with 3 steps (fences, moving files, tools). Go through it with "İleri" (Next) and "Bitti" (Done), or press "Şimdilik atla" (Skip for now): skipping changes nothing on the desktop and opens the main window.
- No file is moved before the user agrees. Automatic moving stays off unless "Evet, dosyalarımı yerine taşı" (Yes, move my files) is chosen in step 2 of the welcome, or it is turned on later on the "Otomatik taşıma" (Auto-move) page. Files are only moved into matching folders that exist on the desktop (default rule folders: PDF, Resimler, Belgeler, Arşivler, Videolar, Müzik). On a clean test machine nothing is moved.
- Closing the main window keeps the app running in the tray. Left-click the tray icon to reopen it. To quit, right-click the tray icon > "Çıkış" (Exit).

ADD WIDGETS
- Press Ctrl+Alt+B, right-click the tray icon > "Widget ekle…" (Add widget…), or start "NestDesk – Widget ekle" from the Start menu. Click a tile, e.g. "Klasörler" (Folders), "Dosyalar" (Files), "Saat" (Clock), "Not" (Note), "Yapılacaklar" (To-do), "Kısayol kutusu" (Launcher).
- To remove a widget, click the × in its top-right corner or right-click it > "Kaldır" (Remove).

AUTOMATIC MOVING
- Turn it on: main window > "Otomatik taşıma" (Auto-move) > the switch at the top, or tray > "Otomatik taşıma" (checked = on).
- Create a folder named "PDF" on the desktop, then copy a .pdf file to the desktop. After about 2 seconds it is moved into the PDF folder and a notification appears.
- Undo it on the "Otomatik taşıma" (Auto-move) page with "Geri al" (Undo), or with tray > "Son taşımayı geri al" (Undo last move). Files are only moved if the target folder already exists.

DESKTOP ICONS MODE ("show desktop icons only in fences")
- Turn on: main window > "Widget'lar" (Widgets) > the switch "Masaüstü simgelerini yalnızca bölmelerde göster" (Show desktop icons only in fences), or "Masaüstümü bölmelere ayır" (Split my desktop into fences) in the "Widget ekle" (Add widget) window. This adds fences for folders, shortcuts and files and hides the Windows desktop icons; everything is shown inside the fences. No files are moved or changed.
- Turn off: the same switch, tray > "Masaüstü simgeleri yalnızca bölmelerde", or right-click any fence > "Diğer" (More) > "Masaüstü simgelerini yalnızca bölmelerde göster". The Windows desktop icons come back immediately.
- When the app exits (tray > "Çıkış") or the user signs out, the Windows desktop icons are restored automatically.
- Quick hide: double-click an empty area of the desktop or press Ctrl+Alt+H to hide icons and widgets; repeat to show them again. This can be turned off in "Ayarlar" (Settings) > "Masaüstü" (Desktop).

OPTIONAL AI FEATURE
- AI-generated folder icons require the user's own Anthropic API key ("Ayarlar" (Settings) > "Gelişmiş" (Advanced) > "Yapay zekâ ile klasör simgesi" (AI folder icon)). Without a key, the "Üret" (Generate) button is disabled; all other features work offline and without a key. The app has no other network use.
```

---

## Partner Center kontrol listesi

Menü adları Microsoft Learn'deki İngilizce adlardır; Partner Center Türkçe açılırsa karşılıkları farklı görünebilir.

1. **Ürün adı (yapıldı).** Partner Center'daki ürün `NestDesk` adıyla ayrıldı. Paketteki görünen ad (`AppxManifest.xml` → `DisplayName`, uygulamada `Core/AppInfo.cs` → `Name`) ayrılan adla birebir aynı olmalı; her dilin Store listing'inde **Product name** olarak `NestDesk` seç. Ad bir gün değişirse: **Product management** → **Manage product names** → yeni ad → **Check availability** → **Reserve**; ad alınmışsa dur. Paket kimliği (`identity.json` → `Name`) ad değişince değişmez; `AppInfo.Name`, manifest ve bu dosya birlikte değişir.
2. **Ürün kimliği (yapıldı).** `tools/store/identity.json` Partner Center'daki NestDesk ürününün kimliğiyle dolu (`Hasako.NestDesk`). Değerler şuradan gelir: uygulamanın sayfasında sol menü **Product management** → **Product identity** (Ürün yönetimi → Ürün kimliği); **Package/Identity/Name** → `Name`, **Package/Identity/Publisher** (`CN=…` biçiminde) → `Publisher`, **Package/Properties/PublisherDisplayName** → `PublisherDisplayName`. Aynı sayfadaki Package Family Name ve Store ID bilgi içindir; mağaza bağlantın `https://apps.microsoft.com/detail/<Store ID>` olur.
3. **Paketi üret.** `tools/package-msix.ps1 -Strict` betiğini çalıştır (`-Strict`, yer tutucu kalmışsa durur); `dist-store\NestDesk-<sürüm>.msixbundle` üretilir (ör. `2.0.0.0`). Paketin içindeki program dosyası bilerek `Duzenleme.exe` adını taşır (eski ad; kimlik değil, görünmez). Store aynı sürümü ikinci kez kabul etmez: her yeni gönderimde `<Version>` artmalı.
4. **Gizlilik politikasını yayımla.** `PRIVACY.md`'yi GitHub'a gönder ve `https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md` adresinin tarayıcıda açıldığını kontrol et.
5. **Gönderimi başlat.** Uygulamanın genel bakış sayfasında **Start submission**.
6. **Pricing and availability.** **Markets**: varsayılan (tüm pazarlar) kalabilir. **Base price**: **Free**. **Free trial** yok. **Discoverability** ve **Schedule** varsayılan.
7. **Properties.** **Category**: Productivity, **Secondary category**: Utilities + tools. Gizlilik sorusuna **Yes** ve gizlilik URL'si. **Website** ve **Support contact info** yukarıdaki tablodan. **Product declarations**: yalnızca gerçekten geçerli olan kutuları işaretle.
8. **Age ratings.** Anketi yukarıdaki önerilere göre doldur → **Save and generate**.
9. **Packages.** `.msixbundle` dosyasını sürükle-bırak; doğrulama (validation) bitene kadar bekle. Hata çıkarsa çoğunlukla kimlik değerleri ya da görünen ad Product identity ile uyuşmuyordur. **Device family availability**: Windows 10/11 Desktop.
10. **Store listings.** Paket iki dil bildirdiği için Türkçe ve İngilizce listeler hazır gelir; her dili ayrı aç ve bu dosyadaki metinleri yapıştır. **What's new in this version** ilk gönderimde (2.0.0) boş. **Store logos** bölümüne 300×300 PNG uygulama simgesi yüklemen önerilir (yüklemezsen paketteki simge kullanılır).
11. **Ekran görüntüleri** (her dil için ayrı yüklenir; aynı görüntüler kullanılabilir, açıklamalar dile göre):
    - En az 1 zorunlu, en fazla 10 masaüstü görüntüsü; Microsoft 4 ve üstünü (SSS'de 5–8) öneriyor. **6 tane** öneriyorum, sırası yukarıdaki açıklamalarla aynı:
      1. Bölmelere ayrılmış masaüstü (Klasörler, Kısayollar, Dosyalar, PDF bölmeleri + saat + tarih + yapılacaklar) — vitrin görüntüsü.
      2. "Widget ekle" penceresi (Ctrl+Alt+B).
      3. Ana pencere → **Otomatik taşıma** sayfası (kurallar ve birkaç taşıma, "Geri al" düğmeleriyle).
      4. Karşılama penceresi, 2. adım (dosya taşıma önizlemesi). Boş bir `--data` klasörüyle açılan test örneğinde ilk açılışta kendiliğinden gelir.
      5. Klasör simgesi penceresi (hazır kütüphane ve öneri); arkada renkli simgeli klasörler görünürse daha iyi.
      6. Bir widget'a sağ tık → **Görünüm** menüsü açıkken, farklı arka planlarda (cam/koyu/açık) not, yapılacaklar ve kısayol kutusu.
    - Biçim: PNG, yatay, **en az 1366×768** (1920×1080 önerilir), en fazla 50 MB. Pencere kırpması 1366×768'den küçük kalacağı için tam ekran görüntüsü al.
    - Önemli içeriği üst üçte ikiye koy (alt üçte birin üstüne yazı binebilir); görüntülere ek logo ya da reklam yazısı ekleme.
    - Gerçek masaüstünü değil test örneğini kullan: `Duzenleme.exe --desktop <geçici klasör> --data <geçici klasör>`, klasöre örnek dosyalar koy (PDF, Resimler…). Kişisel dosya adı, e-posta ya da API anahtarı görünmesin; sade bir duvar kâğıdı seç. İşin bitince örneği `--exit` ile kapat.
    - Test örneği gerçek masaüstü simgelerini gizlemez: 1. görüntüde Windows simgeleri arkada görünmesin diye çekimden önce masaüstüne sağ tık → **Görünüm** → **Masaüstü simgelerini göster** işaretini kaldır, sonra geri aç.
12. **Submission options.** **Notes for certification** ve **Restricted capabilities** (runFullTrust) kutularına yukarıdaki İngilizce metinleri yapıştır. **Publishing hold options** varsayılan (sertifikasyondan geçince yayımlanır) ya da elle yayımlamak için **Don't publish this submission until I select Publish now**. **E-posta adresini doğrula:** Microsoft, kritik e-posta bildirimlerini alabilmen için adresin Action Center'da doğrulanmasını zorunlu tutuyor: Partner Center → **Action Center** → **My Preferences**. (Hesap sahibi bildirimleri her zaman alır; **Submission notification audience** ile başka kişi eklemek isteğe bağlı.)
13. **Gönder.** Genel bakış sayfasında **Submit for certification**. Durum sertifikasyon aşamasında görünür; sonuç Partner Center'daki Action Center'a ve (12. adımda doğruladığın) e-posta adresine gelir. Reddedilirse sertifikasyon raporundaki madde düzeltilip yeni gönderim yapılır.
14. **Yayından sonra.** Store bağlantısını (`https://apps.microsoft.com/detail/<Store ID>`) README'ye ekle. Sonraki sürümlerde yeni gönderim oluştur, **What's new in this version**'ı doldur ve yalnızca yeni `.msixbundle`'ı yükle.
