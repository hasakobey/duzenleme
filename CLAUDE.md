# Düzenleme

Windows masaüstü düzenleyici (Fences benzeri): masaüstüne düşen dosyaları kullanıcının oluşturduğu klasörlere (ör. `PDF`) otomatik taşır; saat, tarih ve klasör bölmesi widget'ları sunar. .NET 8 WPF + WPF-UI.

## Mimari notları
- `Core/` UI'dan bağımsızdır ve testlenir; UI kodu `Core`'a yalnızca `AppHost` üzerinden erişir.
- İzleyici arka plan iş parçacığında çalışır ve `AppSettings.Rules` listesini okur. Kural eklerken/silerken listeyi yerinde değiştirme, yeni liste ata.
- Widget pencereleri Progman'a sahiplenir (Win+D'de kaybolmasın diye) ve `WM_WINDOWPOSCHANGING` ile en altta tutulur. Explorer yeniden başlarsa `WidgetManager` pencereleri yeniden açar.
- `WidgetView` sınıflarında `Wpf.Ui.Controls` isim alanını açma: `MenuItem`, `Image`, `TextBlock` WPF'inkilerle çakışır; gerekirse takma ad kullan.
- Masaüstü simgelerini gizlerken `Settings.IconsHiddenByApp` tutulur; çökme sonrası açılışta simgeler geri açılır. Çıkışta/oturum kapanırken de geri açılmalı.
- Klasör simgesi `SHGetSetFolderCustomSettings` ile uygulanır; `.ico` klasörün içinde gizli dosyadır ve her uygulamada yeni ad alır (Explorer önbelleği).
- Yapay zekâ simgesi: resmi `Anthropic` C# SDK'sı, `claude-opus-5` + sunucu tarafı yedek (`fallbacks: default`). Model çıktısı güvenilmez: SVG çizilmeden önce `AiIconGenerator.Sanitize` ile script/dış bağlantı temizlenir.
- `JsonFile.Load` okunamayan dosyayı `.bozuk-<zaman>` olarak yedekler; ayarlar sessizce kaybolmasın.

## Kurallar
- Dosya yalnızca hedef klasör masaüstünde **zaten varsa** taşınır ("yoksa oluştur" ayarı varsayılan kapalı).
- `.lnk`, `.url`, `desktop.ini`, gizli/sistem dosyaları, klasörler ve yarım indirmeler asla taşınmaz.
- Her taşıma günlüğe yazılır ve geri alınabilir olmalı; geri alınan dosya bir daha otomatik taşınmaz.
- Uygulamayı denerken gerçek masaüstünü değil `--desktop <geçici klasör> --data <geçici klasör>` kullan.
- Kullanıcıya dönük metinler Türkçe.
