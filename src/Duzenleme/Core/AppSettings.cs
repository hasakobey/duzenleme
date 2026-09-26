using System.Text.Json;

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
public enum DesktopFilter { None, All, Folders, Shortcuts, Files }

public enum AppTheme { System, Dark, Light }

public sealed class LauncherTab
{
    public string Name { get; set; } = "Uygulamalar";
    public List<string> Items { get; set; } = [];
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

    // Kısayol kutusu
    public List<LauncherTab> Tabs { get; set; } = [];
    public int ActiveTab { get; set; }

    public WidgetConfig Clone() =>
        JsonSerializer.Deserialize<WidgetConfig>(JsonSerializer.Serialize(this, JsonFile.Options), JsonFile.Options)!;
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
}

public sealed class AppSettings
{
    public List<Rule> Rules { get; set; } = Rule.Defaults();

    /// <summary>Hedef klasör masaüstünde yoksa oluşturulsun mu? Varsayılan: hayır, kullanıcının yapısına uyulur.</summary>
    public bool CreateMissingFolders { get; set; }

    public bool Paused { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool FirstRunDone { get; set; }
    public List<WidgetConfig> Widgets { get; set; } = [];

    /// <summary>Boş masaüstüne çift tıklamak simgeleri gizler/gösterir.</summary>
    public bool DoubleClickHidesDesktop { get; set; } = true;

    /// <summary>Simgeler gizlenirken widget'lar da gizlensin.</summary>
    public bool HideWidgetsWithIcons { get; set; } = true;

    /// <summary>
    /// Masaüstünü bölmeler yönetir: Windows'un kendi masaüstü simgeleri gizlenir, öğeler yalnızca bölmelerde görünür
    /// (Fences gibi). Dosyalara dokunulmaz; kapatınca ya da uygulamadan çıkınca simgeler geri gelir.
    /// </summary>
    public bool FencesReplaceIcons { get; set; }

    /// <summary>Simgeleri biz gizlediysek, çıkışta/çökmeden sonra geri açabilmek için.</summary>
    public bool IconsHiddenByApp { get; set; }

    public HotkeySettings Hotkeys { get; set; } = new();
    public List<LayoutSnapshot> Layouts { get; set; } = [];

    /// <summary>Masaüstünde yeni klasör oluşturulunca simge önerisi göster.</summary>
    public bool SuggestFolderIcons { get; set; } = true;

    /// <summary>Yapay zekâ simge üretimi için DPAPI ile şifrelenmiş API anahtarı (isteğe bağlı).</summary>
    public string? AiKeyProtected { get; set; }
}
