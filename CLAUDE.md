# Düzenleme

Windows masaüstü düzenleyici (Fences benzeri): masaüstüne düşen dosyaları kullanıcının oluşturduğu klasörlere (ör. `PDF`) otomatik taşır; saat ve tarih widget'ları sunar.

## Yapı
- `src/Duzenleme` — .NET 8 WPF uygulaması, arayüz için [WPF-UI](https://wpfui.lepo.co/) (Fluent, Mica).
  - `Core/` — kural motoru, masaüstü izleyici, dosya taşıma, geri alma günlüğü, ayarlar (UI'dan bağımsız, test edilebilir).
  - `Widgets/` — masaüstüne sabitlenen çerçevesiz saat/tarih pencereleri.
  - `Views/` — ana pencere ve sayfalar.
- `tests/Duzenleme.Tests` — xUnit (`net8.0-windows`).

## Komutlar
- Derleme: `dotnet build Duzenleme.sln`
- Test: `dotnet test Duzenleme.sln`
- Çalıştırma: `dotnet run --project src/Duzenleme`

## Kurallar
- Dosya yalnızca hedef klasör masaüstünde **zaten varsa** taşınır ("yoksa oluştur" ayarı varsayılan kapalı).
- `.lnk`, `.url`, `desktop.ini`, gizli/sistem dosyaları, klasörler ve yarım indirmeler (`.crdownload`, `.tmp`, `.part`) asla taşınmaz.
- Her taşıma günlüğe yazılır ve geri alınabilir olmalı.
- Geliştirme/test sırasında gerçek masaüstü yerine geçici bir test klasörü izlenir; gerçek masaüstünde çalıştırmadan önce kullanıcıya sorulur.
- Kullanıcıya dönük metinler Türkçe.
- `.claude/hooks/build-check.ps1`: .cs/.xaml/.csproj düzenlemesinden sonra otomatik `dotnet build` çalıştırır.
