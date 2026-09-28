# Microsoft Store listesi — NestDesk 2.1.0

Partner Center'a kopyalanacak hazır metinler. Her alan ayrı bir kod bloğunda; GitHub'da bloğun sağ üstündeki kopyala düğmesiyle al. Açıklama alanları düz metindir: içine HTML ya da bağlantı koyma (Microsoft'un önerisi; bağlantılar kendi alanlarına girer).

Sınırlar Microsoft Learn'deki MSIX sayfalarından ("Add and edit Store listing info", "App screenshots, images, and trailers") alındı (Eylül 2026). Keywords alanı MSIX sayfalarında yalnızca "Create an app submission" sayfasının Store listings tablosunda isteğe bağlı alan olarak geçiyor, sınırı yazmıyor; anahtar kelime sınırı Microsoft Learn'ün "Store listing for titles on XBOX devices" sayfasındaki (oyun yayımlama rehberi, Additional information tablosu) değerdir:

| Alan (Partner Center) | Sınır | Bu dosyada (TR / EN) |
|---|---|---|
| Description | zorunlu, en fazla 10.000 karakter | ~5.100 / ~5.150 karakter |
| Short description | en fazla 1.000; bazı görünümlerde yalnızca ilk 270 karakter görünür | < 270 |
| Product features | en fazla 20 madde, her biri en fazla 200 karakter; madde işareti koyma | 20 madde, en uzunu < 120 karakter |
| Keywords | en fazla 7 terim, tüm terimlerde toplam en fazla 21 kelime; terim başına en fazla 40 karakter (Partner Center daha kısa bir sınır gösterirse o geçerli) | 7 terim, 14 kelime, en uzunu 20 karakter |
| What's new in this version | en fazla 1.500 karakter; **ilk gönderimde boş bırak** | metin hazır (< 1.200 karakter); 2.1.0 ilk gönderimse alan boş kalır |
| Screenshot caption | en fazla 200 karakter | 8 açıklama (baştaki "1." numarasını yapıştırma) |

**Terimler (İngilizce):** "bölme" = **panel**. İngilizce metinlerde "fence" ya da "Fences" yazma (Stardock'un markası); başka ürün adları da anma. "Kısayol kutusu" = **shortcut box**, "Otomatik taşıma" = **Auto-move**, "Windows masaüstüne göz at" = **Peek at the Windows desktop**.

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
| Diller | Paket `en-US` (varsayılan) ve `tr-TR` bildirir; uygulamanın arayüzü de İngilizce ve Türkçedir (Windows'un diline uyar, Ayarlar → Genel → Dil / Language) |

Depo adresi şimdilik `hasakobey/duzenleme` kalır (uygulamanın eski adı; depo yeniden adlandırılınca bu bağlantılar ve `Core/AppInfo.cs` → `RepoUrl` birlikte güncellenir).

**Kategori gerekçesi:** Microsoft'un tablosunda *Productivity* "bir işi daha verimli bitirmeye yardım eden uygulamalar" (örnekler: not alma, verileri görüntüleme ve sıralama) olarak tanımlanıyor; NestDesk'in asıl işi masaüstünü düzenli tutmak ve dosyaları yerine koymak, not, yapılacaklar, zamanlayıcı ve Pomodoro widget'ları da bu tanıma giriyor. *Utilities + tools* (örnek: dosya yöneticisi) ikincil kategori olarak dosya taşıma, bölme, sistem durumu ve Geri Dönüşüm Kutusu tarafını kapsar. Productivity'nin alt kategorisi yok. *Personalization* (tema, duvar kâğıdı) widget'lara ve klasör simgelerine uyardı ama uygulamanın ana değerini anlatmıyor.

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
Masaüstünü bölmelere ayırır, masaüstüne düşen dosyaları senin klasörlerine taşır (yalnızca sen istersen, her taşıma geri alınır) ve saat, takvim, not, yapılacaklar, zamanlayıcı gibi widget'lar ekler. Türkçe ve İngilizce; ücretsiz, reklamsız.
```

### Açıklama (Description)

```text
NestDesk, Windows masaüstünü derli toplu tutan ücretsiz ve açık kaynaklı bir düzenleyicidir. Masaüstünü bölmelere ayırır, istersen masaüstüne düşen dosyaları kendi oluşturduğun klasörlere taşır ve duvar kâğıdının üzerine saat, takvim, not, yapılacaklar, zamanlayıcı gibi widget'lar ekler. Masaüstün için derli toplu bir yuva.

İLK AÇILIŞ
• Kısa bir karşılama masaüstünü seninle birlikte kurar: bölmeler, dosya taşıma ve araçlar. İstersen atlayabilirsin.
• Sen onay vermeden hiçbir dosya taşınmaz: otomatik taşıma sen açana kadar kapalıdır. Karşılama, masaüstündeki dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir.

BÖLMELER
• Masaüstündeki klasörleri, kısayolları ve dosyaları ayrı bölmelerde topla ya da bir bölmede herhangi bir klasörü göster: İndirilenler, Belgeler, Resimler ya da seçtiğin başka bir klasör (masaüstünde olması gerekmez).
• Bölmede yazdıkça ara (alt klasörler dahil), dosyaları bölmeden bölmeye sürükle, resim, video ve PDF önizlemelerini gör; ada, türe, tarihe, boyuta göre ya da elle sırala.
• Klavyeyle kullan: Enter açar, F2 yeniden adlandırır, Delete Geri Dönüşüm Kutusu'na gönderir, Ctrl+F arar.
• Windows masaüstü simgeleri için üç seçenek: hepsi masaüstünde görünsün, kutulara eklediklerin masaüstünden kalksın ya da her şey yalnızca bölmelerde görünsün. "Masaüstümü bölmelere ayır" hepsini tek tıkla kurar; geri alınabilir.

WINDOWS MASAÜSTÜNE GÖZ AT
• Ctrl+Alt+G'ye bas: Windows'un kendi masaüstü simgeleri görünür, widget'lar kenara çekilir. Ekranın üstündeki küçük çubuk kalan süreyi gösterir; "NestDesk'e dön" ile ya da birkaç dakika sonra kendiliğinden geri dönülür. Hiçbir dosya değişmez.
• Boş masaüstüne çift tıklayınca ne olacağını sen seçersin: gizle/göster, göz at ya da hiçbir şey.

KISAYOL KUTUSU
• Uygulamalar, dosyalar ve klasörler için sekmeli raf: tek tıkla aç, istersen yönetici olarak çalıştır. Her öğeye kendi adını ve simgesini ver; dosyanın kendisi değişmez.
• İstersen kutuya eklediğin masaüstü öğesi masaüstünden kalkar: kullanıcı klasöründeki görünür "NestDesk" klasörüne taşınır ve kutuda durmaya devam eder. Bu yalnızca sen bu seçeneği açarsan olur; "Masaüstüne geri koy" ile tek tıkla geri döner.

WIDGET'LAR
• Saat (12 ya da 24 saat), tarih ve aylık takvim
• Dünya saati: başka şehirlerde saat kaç, kaç saat fark var
• Geri sayım: tatile, doğum gününe kaç gün kaldı (her yıl yinelenebilir)
• Zamanlayıcı, Pomodoro ve kronometre; süre dolunca bildirim
• Yapışkan not (altı renk, Ctrl+tekerlekle yazı boyutu) ve onay kutulu yapılacaklar listesi
• Sistem durumu: işlemci, bellek, disk ve pil
• Geri Dönüşüm Kutusu: kaç öğe var, ne kadar yer tutuyor; boşaltmadan önce sorar

AD, SİMGE VE GÖRÜNÜM
• Eklerken adını yaz, başlıktaki simgeye tıklayıp 64 simgeden birini seç. F2 ile istediğin an yeniden adlandır.
• Sağ tık menüsü açık kalır: seçenekleri art arda değiştir; arka planı, vurgu rengini, köşeleri ve saydamlığı üzerine gelerek önizle.
• Cam, koyu ya da açık arka plan; vurgu rengi, ölçek ve saydamlık. Widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır, üst üste binmez; düzenlerini adıyla kaydedip tek tıkla geri dön.
• Yeni widget'ın nereye geleceğini sen seçersin: imlecin yanına, ekranın ortasına ya da köşeye. Yeni widget birkaç saniye vurgulu durur.
• Her ekran ölçeğinde keskin yazı ve simgeler; birden çok monitör desteklenir, widget'lar ekrana sığar.

OTOMATİK TAŞIMA (isteğe bağlı)
• Masaüstünde "PDF" adında bir klasörün varsa masaüstüne düşen PDF'ler oraya gider. Resimler, belgeler, arşivler, videolar ve müzik için hazır kurallar var; klasör adlarını ve uzantıları değiştirebilirsin.
• Klasör masaüstünde yoksa dosyaya dokunulmaz. Kısayollar, gizli ve sistem dosyaları ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz.
• Her taşıma listelenir ve tek tıkla geri alınır; geri aldığın dosya bir daha kendiliğinden taşınmaz.

KLASÖR SİMGELERİ
• 30 sembol ve 10 renkten oluşan hazır kütüphane, klasör adına göre öneri; kendi .ico, .png ya da .svg dosyanı da kullanabilirsin. Verdiğin simgelerin hepsini tek düğmeyle kaldırabilirsin.
• İsteğe bağlı üretken yapay zekâ: kendi Anthropic API anahtarını girersen Anthropic'in Claude modeli yazdığın kısa açıklamaya göre bir klasör simgesi çizer. Zorunlu değildir; kullanım ücretini Anthropic senin hesabına yansıtır. Uygunsuz bir sonucu uygulamanın içinden bildirebilirsin.

GİZLİLİK
Hesap, reklam ve telemetri yok. Ayarların, notların ve geçmişin yalnızca bilgisayarında kalır. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir: yalnızca sen istediğinde klasörün adını ve yazdığın açıklamayı Anthropic'e gönderir (ayrıntılar gizlilik politikasında).

KLAVYE KISAYOLLARI (değiştirilebilir)
Ctrl+Alt+B widget ekle · Ctrl+Alt+G Windows masaüstüne göz at · Ctrl+Alt+H masaüstünü gizle/göster · Ctrl+Alt+N yeni not · Ctrl+Alt+O şimdi düzenle · Ctrl+Alt+D NestDesk'i aç · Ctrl+Alt+W widget'ları öne getir

Arayüz Türkçe ve İngilizcedir: Windows'un diline uyar, Ayarlar'dan değiştirilebilir. NestDesk bildirim alanında (sistem tepsisi) çalışır; ana pencereyi kapatmak uygulamayı kapatmaz. Açık kaynaklıdır (MIT lisansı).
```

### Ürün özellikleri (Product features) — her satır ayrı bir kutuya

```text
Masaüstünü bölmelere ayır: klasörler, kısayollar, dosyalar ya da herhangi bir klasörün içi (İndirilenler, Belgeler…)
Windows masaüstü simgeleri için üç seçenek: hepsi görünsün, kutuya eklenenler kalksın ya da yalnızca bölmelerde
Windows masaüstüne göz at (Ctrl+Alt+G): simgeler görünür, widget'lar çekilir, sonra kendiliğinden geri dönülür
Bölmede anında arama, sürükle-bırak, resim/video/PDF önizlemesi ve sıralama; Enter, F2, Delete ve Ctrl+F
Sekmeli kısayol kutusu: uygulama, dosya ve klasörleri tek tıkla aç; her öğeye kendi adını ve simgesini ver
Saat, tarih, aylık takvim, dünya saati ve geri sayım widget'ları
Zamanlayıcı, Pomodoro ve kronometre; süre dolunca bildirim
Altı renkli yapışkan notlar ve onay kutulu yapılacaklar listesi
Sistem durumu (işlemci, bellek, disk, pil) ve onaylı boşaltmalı Geri Dönüşüm Kutusu widget'ları
Eklerken ad ve simge seç; F2 ile widget'ı, bölmedeki dosyayı ya da kutudaki öğeyi yerinde yeniden adlandır
Açık kalan sağ tık menüleri; arka plan, renk, köşe ve saydamlık üzerine gelince önizlenir
Mıknatıslı yerleşim, çakışma engeli ve kayıtlı düzenler; yeni widget'ın yerini sen seç
Cam, koyu ve açık arka plan; vurgu rengi, ölçek ve saydamlık ayarı
Her ekran ölçeğinde keskin yazı ve simgeler; birden çok monitör desteği
İsteğe bağlı otomatik taşıma: masaüstüne düşen dosyalar senin klasörlerine gider; her taşıma geri alınır
Kısayollar, sistem dosyaları ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz
300 hazır klasör simgesi (30 sembol × 10 renk), ada göre öneri ya da kendi .ico/.png/.svg dosyan
İsteğe bağlı üretken yapay zekâ: kendi Anthropic API anahtarınla Claude'a klasör simgesi çizdir
Türkçe ve İngilizce arayüz; değiştirilebilir genel klavye kısayolları
Hesap, reklam ve telemetri yok; verilerin bilgisayarında kalır. Ücretsiz ve açık kaynak (MIT)
```

### Anahtar kelimeler (Keywords)

```text
masaüstü düzenleyici
masaüstü bölmeleri
dosya düzenleme
masaüstü widget
yapışkan not
pomodoro zamanlayıcı
klasör simgesi
```

### Bu sürümdeki yenilikler (What's new in this version) — 2.1.0

2.1.0 NestDesk'in Store'daki **ilk** gönderimiyse bu alan **boş bırakılır** (Microsoft ilk gönderimde boş bırakmanı istiyor); aşağıdaki metin GitHub sürüm notu olarak kullanılır. Store'da daha önce yayımlanmış bir sürüm varsa metni olduğu gibi yapıştır. Sonraki sürümlerde (ör. 2.1.1) buraya o sürümün yenilikleri aynı biçimde (`• ` ile başlayan satırlar, en fazla 1.500 karakter) yazılır.

```text
• Daha hızlı ve akıcı: ayarlar arka planda kaydedilir, bölmeler yalnızca değişeni günceller; yavaş bilgisayarda ve ağ klasörlerinde donma yok.
• Her ekran ölçeğinde keskin yazı ve simgeler; widget'lar ekrana sığar.
• İngilizce arayüz: NestDesk artık Türkçe ve İngilizce (Ayarlar > Genel > Dil).
• Sağ tık menüsü açık kalır, değişiklik hemen görünür; arka planı, rengi, köşeleri ve saydamlığı üzerine gelerek önizle.
• Eklerken ad ve simge seç; F2 ile widget'ı, bölmedeki dosyayı ya da kutudaki öğeyi yeniden adlandır.
• Windows masaüstüne göz at (Ctrl+Alt+G): Windows simgeleri görünür, birkaç dakika sonra kendiliğinden geri dönülür.
• Yeni widget'ların yeri seçilebilir: imlecin yanı, ekranın ortası ya da köşe.
• Yeni seçenek: kısayol kutusuna eklediğin masaüstü öğeleri masaüstünden kalkar, görünür NestDesk klasöründe durur; tek tıkla geri konur.
• Yeni widget'lar: aylık takvim, geri sayım, zamanlayıcı/Pomodoro/kronometre, dünya saati, sistem durumu, Geri Dönüşüm Kutusu ve herhangi bir klasörü gösteren bölme.
• Program dosyası NestDesk.exe, ayar klasörü %AppData%\NestDesk oldu; güncellemede her şey kendiliğinden taşınır.
```

### Ekran görüntüsü açıklamaları (caption)

```text
1. Bölmelere ayrılmış, derli toplu bir masaüstü: klasörler, kısayollar, dosyalar ve İndirilenler bölmeleri; saat, takvim ve yapılacaklar
2. Ctrl+Alt+B ile "Widget ekle": bölmeler, araçlar, saat ve bilgi widget'ları tek tıkla masaüstünde
3. Sağ tık menüsü açık kalır: arka planı, vurgu rengini ve saydamlığı üzerine gelerek önizle
4. Eklerken ad ve simge seç; F2 ile istediğin an yeniden adlandır
5. Zamanlayıcı, Pomodoro, dünya saati, geri sayım, sistem durumu ve Geri Dönüşüm Kutusu widget'ları
6. Ctrl+Alt+G ile Windows masaüstüne göz at: simgeler görünür, widget'lar çekilir, sonra kendiliğinden dönülür
7. Kısayol kutusu: istersen kutuya eklediğin masaüstü öğeleri masaüstünden kalkar ve NestDesk klasöründe durur
8. İsteğe bağlı otomatik taşıma: hangi dosyanın nereye gideceğini sen seçersin; her taşıma tek tıkla geri alınır
```

---

## English (en-US)

Uygulamanın arayüzü artık İngilizce de olduğu için İngilizce listede "arayüz Türkçedir" uyarısı yok. İngilizce listenin ekran görüntüleri İngilizce arayüzle çekilir (`NESTDESK_LANG=en`, aşağıdaki kontrol listesinin 11. adımı).

### Product name

```text
NestDesk
```

### Short description

```text
Splits your desktop into panels, moves files dropped on it into your own folders (only if you want, and every move can be undone) and adds widgets such as clock, calendar, notes, to-do lists and timers. English and Turkish; free, no ads.
```

### Description

```text
NestDesk is a free, open source organizer that keeps your Windows desktop tidy. It splits your desktop into panels, can move files that land on the desktop into folders you created, and puts widgets such as a clock, calendar, notes, to-do lists and timers right on your wallpaper. A tidy home for your desktop.

FIRST LAUNCH
• A short welcome sets up your desktop with you: panels, moving files and tools. You can skip it.
• No file is moved before you agree: auto-move stays off until you turn it on. The welcome shows in advance which of the files on your desktop would go to which folder.

PANELS
• Gather the folders, shortcuts and files on your desktop into separate panels, or show any folder in a panel: Downloads, Documents, Pictures or another folder you pick (it doesn't have to be on the desktop).
• Search a panel as you type (including subfolders), drag files between panels, preview pictures, videos and PDFs, and sort by name, type, date, size or by hand.
• Use the keyboard: Enter opens, F2 renames, Delete sends to the Recycle Bin, Ctrl+F searches.
• Three choices for the Windows desktop icons: show everything on the desktop, let items you add to boxes leave the desktop, or show everything only in panels. "Sort my desktop into panels" sets it all up with one click and can be undone.

PEEK AT THE WINDOWS DESKTOP
• Press Ctrl+Alt+G: the Windows desktop icons appear and your widgets step aside. A small bar at the top of the screen shows the time left; select "Back to NestDesk" or wait a few minutes to return automatically. No file is changed.
• You decide what a double-click on the empty desktop does: hide/show, peek, or nothing.

SHORTCUT BOX
• A tabbed shelf for apps, files and folders: open them with one click, or run an app as administrator. Give each item its own name and icon; the file itself is not changed.
• Optionally, a desktop item you add to a box leaves the desktop: it is moved into a visible "NestDesk" folder in your user folder and stays in the box. This happens only if you turn the option on, and "Put back on the desktop" returns it with one click.

WIDGETS
• Clock (12- or 24-hour), date and monthly calendar
• World clock: the time in other cities and how far ahead or behind they are
• Countdown: days left until a vacation or birthday (can repeat every year)
• Timer, Pomodoro and stopwatch, with a notification when time is up
• Sticky notes (six colors, Ctrl+wheel for text size) and to-do checklists
• System status: processor, memory, disk and battery
• Recycle Bin: how many items and how much space; asks before emptying

NAME, ICON AND LOOK
• Type a name as you add a panel, box or note, and click the icon in its title to pick one of 64 icons. Press F2 to rename at any time.
• Right-click menus stay open, so you can change several options in a row; hover over background, accent color, corners or transparency to preview them.
• Glass, dark or light background, accent color, scale and transparency. Widgets snap to each other and to screen edges like magnets and never overlap; save named layouts and switch back with one click.
• Choose where new widgets appear: next to the pointer, in the center of the screen or in a corner. A new widget stays highlighted for a few seconds.
• Crisp text and icons at any display scaling; multiple monitors are supported and widgets always fit on the screen.

AUTO-MOVE (optional)
• If your desktop has a folder named "PDF", PDFs saved to the desktop go there. Ready-made rules cover pictures, documents, archives, videos and music; change folder names and extensions as you like.
• If the target folder doesn't exist on the desktop, the file is left alone. Shortcuts, hidden and system files and downloads in progress are never moved, and no file is ever overwritten.
• Every move is listed and can be undone with one click; a file you moved back is never moved automatically again.

FOLDER ICONS
• A library of 30 symbols in 10 colors with suggestions based on the folder name; you can also use your own .ico, .png or .svg files. Remove every icon you gave with a single button.
• Optional generative AI: if you enter your own Anthropic API key, Anthropic's Claude model draws a folder icon from a short description you type. This is not required, and Anthropic bills the usage to your own account. You can report an inappropriate result from inside the app.

PRIVACY
No account, no ads, no telemetry. Your settings, notes and history stay on your PC. The only feature that uses the internet is the optional AI folder icon: only when you ask for one, it sends the folder name and your description to Anthropic (details in the privacy policy).

KEYBOARD SHORTCUTS (customizable)
Ctrl+Alt+B add widget · Ctrl+Alt+G peek at the Windows desktop · Ctrl+Alt+H hide/show desktop · Ctrl+Alt+N new note · Ctrl+Alt+O tidy up now · Ctrl+Alt+D open NestDesk · Ctrl+Alt+W bring widgets to front

The app is available in English and Turkish: it follows your Windows display language, and you can change it in Settings. NestDesk runs in the notification area (system tray); closing the main window keeps it running. It is open source under the MIT License.
```

### Product features — one line per box

```text
Split your desktop into panels: folders, shortcuts, files or the contents of any folder (Downloads, Documents…)
Three choices for Windows desktop icons: show all, let items added to boxes leave the desktop, or show only in panels
Peek at the Windows desktop (Ctrl+Alt+G): icons appear, widgets step aside, and NestDesk comes back on its own
Instant search, drag and drop, picture/video/PDF previews and sorting in panels; Enter, F2, Delete and Ctrl+F
Tabbed shortcut box: open apps, files and folders with one click; give each item its own name and icon
Clock, date, monthly calendar, world clock and countdown widgets
Timer, Pomodoro and stopwatch with a notification when time is up
Sticky notes in six colors and to-do checklists
System status (processor, memory, disk, battery) and Recycle Bin widgets; emptying always asks first
Choose a name and icon as you add; press F2 to rename a widget, a file in a panel or a box item in place
Right-click menus stay open; hover to preview background, color, corners and transparency
Magnetic snapping, no overlapping and saved layouts; choose where new widgets appear
Glass, dark and light backgrounds with accent colors, scale and transparency
Crisp text and icons at any display scaling; multiple monitor support
Optional auto-move: files dropped on the desktop go to your folders, and every move can be undone
Never moves shortcuts, system files or downloads in progress, and never overwrites a file
300 ready-made folder icons (30 symbols × 10 colors), suggestions by folder name, or your own .ico/.png/.svg
Optional generative AI: let Claude draw a folder icon with your own Anthropic API key
English and Turkish interface; customizable global keyboard shortcuts
No account, no ads, no telemetry; your data stays on your PC. Free and open source (MIT)
```

### Keywords

```text
desktop organizer
desktop panels
file organizer
desktop widgets
sticky notes
pomodoro timer
folder icons
```

### What's new in this version — 2.1.0

Leave blank if 2.1.0 is NestDesk's first Store submission (see the Turkish note above); otherwise paste:

```text
• Faster and smoother: settings are saved in the background and panels update only what changed, so slow PCs and network folders no longer freeze the app.
• Crisp text and icons at any display scaling; widgets always fit on the screen.
• English interface: NestDesk now speaks English and Turkish (Settings > General > Language).
• Right-click menus stay open and show changes right away; hover to preview background, color, corners and transparency.
• Choose a name and an icon as you add; press F2 to rename a widget, a file in a panel or a box item.
• Peek at the Windows desktop (Ctrl+Alt+G): the Windows icons appear, and NestDesk comes back on its own after a few minutes.
• Choose where new widgets appear: next to the pointer, in the center of the screen or in a corner.
• New option: desktop items you add to a shortcut box leave the desktop and wait in a visible NestDesk folder; put them back with one click.
• New widgets: monthly calendar, countdown, timer/Pomodoro/stopwatch, world clock, system status, Recycle Bin, and panels that show any folder.
• The program file is now NestDesk.exe and the settings folder %AppData%\NestDesk; updating moves everything automatically.
```

### Screenshot captions

```text
1. A tidy desktop split into panels for folders, shortcuts, files and Downloads, with clock, calendar and to-do widgets
2. Add widget (Ctrl+Alt+B): panels, tools, clocks and info widgets, each one click away
3. Menus stay open while you try options: hover to preview the background, accent color and transparency
4. Choose a name and an icon as you add a panel; press F2 to rename it anytime
5. Timer, Pomodoro, world clock, countdown, system status and Recycle Bin widgets
6. Ctrl+Alt+G peeks at the Windows desktop: icons appear, widgets step aside, and NestDesk comes back on its own
7. Shortcut box: optionally, desktop items you add leave the desktop and wait in a NestDesk folder
8. Optional auto-move: you choose which files go to which folder, and every move can be undone with one click
```

---

## Ekran görüntüleri: ne çekilecek (iki dil için aynı sahne)

Her görüntü iki kez çekilir: Türkçe liste için `NESTDESK_LANG=tr`, İngilizce liste için `NESTDESK_LANG=en` ile açılan test örneğinde. Sıra yukarıdaki açıklamalarla aynıdır.

1. **Vitrin.** Masaüstü bölmelere ayrılmış: Klasörler (içinde renkli simgeli birkaç klasör), Kısayollar, Dosyalar ve İndirilenler (klasör portalı) bölmeleri; sağ üstte saat, altında aylık takvim, yanında 3–4 maddeli bir yapılacaklar listesi (bir maddesi işaretli). Sade bir duvar kâğıdı.
2. **"Widget ekle" penceresi** (Ctrl+Alt+B) açık: Bölmeler, Araçlar, Saat ve bilgi bölümleri ve "Masaüstümü bölmelere ayır" düğmesi görünsün; arkada birkaç widget.
3. **Canlı menü.** Bir bölmeye ya da nota sağ tık → Görünüm ▸ Arka plan açık, fare "Açık"ın üzerinde; widget önizlemeyi (açık zemin) gösteriyor. Menü ve widget yan yana görünsün.
4. **Ad ve simge.** "Widget ekle"den yeni eklenen bir bölmenin başlığı düzenleme hâlinde (ör. "Projeler" yazılmış) ve başlıktaki simgeye tıklanınca açılan simge seçici görünür.
5. **Yeni widget'lar.** Zamanlayıcı (çalışıyor, ör. 12:34), Pomodoro (Odak · 2/4), dünya saati (3 şehir), geri sayım ("Tatil: 12 gün kaldı"), sistem durumu ve Geri Dönüşüm Kutusu widget'ları düzenli yerleştirilmiş.
6. **Windows masaüstüne göz at.** Ctrl+Alt+G sonrası: widget'lar çekilmiş, Windows'un masaüstü simgeleri görünür, ekranın üst ortasında "Windows masaüstü · 1:42 · +5 dk · NestDesk'e dön" çubuğu. Bu görüntüde Windows'un **gerçek** masaüstü simgeleri görünür (test örneği onlara dokunmaz, yalnızca widget'ları çeker): kişisel dosya adları görünmesin diye masaüstünde yalnızca örnek dosyaların bulunduğu ayrı bir Windows hesabında ya da sanal makinede çek.
7. **Kısayol kutusu ve kutu seçeneği.** Solda birkaç uygulama ve dosya taşıyan sekmeli bir kısayol kutusu (bir öğe kendi adı ve simgesiyle), sağda ana pencerede Ayarlar → Masaüstü → "Windows masaüstü simgeleri" = "Kutulara eklediklerim masaüstünden kalksın", altında "N öğe NestDesk klasöründe · Klasörü aç · Hepsini masaüstüne geri koy".
8. **Otomatik taşıma** sayfası: kurallar ("Hangi dosya nereye gitsin?") ve birkaç taşıma, "Geri al" düğmeleriyle; üstte anahtar açık.

---

## Kısıtlı yetenek gerekçesi: runFullTrust (Submission options → Restricted capabilities)

İngilizce yaz; kutu kısaysa aşağıdaki kısa sürümü kullan.

```text
NestDesk is a Win32 desktop application (WPF, .NET 10) packaged as MSIX with the Windows.FullTrustApplication entry point. It needs runFullTrust because its core features rely on desktop and shell APIs that are not available to app-container apps. Every change to the user's desktop or files happens only after the user turns the related option on or gives an explicit command, and all of it can be reversed.

1. Desktop icon visibility: when the user chooses "Show only in panels", peeks at the Windows desktop (Ctrl+Alt+G), double-clicks the empty desktop or presses Ctrl+Alt+H, the app finds Explorer's desktop icon view (Progman/WorkerW > SHELLDLL_DefView > SysListView32) and hides or shows it with ShowWindowAsync. No files or Windows settings are changed. The icons are shown again when the option is turned off, when a peek ends, when the app exits or crashes, and when the user signs out.
2. Desktop-attached widgets: panel, shortcut box, clock, calendar, note, timer and other widget windows are owned by the desktop window and kept at the bottom of the z-order (SetWindowPos, WM_WINDOWPOSCHANGING, WS_EX_NOACTIVATE) so they stay on the desktop and survive Win+D; the small peek bar is a window of the same kind. Per-monitor DPI APIs place them on the right screen. The optional "minimize open windows while peeking" setting (off by default) calls the shell's Shell.Application ToggleDesktop, the same as Win+D.
3. Double-click detection: raw mouse input (RegisterRawInputDevices with RIDEV_INPUTSINK on a message-only window; no hook, the pointer is never delayed) records only the time and position of left-button presses to detect a double-click on an empty area of the desktop. Nothing is logged or transmitted, and the registration is removed when the user sets the double-click action to "Do nothing" in Settings.
4. Global hotkeys: seven user-configurable shortcuts (e.g. Ctrl+Alt+G, Ctrl+Alt+B) are registered with RegisterHotKey. No keyboard hook is used.
5. Files, on the user's behalf:
   a. A FileSystemWatcher monitors the user's Desktop folder. Only after the user turns auto-move on, new files are moved into folders the user created on the desktop according to user-defined rules. Every move is journaled and can be undone.
   b. Only if the user chooses "Items I add to boxes leave the desktop", a desktop item the user adds to a shortcut box is moved into a visible folder next to the Desktop folder (%USERPROFILE%\NestDesk\<box name>), recorded, and can be put back with one click. File attributes are never changed.
   c. Panels list the Desktop, the Public Desktop and any folder the user picks (e.g. Downloads, Documents). On explicit user request they open, rename (F2), create folders and send items to the Recycle Bin using Windows' own shell file operations (SHFileOperation, IFileOperation; UAC only for Public Desktop items).
6. Shell integration: folder icons with SHGetSetFolderCustomSettings (desktop.ini plus a hidden .ico inside the folder); icons and thumbnails from SHGetFileInfo and IShellItemImageFactory (no thumbnails for online-only cloud files, so nothing is downloaded); the Windows "Change Icon" dialog (PickIconDlg); items are opened with ShellExecute, including "Run as administrator" for shortcut-box items the user added. The Recycle Bin widget reads the item count and size with SHQueryRecycleBin, watches changes with SHChangeNotifyRegister, and empties it with SHEmptyRecycleBin only after the user confirms.
7. System status widget: reads processor, memory, disk and battery values with GetSystemTimes, GlobalMemoryStatusEx, GetDiskFreeSpaceEx and GetSystemPowerStatus while the widget is visible. The values are only displayed; they are not stored or sent.
8. A notification area (tray) icon is the app's main entry point and shows move and timer notifications. The "NestDesk – Add widget" Start menu entry hands its request to the main app identity with IApplicationActivationManager.

The app does not install drivers or services and does not require administrator rights. Its only network use is optional and goes only to the Anthropic API: when the user saves their own Anthropic API key, the key is verified with one request, and when the user requests an AI-generated folder icon, the folder name and the user's description are sent.
```

Kısa sürüm:

```text
Win32 WPF (.NET 10) desktop app packaged with the Windows.FullTrustApplication entry point. runFullTrust is required to: hide/show Explorer's desktop icon view (SHELLDLL_DefView/SysListView32) on user request (panel mode, peek, double-click) and restore it on exit; keep widget windows owned by the desktop at the bottom of the z-order; detect double-clicks on the empty desktop with raw mouse input (no hook; time/position only, nothing stored or sent); register user-configurable global hotkeys (RegisterHotKey); watch the user's Desktop folder and, only after opt-in, move files into user-created folders by user rules or into a visible %USERPROFILE%\NestDesk folder for shortcut boxes (journaled, undoable); list, rename and recycle items on explicit request with shell file operations; apply folder icons with SHGetSetFolderCustomSettings; query and, after confirmation, empty the Recycle Bin; read CPU/memory/disk/battery for display only; and launch user-chosen items via ShellExecute. No drivers, services or admin rights.
```

---

## Üretken yapay zekâ bildirimi (Store 11.16)

Partner Center gönderiminde canlı üretken yapay zekâ içeriği bildirimi istenirse işaretle ve aşağıdaki metni kullan (açıklamada ayrıca yazıyor; sertifikasyon notlarında da var).

```text
NestDesk has one optional generative AI feature: folder icons drawn by Anthropic's Claude model. It works only after the user enters their own Anthropic API key (Settings > Advanced > "AI folder icons (optional)") and presses Generate; without a key the feature is disabled and the app makes no network requests. The request contains the folder name, the user's short description and a fixed drawing instruction that asks for simple shapes without text. The result is an SVG drawing that is sanitized (scripts and external links removed) before it is shown, and it is kept only on the user's PC. Users can report inappropriate output with the "Report inappropriate content" link in the folder icon window; it opens a pre-filled GitHub issue form in the browser (containing only the app version) and sends nothing by itself.
```

```text
NestDesk'te isteğe bağlı tek bir üretken yapay zekâ özelliği vardır: Anthropic'in Claude modelinin çizdiği klasör simgeleri. Yalnızca kullanıcı kendi Anthropic API anahtarını girip (Ayarlar > Gelişmiş > Yapay zekâ ile klasör simgesi) "Üret"e bastığında çalışır; anahtar yoksa özellik kapalıdır ve uygulama internete bağlanmaz. İstekte klasörün adı, kullanıcının kısa açıklaması ve yazısız, basit şekiller isteyen sabit bir çizim talimatı bulunur. Gelen SVG çizimi gösterilmeden önce temizlenir (betikler ve dış bağlantılar atılır) ve yalnızca kullanıcının bilgisayarında kalır. Uygunsuz bir sonuç, klasör simgesi penceresindeki "Uygunsuz içeriği bildir" bağlantısıyla bildirilir: tarayıcıda önceden doldurulmuş bir GitHub formu açılır (içinde yalnızca uygulamanın sürümü vardır), kendiliğinden hiçbir şey gönderilmez.
```

## Gizlilik özeti (listeye ya da Partner Center sorularına)

```text
NestDesk does not collect personal data and has no server, accounts, ads or telemetry. Settings, notes, move history and box records are stored only on the user's PC (%AppData%\NestDesk, or the package's own folder in the Store edition). Files are moved only after the user turns auto-move or the shortcut-box option on, every move is recorded and can be undone, and the Recycle Bin is emptied only after confirmation. System status values are only displayed. The only network use is the optional AI folder icon, which sends the folder name and the user's description to Anthropic with the user's own API key. Full policy: https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md
```

```text
NestDesk kişisel veri toplamaz; sunucusu, hesabı, reklamı ve telemetrisi yoktur. Ayarlar, notlar, taşıma geçmişi ve kutu kayıtları yalnızca kullanıcının bilgisayarında tutulur (%AppData%\NestDesk; Store sürümünde paketin kendi klasörü). Dosyalar yalnızca kullanıcı otomatik taşımayı ya da kısayol kutusu seçeneğini açtıktan sonra taşınır; her taşıma kaydedilir ve geri alınabilir; Geri Dönüşüm Kutusu yalnızca onaydan sonra boşaltılır. Sistem durumu değerleri yalnızca gösterilir. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir: kullanıcının kendi API anahtarıyla klasörün adını ve kullanıcının açıklamasını Anthropic'e gönderir. Politikanın tamamı: https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md
```

---

## Yaş derecelendirmesi (Age ratings) — IARC anketi için yanıt önerileri

Anket soruları IARC tarafından zaman zaman değiştirilir; aşağıdakiler yanıtların **dayanağıdır**, soru metinleri formda farklı olabilir. Her soruyu uygulamanın gerçekte yaptığına göre yanıtla.

- **İlk soru (uygulama türü):** oyun olmayan, yardımcı program / üretkenlik türünü seç (formda "Utility", "Productivity" ya da "Other" gibi geçer).
- **Şiddet, korku, cinsellik/çıplaklık, küfür, uyuşturucu/alkol/tütün, kumar, ayrımcı içerik:** Hayır. Uygulamada böyle bir içerik yok.
- **Kullanıcılar birbiriyle etkileşiyor ya da içerik paylaşıyor mu (sohbet, mesaj, paylaşım):** Hayır. Uygulamada kullanıcılar arası hiçbir iletişim yok.
- **Kişisel bilgiler üçüncü kişilerle paylaşılıyor mu:** Hayır. Uygulama kişisel bilgi toplamaz. (İsteğe bağlı yapay zekâ özelliği, kullanıcı istediğinde klasörün adını ve kullanıcının yazdığı açıklamayı kullanıcının kendi Anthropic API anahtarıyla Anthropic'e gönderir; ayrıntılar gizlilik politikasının 5. bölümünde.)
- **Kullanıcının konumu paylaşılıyor mu:** Hayır. (Dünya saati şehirleri kullanıcı seçer; konum okunmaz.)
- **Dijital ürün satın alma / uygulama içi satın alma:** Hayır. (Anthropic API ücreti kullanıcının Anthropic ile kendi hesabıdır; uygulama bir şey satmaz.)
- **Reklam:** Hayır.
- **Sınırsız internet erişimi (web tarayıcı, arama motoru):** Hayır. Uygulama web içeriği göstermez; internet yalnızca isteğe bağlı yapay zekâ simgesi için ve yalnızca Anthropic API'sine kullanılır.
- **Üretken yapay zekâ ile ilgili bir soru çıkarsa:** Evet, isteğe bağlı — kullanıcı kendi API anahtarını girerse metin açıklamasından yazı içermeyen, basit şekillerden oluşan bir klasör simgesi (SVG) üretilir; çıktı yalnızca o kullanıcının bilgisayarında kalır, kimseyle paylaşılmaz; uygulama içinde bildirme bağlantısı vardır.

Beklenen sonuç en düşük yaş derecesidir (ör. PEGI 3 / ESRB Everyone); formu doldurunca **Save and generate** ile derecelendirmeleri gör.

Partner Center'daki gizlilik sorusu (**Properties** → uygulama kişisel bilgiye erişiyor, topluyor ya da iletiyor mu?): uygulama kişisel veri toplamasa da kullanıcının yazdığı metni Anthropic'e iletebildiği ve `runFullTrust` bildirdiği için **Yes** seç ve gizlilik politikası URL'sini gir. Microsoft da bu durumda URL'yi zaten isteyebiliyor.

---

## Sertifikasyon test notları (Submission options → Notes for certification)

Etiketler İngilizce arayüzdeki adlardır (sertifikasyon bilgisayarı çoğunlukla İngilizcedir). Yerelleştirme bittiğinde metni `NESTDESK_LANG=en` ile açılan arayüzle karşılaştır; bir etiket farklıysa burada düzelt.

```text
Thank you for testing NestDesk. No account or sign-in is needed, and the app does not require administrator rights. The interface follows the Windows display language: English on non-Turkish Windows (Turkish can be chosen in Settings > General > Language). The labels below are the English ones.

GETTING STARTED
1. On first launch the app adds an icon to the notification area (system tray) and opens a short welcome with 3 steps (panels, moving files, tools). Go through it with "Next" and "Done", or press "Skip for now": skipping changes nothing on the desktop and opens the main window.
2. No file is moved before the user agrees. Auto-move stays OFF unless the user picks "Yes, move my files into place" in step 2 of the welcome or turns it on later on the "Auto-move" page. Files are only moved into matching folders that already exist on the desktop (default rule folders: PDF, Pictures, Documents, Archives, Videos, Music). On a clean test machine nothing is moved.
3. Closing the main window keeps the app running in the tray. Left-click the tray icon to reopen it. To quit, right-click the tray icon > "Exit".

ADD WIDGETS, NAME AND ICON
4. Press Ctrl+Alt+B, right-click the tray icon > "Add widget…", or start "NestDesk – Add widget" from the Start menu. Click a tile, e.g. "Folders", "Files", "Clock", "Calendar", "Note", "To-do", "Shortcut box", "Timer". The new widget appears next to the mouse pointer and is highlighted for a few seconds ("Where new widgets appear" in Settings > Desktop can change this to the screen center or a corner).
5. A panel, shortcut box or note added from this window starts with its title in edit mode: type a name and press Enter. Click the icon in its title bar to choose an icon. The "New panel…" tile asks for the name, what to show and the icon in one window.
6. F2 renames: click a widget's title and press F2. In a panel, select a file you created for the test and press F2 to rename it on disk; in a shortcut box, F2 changes only the displayed name.
7. To remove a widget, click the × in its top-right corner or right-click it > "Remove". The notice's "Undo" or the tray menu's "Bring back" item restores it.

LIVE MENUS
8. Right-click a widget > "Appearance" > "Background": hover over "Glass", "Dark" and "Light" to preview them, click one to apply. Option menus stay open so several options can be changed in a row; Esc or a click outside closes the menu.

PEEK AT THE WINDOWS DESKTOP
9. Press Ctrl+Alt+G (or tray > "Peek at the Windows desktop", or right-click a widget > "Peek at the Windows desktop"). The Windows desktop icons become visible, widgets step aside, and a small bar at the top center shows the time left with "+5 min" and "Back to NestDesk". It returns automatically after 2 minutes. Nothing on disk is changed.

PANELS AND DESKTOP ICONS
10. Settings > Desktop > "Windows desktop icons" has three choices: "Show everything on the desktop" (default), "Items I add to boxes leave the desktop", "Show only in panels". The same choice is in the tray menu and on the "Widgets" page. "Sort my desktop into panels" in the Add widget window adds panels for folders, shortcuts and files and switches to "Show only in panels"; the notice's "Undo" reverts it.
11. With "Show only in panels" the Windows desktop icons are hidden and shown in the panels instead; no files are moved or changed. The icons come back immediately when another choice is selected, when the app exits (tray > "Exit") and when the user signs out.
12. Double-clicking an empty area of the desktop (or pressing Ctrl+Alt+H) hides and shows icons and widgets; while panels manage the desktop, a double-click peeks instead. This can be changed or turned off in Settings > Desktop > "Double-click on the empty desktop".

SHORTCUT BOX OPTION (moves files only after the user picks it)
13. By default a shortcut box only stores links; nothing is moved. To test the option: Settings > Desktop > "Windows desktop icons" > "Items I add to boxes leave the desktop". Add a "Shortcut box" widget, create a test file on the desktop and drag it onto the box (or right-click the box > "Add item" > "App or file…"). The file is moved to %USERPROFILE%\NestDesk\<box name> (a normal visible folder next to the Desktop folder), stays in the box, and a notice offers "Undo".
14. Right-click the item > "Put back on the desktop" returns it. Settings > Desktop shows how many items are in the NestDesk folder with "Open folder" and "Put all back on the desktop"; switching to another choice asks whether to put the items back.

AUTO-MOVE
15. Turn it on: main window > "Auto-move" > the switch at the top, or tray > "Auto-move" (checked = on). Create a folder named "PDF" on the desktop, then copy a .pdf file to the desktop. After about 2 seconds it is moved into the PDF folder and a notification appears. Undo it on the "Auto-move" page with "Undo", or tray > "Undo last move".

NEW WIDGETS (Add widget window)
16. Calendar: Page Up / Page Down change the month, Home returns to today. Countdown: asks for an event name and a date.
17. Timer: press Space or "Start"; right-click > "Duration" > "1 min" for a quick test. When time is up a notification appears in the notification area (right-click > "Notify me when time is up" turns it off). Pomodoro is a separate tile; right-click a timer > "Mode" > "Stopwatch" for a stopwatch.
18. World clock: right-click > "Add city…". System status: shows processor, memory, disk and battery; values are only displayed.
19. Recycle Bin: shows the item count and size. "Empty…" always asks for confirmation first (Windows' own progress and sound follow); files can be dropped on it to recycle them.
20. Folder panels: "Downloads", "Documents", "Pictures" or "Other folder…" show any folder (newest first, up to 300 items).

OTHER
21. Language: Settings > General > "Language / Dil" > choose, then "Restart now".
22. Optional AI feature: AI-generated folder icons require the user's own Anthropic API key (Settings > Advanced > "AI folder icons (optional)"). Without a key the "Generate" button is disabled; all other features work offline and without a key. The app has no other network use. Inappropriate AI output can be reported with the "Report inappropriate content" link in the folder icon window; it opens a pre-filled GitHub issue form in the browser and sends nothing by itself.
23. Uninstalling: the Store package removes the app's settings. Desktop icons hidden by the app are shown again when it exits. Files the user put into the NestDesk box folder and folder icons the user applied stay as the user's own content; they can be returned/removed beforehand with "Put all back on the desktop" (Settings > Desktop) and "Remove all folder icons" (Settings > Advanced). The desktop installer tells the user where the box folder is when it is uninstalled.
```

---

## Partner Center kontrol listesi

Menü adları Microsoft Learn'deki İngilizce adlardır; Partner Center Türkçe açılırsa karşılıkları farklı görünebilir.

1. **Ürün adı (yapıldı).** Partner Center'daki ürün `NestDesk` adıyla ayrıldı. Paketteki görünen ad (`AppxManifest.xml` → `DisplayName`, uygulamada `Core/AppInfo.cs` → `Name`) ayrılan adla birebir aynı olmalı; her dilin Store listing'inde **Product name** olarak `NestDesk` seç. Ad bir gün değişirse: **Product management** → **Manage product names** → yeni ad → **Check availability** → **Reserve**; ad alınmışsa dur. Paket kimliği (`identity.json` → `Name`) ad değişince değişmez; `AppInfo.Name`, manifest ve bu dosya birlikte değişir.
2. **Ürün kimliği (yapıldı).** `tools/store/identity.json` Partner Center'daki NestDesk ürününün kimliğiyle dolu (`Hasako.NestDesk`). Değerler şuradan gelir: uygulamanın sayfasında sol menü **Product management** → **Product identity** (Ürün yönetimi → Ürün kimliği); **Package/Identity/Name** → `Name`, **Package/Identity/Publisher** (`CN=…` biçiminde) → `Publisher`, **Package/Properties/PublisherDisplayName** → `PublisherDisplayName`. Aynı sayfadaki Package Family Name ve Store ID bilgi içindir; mağaza bağlantın `https://apps.microsoft.com/detail/<Store ID>` olur.
3. **Paketi üret.** `tools/package-msix.ps1 -Strict` betiğini çalıştır (`-Strict`, yer tutucu kalmışsa durur); `dist-store\NestDesk-<sürüm>.msixbundle` üretilir (paket sürümü ör. `2.1.0.0`; program dosyası `NestDesk.exe`). Betik bildirimdeki `ms-resource:` metinlerinin `tools/store/Strings/en-US` ve `tr-TR` altında bulunduğunu denetler. Store aynı sürümü ikinci kez kabul etmez: her yeni gönderimde `<Version>` artmalı.
4. **Gizlilik politikasını yayımla.** `PRIVACY.md`'yi GitHub'a gönder ve `https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md` adresinin tarayıcıda açıldığını kontrol et (uygulamadaki Hakkında ve yapay zekâ bölümündeki bağlantı bu adrese gider).
5. **Gönderimi başlat.** Uygulamanın genel bakış sayfasında **Start submission**.
6. **Pricing and availability.** **Markets**: varsayılan (tüm pazarlar) kalabilir. **Base price**: **Free**. **Free trial** yok. **Discoverability** ve **Schedule** varsayılan.
7. **Properties.** **Category**: Productivity, **Secondary category**: Utilities + tools. Gizlilik sorusuna **Yes** ve gizlilik URL'si. **Website** ve **Support contact info** yukarıdaki tablodan. **Product declarations**: yalnızca gerçekten geçerli olan kutuları işaretle. İsteğe bağlı yapay zekâ simgesi nedeniyle (Store 11.16) canlı üretken yapay zekâ içeriği bildirimi istenirse işaretle ve yukarıdaki "Üretken yapay zekâ bildirimi" metnini kullan; uygulama içi bildirme yolu klasör simgesi penceresindeki **Uygunsuz içeriği bildir** bağlantısıdır.
8. **Age ratings.** Anketi yukarıdaki önerilere göre doldur → **Save and generate**.
9. **Packages.** `.msixbundle` dosyasını sürükle-bırak; doğrulama (validation) bitene kadar bekle. Hata çıkarsa çoğunlukla kimlik değerleri ya da görünen ad Product identity ile uyuşmuyordur. **Device family availability**: Windows 10/11 Desktop.
10. **Store listings.** Paket iki dil bildirdiği için İngilizce ve Türkçe listeler hazır gelir; her dili ayrı aç ve bu dosyadaki metinleri yapıştır. **What's new in this version** 2.1.0 ilk gönderimse boş. **Store logos** bölümüne 300×300 PNG uygulama simgesi yüklemen önerilir (yüklemezsen paketteki simge kullanılır).
11. **Ekran görüntüleri** (her dil için ayrı yüklenir, açıklamalar dile göre):
    - En az 1 zorunlu, en fazla 10 masaüstü görüntüsü; Microsoft 4 ve üstünü (SSS'de 5–8) öneriyor. **8 tane** hazırla; sahneler yukarıdaki "Ekran görüntüleri: ne çekilecek" bölümünde, sıra açıklamalarla aynı.
    - Türkçe liste Türkçe arayüzle, İngilizce liste İngilizce arayüzle çekilir (Store 10.7: her dildeki deneyim aynı olmalı): test örneğini `NESTDESK_LANG=tr` ya da `NESTDESK_LANG=en` ile aç.
    - Biçim: PNG, yatay, **en az 1366×768** (1920×1080 önerilir), en fazla 50 MB. Pencere kırpması 1366×768'den küçük kalacağı için tam ekran görüntüsü al.
    - Önemli içeriği üst üçte ikiye koy (alt üçte birin üstüne yazı binebilir); görüntülere ek logo ya da reklam yazısı ekleme.
    - Gerçek masaüstünü değil test örneğini kullan: `NestDesk.exe --desktop <geçici klasör> --data <geçici klasör>`, klasöre örnek dosyalar koy (PDF, Resimler…). Kişisel dosya adı, e-posta ya da API anahtarı görünmesin; sade bir duvar kâğıdı seç. İşin bitince örneği `--exit` ile kapat.
    - Test örneği gerçek masaüstü simgelerini gizlemez: Windows simgeleri arkada görünmesin diye (6. görüntü hariç) çekimden önce masaüstüne sağ tık → **Görünüm** → **Masaüstü simgelerini göster** işaretini kaldır, sonra geri aç.
12. **Submission options.** **Notes for certification** ve **Restricted capabilities** (runFullTrust) kutularına yukarıdaki İngilizce metinleri yapıştır. **Publishing hold options** varsayılan (sertifikasyondan geçince yayımlanır) ya da elle yayımlamak için **Don't publish this submission until I select Publish now**. **E-posta adresini doğrula:** Microsoft, kritik e-posta bildirimlerini alabilmen için adresin Action Center'da doğrulanmasını zorunlu tutuyor: Partner Center → **Action Center** → **My Preferences**. (Hesap sahibi bildirimleri her zaman alır; **Submission notification audience** ile başka kişi eklemek isteğe bağlı.)
13. **Gönder.** Genel bakış sayfasında **Submit for certification**. Durum sertifikasyon aşamasında görünür; sonuç Partner Center'daki Action Center'a ve (12. adımda doğruladığın) e-posta adresine gelir. Reddedilirse sertifikasyon raporundaki madde düzeltilip yeni gönderim yapılır.
14. **Yayından sonra.** Store bağlantısını (`https://apps.microsoft.com/detail/<Store ID>`) README'ye ekle. Sonraki sürümlerde yeni gönderim oluştur, **What's new in this version**'ı doldur ve yalnızca yeni `.msixbundle`'ı yükle.
