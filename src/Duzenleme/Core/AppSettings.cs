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
    public string Name { get; set; } = "Uygulamalar";
    public List<string> Items { get; set; } = [];

    // 2.1 P6
    /// <summary>Daha yeni sürümün yazdığı bilinmeyen alanlar (aynen geri yazılır).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
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

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Icon);

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
    /// "Kutulara eklediklerim masaüstünden kalksın": kısayol kutusuna eklenen masaüstü öğesi görünür bir klasöre
    /// (masaüstünün yanındaki NestDesk klasörü) taşınır. Bölmeler masaüstünü yönetirken (FencesReplaceIcons) etkisizdir.
    /// </summary>
    public bool BoxItemsLeaveDesktop { get; set; }

    /// <summary>Ortak masaüstündeki (tüm hesaplar) öğeler de kutuya taşınsın. Varsayılan kapalı: yalnızca bağlantı eklenir.</summary>
    public bool BoxIncludesPublicDesktop { get; set; }

    /// <summary>Ortak masaüstü öğesinin taşınmadığını anlatan bildirim gösterildi mi?</summary>
    public bool PublicBoxNoticeShown { get; set; }

    // 2.1 P6
    /// <summary>
    /// Daha yeni bir sürümün yazdığı, bu sürümün bilmediği ayarlar: okunur ve aynen geri yazılır. Böylece 2.2'den 2.1'e
    /// dönen kullanıcı yeniden 2.2'ye geçtiğinde ayarlarını kaybetmez.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
