using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>Ekleme yüzeylerindeki bölüm (kalıcı değil): Bölmeler, Araçlar (not, liste, kutu, zamanlayıcılar), Saat ve bilgi.</summary>
internal enum WidgetGroup { Fence, Tool, Info }

/// <summary>
/// Eklenebilecek bir widget. Key: AutomationId'nin ("Add." + Key) ve karşılamadaki seçimin anahtarı. Add: widget'ı ekler
/// (klasör açılamazsa ya da kullanıcı vazgeçerse null). Matches: masaüstündeki bir widget bu seçimin türünden mi?
/// ShowInWelcome: karşılamanın 3. adımında da gösterilir (yalnızca ilk beş araç; yeni türler "Widget ekle"de ve Widget'lar sayfasında).
/// </summary>
internal sealed record WidgetChoice(string Key, string Label, SymbolRegular Icon, string Tip, WidgetGroup Group,
    Func<WidgetConfig?> Add, bool FocusAfterAdd = false, string? Badge = null, Func<WidgetConfig, bool>? Matches = null,
    bool ShowInWelcome = false, bool AsksName = false);

/// <summary>Bütün ekleme yüzeylerinin (Widget ekle penceresi, Widget'lar sayfası, karşılama) tek kaynağı.</summary>
internal static class WidgetCatalog
{
    /// <summary>Kutucuğu gösteren yüzeyin ilk açılışta gösterdiği klasör bölmesi sayısı; kalanlar "Diğer klasörler" ile açılır.</summary>
    public const int VisibleFolderCount = 8;

    /// <summary>
    /// Bölme dışındaki bütün widget'lar (Araçlar ve Saat ve bilgi bölümleri). İlk beşi karşılamada da vardır (bu sırayla).
    /// Metinler arayüz dilinde: her çağrıda kurulur.
    /// </summary>
    public static IReadOnlyList<WidgetChoice> Tools =>
    [
        new("Clock", L.T("Saat"), SymbolRegular.Clock24, L.T("Büyük dijital saat"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Clock), Matches: c => c.Kind == WidgetKind.Clock && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Date", L.T("Tarih"), SymbolRegular.CalendarLtr24, L.T("Gün, ay ve haftalık şerit"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Date), Matches: c => c.Kind == WidgetKind.Date && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Note", L.T("Not"), SymbolRegular.Note24, L.T("Yapışkan not; yazdıkça kaydedilir"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Note), FocusAfterAdd: true, Matches: c => c.Kind == WidgetKind.Note && !c.NoteChecklist, ShowInWelcome: true),
        new("Checklist", L.T("Yapılacaklar"), SymbolRegular.TaskListLtr24, L.T("Onay kutulu liste; işaretledikçe kaydedilir"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Checklist), FocusAfterAdd: true, Matches: c => c.Kind == WidgetKind.Note && c.NoteChecklist, ShowInWelcome: true),
        new("Launcher", L.T("Kısayol kutusu"), SymbolRegular.AppsAddIn24, L.T("Sekmeli uygulama rafı; tek tıkla açar"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Launcher), Matches: c => c.Kind == WidgetKind.Launcher && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Calendar", L.T("Takvim"), SymbolRegular.CalendarMonth24, L.T("Aylık takvim; ay ay gezinilir"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Calendar), Matches: c => WidgetVariants.Is(c, WidgetVariants.Month)),
        new("Countdown", L.T("Geri sayım"), SymbolRegular.CalendarStar24, L.T("Bir güne kaç gün kaldı (tatil, doğum günü…)"), WidgetGroup.Tool,
            AddCountdown, Matches: c => WidgetVariants.Is(c, WidgetVariants.Countdown), AsksName: true),
        new("Timer", L.T("Zamanlayıcı"), SymbolRegular.Timer24, L.T("Geri sayan zamanlayıcı; süre dolunca haber verir. Kronometre de olur."), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Timer), Matches: c => WidgetVariants.Is(c, WidgetVariants.Timer) && TimerModes.Normalize(c.Timer?.Mode) != TimerModes.Pomodoro),
        new("Pomodoro", L.T("Pomodoro"), SymbolRegular.ClockAlarm24, L.T("25 dakika odak, 5 dakika mola; dört turda bir uzun mola"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Pomodoro), Matches: c => WidgetVariants.Is(c, WidgetVariants.Timer) && TimerModes.Normalize(c.Timer?.Mode) == TimerModes.Pomodoro),
        new("WorldClock", L.T("Dünya saati"), SymbolRegular.GlobeClock24, L.T("Başka şehirlerde saat kaç, kaç saat fark var"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.WorldClock), Matches: c => WidgetVariants.Is(c, WidgetVariants.World)),
        new("SystemStatus", L.T("Sistem durumu"), SymbolRegular.Gauge24, L.T("İşlemci, bellek, disk ve pil"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.SystemStatus), Matches: c => WidgetVariants.Is(c, WidgetVariants.System)),
        new("RecycleBin", L.T("Geri Dönüşüm Kutusu"), SymbolRegular.Delete24, L.T("Kaç öğe var, ne kadar yer tutuyor; boşaltmadan önce sorar"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.RecycleBin), Matches: c => WidgetVariants.Is(c, WidgetVariants.Recycle)),
    ];

    /// <summary>"Yeni bölme…" kutucuğunun anahtarı (AutomationId "Add.NewFence"): ad, kaynak ve simge soran pencereyi açar.</summary>
    public const string NewFenceKey = "NewFence";

    /// <summary>Karşılamada gösterilen araçlar (Saat, Tarih, Not, Yapılacaklar, Kısayol kutusu).</summary>
    public static IEnumerable<WidgetChoice> WelcomeTools => Tools.Where(c => c.ShowInWelcome);

    private static WidgetConfig? Seed(string key) => WidgetSeeds.Create(key) is { } seed ? AppHost.Widgets.AddSeed(seed) : null;

    /// <summary>Geri sayım: önce etkinliğin adı ve günü sorulur; vazgeçilirse eklenmez.</summary>
    private static WidgetConfig? AddCountdown()
    {
        var seed = WidgetSeeds.Create(WidgetSeeds.Countdown)!;
        if (CountdownDialog.Ask(null, seed.TargetDate ?? DateTime.Today, false, AppHost.Widgets.PlacementHint?.Anchor) is not { } choice)
            return null;
        seed.Title = choice.Title;
        seed.TargetDate = choice.Date;
        seed.CountdownYearly = choice.Yearly;
        return AppHost.Widgets.AddSeed(seed);
    }

    /// <summary>
    /// Bölmeler: önce "Yeni bölme…" (adı, kaynağı ve simgesiyle), masaüstü türleri (Klasörler, Kısayollar, Dosyalar, Tüm
    /// masaüstü), klasör portalları (İndirilenler, Belgeler, Resimler, başka bir klasör), sonra klasör bölmeleri
    /// (<see cref="WidgetManager.FolderFenceChoices"/> sırasıyla; masaüstünde olmayan kural klasörü "yeni klasör" rozetli).
    /// Her çağrıda yeniden kurulur: klasörler değişebilir.
    /// </summary>
    public static List<WidgetChoice> Fences()
    {
        var list = new List<WidgetChoice>
        {
            new(NewFenceKey, L.T("Yeni bölme…"), SymbolRegular.AddSquare24, L.T("Adını, ne göstereceğini ve simgesini seçerek bölme ekle"),
                WidgetGroup.Fence, () => NewFenceDialog.Ask(null), AsksName: true),
            Filter("Folders", DesktopFilter.Folders, "Klasörler", SymbolRegular.Folder24, "Masaüstündeki klasörler"),
            Filter("Shortcuts", DesktopFilter.Shortcuts, "Kısayollar", SymbolRegular.Apps24, "Uygulama kısayolları, Bu Bilgisayar, Geri Dönüşüm Kutusu"),
            Filter("Files", DesktopFilter.Files, "Dosyalar", SymbolRegular.DocumentMultiple24, "Masaüstünde duran dosyalar"),
            Filter("All", DesktopFilter.All, "Tüm masaüstü", SymbolRegular.Desktop24, "Masaüstündeki her şey tek bölmede"),
        };
        foreach (var id in new[] { FolderPortal.Downloads, FolderPortal.Documents, FolderPortal.Pictures })
            if (KnownFolders.PathOf(id) is { } path) list.Add(KnownPortal(id, path));
        list.Add(new WidgetChoice("Portal:Pick", L.T("Başka klasör…"), SymbolRegular.FolderLink24,
            L.T("Bilgisayardaki herhangi bir klasörü bölmede göster (masaüstünde olması gerekmez)"), WidgetGroup.Fence, AddPickedFolder));
        foreach (var (name, exists) in AppHost.Widgets.FolderFenceChoices())
        {
            var icon = name.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24;
            var tip = exists
                ? $"\"{name}\" klasörünün içi"
                : $"Masaüstünde \"{name}\" klasörü açılır; otomatik taşıma açıksa uygun dosyalar oraya taşınır";
            list.Add(new WidgetChoice("Folder:" + name, name, icon, tip, WidgetGroup.Fence, () => AppHost.Widgets.AddFolderFence(name),
                Badge: exists ? null : "yeni klasör",
                Matches: c => c.Kind == WidgetKind.Fence && c.Filter == DesktopFilter.None && FolderName.Equal(c.FolderName ?? "", name)));
        }
        return list;
    }

    private static WidgetChoice Filter(string key, DesktopFilter filter, string label, SymbolRegular icon, string tip) =>
        new(key, label, icon, tip, WidgetGroup.Fence, () => AppHost.Widgets.AddFence(filter),
            Matches: c => c.Kind == WidgetKind.Fence && c.Filter == filter);

    /// <summary>
    /// Bilinen klasörün (İndirilenler…) portalı. Klasör masaüstündeyse klasik klasör bölmesi olur. Rozet, aynı adlı masaüstü
    /// klasörü kutucuğundan (ör. "Belgeler · yeni klasör") ayırt ettirir.
    /// </summary>
    private static WidgetChoice KnownPortal(string id, string path) =>
        new("Portal:" + id, FolderPortal.KnownName(id), KnownIcon(id), L.F("{0} klasörünün içi, en yeni üstte", FolderPortal.KnownName(id)),
            WidgetGroup.Fence, () => AddFolder(path, id), Badge: L.T("Windows klasörü"),
            Matches: c => c.Kind == WidgetKind.Fence && c.Filter == DesktopFilter.None && c.FolderName is { } f && FolderPortal.SamePath(f, path));

    /// <summary>"Başka klasör…": klasör seçtirir; vazgeçilirse eklenmez.</summary>
    private static WidgetConfig? AddPickedFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("Bölmede gösterilecek klasörü seç"),
            InitialDirectory = KnownFolders.PathOf(FolderPortal.Documents) ?? AppHost.DesktopDirectory,
        };
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName) ? AddFolder(dialog.FolderName, null) : null;
    }

    /// <summary>
    /// Klasörü gösteren bölme ekler: masaüstünün kendisi "Tüm masaüstü", masaüstündeki klasör klasik klasör bölmesi, başka her
    /// yer portal (başlık klasörün adı; 2.0 da başlığı gösterir).
    /// </summary>
    private static WidgetConfig? AddFolder(string path, string? knownId)
    {
        var source = FolderPortal.Normalize(path, AppHost.DesktopDirectory);
        if (source.Filter != DesktopFilter.None) return AppHost.Widgets.AddFence(source.Filter);
        var folder = source.FolderName!;
        if (!System.IO.Path.IsPathFullyQualified(folder)) return AppHost.Widgets.AddFolderFence(folder);
        var title = knownId is not null ? FolderPortal.KnownName(knownId) : FolderPortal.DisplayName(folder);
        return AppHost.Widgets.AddSeed(WidgetSeeds.Portal(folder, knownId, title));
    }

    private static SymbolRegular KnownIcon(string? id) => WidgetIcons.ForKnownFolder(id);

    /// <summary>128×82 kutucuk: 26 px simge, etiket (CharacterEllipsis, MaxWidth 110), Badge varsa altında 11 pt, Opacity 0.7.
    /// AutomationId "Add." + Key; AutomationProperties.Name = Label; HelpText = Tip; ToolTip = Tip.</summary>
    public static Button Tile(WidgetChoice choice, Action<WidgetChoice> onClick)
    {
        var content = new StackPanel();
        content.Children.Add(new SymbolIcon { Symbol = choice.Icon, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock
        {
            Text = choice.Label, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110,
        });
        if (choice.Badge is { } badge)
            content.Children.Add(new TextBlock
            {
                Text = badge, FontSize = 11, Opacity = 0.7, TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110,
            });
        var button = new Button
        {
            Content = content, Width = 128, Height = 82, Margin = new Thickness(0, 0, 8, 8), ToolTip = choice.Tip,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 4, 6, 4),
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "Add." + choice.Key);
        System.Windows.Automation.AutomationProperties.SetName(button, choice.Label);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, choice.Tip);
        button.Click += (_, _) => onClick(choice);
        return button;
    }

    /// <summary>
    /// Kutucukları panele ekler. Klasör bölmelerinin ilk <see cref="VisibleFolderCount"/> tanesi görünür; kalanlar
    /// "Diğer klasörler ({n})" düğmesine basınca aynı yerde, animasyonsuz açılır.
    /// </summary>
    public static void AddTiles(Panel panel, IEnumerable<WidgetChoice> choices, Action<WidgetChoice> onClick)
    {
        var hidden = new List<UIElement>();
        var folders = 0;
        foreach (var choice in choices)
        {
            var tile = Tile(choice, onClick);
            if (choice.Key.StartsWith("Folder:", StringComparison.Ordinal) && ++folders > VisibleFolderCount)
            {
                tile.Visibility = Visibility.Collapsed;
                hidden.Add(tile);
            }
            panel.Children.Add(tile);
        }
        if (hidden.Count == 0) return;

        var more = new Button
        {
            Content = $"Diğer klasörler ({hidden.Count})", Height = 82, Margin = new Thickness(0, 0, 8, 8),
            Icon = new SymbolIcon { Symbol = SymbolRegular.ChevronDown24 },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(more, "Add.MoreFolders");
        more.Click += (_, _) =>
        {
            foreach (var tile in hidden) tile.Visibility = Visibility.Visible;
            more.Visibility = Visibility.Collapsed;
        };
        // "Diğer klasörler" ilk gizli kutucuğun yerinde durur; açılınca kutucuklar onun yerine gelir.
        panel.Children.Insert(panel.Children.IndexOf(hidden[0]), more);
    }

    /// <summary>
    /// near verilirse widget o noktanın ekranına yerleşir: followSetting ise ayardaki kiple ("Widget ekle" penceresi kapanır,
    /// widget onun yerine gelir), değilse türüne göre köşeye (ana pencere ve karşılama açık kalır; imlecin yanına konan widget
    /// onların arkasında kaybolurdu). Add() çağrılır; FocusAfterAdd ise FocusNote. Klasör açılamazsa ya da vazgeçilirse null.
    /// renameAfterAdd: yeni bölme/kutu/not başlığı düzenlenir hâlde gelir (Gezgin'deki "Yeni klasör" gibi; bölme ve kutuda
    /// simgesi tıklanınca seçici açılır, notta Enter ile yazı alanına geçilir). "Widget ekle" penceresi ve Widget'lar sayfası
    /// ister; karşılama ve toplu eklemeler istemez. Adını kendi penceresinde soran seçim ("Yeni bölme…", geri sayım;
    /// <see cref="WidgetChoice.AsksName"/>) ve alt türlü araçlar yeniden adlandırmaya açılmaz.
    /// "Yeni bölme…" adı ve simgeyi kendi penceresinde sorar (near'ın monitöründe).
    /// </summary>
    public static WidgetConfig? Invoke(WidgetChoice choice, NativeMethods.POINT? near, bool followSetting = false, bool renameAfterAdd = false)
    {
        if (near is { } point)
            AppHost.Widgets.PlacementHint = followSetting ? WidgetPlacement.FromSettingsAt(point) : WidgetPlacement.CornerNear(point);
        var named = choice.Key == NewFenceKey;
        var config = named ? NewFenceDialog.Ask(near) : choice.Add();
        if (config is null)
        {
            // Eklenemedi: yer ipucu sonraki (başka yerden eklenen) widget'a kalmasın.
            AppHost.Widgets.PlacementHint = null;
            return null;
        }
        // Adlandırarak eklemede not da önce başlığını alır (sonra yazı alanına geçilir); diğer yüzeylerde not yazı alanıyla açılır.
        // Yalnızca klasik bölme/kutu/not (ve klasör portalı); alt türlü küçük araçlar (zamanlayıcı, Geri Dönüşüm Kutusu…) F2 ile.
        if (renameAfterAdd && !choice.AsksName && WidgetVariants.Of(config) is null) AppHost.Widgets.BeginRename(config.Id);
        else if (choice.FocusAfterAdd) AppHost.Widgets.FocusNote(config.Id);
        return config;
    }
}
