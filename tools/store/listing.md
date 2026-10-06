# Microsoft Store listesi — NestDesk 2.2.0

Partner Center'a kopyalanacak hazır metinler. Her alan ayrı bir kod bloğunda; GitHub'da bloğun sağ üstündeki kopyala düğmesiyle al. Açıklama alanları düz metindir: içine HTML ya da bağlantı koyma (Microsoft'un önerisi; bağlantılar kendi alanlarına girer).

Sınırlar Microsoft Learn'deki MSIX sayfalarından ("Add and edit Store listing info", "App screenshots, images, and trailers") alındı (Eylül 2026). Keywords alanı MSIX sayfalarında yalnızca "Create an app submission" sayfasının Store listings tablosunda isteğe bağlı alan olarak geçiyor, sınırı yazmıyor; anahtar kelime sınırı Microsoft Learn'ün "Store listing for titles on XBOX devices" sayfasındaki (oyun yayımlama rehberi, Additional information tablosu) değerdir:

| Alan (Partner Center) | Sınır | Bu dosyada (TR / EN) |
|---|---|---|
| Description | zorunlu, en fazla 10.000 karakter | ~7.400 / ~7.600 karakter |
| Short description | en fazla 1.000; bazı görünümlerde yalnızca ilk 270 karakter görünür | < 270 |
| Product features | en fazla 20 madde, her biri en fazla 200 karakter; madde işareti koyma | 20 madde, en uzunu < 125 karakter |
| Keywords | en fazla 7 terim, tüm terimlerde toplam en fazla 21 kelime; terim başına en fazla 40 karakter (Partner Center daha kısa bir sınır gösterirse o geçerli) | 7 terim, 14 kelime, en uzunu 20 karakter |
| What's new in this version | en fazla 1.500 karakter; yalnızca ilk gönderimde boş bırakılır | 2.2.0 bir güncellemedir (uygulama Store'da yayında): metin hazır, < 1.500 karakter |
| Screenshot caption | en fazla 200 karakter | 9 açıklama (baştaki "1." numarasını yapıştırma) |

**Terimler (İngilizce):** "bölme" = **panel**. İngilizce metinlerde "fence" ya da "Fences" yazma (Stardock'un markası); başka ürün adları da anma (halka menü için de: listede ve anahtar kelimelerde esin kaynağı olan başka ürünün adı geçmez). "Kısayol kutusu" = **shortcut box**, "Otomatik taşıma" = **Auto-move**, "Windows masaüstüne göz at" = **Peek at the Windows desktop**, "Halka menü" = **ring menu**.

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
| Diller | Paket `en-US` (varsayılan) ve `tr-TR` bildirir; uygulamanın arayüzü de İngilizce ve Türkçedir (Windows'un diline uyar, Ayarlar → Dil / Language) |

Depo adresi şimdilik `hasakobey/duzenleme` kalır (uygulamanın eski adı; depo yeniden adlandırılınca bu bağlantılar ve `Core/AppInfo.cs` → `RepoUrl` birlikte güncellenir).

**Kategori gerekçesi:** Microsoft'un tablosunda *Productivity* "bir işi daha verimli bitirmeye yardım eden uygulamalar" (örnekler: not alma, verileri görüntüleme ve sıralama) olarak tanımlanıyor; NestDesk'in asıl işi masaüstünü düzenli tutmak ve dosyaları yerine koymak, not, yapılacaklar, zamanlayıcı, Pomodoro ve halka menü de bu tanıma giriyor. *Utilities + tools* (örnek: dosya yöneticisi) ikincil kategori olarak dosya taşıma, bölme, sistem durumu ve Geri Dönüşüm Kutusu tarafını kapsar. Productivity'nin alt kategorisi yok. *Personalization* (tema, duvar kâğıdı) widget temalarına ve klasör simgelerine uyardı ama uygulamanın ana değerini anlatmıyor.

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
Masaüstünü bölmelere ayırır, masaüstüne düşen dosyaları senin klasörlerine taşır (yalnızca sen istersen, her taşıma geri alınır), uygulamalarını bir halka menüde toplar ve saat, takvim, not gibi widget'lar ekler. Türkçe ve İngilizce; ücretsiz, reklamsız.
```

### Açıklama (Description)

```text
NestDesk, Windows masaüstünü derli toplu tutan ücretsiz ve açık kaynaklı bir düzenleyicidir. Masaüstünü bölmelere ayırır, istersen masaüstüne düşen dosyaları kendi oluşturduğun klasörlere taşır, uygulamalarını bir halka menüde toplar ve duvar kâğıdının üzerine saat, takvim, not, yapılacaklar, zamanlayıcı gibi widget'lar ekler. Masaüstün için derli toplu bir yuva.

İLK AÇILIŞ
• Üç adımlık kısa bir karşılama masaüstünü seninle birlikte kurar: bölmeler, dosya taşıma ve NestDesk halkan. İstersen atlayabilirsin.
• Sen onay vermeden hiçbir dosya taşınmaz: otomatik taşıma sen açana kadar kapalıdır. Karşılama, masaüstündeki dosyalardan hangisinin hangi klasöre gideceğini önceden gösterir.

BÖLMELER
• Masaüstündeki klasörleri, kısayolları ve dosyaları ayrı bölmelerde topla ya da bir bölmede herhangi bir klasörü göster: İndirilenler, Belgeler, Resimler ya da seçtiğin başka bir klasör (masaüstünde olması gerekmez).
• Bölmede yazdıkça ara (alt klasörler dahil), dosyaları bölmeden bölmeye sürükle, resim, video ve PDF önizlemelerini gör; ada, türe, tarihe, boyuta göre ya da elle sırala.
• Klavyeyle kullan: Enter açar, F2 yeniden adlandırır, Delete Geri Dönüşüm Kutusu'na gönderir, Ctrl+F arar. İstersen 1–9 ilk dokuz öğeyi açar; harf yazınca o öğeye gidilir.
• Windows masaüstü simgeleri ya her zamanki gibi görünür ya da her şey yalnızca bölmelerde toplanır. "Masaüstümü bölmelere ayır" hepsini tek tıkla kurar; geri alınabilir.

WINDOWS MASAÜSTÜNE GÖZ AT
• Ctrl+Alt+G'ye bas: Windows'un kendi masaüstü simgeleri görünür, widget'lar kenara çekilir. Ekranın üstündeki küçük çubuk kalan süreyi gösterir; "NestDesk'e dön" ile ya da birkaç dakika sonra kendiliğinden geri dönülür. Hiçbir dosya değişmez.
• Boş masaüstüne çift tıklayınca ne olacağını sen seçersin: halkayı aç, gizle/göster, göz at ya da hiçbir şey.

KISAYOL KUTUSU
• Uygulamalar, dosyalar ve klasörler için sekmeli raf: tek tıkla aç, istersen yönetici olarak çalıştır. Her öğeye kendi adını ve simgesini ver; dosyanın kendisi değişmez.
• Başlat menüsünden bir uygulamayı ya da tarayıcıdan bir bağlantıyı kutuya sürükleyip bırak; bağlantılar yalnızca sen açınca tarayıcında açılır.
• Widget'ta olan masaüstünden kalkar, olmayan masaüstünde durur: istersen kutuya, halkaya ya da klasör bölmesine koyduğun masaüstü öğesi (dosya ya da klasör) masaüstünden kalkar; çıkarınca geri gelir. Tek bir öğeyi de sağ tıklayıp kaldırabilir, geri koyabilirsin. Simgeler görünürken öğe kullanıcı klasöründeki görünür "NestDesk" klasörüne taşınır, bölmeler masaüstünü yönetirken hiçbir dosya taşınmaz (yalnızca bölmelerden kalkar). İlk öğede bir kez sorulur; otomatik taşıma kurallarının klasörleri yerinde kalır.

HALKA MENÜ
• NestDesk halkası hazır gelir: boş masaüstüne çift tıkla (karşılamada seçilir) ya da tepsiden "Halkayı aç"; halka imlecin yanında açılır. Yeni not, widget ekleme ve masaüstüne göz atma içinde.
• Uygulamalarını, klasörlerini, web adreslerini, komutlarını, tuş kısayollarını ve metin parçacıklarını ekle; tıkla ya da 1–9'a bas. Alt halkalarla grupla.
• Halkanın altındaki "Halkayı düzenle" canlı önizlemeli düzenleyiciyi açar: öğe ekle, sırala, değiştir, kaldır. Halkayı masaüstüne widget olarak da koyabilir, dört biçimden birini seçebilirsin: Klasik, Daire, Etiketli, Işıltı.
• İstersen kendi seçtiğin kısayolla da açılır (varsayılan kapalı): kısayolu basılı tutup imleci bir öğeye doğru götür, bırakınca o öğe açılır.
• Tuş kısayolu ve metin öğeleri yalnızca kısayolla açılan halkada çalışır: yalnızca sen seçince, halkayı açmadan önceki pencereye gider. Yapıştırılan metin pano geçmişine eklenmez; önceki pano içeriği (metin, resim ya da dosya listesi) geri konur. Komutlar yalnızca sen seçince çalışır.
• Kilit ekranında ve yönetici onayı ekranında açılmaz; yönetici olarak çalışan pencerelere tuş ve metin gönderilemez.

WIDGET'LAR
• Saat (12 ya da 24 saat), tarih ve aylık takvim
• Dünya saati: başka şehirlerde saat kaç, kaç saat fark var
• Geri sayım: tatile, doğum gününe kaç gün kaldı (her yıl yinelenebilir)
• Zamanlayıcı, Pomodoro ve kronometre; süre dolunca bildirim
• Yapışkan not (altı renk, Ctrl+tekerlekle yazı boyutu) ve onay kutulu yapılacaklar listesi
• Sistem durumu: işlemci, bellek, disk ve pil
• Geri Dönüşüm Kutusu: kaç öğe var, ne kadar yer tutuyor; boşaltmadan önce sorar

AD, SİMGE VE GÖRÜNÜM
• Her widget'ın köşesindeki ⋯ ya da sağ tık → "Düzenle…" tek bir panel açar: ad, simge (64 simge), widget'ın kendi ayarları, görünüm ve boyut. Her değişiklik hemen uygulanır, "Geri al" panel açıldığındaki hâline döndürür. F2 ile istediğin an yeniden adlandır.
• Sağ tık menüsü kısadır: Düzenle, widget'ın en sık iki işi ve Kaldır.
• Görünüm galerisi gerçek widget gibi çizilir: Otomatik, Cam, Koyu, Açık ve Neon, Gece, Kâğıt, Okyanus temaları; beş vurgu rengi ya da özel renk. Yüksek karşıtlıkta widget'lar Windows'un renklerini kullanır. Temalar yalnızca widget'ların görünüşünü değiştirir; uygulamanın adı ve simgesi değişmez.
• Widget'lar sayfası yönetim yeridir: masaüstündeki widget'ları listeler, seçileni yanında düzenletir, bir görünümü hepsine tek tıkla uygular. Widget'lar birbirine ve ekran kenarına mıknatıs gibi yapışır, üst üste binmez; düzenlerini adıyla kaydedip tek tıkla geri dön.
• Animasyonlar Windows'un "Animasyon efektleri" ayarına uyar; Ayarlar'da Canlı / Sakin / Kapalı.
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
Hesap, reklam ve telemetri yok. Ayarların, notların ve geçmişin yalnızca bilgisayarında kalır. Klavye kancası yok; halka menü kısayolu varsayılan olarak kapalı. Sorunları bulmak için kişisel bilgi içermeyen yerel bir hata günlüğü tutulur; hiçbir şey kendiliğinden gönderilmez, tanılama paketini istersen kendin oluşturursun. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir: yalnızca sen istediğinde klasörün adını ve yazdığın açıklamayı Anthropic'e gönderir (ayrıntılar gizlilik politikasında).

KLAVYE KISAYOLLARI (değiştirilebilir)
Ctrl+Alt+B widget ekle · Ctrl+Alt+G Windows masaüstüne göz at · Ctrl+Alt+H masaüstünü gizle/göster · Ctrl+Alt+N yeni not · Ctrl+Alt+O dosyaları şimdi taşı · Ctrl+Alt+D NestDesk'i aç · Ctrl+Alt+W widget'ları öne getir · halka menüyü imleçte aç (varsayılan kapalı; açınca kısayolu sen seçersin)

Arayüz Türkçe ve İngilizcedir: Windows'un diline uyar, Ayarlar'dan değiştirilebilir. NestDesk bildirim alanında (sistem tepsisi) çalışır; ana pencereyi kapatmak uygulamayı kapatmaz. Açık kaynaklıdır (MIT lisansı).
```

### Ürün özellikleri (Product features) — her satır ayrı bir kutuya

```text
Masaüstünü bölmelere ayır: klasörler, kısayollar, dosyalar ya da herhangi bir klasörün içi (İndirilenler, Belgeler…)
Windows simgeleri görünsün ya da yalnızca bölmelerde; widget'a koyduğun öğe masaüstünden kalkar
Windows masaüstüne göz at (Ctrl+Alt+G): simgeler görünür, widget'lar çekilir, sonra kendiliğinden geri dönülür
Bölmede anında arama, sürükle-bırak, resim/video/PDF önizlemesi ve sıralama; Enter, F2, Delete, Ctrl+F ve 1–9
Sekmeli kısayol kutusu: tek tıkla aç; Başlat menüsünden uygulama, tarayıcıdan bağlantı sürükle; her öğeye ad ve simge
Halka menü: boş masaüstüne çift tıkla, imlecin yanında açılsın; uygulama, klasör, web adresi, komut, tuş ve metin
Saat, tarih, takvim, dünya saati, geri sayım, zamanlayıcı, Pomodoro ve kronometre widget'ları
Yapışkan notlar, yapılacaklar listesi, sistem durumu ve onaylı boşaltmalı Geri Dönüşüm Kutusu widget'ları
Halka düzenleyicisi canlı önizlemeli; dört halka biçimi: Klasik, Daire, Etiketli, Işıltı
Tek Düzenle paneli: ad, simge, ayarlar ve görünüm hemen uygulanır; F2 ile yerinde yeniden adlandır
Widget temaları (Neon, Gece, Kâğıt, Okyanus), Windows'a uyan Otomatik tema ve özel vurgu rengi
Cam, koyu, açık arka plan, boyut ve saydamlık; mıknatıslı yerleşim, kayıtlı düzenler, her ölçekte keskin görüntü
Canlı, Sakin ya da Kapalı animasyonlar; Windows'un animasyon ayarına her zaman uyar
İsteğe bağlı otomatik taşıma: masaüstüne düşen dosyalar senin klasörlerine gider; her taşıma geri alınır
Kısayollar, sistem dosyaları ve inmekte olan dosyalar asla taşınmaz; hiçbir dosyanın üzerine yazılmaz
300 hazır klasör simgesi (30 sembol × 10 renk), ada göre öneri ya da kendi .ico/.png/.svg dosyan
İsteğe bağlı üretken yapay zekâ: kendi Anthropic API anahtarınla Claude'a klasör simgesi çizdir
Türkçe ve İngilizce arayüz; değiştirilebilir genel klavye kısayolları; birden çok monitör desteği
Yerel tanılama: kişisel bilgi içermeyen hata günlüğü, tanılama sayfası ve paketi; hiçbir şey gönderilmez
Hesap, reklam ve telemetri yok; verilerin bilgisayarında kalır. Ücretsiz ve açık kaynak (MIT)
```

### Anahtar kelimeler (Keywords)

```text
masaüstü düzenleyici
masaüstü bölmeleri
dosya düzenleme
masaüstü widget
yapışkan not
halka menü
klasör simgesi
```

### Bu sürümdeki yenilikler (What's new in this version) — 2.2.0

```text
• Halka menü: boş masaüstüne çift tıkla ya da tepsiden aç; uygulamaların, web adreslerin, komutların, tuş ve metin parçacıkların imlecin yanında bir halkada. Canlı önizlemeli düzenleyici, dört biçim (Klasik, Daire, Etiketli, Işıltı); istersen kısayolla da açılır (varsayılan kapalı).
• Daha sade: her widget'ın tek Düzenle paneli (⋯), kısa sağ tık menüleri, yönetim yeri olan Widget'lar sayfası, yedi satırlık Ayarlar ve üç adımlık karşılama.
• Widget temaları: Neon, Gece, Kâğıt, Okyanus ve Windows'a uyan Otomatik; özel vurgu rengi. Yüksek karşıtlıkta Windows'un renkleri.
• Daha canlı hareket: Canlı, Sakin ya da Kapalı; Windows'un animasyon ayarına her zaman uyar.
• Bölmede ve kutuda 1–9 ile ilk dokuz öğeyi aç; kutuya Başlat menüsünden uygulama, tarayıcıdan bağlantı sürükle.
• Kutuya, halkaya ya da klasör bölmesine koyduğun masaüstü öğesi istersen masaüstünden kalkar, çıkarınca geri gelir; tek bir öğeyi de kaldırıp geri koyabilirsin.
• Yerel tanılama: kişisel bilgi içermeyen hata günlüğü, tanılama sayfası ve paketi (hiçbir şey gönderilmez).
• Güncellemeden önce ayarların yedeklenir; daha az kaynak ve Windows'un kendi tepsi menüsü.
```

Önceki sürümlerin yenilikleri GitHub'daki sürüm notlarında ve bu dosyanın geçmişindedir. Sonraki sürümlerde buraya o sürümün yenilikleri aynı biçimde (`• ` ile başlayan satırlar, en fazla 1.500 karakter) yazılır.

### Ekran görüntüsü açıklamaları (caption)

```text
1. Bölmelere ayrılmış, derli toplu bir masaüstü: klasörler, kısayollar, dosyalar ve İndirilenler bölmeleri; saat, takvim ve yapılacaklar
2. Ctrl+Alt+B ile "Widget ekle": bölmeler, araçlar, saat ve bilgi widget'ları tek tıkla masaüstünde
3. Düzenle paneli: ad, simge, ayarlar ve görünüm tek yerde; her seçim widget'ta hemen görünür
4. Halka düzenleyicisi: öğe ekle, sırala, değiştir; dört halka biçimi
5. Zamanlayıcı, Pomodoro, dünya saati, geri sayım, sistem durumu ve Geri Dönüşüm Kutusu widget'ları
6. Notlar, yapılacaklar ve her widget için Cam, Koyu ya da Açık görünüm; renkleri ve saydamlığı sen seç
7. Kısayol kutusu, halka ve klasör bölmesi: istersen koyduğun masaüstü öğesi masaüstünden kalkar, çıkarınca geri gelir
8. İsteğe bağlı otomatik taşıma: hangi dosyanın nereye gideceğini sen seçersin; her taşıma tek tıkla geri alınır
9. Halka menü: boş masaüstüne çift tıkla, uygulamaların ve web adreslerin imlecin yanında bir halkada
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
Splits your desktop into panels, moves files dropped on it into your own folders (only if you want, and every move can be undone), gathers your apps in a ring menu and adds widgets such as clock, calendar and notes. English and Turkish; free, no ads.
```

### Description

```text
NestDesk is a free, open source organizer that keeps your Windows desktop tidy. It splits your desktop into panels, can move files that land on the desktop into folders you created, gathers your apps in a ring menu, and puts widgets such as a clock, calendar, notes, to-do lists and timers right on your wallpaper. A tidy home for your desktop.

FIRST LAUNCH
• A short three-step welcome sets up your desktop with you: panels, moving files and your NestDesk ring. You can skip it.
• No file is moved before you agree: auto-move stays off until you turn it on. The welcome shows in advance which of the files on your desktop would go to which folder.

PANELS
• Gather the folders, shortcuts and files on your desktop into separate panels, or show any folder in a panel: Downloads, Documents, Pictures or another folder you pick (it doesn't have to be on the desktop).
• Search a panel as you type (including subfolders), drag files between panels, preview pictures, videos and PDFs, and sort by name, type, date, size or by hand.
• Use the keyboard: Enter opens, F2 renames, Delete sends to the Recycle Bin, Ctrl+F searches. Optionally, 1–9 opens the first nine items; type a letter to jump to an item.
• Windows desktop icons either show as usual or everything is gathered only into panels. "Sort my desktop into panels" sets it all up with one click and can be undone.

PEEK AT THE WINDOWS DESKTOP
• Press Ctrl+Alt+G: the Windows desktop icons appear and your widgets step aside. A small bar at the top of the screen shows the time left; select "Back to NestDesk" or wait a few minutes to return automatically. No file is changed.
• You decide what a double-click on the empty desktop does: open the ring, hide/show, peek, or nothing.

SHORTCUT BOX
• A tabbed shelf for apps, files and folders: open them with one click, or run an app as administrator. Give each item its own name and icon; the file itself is not changed.
• Drag an app from the Start menu or a link from your browser onto a box; links open in your browser only when you open them.
• What's in a widget leaves the desktop; what isn't stays: optionally, a desktop item (file or folder) you put in a box, ring or folder panel leaves the desktop; take it out and it comes back. You can also right-click a single item to remove it from the desktop or put it back. With Windows icons visible, the item moves into a visible "NestDesk" folder in your user folder; while panels manage the desktop, no file is moved (it just leaves the panels). You're asked once, with the first item; auto-move rule folders stay where they are.

RING MENU
• Your NestDesk ring is ready from the start: double-click the empty desktop (you choose this in the welcome) or select "Open the ring" in the notification area, and it opens next to the pointer. It already has a new note, add widget and peek at the desktop.
• Add your apps, folders, web addresses, commands, keyboard shortcuts and text snippets; click or press 1–9. Group them in sub-rings.
• "Edit ring" below the ring opens an editor with a live preview: add, reorder, change and remove items. You can also put a ring on the desktop as a widget and pick one of four styles: Classic, Orbit, Labeled or Glow.
• If you like, it also opens with a shortcut you choose (off by default): hold the shortcut, move the pointer toward an item and let go to open it.
• Keyboard shortcut and text items work only in the ring opened by the shortcut: they go only where you were before you opened the ring, and only when you pick them. Pasted text isn't added to clipboard history, and your previous clipboard content (text, an image or a list of files) is put back. Commands run only when you pick them.
• It doesn't open on the lock screen or on administrator approval prompts, and keys and text can't be sent to windows running as administrator.

WIDGETS
• Clock (12- or 24-hour), date and monthly calendar
• World clock: the time in other cities and how far ahead or behind they are
• Countdown: days left until a vacation or birthday (can repeat every year)
• Timer, Pomodoro and stopwatch, with a notification when time is up
• Sticky notes (six colors, Ctrl+wheel for text size) and to-do checklists
• System status: processor, memory, disk and battery
• Recycle Bin: how many items and how much space; asks before emptying

NAME, ICON AND LOOK
• The ⋯ button in a widget's corner, or right-click > "Edit…", opens one panel: name, icon (64 icons), the widget's own settings, look and size. Every change applies at once, and "Undo" returns to how it was when the panel opened. Press F2 to rename at any time.
• Right-click menus are short: Edit, the widget's two most common actions, and Remove.
• The look gallery is drawn like the real widget: Automatic, Glass, Dark, Light and the Neon, Night, Paper and Ocean themes; five accent colors or a custom color. In high contrast, widgets use your Windows colors. Themes only change how widgets look; the app's name and icon never change.
• The Widgets page is where you manage them: it lists the widgets on your desktop, edits the selected one right next to the list and applies a look to all of them with one click. Widgets snap to each other and to screen edges like magnets and never overlap; save named layouts and switch back with one click.
• Motion follows the Windows "Animation effects" setting; choose Lively / Calm / Off in Settings.
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
No account, no ads, no telemetry. Your settings, notes and history stay on your PC. No keyboard hook; the ring menu shortcut is off by default. A local error log without personal information helps track down problems; nothing is sent automatically, and you create a diagnostic package yourself if you want to share one. The only feature that uses the internet is the optional AI folder icon: only when you ask for one, it sends the folder name and your description to Anthropic (details in the privacy policy).

KEYBOARD SHORTCUTS (customizable)
Ctrl+Alt+B add widget · Ctrl+Alt+G peek at the Windows desktop · Ctrl+Alt+H hide/show desktop · Ctrl+Alt+N new note · Ctrl+Alt+O move files now · Ctrl+Alt+D open NestDesk · Ctrl+Alt+W bring widgets to front · open the ring menu at the pointer (off by default; you choose the shortcut when you turn it on)

The app is available in English and Turkish: it follows your Windows display language, and you can change it in Settings. NestDesk runs in the notification area (system tray); closing the main window keeps it running. It is open source under the MIT License.
```

### Product features — one line per box

```text
Split your desktop into panels: folders, shortcuts, files or the contents of any folder (Downloads, Documents…)
Windows icons visible or only in panels; items you put in a widget leave the desktop
Peek at the Windows desktop (Ctrl+Alt+G): icons appear, widgets step aside, and NestDesk comes back on its own
Instant search, drag and drop, picture/video/PDF previews and sorting in panels; Enter, F2, Delete, Ctrl+F and 1–9
Tabbed shortcut box: one-click open; drag apps from the Start menu and links from your browser; name and icon per item
Ring menu: double-click the empty desktop to open it at the pointer; apps, folders, web addresses, commands, keys and text
Clock, date, calendar, world clock, countdown, timer, Pomodoro and stopwatch widgets
Sticky notes, to-do checklists, system status and a Recycle Bin widget that always asks before emptying
A ring editor with a live preview; four ring styles: Classic, Orbit, Labeled and Glow
One Edit panel: name, icon, settings and look apply at once; press F2 to rename in place
Widget themes (Neon, Night, Paper, Ocean), an Automatic theme that follows Windows, and a custom accent color
Glass, dark and light backgrounds, size and transparency; magnetic snapping, saved layouts, crisp text at any scaling
Lively, Calm or Off animations that always follow the Windows animation setting
Optional auto-move: files dropped on the desktop go to your folders, and every move can be undone
Never moves shortcuts, system files or downloads in progress, and never overwrites a file
300 ready-made folder icons (30 symbols × 10 colors), suggestions by folder name, or your own .ico/.png/.svg
Optional generative AI: let Claude draw a folder icon with your own Anthropic API key
English and Turkish interface; customizable global keyboard shortcuts; multiple monitor support
Local diagnostics: an error log without personal information, a diagnostics page and package; nothing is sent
No account, no ads, no telemetry; your data stays on your PC. Free and open source (MIT)
```

### Keywords

```text
desktop organizer
desktop panels
file organizer
desktop widgets
sticky notes
radial menu
folder icons
```

### What's new in this version — 2.2.0

```text
• Ring menu: double-click the empty desktop or open it from the notification area, and your apps, web addresses, commands, keys and text snippets appear in a ring next to the pointer. An editor with a live preview, four styles (Classic, Orbit, Labeled, Glow), and an optional shortcut (off by default).
• Simpler: one Edit panel per widget (⋯), short right-click menus, a Widgets page to manage them all, a seven-row Settings page and a three-step welcome.
• Widget themes: Neon, Night, Paper, Ocean and an Automatic theme that follows Windows, plus a custom accent color. Windows colors in high contrast.
• Livelier motion: Lively, Calm or Off; it always follows the Windows animation setting.
• Open the first nine items of a panel or box with 1–9, and drag apps from the Start menu and links from your browser onto boxes.
• If you like, a desktop item you put in a box, ring or folder panel leaves the desktop and comes back when taken out; you can also remove or put back a single item.
• Local diagnostics: an error log without personal information, a diagnostics page and package (nothing is sent).
• Your settings are backed up before an update; lighter on resources, with the native Windows tray menu.
```

### Screenshot captions

```text
1. A tidy desktop split into panels for folders, shortcuts, files and Downloads, with clock, calendar and to-do widgets
2. Add widget (Ctrl+Alt+B): panels, tools, clocks and info widgets, each one click away
3. The Edit panel: name, icon, settings and look in one place; every choice shows on the widget at once
4. The ring editor: add, reorder and change items; four ring styles
5. Timer, Pomodoro, world clock, countdown, system status and Recycle Bin widgets
6. Notes, to-do lists and a Glass, Dark or Light look for every widget; you choose the colors and transparency
7. Shortcut box, ring and folder panel: optionally, items you put in leave the desktop and come back when taken out
8. Optional auto-move: you choose which files go to which folder, and every move can be undone with one click
9. Ring menu: double-click the empty desktop and your apps and web addresses appear in a ring next to the pointer
```

---

## Ekran görüntüleri: ne çekilecek (iki dil için aynı sahne)

Her görüntü iki kez çekilir: Türkçe liste için `NESTDESK_LANG=tr`, İngilizce liste için `NESTDESK_LANG=en` ile açılan test örneğinde. Sıra yukarıdaki açıklamalarla aynıdır. Bütün sahneler varsayılan görünümle (Klasik tema) çekilir: temalar listede yazıyla anlatılır, ekran görüntüsünde gösterilmez. Hareket bitmeden çekmemek için test ayarlarına `"AnimationsOff": true` koy.

1. **Vitrin.** Masaüstü bölmelere ayrılmış: Klasörler (içinde renkli simgeli birkaç klasör), Kısayollar, Dosyalar ve İndirilenler (klasör portalı) bölmeleri; sağ üstte saat, altında aylık takvim, yanında 3–4 maddeli bir yapılacaklar listesi (bir maddesi işaretli). Sade bir duvar kâğıdı.
2. **"Widget ekle" penceresi** (Ctrl+Alt+B) açık: Bölmeler, Araçlar, Saat ve bilgi bölümleri ve "Masaüstümü bölmelere ayır" düğmesi görünsün; arkada birkaç widget.
3. **Düzenle paneli.** Bir bölmenin ⋯ düğmesiyle açılmış Düzenle paneli bölmenin yanında: ad ("Projeler"), simge, görünüm galerisi (Açık seçili) ve renk noktaları; bölme Açık zeminle görünür.
4. **Halka düzenleyicisi.** Ana sayfa → "Halkayı düzenle" ile açılan pencere: canlı önizlemede bir öğe seçili, altında "◀ ▶ · Değiştir… · Kaldır" ve ekleme çubuğu (+ Uygulama, + Klasör, + Web adresi…).
5. **Yeni widget'lar.** Zamanlayıcı (çalışıyor, ör. 12:34), Pomodoro (Odak · 2/4), dünya saati (3 şehir), geri sayım ("Tatil: 12 gün kaldı"), sistem durumu ve Geri Dönüşüm Kutusu widget'ları düzenli yerleştirilmiş.
6. **Notlar ve görünüm.** Koyu zeminli resim önizlemeli Fotoğraflar bölmesi, Açık zeminli Dosyalar bölmesi, pembe/yeşil/mavi/mor notlar (biri yapılacaklar listesi), Açık saat ve Koyu tarih. (Göz atma sahnesi gerçek masaüstü simgelerini gösterdiği için çekilmez.)
7. **Kısayol kutusu ve kutu seçeneği.** Solda birkaç uygulama ve dosya taşıyan sekmeli bir kısayol kutusu (bir öğe kendi adı ve simgesiyle), sağda ana pencerede Ayarlar'ın ilk sayfasında "Simgeleri bölmelerde topla"nın altında "Widget'lara koyduğum öğeler masaüstünden kalksın" açık (Gelişmiş ayarlar → Masaüstünden kalkanlar: "Masaüstünden kalkan N öğe widget'larda duruyor; dosyalar … klasöründe · Klasörü aç · Hepsini masaüstüne geri koy" ve "Uygulama eklenince masaüstündeki kısayolu da kalksın").
8. **Otomatik taşıma** sayfası: kurallar ("Hangi dosya nereye gitsin?") ve birkaç taşıma, "Geri al" düğmeleriyle; üstte anahtar açık.
9. **Halka menü.** Masaüstünde Daire biçimli bir halka menü widget'ı (6–8 öğe: Not Defteri, Hesap Makinesi, bir web adresi, "Yeni not", "Widget ekle" ve bir alt halka); yanında imlecin yanında açılmış NestDesk halkası, bir öğesi vurgulu ve ortasında simgesiyle adı, altında "Halkayı düzenle". Açılır halkayı test örneğinde `NESTDESK_TEST_RING=1` ve `NESTDESK_RING_AT` ile, "DuzenlemeHotkeys" penceresine WM_HOTKEY (id 8) göndererek aç (gerçek kısayol atama). Metin ya da komut öğelerinin içeriği görünmesin.

---

## Kısıtlı yetenek gerekçesi: runFullTrust (Submission options → Restricted capabilities)

İngilizce yaz; kutu kısaysa aşağıdaki kısa sürümü kullan.

```text
NestDesk is a Win32 desktop application (WPF, .NET 10) packaged as MSIX with the Windows.FullTrustApplication entry point. It needs runFullTrust because its core features rely on desktop and shell APIs that are not available to app-container apps. Every change to the user's desktop or files happens only after the user turns the related option on or gives an explicit command, and all of it can be reversed.

1. Desktop icon visibility: when the user chooses "Gather icons into panels", peeks at the Windows desktop (Ctrl+Alt+G), double-clicks the empty desktop (if set to hide/show or peek) or presses Ctrl+Alt+H, the app finds Explorer's desktop icon view (Progman/WorkerW > SHELLDLL_DefView > SysListView32) and hides or shows it with ShowWindowAsync. No files or Windows settings are changed. The icons are shown again when the option is turned off, when a peek ends, when the app exits or crashes, and when the user signs out.
2. Desktop-attached widgets: panel, shortcut box, ring menu, clock, calendar, note, timer and other widget windows are owned by the desktop window and kept at the bottom of the z-order (SetWindowPos, WM_WINDOWPOSCHANGING, WS_EX_NOACTIVATE) so they stay on the desktop and survive Win+D; the small peek bar is a window of the same kind. Per-monitor DPI APIs place them on the right screen. The optional "minimize open windows while peeking" setting (off by default) calls the shell's Shell.Application ToggleDesktop, the same as Win+D.
3. Double-click detection: raw mouse input (RegisterRawInputDevices with RIDEV_INPUTSINK on a message-only window; no hook, the pointer is never delayed) records only the time and position of primary-button presses to detect a double-click on an empty area of the desktop, which opens the ring menu at the pointer (the default for new users, chosen in the welcome), hides/shows the desktop or peeks, as the user chose. Nothing is logged or transmitted, and the registration is removed when the user sets the double-click action to "Do nothing" in Settings.
4. Global hotkeys: up to eight user-configurable shortcuts (e.g. Ctrl+Alt+G, Ctrl+Alt+B) are registered with RegisterHotKey. Seven have defaults; the ring menu shortcut is off and unassigned by default and is registered only after the user turns it on. No keyboard hook is used.
5. Ring menu: the ring opened by a double-click on the empty desktop, from the tray menu or by its shortcut is the app's own topmost window at the pointer position; Esc, a right-click, a click outside or the shortcut again closes it, and it does not appear on the secure desktop (lock screen, UAC). Key and text items run only in the ring opened by the shortcut; in a ring opened by a double-click or from the tray they are dimmed and do nothing. When it opens, only the handle of the foreground window is kept in memory (its title and process name are not read), to return to it and as the target of key and text items. While the ring is open, the app checks only whether the shortcut's own keys have been released: through its own window's key events, or with GetAsyncKeyState for at most 10 seconds if the ring could not take the focus. Ring items are defined by the user and run only when the user picks one:
   a. Apps, files and folders open with ShellExecute. Web addresses open in the default browser; http, https and mailto are accepted, another scheme needs a one-time confirmation when the item is added, and file, UNC, shell and script schemes are refused.
   b. User-defined command lines start with ShellExecute; the console window is visible unless the user hides it for that item. "Run as administrator" uses the runas verb, so UAC asks every time. NestDesk ships no preset commands and never runs elevated itself.
   c. A keyboard shortcut item sends its user-defined key combinations once (at most 8, SendInput), after the ring has closed and the shortcut's keys are released (a modifier key still held down is released first), only to the window that was in front before the ring opened and only while it is still in front; never to the desktop, to NestDesk's own windows or to windows at a higher integrity level such as elevated ones (a notice tells the user). There is no recording, looping, repeating or scheduling.
   d. A text item is placed on the clipboard together with the ExcludeClipboardContentFromMonitorProcessing, CanIncludeInClipboardHistory=0 and CanUploadToCloudClipboard=0 formats and pasted with Ctrl+V into the same target; about a second later the previous clipboard content is put back, unless something else changed the clipboard in the meantime.
   Item contents, addresses, commands, keys and window details are never logged or transmitted.
6. Files, on the user's behalf:
   a. Only while auto-move is on, a FileSystemWatcher monitors the user's Desktop folder, and new files are moved into folders the user created on the desktop according to user-defined rules. Every move is journaled and can be undone. (Panels keep their item lists current with their own folder watchers; they never move files by themselves.)
   b. Only if the user turns on "Items I put in widgets leave the desktop" (asked once, the first time a desktop item is put in a widget), or chooses "Remove from desktop" for a single item, a desktop file or folder the user puts in a shortcut box, ring or folder panel is moved into a visible folder next to the Desktop folder (%USERPROFILE%\NestDesk\<widget name>), recorded, and put back when it is taken out of the widget, when the user chooses "Put back on the desktop", or when the option is turned off. Folders that an auto-move rule or a ring command uses are never moved, file attributes are never changed, and a "What is this folder.txt" file in that folder lists each file's former location. While panels manage the desktop, nothing is moved (boxed icons are only left out of the desktop panels). Items on the Public Desktop are moved only after a separate question and Windows' UAC prompt, and only in the installer edition: the Store edition never moves them. The installer edition puts everything back when it is uninstalled.
   c. Panels list the Desktop, the Public Desktop and any folder the user picks (e.g. Downloads, Documents). On explicit user request they open, rename (F2), create folders and send items to the Recycle Bin using Windows' own shell file operations (SHFileOperation, IFileOperation; UAC only for Public Desktop items).
   d. Web and e-mail links the user drops on a shortcut box (http, https and mailto only) are saved as .url files in the app's data folder and opened in the default browser only when the user opens them.
7. Shell integration: folder icons with SHGetSetFolderCustomSettings (desktop.ini plus a hidden .ico inside the folder); icons and thumbnails from SHGetFileInfo and IShellItemImageFactory (no thumbnails for online-only cloud files, so nothing is downloaded); the Windows "Change Icon" dialog (PickIconDlg); items are opened with ShellExecute, including "Run as administrator" for shortcut-box and ring items the user added. Apps dropped from the Start menu are opened through shell:AppsFolder. The Recycle Bin widget reads the item count and size with SHQueryRecycleBin, watches changes with SHChangeNotifyRegister, and empties it with SHEmptyRecycleBin only after the user confirms.
8. System status widget: reads processor, memory, disk and battery values with GetSystemTimes, GlobalMemoryStatusEx, GetDiskFreeSpaceEx and GetSystemPowerStatus while the widget is visible. The values are only displayed; they are not stored or sent.
9. A notification area (tray) icon (Shell_NotifyIcon) is the app's main entry point and shows move and timer notifications. The "NestDesk – Add widget" Start menu entry hands its request to the main app identity with IApplicationActivationManager.
10. Local diagnostics: the app keeps a local, rotating, scrubbed error log in its data folder (no paths, file names or user text; 4 MB/14-day cap; the user can turn it off and delete it in Settings). After a crash, the next start asks in a non-modal strip whether to save a small report on the PC. A diagnostic zip is created only on user request, after its contents are listed, and saved where the user chooses. Nothing is uploaded.
11. Widget themes are data only: built-in color presets stored as plain values in the settings file. No theme files, code or scripts are loaded.

The app does not install drivers or services and does not require administrator rights. Its only network use is optional and goes only to the Anthropic API: when the user saves their own Anthropic API key, the key is verified with one request, and when the user requests an AI-generated folder icon, the folder name and the user's description are sent.
```

Kısa sürüm:

```text
Win32 WPF (.NET 10) desktop app packaged with the Windows.FullTrustApplication entry point. runFullTrust is required to: hide/show Explorer's desktop icon view (SHELLDLL_DefView/SysListView32) on user request (panel mode, peek, double-click) and restore it on exit; keep widget windows owned by the desktop at the bottom of the z-order; detect double-clicks on the empty desktop with raw mouse input to open the ring menu or hide/peek (no hook; time/position only, nothing stored or sent); register user-configurable global hotkeys (RegisterHotKey; the ring menu shortcut is off by default; no keyboard hook is used); on the user's pick of a ring item only, send that item's user-defined key combinations once (at most 8, SendInput, only to the window that was in front before the ring opened and only while it is still in front, never to windows at a higher integrity level such as elevated ones) or paste its user-defined text via the clipboard with the clipboard-history/cloud exclusion formats, restoring the previous clipboard; start user-defined command lines via ShellExecute (runas only through UAC); watch the user's Desktop folder and, only after opt-in, move files into user-created folders by user rules or into a visible %USERPROFILE%\NestDesk folder for shortcut boxes (journaled, undoable); list, rename and recycle items on explicit request with shell file operations; apply folder icons with SHGetSetFolderCustomSettings; query and, after confirmation, empty the Recycle Bin; read CPU/memory/disk/battery for display only; launch user-chosen items via ShellExecute; and keep a local, rotating, scrubbed error log in the app data folder (no paths, file names or user text; 4 MB/14-day cap; the user can turn it off and delete it in Settings). Widget themes are data only (built-in color values). No drivers, services or admin rights.
```

---

## Üretken yapay zekâ bildirimi (Store 11.16)

Partner Center gönderiminde canlı üretken yapay zekâ içeriği bildirimi istenirse işaretle ve aşağıdaki metni kullan (açıklamada ayrıca yazıyor; sertifikasyon notlarında da var).

```text
NestDesk has one optional generative AI feature: folder icons drawn by Anthropic's Claude model. It works only after the user enters their own Anthropic API key (Settings > "Advanced settings" > Folder icons > "AI folder icons (optional)") and presses Generate; without a key the feature is disabled and the app makes no network requests. The request contains the folder name, the user's short description and a fixed drawing instruction that asks for simple shapes without text. The result is an SVG drawing that is sanitized (scripts and external links removed) before it is shown, and it is kept only on the user's PC. Users can report inappropriate output with the "Report inappropriate content" link in the folder icon window; it opens a pre-filled GitHub issue form in the browser (containing only the app version) and sends nothing by itself.
```

```text
NestDesk'te isteğe bağlı tek bir üretken yapay zekâ özelliği vardır: Anthropic'in Claude modelinin çizdiği klasör simgeleri. Yalnızca kullanıcı kendi Anthropic API anahtarını girip (Ayarlar > Gelişmiş ayarlar > Klasör simgeleri > Yapay zekâ ile klasör simgesi) "Üret"e bastığında çalışır; anahtar yoksa özellik kapalıdır ve uygulama internete bağlanmaz. İstekte klasörün adı, kullanıcının kısa açıklaması ve yazısız, basit şekiller isteyen sabit bir çizim talimatı bulunur. Gelen SVG çizimi gösterilmeden önce temizlenir (betikler ve dış bağlantılar atılır) ve yalnızca kullanıcının bilgisayarında kalır. Uygunsuz bir sonuç, klasör simgesi penceresindeki "Uygunsuz içeriği bildir" bağlantısıyla bildirilir: tarayıcıda önceden doldurulmuş bir GitHub formu açılır (içinde yalnızca uygulamanın sürümü vardır), kendiliğinden hiçbir şey gönderilmez.
```

## Gizlilik özeti (listeye ya da Partner Center sorularına)

```text
NestDesk does not collect personal data and has no server, accounts, ads or telemetry. Settings, notes, ring menus, move history and box records are stored only on the user's PC (%AppData%\NestDesk, or the package's own folder in the Store edition). A local error log without personal information (error types, scrubbed messages and stack frames; paths, names and keys are replaced with placeholders) is kept in the same folder. It is size-capped (4 MB, 14 days) and can be turned off or deleted in Settings. After a crash, a small report without personal information waits on the PC until the user chooses Save or Don't save (Don't save deletes it); in Settings the user can also choose to save reports without asking or never to save them. Nothing is sent automatically; a diagnostic zip is created only on user request, after its contents are listed. Files are moved only after the user turns auto-move or the shortcut-box option on, every move is recorded and can be undone, and the Recycle Bin is emptied only after confirmation. System status values are only displayed. No keyboard hook is used, and the ring menu shortcut is off by default. When the ring opens, only the foreground window is noted in memory (its title and process name are not read); key and text items go only to that window and only when the user picks them. Text pasted by the ring menu isn't added to clipboard history, and the previous clipboard is put back. The only network use is the optional AI folder icon, which sends the folder name and the user's description to Anthropic with the user's own API key. Full policy: https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md
```

```text
NestDesk kişisel veri toplamaz; sunucusu, hesabı, reklamı ve telemetrisi yoktur. Ayarlar, notlar, halka menüler, taşıma geçmişi ve kutu kayıtları yalnızca kullanıcının bilgisayarında tutulur (%AppData%\NestDesk; Store sürümünde paketin kendi klasörü). Kişisel bilgi içermeyen yerel bir hata günlüğü (hata türleri, temizlenmiş iletiler ve yığın kareleri; yollar, adlar ve anahtarlar yer tutucuyla değiştirilir) aynı klasörde tutulur. Boyutu sınırlıdır (4 MB, 14 gün) ve Ayarlar'dan kapatılıp silinebilir. Bir çökmeden sonra kişisel bilgi içermeyen küçük bir rapor, kullanıcı "Kaydet" ya da "Kaydetme"yi seçene dek bilgisayarda bekler ("Kaydetme" onu siler); Ayarlar'da raporların sormadan kaydedilmesi ya da hiç kaydedilmemesi de seçilebilir. Hiçbir şey kendiliğinden gönderilmez; tanılama zip'i yalnızca kullanıcı isteyince, içeriği listelendikten sonra oluşturulur. Dosyalar yalnızca kullanıcı otomatik taşımayı ya da kısayol kutusu seçeneğini açtıktan sonra taşınır; her taşıma kaydedilir ve geri alınabilir; Geri Dönüşüm Kutusu yalnızca onaydan sonra boşaltılır. Sistem durumu değerleri yalnızca gösterilir. Klavye kancası yok; halka menü kısayolu varsayılan olarak kapalı. Halka açılırken yalnızca öndeki pencere bellekte not edilir (başlığı ve programının adı okunmaz); tuş ve metin öğeleri yalnızca kullanıcı seçince ve yalnızca o pencereye gider. Halka menünün yapıştırdığı metin pano geçmişine eklenmez, önceki pano geri konur. İnternete çıkan tek özellik isteğe bağlı yapay zekâ simgesidir: kullanıcının kendi API anahtarıyla klasörün adını ve kullanıcının açıklamasını Anthropic'e gönderir. Politikanın tamamı: https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md
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
1. On first launch the app adds an icon to the notification area (system tray) and opens a short welcome with 3 steps (panels, moving files, "Your NestDesk ring"). Go through it with "Next" and "Done", or press "Skip for now": skipping changes nothing on the desktop and opens the main window. In step 3, "Open when I double-click the empty desktop" is on by default and "Also open with a key" is off.
2. No file is moved before the user agrees. Auto-move stays OFF unless the user picks "Yes, move my files into place" in step 2 of the welcome or turns it on later on the "Auto-move" page. Files are only moved into matching folders that already exist on the desktop (default rule folders: PDF, Pictures, Documents, Archives, Videos, Music). On a clean test machine nothing is moved.
3. Closing the main window keeps the app running in the tray. Left-click the tray icon to reopen it. To quit, right-click the tray icon > "Exit".

ADD, EDIT AND REMOVE WIDGETS
4. Press Ctrl+Alt+B, right-click the tray icon > "Add widget…", select "Add widget" on the Home page, or start "NestDesk – Add widget" from the Start menu. Click a tile, e.g. "Clock", "Calendar", "Note", "To-do", "Shortcut box", "Timer"; "Show all" has "Ring menu", "Pomodoro", "World clock" and more, and "Panel ›" has the panels ("Folders", "Files", "Downloads", "New panel…"). The new widget appears next to the mouse pointer and is highlighted for a few seconds (Settings > "Advanced settings" > Desktop > "Where new widgets appear" can change this to the screen center or a corner). Below the tiles, "From the desktop" shows up to six desktop items: a folder becomes a panel, a shortcut or file goes into a shortcut box ("All…" lists everything with search and multi-select; dropping files from the desktop onto the window does the same). Nothing is moved on disk unless "Icons I put in boxes leave the desktop" is on (it is asked the first time).
5. Click the ⋯ button in a widget's corner, or right-click it > "Edit…": the Edit panel opens next to the widget with its name, icon (click the icon to choose one of 64), the widget's own settings, "Appearance" (Auto, Glass, Dark, Light, Neon, Night, Paper, Ocean; five colors and "Custom…"; Size), "Show" and "More". Every change applies at once; "Undo" returns to the state when the panel opened; "Done" or Esc closes it. Right-click menus have at most five rows (Edit…, up to two actions of the widget, Remove).
6. F2 renames: click a widget's title and press F2. In a panel, select a file you created for the test and press F2 to rename it on disk; in a shortcut box, F2 changes only the displayed name.
7. To remove a widget, click the × in its top-right corner, right-click it > "Remove", or "Remove" in the Edit panel. The notice's "Undo" or the tray menu's "Bring back" item restores it.
8. The "Widgets" page of the main window lists the widgets on the desktop ("On your desktop"); selecting one shows its Edit panel next to the list. "All widgets" applies a look or a color to every widget at once (with "Undo").

THEMES AND ANIMATIONS
9. In the Edit panel, click "Neon", "Night", "Paper", "Ocean" or "Auto" (follows Windows light/dark) under "Appearance"; the widget changes at once. "Custom…" next to the colors opens a color picker next to the widget with a live preview. Themes only change how widgets look; the app's name and icon never change. In High Contrast, widgets use the Windows high contrast colors, the gallery is replaced by a short explanation, and the new-widget glow is not drawn.
10. Settings > "Advanced settings" > Appearance > Animations (Lively / Calm / Off). If Windows Settings > Accessibility > Visual effects > Animation effects is off, none of NestDesk's own animations play, whatever is chosen here, and every change applies instantly. (Standard toggle switches, check boxes and drop-down lists from the UI library keep their brief built-in state transition.) Energy saver and Remote Desktop switch to Calm. There is no flashing or looping animation, and input never waits for an animation.

PEEK AT THE WINDOWS DESKTOP
11. Press Ctrl+Alt+G (or tray > "Peek at the Windows desktop"). The Windows desktop icons become visible, widgets step aside, and a small bar at the top center shows the time left with "+5 min" and "Back to NestDesk". It returns automatically after 2 minutes. Nothing on disk is changed.

PANELS AND DESKTOP ICONS
12. Settings > "Gather icons into panels" (on after "Yes, sort into panels" in step 1 of the welcome, otherwise off). "Sort my desktop into panels" in the Add widget window adds panels for folders, shortcuts and files and turns this on; the notice's "Undo" reverts it. The separate switch "Icons I put in boxes leave the desktop" is described in steps 16–17.
13. With "Gather icons into panels" on, the Windows desktop icons are hidden and shown in the panels instead; no files are moved or changed. The icons come back immediately when it is turned off, when the app exits (tray > "Exit") and when the user signs out.
14. Double-clicking an empty area of the desktop opens the ring menu if "Open when I double-click the empty desktop" was left on in the welcome (see step 18). Otherwise, or after choosing another action in Settings > "Advanced settings" > Desktop > "Double-click on the empty desktop" (Automatic (recommended) / Hide / show desktop / Peek at the Windows desktop / Do nothing / Open the ring), it hides and shows icons and widgets (Ctrl+Alt+H does the same); while panels manage the desktop, Automatic peeks instead.
15. Optional: Edit panel of a panel or shortcut box > "More" > "Open with number keys (1–9)"; then 1–9 opens the first nine items while the widget is active. Dragging a link from the browser onto a shortcut box adds it as a link that opens in the browser.

SHORTCUT BOX OPTION (moves files only after the user picks it)
16. By default a shortcut box only stores links; nothing is moved. To test the option: with "Gather icons into panels" off, add a "Shortcut box" widget, create a test file on the desktop and drag it onto the box (or right-click the box > "Add app…" > "Choose a file or folder…"). A small window next to the box asks "Remove the icon from the desktop too?": choose "Yes, remove it" (the same switch is Settings > "Advanced settings" > Shortcut boxes > "Icons I put in boxes leave the desktop"). The file is moved to %USERPROFILE%\NestDesk\<box name> (a normal visible folder next to the Desktop folder), stays in the box, and a notice offers "Undo".
17. Right-click the item > "Remove from box" (or Delete) returns it to the desktop; the notice's "Undo" puts it back in the box. Settings > "Advanced settings" > Shortcut boxes shows how many icons are in the NestDesk folder with "Open folder" and "Put all back on the desktop"; turning the switch off puts everything back (with "Undo"). With "Gather icons into panels" on, putting a desktop icon in a box moves no file: the icon simply no longer shows in the desktop panels until it is taken out of the box.

RING MENU (keys and text are sent only after the user picks an item)
18. Double-click an empty area of the desktop (after the welcome with its default choice), or right-click the tray icon > "Open the ring": the NestDesk ring opens next to the pointer with "New note", "Peek at the Windows desktop", "Add widget" and "Open NestDesk". Click an item or press its number (1–9); Esc, a right-click or a click outside closes it. Settings > "Ring menu" > "Show" has the same switches as the welcome.
19. "Edit ring" below the ring (or "Edit ring" on the Home page) opens the ring editor with a live preview: click a slot to select it, move it with ◀ / ▶ (Alt+Left/Right), "Change…" or "Remove" it (the notice offers "Undo"). The add bar has "+ App", "+ Folder", "+ Web address", "+ NestDesk action" and "More ▾" (Command…, Press keys…, Paste text…, Sub-ring…). "Done" closes it.
20. Ring styles: "Add widget" > "Show all" > "Ring menu" puts a ring widget on the desktop; its Edit panel (⋯) has "Ring style" with Classic, Orbit, Labeled and Glow. The style only changes how the ring is drawn; in High Contrast every style is drawn as Classic with Windows colors.
21. Turn on "Open next to the pointer with a key" (Settings > "Ring menu", or at the bottom of the ring editor). The ring menu shortcut is off and unassigned by default; turning it on registers Ctrl+Alt+R (if that is taken: Ctrl+Alt+K, J or Y) with RegisterHotKey, and a notice says "The ring menu now opens next to the pointer with Ctrl+Alt+R." No keyboard hook is used. Open Notepad and press the shortcut: the ring opens at the pointer; Esc (or the shortcut again) closes it and Notepad is active again.
22. Text item: ring editor > "More ▾" > "Paste text…", type a test text, then "Save". In Notepad, copy some other text to the clipboard (Ctrl+C), press the shortcut and press the item's number (or click it). The test text is pasted, Win+V shows no entry for it, and about a second later Ctrl+V pastes the earlier copied text again. In a ring opened by a double-click or from the tray, key and text items are dimmed and only say "This item only works when you open the ring with its shortcut".
23. Key item: "More ▾" > "Press keys…", press Ctrl+A in the capture box, then "Save". In Notepad, open the ring with the shortcut and pick the item: the text is selected.
24. Start Notepad with "Run as administrator", open the ring there and pick the key item. Nothing is sent and NestDesk shows "Keys can't be sent to a window running as administrator." The ring does not open on the lock screen or on UAC prompts.
25. Commands ("More ▾" > "Command…") run only when picked; a command marked "Run as administrator (Windows asks every time)" shows the UAC prompt every time. NestDesk ships no preset commands. Settings > "Advanced settings" > Keyboard shortcuts > "Reset shortcuts to defaults" turns the ring shortcut off and says so.

AUTO-MOVE
26. Turn it on: main window > "Auto-move" > the switch at the top, or tray > "Auto-move" (checked = on). Create a folder named "PDF" on the desktop, then copy a .pdf file to the desktop. After about 2 seconds it is moved into the PDF folder and a notification appears. Undo it on the "Auto-move" page with "Undo", or tray > "Undo last move".

NEW WIDGETS (Add widget window)
27. Calendar: Page Up / Page Down change the month, Home returns to today. Countdown: asks for an event name and a date.
28. Timer: press Space or "Start"; Edit panel (⋯) > "Duration" > "1 min" for a quick test. When time is up a notification appears in the notification area ("Notify me when time is up" in the Edit panel turns it off). Pomodoro is a separate tile; Edit panel > "Mode" > "Stopwatch" for a stopwatch.
29. World clock: right-click > "Add city…". System status: shows processor, memory, disk and battery; values are only displayed.
30. Recycle Bin: shows the item count and size. "Empty…" always asks for confirmation first (Windows' own progress and sound follow); files can be dropped on it to recycle them.
31. Folder panels ("Panel ›" page): "Downloads", "Documents", "Pictures" or "Other folder…" show any folder (newest first, up to 300 items).

DIAGNOSTICS (local only)
32. The error log is in %LOCALAPPDATA%\Packages\<PFN>\LocalCache\Roaming\NestDesk\logs. Settings > "Advanced settings" > Troubleshooting > "Error log" > "Show" lists "Keep an error log", "When the app crashes" (Ask every time / Save without asking / Don't save), "Open log folder" and "Delete logs". In the same section, "Open diagnostics page" opens a local, script-free HTML file, and "Create diagnostic package" first lists every file with its size, then saves a zip only where the user chooses (Documents by default). Settings > Help > "Open an issue on GitHub" opens a public issue form; its URL carries only the app version. Nothing is uploaded by the app. After a crash, the next start shows a non-modal strip at the bottom of the main window (or a notification, "Click to answer.", if the window is hidden): "Save" / "Don't save" / "Always save".

OTHER
33. Language: Settings > "Dil / Language" > choose, then "Restart now".
34. Optional AI feature: AI-generated folder icons require the user's own Anthropic API key (Settings > "Advanced settings" > Folder icons > "AI folder icons (optional)"). Without a key the "Generate" button is disabled; all other features work offline and without a key. The app has no other network use. Inappropriate AI output can be reported with the "Report inappropriate content" link in the folder icon window; it opens a pre-filled GitHub issue form in the browser and sends nothing by itself.
35. Uninstalling: the Store package removes the app's settings, ring menus, settings backups, error logs and crash reports. Desktop icons hidden by the app are shown again when it exits. Files the user put into the NestDesk box folder, folder icons the user applied and diagnostic zips the user saved stay as the user's own content; the first two can be returned/removed beforehand by turning off "Icons I put in boxes leave the desktop" or with "Put all back on the desktop" (Settings > "Advanced settings" > Shortcut boxes) and with "Remove all folder icons" (Settings > "Advanced settings" > Folder icons). The box folder is a normal visible folder and contains "What is this folder.txt", which lists each file's former location. The Store edition never moves items from the Public Desktop. The desktop installer puts moved items back on the desktop when it is uninstalled and names the folder if anything could not be put back.
```

---

## Partner Center kontrol listesi

Menü adları Microsoft Learn'deki İngilizce adlardır; Partner Center Türkçe açılırsa karşılıkları farklı görünebilir.

1. **Ürün adı (yapıldı).** Partner Center'daki ürün `NestDesk` adıyla ayrıldı. Paketteki görünen ad (`AppxManifest.xml` → `DisplayName`, uygulamada `Core/AppInfo.cs` → `Name`) ayrılan adla birebir aynı olmalı; her dilin Store listing'inde **Product name** olarak `NestDesk` seç. Ad bir gün değişirse: **Product management** → **Manage product names** → yeni ad → **Check availability** → **Reserve**; ad alınmışsa dur. Paket kimliği (`identity.json` → `Name`) ad değişince değişmez; `AppInfo.Name`, manifest ve bu dosya birlikte değişir. 2.2'de ad, logo ve simge değişmez; temalar yalnızca widget'ların görünüşünü değiştirir.
2. **Ürün kimliği (yapıldı).** `tools/store/identity.json` Partner Center'daki NestDesk ürününün kimliğiyle dolu (`Hasako.NestDesk`). Değerler şuradan gelir: uygulamanın sayfasında sol menü **Product management** → **Product identity** (Ürün yönetimi → Ürün kimliği); **Package/Identity/Name** → `Name`, **Package/Identity/Publisher** (`CN=…` biçiminde) → `Publisher`, **Package/Properties/PublisherDisplayName** → `PublisherDisplayName`. Aynı sayfadaki Package Family Name ve Store ID bilgi içindir; mağaza bağlantın `https://apps.microsoft.com/detail/<Store ID>` olur.
3. **Paketi üret.** `tools/package-msix.ps1 -Strict` betiğini çalıştır (`-Strict`, yer tutucu kalmışsa ya da PRIVACY.md ve bu dosya hata günlüğünü anlatmıyorsa durur); `dist-store\NestDesk-<sürüm>.msixbundle` üretilir (paket sürümü ör. `2.2.0.0`; program dosyası `NestDesk.exe`). Betik bildirimdeki `ms-resource:` metinlerinin `tools/store/Strings/en-US` ve `tr-TR` altında bulunduğunu denetler. Store aynı sürümü ikinci kez kabul etmez: her yeni gönderimde `<Version>` artmalı.
4. **Gizlilik politikasını yayımla.** `PRIVACY.md`'yi GitHub'a gönder ve `https://github.com/hasakobey/duzenleme/blob/main/PRIVACY.md` adresinin tarayıcıda açıldığını kontrol et (uygulamadaki Hakkında ve yapay zekâ bölümündeki bağlantı bu adrese gider). 2.2 paketi gönderilmeden önce çevrimiçi politika 2.2'yi (halka menü, hata günlüğü) anlatıyor olmalı.
5. **Gönderimi başlat.** Uygulama yayında olduğu için genel bakış sayfasında **Start update** (ilk gönderimde **Start submission** idi).
6. **Pricing and availability.** **Markets**: varsayılan (tüm pazarlar) kalabilir. **Base price**: **Free**. **Free trial** yok. **Discoverability** ve **Schedule** varsayılan.
7. **Properties.** **Category**: Productivity, **Secondary category**: Utilities + tools. Gizlilik sorusuna **Yes** ve gizlilik URL'si. **Website** ve **Support contact info** yukarıdaki tablodan. **Product declarations**: yalnızca gerçekten geçerli olan kutuları işaretle. İsteğe bağlı yapay zekâ simgesi nedeniyle (Store 11.16) canlı üretken yapay zekâ içeriği bildirimi istenirse işaretle ve yukarıdaki "Üretken yapay zekâ bildirimi" metnini kullan; uygulama içi bildirme yolu klasör simgesi penceresindeki **Uygunsuz içeriği bildir** bağlantısıdır. Üçüncü taraf ticaret kutusunu işaretleme (uygulama bir şey satmaz). Erişilebilirlik kutusunu yalnızca Ekran Okuyucusu, yüksek karşıtlık, klavye ve yüksek DPI ile denedikten sonra işaretle.
8. **Age ratings.** Anket 2.2'de değişmedi (satın alma, kullanıcılar arası paylaşım ya da yeni içerik türü yok); yeniden doldurman istenirse yukarıdaki önerilere göre doldur → **Save and generate**.
9. **Packages.** `.msixbundle` dosyasını sürükle-bırak; doğrulama (validation) bitene kadar bekle. Hata çıkarsa çoğunlukla kimlik değerleri ya da görünen ad Product identity ile uyuşmuyordur. **Device family availability**: Windows 10/11 Desktop. Önceki sürümün paketini kaldırma: yalnızca yenisi eklenir.
10. **Store listings.** Paket iki dil bildirdiği için İngilizce ve Türkçe listeler hazır gelir; her dili ayrı aç ve bu dosyadaki metinleri yapıştır (açıklama, özellikler, anahtar kelimeler ve **What's new in this version**: 2.2.0). **Store logos** bölümüne 300×300 PNG uygulama simgesi yüklemen önerilir (yüklemezsen paketteki simge kullanılır).
11. **Ekran görüntüleri** (her dil için ayrı yüklenir, açıklamalar dile göre):
    - En az 1 zorunlu, en fazla 10 masaüstü görüntüsü; Microsoft 4 ve üstünü (SSS'de 5–8) öneriyor. **9 tane** hazırla (2.2'de 9. görüntü halka menü); sahneler yukarıdaki "Ekran görüntüleri: ne çekilecek" bölümünde, sıra açıklamalarla aynı.
    - Türkçe liste Türkçe arayüzle, İngilizce liste İngilizce arayüzle çekilir (Store 10.7: her dildeki deneyim aynı olmalı): test örneğini `NESTDESK_LANG=tr` ya da `NESTDESK_LANG=en` ile aç.
    - Biçim: PNG, yatay, **en az 1366×768** (1920×1080 önerilir), en fazla 50 MB. Pencere kırpması 1366×768'den küçük kalacağı için tam ekran görüntüsü al.
    - Önemli içeriği üst üçte ikiye koy (alt üçte birin üstüne yazı binebilir); görüntülere ek logo ya da reklam yazısı ekleme.
    - Gerçek masaüstünü değil test örneğini kullan: `NestDesk.exe --desktop <geçici klasör> --data <geçici klasör>`, klasöre örnek dosyalar koy (PDF, Resimler…). Kişisel dosya adı, e-posta ya da API anahtarı görünmesin; sade bir duvar kâğıdı seç. İşin bitince örneği `--exit` ile kapat.
    - Test örneği gerçek masaüstü simgelerini gizlemez: Windows simgeleri arkada görünmesin diye (6. görüntü hariç) çekimden önce masaüstüne sağ tık → **Görünüm** → **Masaüstü simgelerini göster** işaretini kaldır, sonra geri aç.
12. **Submission options.** **Notes for certification** ve **Restricted capabilities** (runFullTrust) kutularına yukarıdaki İngilizce metinleri yapıştır. **Publishing hold options** varsayılan (sertifikasyondan geçince yayımlanır) ya da elle yayımlamak için **Don't publish this submission until I select Publish now**. **E-posta adresini doğrula:** Microsoft, kritik e-posta bildirimlerini alabilmen için adresin Action Center'da doğrulanmasını zorunlu tutuyor: Partner Center → **Action Center** → **My Preferences**. (Hesap sahibi bildirimleri her zaman alır; **Submission notification audience** ile başka kişi eklemek isteğe bağlı.)
13. **Gönder.** Genel bakış sayfasında **Submit for certification**. Durum sertifikasyon aşamasında görünür; sonuç Partner Center'daki Action Center'a ve (12. adımda doğruladığın) e-posta adresine gelir. Reddedilirse sertifikasyon raporundaki madde düzeltilip yeni gönderim yapılır.
14. **Yayından sonra.** Store bağlantısı (`https://apps.microsoft.com/detail/9PCQ9C4BBWKP`) README'de. Sonraki sürümlerde **Start update** ile yeni gönderim oluştur, **What's new in this version**'ı doldur ve yalnızca yeni `.msixbundle`'ı yükle.
