# Düzenleme

Windows için modern masaüstü düzenleyici. Masaüstüne düşen dosyaları **senin oluşturduğun klasörlere** kendiliğinden taşır, masaüstüne **saat**, **tarih** ve **klasör bölmesi** widget'ları ekler.

## Ne yapar?

- **Otomatik düzenleme:** Masaüstüne bir PDF indirdiğinde, masaüstünde `PDF` adında bir klasör varsa dosya oraya gider. Aynısı resimler, belgeler, arşivler, videolar ve müzik için de geçerli. Kuralları (klasör adı ↔ uzantılar) istediğin gibi değiştirebilirsin.
- **Senin yapına uyar:** Klasör masaüstünde yoksa dosyaya dokunmaz (istersen "Klasör yoksa oluştur" açılabilir). Klasör adında büyük/küçük harf ve Türkçe karakter fark etmez: `Arşivler` = `arsivler`.
- **Güvenli:** Kısayollar (`.lnk`), sistem/gizli dosyalar ve inmekte olan dosyalar (`.crdownload`, `.part`, `.tmp`) asla taşınmaz. İnen dosya, indirme bitince taşınır. Aynı adda dosya varsa `ad (1).pdf` olarak kaydedilir, hiçbir şeyin üzerine yazılmaz.
- **Geri alınabilir:** Her taşıma geçmişe yazılır. Geri aldığın dosya masaüstüne döner ve bir daha otomatik taşınmaz.
- **Widget'lar:** Saat (saniye isteğe bağlı), tarih (haftalık şeritli) ve Fences tarzı klasör bölmesi. Bölme, klasörün içeriğini masaüstünde gösterir; dosyaları üstüne sürükleyince klasöre taşır. Widget'lar masaüstüne yapışıktır, "Masaüstünü göster" (Win+D) ile kaybolmaz. Sağ tıkla: Cam / Koyu / Açık görünüm, konumu kilitle, kaldır.
- **Tepside çalışır:** Pencereyi kapatınca tepside çalışmaya devam eder. Tepsi menüsünden duraklat, şimdi düzenle, son taşımayı geri al.

## Kurulum

`powershell -File tools/publish.ps1` ile `dist/Duzenleme.exe` üret (ya da yayınlandıysa [Releases](../../releases) sayfasından indir). Tek dosyadır, .NET kurulumu gerekmez. Ayarlar'dan "Windows ile başlat"ı açabilirsin.

## Geliştirme

Gerekenler: Windows 10/11, .NET 8 SDK.

```powershell
dotnet test Duzenleme.sln                     # testler
dotnet run --project src/Duzenleme            # çalıştır (gerçek masaüstünü izler!)
dotnet run --project src/Duzenleme -- --desktop C:\tmp\Desktop --data C:\tmp\data   # test klasörüyle çalıştır
powershell -File tools/publish.ps1            # dist/Duzenleme.exe (tek dosya)
```

Ayarlar ve geçmiş `%AppData%\Duzenleme` altında (`settings.json`, `journal.json`) tutulur.
