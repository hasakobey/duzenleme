using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duzenleme.Core;

public enum WidgetKind { Clock, Date, Fence, Note, Launcher }

public enum WidgetStyle { Dark, Light, Glass }

public enum WidgetAccent { Violet, Blue, Green, Orange, Pink }

public enum NoteColor { Yellow, Pink, Green, Blue, Purple, Graphite }

public enum IconSize { Small, Medium, Large, ExtraLarge }

public enum ItemView { Icons, List }

public enum FenceSort { Newest, Name, Type }

/// <summary>Bölmedeki simgelerin satır içi hizası.</summary>
public enum TileAlign { Left, Center, Right }

/// <summary>Simgeler arası boşluk.</summary>
public enum TileSpacing { Compact, Normal, Wide }

public enum LabelSize { Small, Normal, Large }

public enum CornerStyle { Round, Soft, Square }

/// <summary>Bölme bir klasörü değil masaüstündeki öğeleri gösteriyorsa hangilerini.</summary>
/// <remarks>Tanınmayan (daha yeni sürümün) değer "Tümü" okunur: hiçbir masaüstü öğesi görünmez kalmasın.</remarks>
[EnumFallback(All)]
public enum DesktopFilter { None, All, Folders, Shortcuts, Files }

public enum AppTheme { System, Dark, Light }

public sealed class LauncherTab
{
    // Kalıcı varsayılan: sekme oluşturulurken bir kez o anki dilde yazılır.
    public string Name { get; set; } = L.T("Uygulamalar");
    public List<string> Items { get; set; } = [];

    // 2.1 P6
    /// <summary>Daha yeni sürümün yazdığı bilinmeyen alanlar (aynen geri yazılır).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    // 2.2 Halka
    /// <summary>
    /// Yol olmayan öğeler (web adresi, komut, tuş kısayolu, metin, NestDesk eylemi); <see cref="Items"/> yalnızca yol kalır.
    /// Sıra <see cref="LauncherAction.PathsBefore"/> ile tutulur (bkz. <see cref="LauncherEntries"/>). 2.1 bu alanı tanımaz,
    /// <see cref="Extra"/> içinde aynen geri yazar; 2.0 kaydederse düşer ama ayarlar bozulmaz. Boşken yazılmaz.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LauncherAction>? Actions { get; set; }
}

/// <summary>
/// Halka menüdeki (ve kutudaki) yol olmayan öğe. Tür metindir, enum değil (<see cref="RingActionTypes"/>): bu sürümün
/// tanımadığı tür korunur ve çalıştırılmaz. Boş alanlar yazılmaz.
/// </summary>
public sealed class LauncherAction
{
    /// <summary>Kutu içinde öğenin kimliği (8 onaltılık hane; sürükleme, menü ve liste farkı için).</summary>
    public string Id { get; set; } = NewId();

    /// <summary>"url", "command", "keys", "text", "app"; tanınmayan tür korunur.</summary>
    public string Type { get; set; } = "";

    /// <summary>Sekmede bu öğeden önce gelen yol (Items) sayısı; fazlası sona kısılır (bkz. <see cref="LauncherEntries"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PathsBefore { get; set; }

    /// <summary>Görünen ad; null ise türün varsayılanı.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>Simge (<see cref="IconRef"/> metni); null ise türün varsayılanı.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    /// <summary>url: mutlak, file: ve UNC olmayan adres (bkz. <see cref="RingActions.CheckUrl"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    /// <summary>command: çalıştırılacak program ya da komut (yalnızca kullanıcıdan gelir).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Args { get; set; }

    /// <summary>command: çalışma klasörü.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dir { get; set; }

    /// <summary>command: konsol penceresi gizlensin (öğe başına açık seçim; varsayılan görünür).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HideWindow { get; set; }

    /// <summary>command: yönetici olarak ("runas"; Windows her seferinde sorar).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Admin { get; set; }

    /// <summary>İçe aktarıldı ve henüz onaylanmadı: gösterilir ama çalışmaz.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Disabled { get; set; }

    /// <summary>keys: en çok <see cref="RingActions.MaxChords"/> tuş birleşimi ("Ctrl+Shift+T", "F5").</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Keys { get; set; }

    /// <summary>text: yapıştırılacak metin (en çok <see cref="RingActions.MaxTextLength"/> karakter). Günlüğe asla yazılmaz.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>app: NestDesk eyleminin kendi kimliği (<see cref="RingAppIds"/>; HotkeyAction adı değil).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? App { get; set; }

    /// <summary>Daha yeni sürümün yazdığı bilinmeyen alanlar (aynen geri yazılır).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

public sealed class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public WidgetKind Kind { get; set; }
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;

    /// <summary>
    /// Sol üst köşe, fiziksel piksel. Farklı ölçekli (DPI) monitörlerde DIP konumu kayar;
    /// geri yüklemede önce bu kullanılır.
    /// </summary>
    public int? PixelLeft { get; set; }
    public int? PixelTop { get; set; }
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public WidgetStyle Style { get; set; } = WidgetStyle.Glass;
    public WidgetAccent Accent { get; set; } = WidgetAccent.Violet;

    /// <summary>İçerik ölçeği (0.5 – 2.5; menü ya da Ctrl + fare tekerleği).</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Pencere saydamlığı (0.4 – 1).</summary>
    public double Opacity { get; set; } = 1.0;

    public bool Locked { get; set; }

    /// <summary>Bölmede gösterilmeyen öğeler (yol ya da "::{CLSID}"). Yalnızca gizlenir; dosyalara dokunulmaz.</summary>
    public List<string> HiddenItems { get; set; } = [];

    /// <summary>Kullanıcının kapattığı widget parçaları (ör. "count", "search", "greeting", "week").</summary>
    public List<string> HiddenParts { get; set; } = [];

    /// <summary>Parça görünüyor mu?</summary>
    public bool Shows(string part) => !HiddenParts.Contains(part);

    /// <summary>Widget'lar arasındaki sıra: büyük olan öndedir (son tıklanan/eklenen). Hepsi masaüstü katmanında kalır.</summary>
    public long Z { get; set; }

    public CornerStyle Corners { get; set; } = CornerStyle.Round;
    public bool Shadow { get; set; } = true;

    /// <summary>Fare üstünde değilken soluk durur (dikkat dağıtmasın).</summary>
    public bool FadeUntilHover { get; set; }

    /// <summary>Bölme/kutu fare çekilince başlığa katlanır, üzerine gelince açılır.</summary>
    public bool AutoRollup { get; set; }

    /// <summary>Bölme/kutu yalnızca başlığa katlanmış mı?</summary>
    public bool Collapsed { get; set; }

    /// <summary>Kullanıcının verdiği başlık; boşsa varsayılan kullanılır.</summary>
    public string? Title { get; set; }

    // Saat
    public bool ShowSeconds { get; set; }

    // Bölme
    public string? FolderName { get; set; }

    /// <summary>None değilse bölme <see cref="FolderName"/> yerine masaüstündeki bu türden öğeleri gösterir.</summary>
    public DesktopFilter Filter { get; set; }
    public FenceSort Sort { get; set; } = FenceSort.Newest;

    /// <summary>Bölmede öğeler tek tıkla açılsın (varsayılan: çift tık, masaüstü gibi).</summary>
    public bool SingleClick { get; set; }

    // Bölme ve kısayol kutusu
    public IconSize IconSize { get; set; } = IconSize.Medium;
    public ItemView View { get; set; } = ItemView.Icons;
    public TileAlign Align { get; set; } = TileAlign.Left;
    public TileSpacing Spacing { get; set; } = TileSpacing.Normal;
    public LabelSize LabelSize { get; set; } = LabelSize.Normal;
    public bool HideLabels { get; set; }

    /// <summary>Resim, video ve PDF'lerde simge yerine küçük önizleme.</summary>
    public bool ShowPreviews { get; set; }

    // Not
    public string NoteText { get; set; } = "";
    public NoteColor NoteColor { get; set; } = NoteColor.Yellow;

    /// <summary>Not onay kutulu liste (Yapılacaklar) olarak gösterilsin. Maddeler NoteText'te satır satır "☐ " / "☑ " önekiyle
    /// tutulur: bu alanı tanımayan eski sürüm listeyi okunur düz not olarak gösterir ve metni korur.</summary>
    public bool NoteChecklist { get; set; }

    // Kısayol kutusu
    public List<LauncherTab> Tabs { get; set; } = [];
    public int ActiveTab { get; set; }

    public WidgetConfig Clone() =>
        JsonSerializer.Deserialize<WidgetConfig>(JsonSerializer.Serialize(this, JsonFile.Options), JsonFile.Options)!;

    // 2.1 P3
    /// <summary>
    /// Kısayol kutusu: masaüstünden taşınan öğelerin klasörünün adı (NestDesk klasörünün altında). İlk taşımada başlıktan
    /// üretilir ve sonra değişmez; başlık değişse de öğeler aynı klasörde kalır.
    /// </summary>
    public string? BoxFolder { get; set; }

    // 2.1 P5 — ad ve simge
    // (Metin ve sözlük: eski sürüm bilmediği alanları yok sayar. Kalıcı enum'a üye eklenmedi. Boşken yazılmaz.)

    /// <summary>
    /// Başlık simgesi (<see cref="IconRef"/>: "sym:Games24", "res:yol,sıra", "img:dosya"); boş ya da tanınmıyorsa türün
    /// varsayılanı (<c>WidgetIcons.For</c>). Başlıkta yalnızca "sym:" çizilir; diğerleri varsayılana düşer.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    /// <summary>
    /// Kısayol kutusu: öğe başına görünen ad ve simge; anahtar öğenin yolu (büyük/küçük harf duyarsız aranır, bkz.
    /// <see cref="ItemLooks"/>). Dosyaya dokunmaz. Öğe taşınınca ya da yeniden adlandırılınca anahtar da taşınır.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ItemLook>? ItemLooks { get; set; }

    // 2.1 P6 — yeni widget'lar ve küçük eklemeler
    // Hepsi yeni metin/sayı/bool ya da yeni küçük sınıf: 2.0 bilmediği alanı yok sayar ve temel türü (Kind) gösterir.
    // Kalıcı enum'a üye eklenmedi (bkz. WidgetVariants). Boş/varsayılan değerler yazılmaz: her widget'a on dört boş alan
    // eklenip ayar dosyası (her kayıtta baştan yazılır) büyümesin.

    /// <summary>
    /// Alt tür (<see cref="WidgetVariants"/>): null = klasik widget. Kind her zaman bilinen beş türden biridir; eski sürüm
    /// Variant'ı tanımaz ve temel türü gösterir. Metin olduğu için daha yeni bir sürümün alt türü burada korunur.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Variant { get; set; }

    /// <summary>
    /// Bölme sırası, <see cref="Sort"/>'u ezer: "size" (büyük üstte), "oldest" (eski üstte), "manual" (elle; bkz.
    /// <see cref="ItemOrder"/>); null = Sort. Sort 2.0 için en yakın değerde tutulur (FenceSort'a üye eklenmez).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SortBy { get; set; }

    /// <summary>Elle sıralı bölmede öğelerin sırası (yol); listede olmayan yeni öğeler sona eklenir.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ItemOrder { get; set; }

    /// <summary>Yapılacaklar: işaretlenen madde listenin altına iner (açık maddeler üstte kalır).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ChecklistDoneLast { get; set; }

    /// <summary>Saat ve dünya saati: null = Windows'un bölge ayarı, true = 12 saat (ÖÖ/ÖS), false = 24 saat.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Clock12Hour { get; set; }

    /// <summary>
    /// Klasör portalı (FolderName tam yol): Windows'un bilinen klasörü ("Downloads", "Documents"…). Kayıtlı yol yoksa
    /// (klasör taşındı, OneDrive'a yönlendi) yeniden bulunur ve FolderName güncellenir.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FolderKnownId { get; set; }

    /// <summary>Takvim: haftanın ilk günü (0 = Pazar, 1 = Pazartesi); null = arayüz dilinin kültürü.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FirstDayOfWeek { get; set; }

    /// <summary>Geri sayım: hedef gün (yalnızca tarih). Başlık etkinliğin adıdır.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? TargetDate { get; set; }

    /// <summary>Geri sayım her yıl yinelenir (doğum günü): gün geçince bir sonraki yıla sayar.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CountdownYearly { get; set; }

    /// <summary>Zamanlayıcı / Pomodoro / kronometre durumu (UTC; yalnızca başlat, duraklat, sıfırla ve aşama sonunda kaydedilir).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimerState? Timer { get; set; }

    /// <summary>Dünya saati: gösterilen saat dilimleri (Windows kimlikleriyle), sırasıyla.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WorldZone>? Zones { get; set; }

    /// <summary>Sistem durumu: yenileme aralığı, saniye (2/3/5/10; null = 3).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StatusIntervalSeconds { get; set; }

    /// <summary>Sistem durumu: boş alanı gösterilen sürücü ("C:\"); null = Windows'un sürücüsü.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDrive { get; set; }

    /// <summary>Daha yeni bir sürümün yazdığı, bu sürümün bilmediği alanlar: okunur ve aynen geri yazılır (kaybolmaz).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    // 2.2 Tema
    // Metin alanları: 2.1 tanımaz, Extra'da aynen geri yazar ve en yakın Style/Accent'i gösterir (tema ya da özel renk
    // seçilince ikisi de en yakın değere yazılır, bkz. WidgetLooks). Kalıcı enum'a üye eklenmedi. Boşken yazılmaz.

    /// <summary>
    /// Widget teması (<see cref="ThemePresets"/>): null = klasik (<see cref="Style"/> + <see cref="Accent"/>), "auto" = Koyu/Açık
    /// Windows'a uyar, "neon", "gece", "kagit", "okyanus". Tanınmayan ad klasik çizilir ve korunur. Yüksek karşıtlıkta yok sayılır.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LookTheme { get; set; }

    /// <summary>
    /// Özel vurgu rengi, yalnızca "#RRGGBB" (adlı renkler <see cref="Accent"/>'te); null = <see cref="Accent"/>. Zeminde okunur
    /// kalsın diye çizerken gerekirse açılır ya da koyulaştırılır (<see cref="ThemeMath.EnsureContrast"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LookAccentColor { get; set; }

    // 2.2 Kutu
    // 2.1 tanımaz, Extra'da aynen geri yazar. Kapalıyken yazılmaz.

    /// <summary>
    /// Bölme ve kısayol kutusu: 1–9 (sayısal tuş takımı da) görünen n'inci öğeyi açar (<see cref="Duzenleme.Core.QuickKeys"/>). Varsayılan
    /// kapalı: harfle arama rakamla başlayan adlarda da çalışsın. Rozet yok; ipucu Simgeler menüsünde.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool QuickKeys { get; set; }

    // 2.2 halka görünümü
    // Metin: 2.1 tanımaz, Extra'da aynen geri yazar. Klasik'te (null) yazılmaz.

    /// <summary>
    /// Halka menünün biçimi (<see cref="RingStyles"/>): null = Klasik, "orbit" (Daire), "labels" (Etiketli), "glow" (Işıltı).
    /// Tanınmayan ad Klasik çizilir ve korunur; yüksek karşıtlıkta yok sayılır.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RingStyle { get; set; }
}

/// <summary>Zamanlayıcı widget'ının kalıcı durumu. Zamanlar UTC: saat ve saat dilimi değişse de süre doğru kalır.</summary>
public sealed class TimerState
{
    /// <summary>"countdown" (zamanlayıcı), "pomodoro" ya da "stopwatch" (kronometre); bkz. <see cref="TimerModes"/>.</summary>
    public string Mode { get; set; } = TimerModes.Countdown;

    /// <summary>Zamanlayıcının süresi (dakika).</summary>
    public int Minutes { get; set; } = 10;

    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;

    /// <summary>Uzun moladan önceki odak turu sayısı.</summary>
    public int RoundsBeforeLong { get; set; } = 4;

    /// <summary>Çalışırken bitiş anı (zamanlayıcı, pomodoro).</summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>Duraklatılmışken kalan süre (tick); null = aşamanın tam süresi.</summary>
    public long? RemainingTicks { get; set; }

    /// <summary>Kronometre çalışırken son başlatma anı.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>Kronometrenin duraklatmadan önce biriken süresi (tick).</summary>
    public long ElapsedTicks { get; set; }

    /// <summary>Pomodoro: kaçıncı odak turu (1'den başlar).</summary>
    public int Round { get; set; } = 1;

    /// <summary>Pomodoro aşaması: "focus", "break", "long".</summary>
    public string Phase { get; set; } = TimerModes.Focus;

    /// <summary>Aşama bitince sonraki kendiliğinden başlasın.</summary>
    public bool AutoStartNext { get; set; }

    /// <summary>Süre dolunca bildirim gösterilsin (dosya taşıma bildirimlerinden bağımsız).</summary>
    public bool Notify { get; set; } = true;

    /// <summary>Son bitişin anı ("Süre doldu · 14:32"); yeniden başlatınca ya da sıfırlayınca silinir.</summary>
    public DateTime? FinishedUtc { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Dünya saatinde bir satır: Windows saat dilimi kimliği ve isteğe bağlı ad (boşsa şehir tablosundan).</summary>
public sealed class WorldZone
{
    public string Id { get; set; } = "";
    public string? Label { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Kısayol kutusundaki bir öğenin kullanıcının verdiği adı ve simgesi (boş olan varsayılandır).</summary>
public sealed class ItemLook
{
    /// <summary>Görünen ad; null ise dosyanın adı.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>Simge (<see cref="IconRef"/>); null ise dosyanın kendi simgesi.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    /// <summary>Ad ve simge yok; daha yeni sürümün yazdığı alan (<see cref="Extra"/>) da yoksa kayıt silinebilir.</summary>
    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Icon) && Extra is not { Count: > 0 };

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Kaydedilmiş widget düzeni (Fences'taki düzen anlık görüntüleri gibi).</summary>
public sealed class LayoutSnapshot
{
    public string Name { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public List<WidgetConfig> Widgets { get; set; } = [];

    public static LayoutSnapshot Capture(string name, IEnumerable<WidgetConfig> widgets) =>
        new() { Name = name, Widgets = widgets.Select(w => w.Clone()).ToList() };

    /// <summary>Uygulanacak kopyalar: anlık görüntü sonradan değişmesin diye her seferinde yeniden kopyalanır.</summary>
    public List<WidgetConfig> Restore() => Widgets.Select(w => w.Clone()).ToList();

    // 2.1 P6
    /// <summary>Daha yeni sürümün yazdığı bilinmeyen alanlar (aynen geri yazılır).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class AppSettings
{
    public List<Rule> Rules { get; set; } = Rule.Defaults();

    /// <summary>Hedef klasör masaüstünde yoksa oluşturulsun mu? Varsayılan: hayır, kullanıcının yapısına uyulur.</summary>
    public bool CreateMissingFolders { get; set; }

    public bool Paused { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Karşılama tamamlandı ya da atlandı. (1.x'te "ilk açılış yapıldı": mevcut kullanıcılarda zaten true, karşılamayı görmezler.)</summary>
    public bool FirstRunDone { get; set; }

    /// <summary>"Düzenleme artık NestDesk" balonu gösterildi mi? Yeni kullanıcıda ilk açılışta gösterilmeden true yapılır.</summary>
    public bool RenameNoticeShown { get; set; }

    /// <summary>Ana pencere ilk kapatıldığında "arka planda çalışıyor" balonu gösterildi mi?</summary>
    public bool CloseToTrayHintShown { get; set; }

    public List<WidgetConfig> Widgets { get; set; } = [];

    /// <summary>Boş masaüstüne çift tıklamak simgeleri gizler/gösterir.</summary>
    public bool DoubleClickHidesDesktop { get; set; } = true;

    /// <summary>Çift tıklayınca gizlenen masaüstünün nasıl geri geleceğini anlatan ipucu kaç kez gösterildi.</summary>
    public int DoubleClickHintsShown { get; set; }

    /// <summary>Simgeler gizlenirken widget'lar da gizlensin.</summary>
    public bool HideWidgetsWithIcons { get; set; } = true;

    /// <summary>
    /// Masaüstünü bölmeler yönetir: Windows'un kendi masaüstü simgeleri gizlenir, öğeler yalnızca bölmelerde görünür
    /// (Fences gibi). Dosyalara dokunulmaz; kapatınca ya da uygulamadan çıkınca simgeler geri gelir.
    /// </summary>
    public bool FencesReplaceIcons { get; set; }

    /// <summary>Sürüklerken widget'lar birbirinin ve ekranın kenarına yapışır (alt alta/yan yana dizmek kolay).</summary>
    public bool SnapWidgets { get; set; } = true;

    /// <summary>Bırakılan ya da büyütülen widget başka bir widget'ın üstüne binmez.</summary>
    public bool PreventOverlap { get; set; } = true;

    /// <summary>Simgeleri biz gizlediysek, çıkışta/çökmeden sonra geri açabilmek için.</summary>
    public bool IconsHiddenByApp { get; set; }

    public HotkeySettings Hotkeys { get; set; } = new();
    public List<LayoutSnapshot> Layouts { get; set; } = [];

    /// <summary>Masaüstünde yeni klasör oluşturulunca simge önerisi göster.</summary>
    public bool SuggestFolderIcons { get; set; } = true;

    /// <summary>Yapay zekâ simge üretimi için DPAPI ile şifrelenmiş API anahtarı (isteğe bağlı).</summary>
    public string? AiKeyProtected { get; set; }

    // 2.1 P2b Yerelleştirme
    /// <summary>
    /// Arayüz dili: null = Windows ile aynı, "tr", "en". Yeniden başlatınca uygulanır (L.Init). Enum değil metin: eski
    /// sürüm tanımadığı özelliği yok sayar, yeni diller için enum'a üye eklemek gerekmez.
    /// </summary>
    public string? Language { get; set; }
    // 2.1 P3 — masaüstü kipleri, göz atma, yeni widget'ın yeri, kutulara taşınan öğeler
    // (Hepsi yeni bool/int/metin: eski sürüm bilmediği alanları yok sayar. Kalıcı enum'a üye eklenmedi.)

    /// <summary>
    /// Boş masaüstüne çift tıklayınca: null/"auto" (bölmeler masaüstünü yönetirken göz at, yoksa gizle/göster), "toggle",
    /// "peek", "none" (bkz. <see cref="DesktopState.DoubleClickChoice"/>). Boşsa eski <see cref="DoubleClickHidesDesktop"/>'a bakılır.
    /// </summary>
    public string? DoubleClickAction { get; set; }

    /// <summary>Windows masaüstüne göz atarken widget'lar da çekilsin (altlarındaki simgelere ulaşılsın).</summary>
    public bool PeekHidesWidgets { get; set; } = true;

    /// <summary>Göz atarken açık pencereler küçültülsün (Win+D gibi; çift tıklamayla başlayınca yapılmaz).</summary>
    public bool PeekShowsDesktop { get; set; }

    /// <summary>Göz atma kaç dakika sonra kendiliğinden biter (1/2/5/10; 0 = ben dönene dek).</summary>
    public int PeekMinutes { get; set; } = DesktopState.DefaultPeekMinutes;

    /// <summary>Çift tıklamayla göz atmanın nasıl biteceğini anlatan ipucu kaç kez gösterildi.</summary>
    public int PeekHintsShown { get; set; }

    /// <summary>Yeni widget'ların yeri: "cursor" (imlecin yanı), "center" (etkin ekranın ortası), "corner" (türüne göre köşe).</summary>
    public string? NewWidgetPlacement { get; set; } = PlaceModes.Cursor;

    /// <summary>Kısayolla ya da "Widget ekle" penceresiyle eklenen widget'ın nereye geldiğini anlatan ipucu kaç kez gösterildi.</summary>
    public int NewWidgetHintsShown { get; set; }

    /// <summary>
    /// "Widget'lara koyduğum öğeler masaüstünden kalksın" (2.1'de "Kutulara eklediklerim masaüstünden kalksın"): kutuya ya da
    /// halkaya konan masaüstü simgesi masaüstünden kalkar, çıkarınca geri gelir. Windows simgeleri görünürken dosya görünür bir
    /// klasöre (masaüstünün yanındaki NestDesk klasörü) taşınır; bölmeler masaüstünü yönetirken (FencesReplaceIcons) dosyaya
    /// dokunulmaz, masaüstü bölmeleri onu göstermez. 2.2'den beri bölme kipi bu ayarı kapatmaz (iki ayrı seçim; 2.1 ikisi de
    /// açıkken bölme kipini görür ve hiçbir şey taşımaz).
    /// </summary>
    public bool BoxItemsLeaveDesktop { get; set; }

    /// <summary>
    /// "Herkesin masaüstündeki simgeler de kalksın": ortak masaüstündeki (tüm hesaplar) öğeler de kutuya taşınır (Windows
    /// yönetici onayı ister). Varsayılan kapalı. 2.2: yalnızca yönetici onaylı taşıma başarılı olunca açılır. Store (MSIX)
    /// sürümü bunu yok sayar (kaldırınca geri koyamaz); 2.1.1'de taşınanlar yine geri döner.
    /// </summary>
    public bool BoxIncludesPublicDesktop { get; set; }

    /// <summary>2.1.1: ortak masaüstü öğesinin taşınmadığını anlatan bildirim gösterildi mi? 2.2 okumaz (eski sürüm için kalır).</summary>
    public bool PublicBoxNoticeShown { get; set; }

    // 2.1 P6
    /// <summary>
    /// Daha yeni bir sürümün yazdığı, bu sürümün bilmediği ayarlar: okunur ve aynen geri yazılır. Böylece 2.2'den 2.1'e
    /// dönen kullanıcı yeniden 2.2'ye geçtiğinde ayarlarını kaybetmez.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    // 2.1 P7 — hareket
    /// <summary>
    /// Ayarlar > Animasyonlar'da "Kapalı": hiçbir geçiş oynamaz. false (varsayılan) = Canlı ya da Sakin
    /// (<see cref="CalmMotion"/>), her zaman Windows'un "Animasyon efektleri" ayarına uyarak (bkz. <see cref="MotionPolicy"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AnimationsOff { get; set; }

    // 2.1 inceleme — klasör simgeleri
    /// <summary>
    /// Uygulamanın simge verdiği klasörler (tam yol; en yenisi sonda, en çok <see cref="FolderIconLog.Max"/>).
    /// "Klasör simgelerinin hepsini kaldır" masaüstü taramasının erişmediği klasörleri de (bölmede derin aramayla bulunan alt
    /// klasör, kaldırılmış bir klasör portalının klasörü) bulsun. Simge kaldırılınca listeden çıkar.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IconFolders { get; set; }

    // 2.2 Hareket
    /// <summary>
    /// Ayarlar > Animasyonlar'da "Sakin": yalnızca kısa solmalar (2.1 davranışı). false (varsayılan) = "Canlı".
    /// <see cref="AnimationsOff"/> ("Kapalı") baskındır ve bunu değiştirmez (bkz. <see cref="MotionPolicy.Flags"/>). 2.1 bu
    /// alanı tanımaz, Extra'da aynen geri yazar; orada Canlı ve Sakin aynı solmadır.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CalmMotion { get; set; }

    // 2.2 ayar güvenliği
    /// <summary>
    /// Dosyayı son açan sürüm ("2.2.0"): açılışta bir kez damgalanır. Çalışan sürümden farklıysa dosya, uygulama ona dokunmadan
    /// önce "yedekler" klasörüne kopyalanır (bkz. <see cref="SettingsVersion"/>); daha yeni bir sürümünse kullanıcıya bir kez
    /// söylenir. null = 2.2'den önceki sürüm. 2.1 bu alanı tanımaz ve <see cref="Extra"/>'da aynen korur.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SavedWithAppVersion { get; set; }

    // 2.2 tanılama
    // (bool ve metin: eski sürüm bilmediği alanları Extra'da korur. Kalıcı enum'a üye eklenmedi. Varsayılanken yazılmaz.)

    /// <summary>
    /// Ayarlar'daki "Hata günlüğü tut" kapatıldı: hata günlüğü dosyaları (<c>logs\nestdesk-*.jsonl</c>) yazılmaz (bellekteki son
    /// kayıtlar ve sayaçlar sürer), çökme raporuna günlüğün son satırları eklenmez. <c>logs\</c> altına yine de yazılabilenler:
    /// oturum işareti (<c>session.json</c>), çökme/donma raporları (<see cref="CrashReportMode"/>'a göre) ve kullanıcının açtığı
    /// tanılama sayfası (<c>tanilama.html</c>). Varsayılan: günlük açık (içeriği temizlenir).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ErrorLogOff { get; set; }

    /// <summary>
    /// Uygulama çökünce: null = sor (sonraki açılışta şeritle), "always" = sormadan kaydet, "never" = kaydetme. Tanınmayan
    /// değer (daha yeni sürümün) "sor" sayılır; bkz. <see cref="CrashReportModes"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CrashReportMode { get; set; }

    // 2.2 Halka
    /// <summary>Kısayolla imleçte açılan halka hangi halka widget'ı (kimlik); null ya da bulunamazsa ilk halka.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RingWidgetId { get; set; }

    /// <summary>
    /// "Tuşu bırakınca seç" kapatıldı: kısayolla açılan halka tuşlar bırakılınca seçim yapmaz, tıklama kipinde kalır.
    /// Ters adlı: varsayılanı true olan bool WhenWritingDefault ile yanlış geri okunurdu.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RingHoldSelectOff { get; set; }

    // 2.2 Kolay NestDesk
    /// <summary>
    /// Ana sayfadaki "Nasıl kullanılır" kartlarında "Anladım"a basıldı: kartlar bir daha gösterilmez (bkz.
    /// <c>Views/HowToCards</c>). false (varsayılan, yazılmaz) = kartlar görünür. 2.1 bu alanı tanımaz, Extra'da aynen geri yazar.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HowToDismissed { get; set; }

    /// <summary>
    /// "Görünüm ve diğer ayarlar artık Düzenle'de." bildirimi gösterildi (ya da kullanıcı paneli kendisi açtı): widget menüsünde
    /// "Görünüm ▸" ve "Diğer ▸" görmüş kullanıcıya bir kez. Yeni kullanıcıda açılışta true yazılır. 2.1 bu alanı tanımaz ve
    /// <see cref="Extra"/>'da aynen korur.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EditPanelNoticeShown { get; set; }

    // 2.2 kutu (kolay)
    // (Metin listesi: 2.1 bilmediği alanı Extra'da aynen korur. Boşken yazılmaz.)

    /// <summary>
    /// Kutuya (halkaya) eklenirken açık olduğu için masaüstünden kalkamayan öğeler (masaüstündeki yolları): "Kutulara
    /// eklediklerim masaüstünden kalksın" açıkken birkaç saniye sonra, masaüstü değişince ve sonraki açılışta yeniden denenir.
    /// Taşınınca, hiçbir kutuda kalmayınca ya da seçenek kapanınca listeden düşer.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? BoxClaimRetry { get; set; }

    /// <summary>
    /// "Widget'lara koyduğum öğeler masaüstünden kalksın" soruldu mu? Kapalıyken bir masaüstü simgesi ilk kez kutuya ya da halkaya
    /// konunca bir kez, widget'ın yanında sorulur; iki yanıt da bunu yazar. <see cref="BoxItemsLeaveDesktop"/> açıksa sorulmuş
    /// sayılır (<see cref="BoxChoices.Asked"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BoxLeaveAsked { get; set; }

    /// <summary>
    /// Herkesin masaüstündeki bir simge kutuya konunca "o da kalksın mı" soruldu mu (bir kez; iki yanıt da yazar). Evet denip
    /// yönetici onaylı taşıma başarılı olursa <see cref="BoxIncludesPublicDesktop"/> açılır.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BoxPublicAsked { get; set; }

    /// <summary>
    /// Kutuya ya da halkaya Başlat menüsünden bir uygulama eklenince masaüstündeki aynı uygulamanın kısayolu: null = ilk
    /// eşleşmede sor, "leave" = o da kalksın, "keep" = kalsın (bkz. <see cref="BoxAppShortcutChoices"/>; tanınmayan değer sor).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BoxAppShortcuts { get; set; }

    // 2.2 halka merkezi
    // (Nesne: 2.1 bilmediği alanı Extra'da aynen korur. Boşken yazılmaz.)

    /// <summary>
    /// NestDesk halkası: masaüstünde widget'ı olmayan halka (Kind=Launcher, Variant="ring"). Çift tıklama, tepsi ve kısayol
    /// masaüstünde halka widget'ı yokken bunu açar (<see cref="RingTargets.Pick(AppSettings)"/>). <see cref="Widgets"/>'ta
    /// durmaz; kutu kayıtları, taşıyıcı ve yeniden adlandırma onu <see cref="Launchers"/> üzerinden görür. İlk gerektiğinde
    /// <see cref="RingTargets.EnsureHub"/> kurar; null (varsayılan) yazılmaz.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WidgetConfig? Hub { get; set; }

    // 2.2 Halka çift tıklaması
    /// <summary>
    /// "Boş masaüstüne çift tıklayınca halka açılsın" açılmadan önceki çift tıklama seçimi (<see cref="DesktopState.DoubleClickChoices"/>):
    /// anahtar kapanınca (Ayarlar ya da karşılama) ona dönülür ve silinir (<see cref="DesktopState.RingDoubleClickOff"/>). Yoksa
    /// "auto". Boşken yazılmaz.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DoubleClickBeforeRing { get; set; }
}

/// <summary><see cref="AppSettings.BoxAppShortcuts"/> değerleri (enum değil metin: yeni değer eski sürümü bozmaz).</summary>
public static class BoxAppShortcutChoices
{
    public const string Leave = "leave";
    public const string Keep = "keep";

    /// <summary>Kayıtlı değeri çözer: "leave" ya da "keep"; boş ya da tanınmayan değerde null (sor).</summary>
    public static string? Normalize(string? choice) => choice?.Trim().ToLowerInvariant() switch
    {
        Leave => Leave,
        Keep => Keep,
        _ => null,
    };
}

/// <summary><see cref="AppSettings.CrashReportMode"/> değerleri (enum değil metin: yeni değer eski sürümü bozmaz).</summary>
public static class CrashReportModes
{
    public const string Always = "always";
    public const string Never = "never";

    /// <summary>Kayıtlı değeri çözer: "always" ya da "never"; boş ya da tanınmayan değerde null (sor).</summary>
    public static string? Normalize(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        Always => Always,
        Never => Never,
        _ => null,
    };
}
