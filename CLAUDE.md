# Düzenleme

Windows masaüstü düzenleyici (Fences benzeri): masaüstüne düşen dosyaları kullanıcının oluşturduğu klasörlere (ör. `PDF`) otomatik taşır; saat, tarih ve klasör bölmesi widget'ları sunar. .NET 10 WPF + WPF-UI.

## Mimari notları
- `Core/` UI'dan bağımsızdır ve testlenir; UI kodu `Core`'a yalnızca `AppHost` üzerinden erişir.
- İzleyici arka plan iş parçacığında çalışır ve `AppSettings.Rules` listesini okur. Kural eklerken/silerken listeyi yerinde değiştirme, yeni liste ata.
- Widget pencereleri Progman'a sahiplenir (Win+D'de kaybolmasın diye) ve `WM_WINDOWPOSCHANGING` ile en altta tutulur. Explorer yeniden başlarsa `WidgetManager` pencereleri yeniden açar.
- `WidgetView` sınıflarında `Wpf.Ui.Controls` isim alanını açma: `MenuItem`, `Image`, `TextBlock` WPF'inkilerle çakışır; gerekirse takma ad kullan.
- Masaüstü simgelerini gizlerken `Settings.IconsHiddenByApp` tutulur; çökme sonrası açılışta simgeler geri açılır. Çıkışta/oturum kapanırken de geri açılmalı.
- Klasör simgesi `SHGetSetFolderCustomSettings` ile uygulanır; `.ico` klasörün içinde gizli dosyadır ve her uygulamada yeni ad alır (Explorer önbelleği).
- Yapay zekâ simgesi: resmi `Anthropic` C# SDK'sı, `claude-opus-5` + sunucu tarafı yedek (`fallbacks: default`). Model çıktısı güvenilmez: SVG çizilmeden önce `AiIconGenerator.Sanitize` ile script/dış bağlantı temizlenir.
- `JsonFile.Load` okunamayan dosyayı `.bozuk-<zaman>` olarak yedekler; ayarlar sessizce kaybolmasın.

## Derleme / paketleme
- .NET 10 (`global.json`). SDK bu makinede kullanıcı başına `%LOCALAPPDATA%\Microsoft\dotnet` altında; Program Files'taki `dotnet` yalnızca SDK 8 içerir. Yeni kabuklarda PATH'in başına o klasörü ekle (`tools/publish.ps1` ve `.claude/hooks/build-check.ps1` bunu kendisi yapar).
- `tools/publish.ps1` → `dist/` (Inno Setup ile tek kurulum + mimari başına taşınabilir zip). Kurulum betiği `tools/installer/Duzenleme.iss`; `.iss` ve `.ps1` dosyaları **BOM'lu UTF-8** olmalı, yoksa Türkçe karakterler bozulur.
- Bu makinede Smart App Control açık: imzasız yeni derlemeler ara sıra "Uygulama Denetimi ilkesi" ile engellenir (CodeIntegrity günlüğünde 3033/3077). Debug derlemesi bu yüzden deterministik değil: engellenirse yeniden derlemek farklı bir dosya üretir.
- UI otomasyon testlerinde ana monitör kullanıcının gerçek masaüstüdür: sürükleme/tıklama yalnızca test monitöründe ve `--desktop/--data` ile açılan örnekte yapılır. Kurulu sürüm de aynı süreç adıyla çalışır; süreçleri yoluna göre ayırt et.
- Widget pencereleri masaüstüne sahipli olduğundan Windows onları `HWND_TOPMOST` yapmaz; öne getirmek için sahiplik geçici olarak kaldırılır (`WidgetWindow.Reveal`).
- Widget'lar `WS_EX_NOACTIVATE`: WPF, ölçeği farklı monitöre geçişte `SWP_NOACTIVATE` olmadan `SetWindowPos` çağırıp odağı çalar (WM_WINDOWPOSCHANGING'de bayrak eklemek işe yaramaz). Tıklamayla etkinleşme `WM_MOUSEACTIVATE → MA_ACTIVATE` ile serbest; klavye için `ActivateForInput`. Pencere en baştan kayıtlı monitörde oluşturulur.
- Konum/boyut yalnızca kullanıcı eylemlerinde (sürükleme, boyutlandırma, yerleştirme) kaydedilir; `LocationChanged`'e bağlanma (monitör çıkınca Windows'un kaydırması düzeni bozar). Ekran değişince `RestoreSavedPosition`.
- Yeni widget yeri (`WidgetManager.FreeSpot`) imlecin monitöründe, fiziksel pikselle hesaplanır.
- Bölme/kutu öğe menüsü `Menus.AttachItemMenu` ile bağlanır: boş alana sağ tık widget menüsüne düşmeli (ListBox'a `ContextMenu` verme). Simgeler `ShellIcons.Request` ile arka plandaki STA iş parçacığında yüklenir.
- Test monitörü (solda) %125 ölçekli: test ayarlarındaki DIP konumları fiziksel/1.25 olmalı.
- `--desktop` ile çalışan test örneği gerçek masaüstü simgelerini gizlemez/açmaz (`AppHost.IsTestDesktop`); kullanıcı o an fareyi kullanıyorsa gerçek fareyle test yapma, mesaj göndererek (ör. WM_MOUSEACTIVATE) doğrula.
- "Masaüstünü bölmeler yönetsin" (`FencesReplaceIcons`): Windows simgeleri gizlenir; Klasörler+Kısayollar+Dosyalar (ya da Tümü) bölmesi her zaman bulunmalı (`EnsureDesktopCoverage`), yoksa mod kapanır (`EnsureNothingInvisible`).
- Yeni widget'lar imlecin monitörüne yerleşir: test örneğinde imleç kullanıcının ekranındaysa test widget'ı onun masaüstüne düşer. Testte `DUZENLEME_QUICKADD_AT` ile "Widget ekle" penceresini test monitörüne aç, düğmelere UI Automation (InvokePattern) ile bas; iş bitince test örneğini `--exit` ile kapat.
- Yerleşim kuralları (mıknatıs, çakışma çözme, büyütürken duvar) `Core/WidgetLayout.cs`'te saf geometri olarak durur ve testlenir; `WidgetWindow` kart dikdörtgenini gölge payını çıkararak (`CardBox`) verir.
- Widget'lar arası sıra `WidgetConfig.Z`; hepsi en alta itildiği için `ApplyZOrder` en öndekinden başlayarak sırayla en alta gönderir.
- "Masaüstüne çift tıkla" yalnızca kök penceresi (`GetAncestor(GA_ROOT)`) Progman ya da DefView taşıyan WorkerW olan tıklamalarda çalışır: Gezgin pencereleri ve dosya iletişim kutuları da `SHELLDLL_DefView` içerir (eskiden klasörde her çift tıklama widget'ları gizleyip açıyordu).
- Explorer'ın pencerelerine UI iş parçacığından eşzamanlı çağrı yapma (Explorer meşgulken widget'lar donar): `ShowWindowAsync`, `SendMessageTimeout`; öğe açma (`TileItem.Launch`) arka planda.
- Widget'ta fare üstüne gelince yerleşim değişmemeli: başlıktaki × hep yerinde (sönük) durur; öğe kaldırma sağ tık menüsündedir.
- Microsoft Store sürümü (MSIX, `tools/package-msix.ps1` → `dist-store/`, kimlik `tools/store/identity.json`): paketliyken (`PackageInfo.IsPackaged`) HKCU yazmaları ve %AppData%'ya yeni yazılan dosyalar pakete özel yere gider. Explorer'ın okuması gereken kayıt defteri değerlerine (Run, HideDesktopIcons) paketliyken yazma; başlangıç `StartupTask` (TaskId `DuzenlemeStartup`, `WinRt.cs` ile projeksiyonsuz COM). TargetFramework'e Windows SDK sürümü ekleme (WinRT projeksiyonu ~25 MB).
- Yapay zekâ istemcisi `AiIconGenerator.CreateClient` ile kurulur (SDK'nın ANTHROPIC_* ortam değişkenlerini yok sayar); her SVG `ToDrawing` içinde temizlenir (dış `url()`/DTD çizici tarafından ağa istek attırır). Gizlilik sözü PRIVACY.md'de: yeni bir ağ kullanımı eklersen orayı ve Store metinlerini (`tools/store/listing.md`) güncelle.

## Kurallar
- Dosya yalnızca hedef klasör masaüstünde **zaten varsa** taşınır ("yoksa oluştur" ayarı varsayılan kapalı).
- `.lnk`, `.url`, `desktop.ini`, gizli/sistem dosyaları, klasörler ve yarım indirmeler asla taşınmaz.
- Her taşıma günlüğe yazılır ve geri alınabilir olmalı; geri alınan dosya bir daha otomatik taşınmaz.
- Uygulamayı denerken gerçek masaüstünü değil `--desktop <geçici klasör> --data <geçici klasör>` kullan.
- Kullanıcıya dönük metinler Türkçe.
