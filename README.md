# Düzenleme

Windows için modern masaüstü düzenleyici. Masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** kendiliğinden taşır; masaüstüne saat, tarih, not, kısayol kutusu ve klasör bölmesi widget'ları ekler; klasörlerine şık simgeler verir.

Stardock Fences, iTop Easy Desktop, SlideSlide, ViPad ve LaunchBar Commander'ın öne çıkan özelliklerinden esinlenildi.

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
| **Klasör bölmesi** | Bir klasörü masaüstünde panel olarak gösterir; üstüne sürüklenen dosyayı klasöre taşır; başlığa çift tıklayınca katlanır; simge/liste görünümü, sıralama (Fences) |
| **Kısayol kutusu** | Sekmeli uygulama/dosya rafı; sürükle-bırakla ekle, tek tıkla aç, yönetici olarak çalıştır (ViPad + LaunchBar Commander) |

Her widget'a sağ tıkla: Cam/Koyu/Açık görünüm, 5 vurgu rengi, 4 boyut, saydamlık, konumu kilitle, çoğalt. Widget düzenlerini ad vererek kaydedip tek tıkla geri dönebilirsin ("İş", "Oyun"…).

### Masaüstü
- **Hızlı gizle:** boş masaüstüne çift tıkla ya da `Ctrl+Alt+H` → simgeler (ve istersen widget'lar) gizlenir (Fences / iTop).
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

## Kurulum

`powershell -File tools/publish.ps1` ile `dist/Duzenleme.exe` üret (ya da yayınlandıysa [Releases](../../releases) sayfasından indir). Tek dosyadır, .NET kurulumu gerekmez. Ayarlar'dan "Windows ile başlat"ı açabilirsin.

**Taşınabilir kullanım:** exe'nin yanına boş bir `portable.txt` koyarsan ayarlar exe'nin yanındaki `data` klasöründe tutulur (USB bellekte taşınabilir; LaunchBar Commander gibi).

> Smart App Control açık bilgisayarlarda imzasız yeni sürümler ilk çalıştırmada engellenebilir.

## Geliştirme

Gerekenler: Windows 10/11, .NET 8 SDK.

```powershell
dotnet test Duzenleme.sln                     # testler
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # test klasörüyle çalıştır
dotnet run --project src/Duzenleme            # gerçek masaüstünü izler!
powershell -File tools/publish.ps1            # dist/Duzenleme.exe (tek dosya)
```

Geliştirme yardımcıları: `--export-icon-sheet out.png` (simge kütüphanesi önizlemesi), `--render-svg in.svg out.png` (SVG'yi yapay zekâ çıktısıyla aynı temizleme/çizim yolundan geçirir, yanına `.ico` yazar).

Ayarlar ve geçmiş `%AppData%\Duzenleme` altında (`settings.json`, `journal.json`, `ai-icons/`) tutulur.
