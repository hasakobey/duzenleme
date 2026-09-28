using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

internal enum WidgetGroup { Fence, Tool }

/// <summary>
/// Eklenebilecek bir widget. Key: AutomationId'nin ("Add." + Key) ve karşılamadaki seçimin anahtarı. Add: widget'ı ekler
/// (klasör açılamazsa null). Matches: masaüstündeki bir widget bu seçimin türünden mi?
/// </summary>
internal sealed record WidgetChoice(string Key, string Label, SymbolRegular Icon, string Tip, WidgetGroup Group,
    Func<WidgetConfig?> Add, bool FocusAfterAdd = false, string? Badge = null, Func<WidgetConfig, bool>? Matches = null);

/// <summary>Bütün ekleme yüzeylerinin (Widget ekle penceresi, Widget'lar sayfası, karşılama) tek kaynağı.</summary>
internal static class WidgetCatalog
{
    /// <summary>Kutucuğu gösteren yüzeyin ilk açılışta gösterdiği klasör bölmesi sayısı; kalanlar "Diğer klasörler" ile açılır.</summary>
    public const int VisibleFolderCount = 8;

    public static IReadOnlyList<WidgetChoice> Tools { get; } =
    [
        new("Clock", "Saat", SymbolRegular.Clock24, "Büyük dijital saat", WidgetGroup.Tool,
            () => AppHost.Widgets.Add(WidgetKind.Clock), Matches: c => c.Kind == WidgetKind.Clock),
        new("Date", "Tarih", SymbolRegular.CalendarLtr24, "Gün, ay ve haftalık şerit", WidgetGroup.Tool,
            () => AppHost.Widgets.Add(WidgetKind.Date), Matches: c => c.Kind == WidgetKind.Date),
        new("Note", "Not", SymbolRegular.Note24, "Yapışkan not; yazdıkça kaydedilir", WidgetGroup.Tool,
            () => AppHost.Widgets.Add(WidgetKind.Note), FocusAfterAdd: true, Matches: c => c.Kind == WidgetKind.Note && !c.NoteChecklist),
        new("Checklist", "Yapılacaklar", SymbolRegular.TaskListLtr24, "Onay kutulu liste; işaretledikçe kaydedilir", WidgetGroup.Tool,
            () => AppHost.Widgets.AddChecklist(), FocusAfterAdd: true, Matches: c => c.Kind == WidgetKind.Note && c.NoteChecklist),
        new("Launcher", "Kısayol kutusu", SymbolRegular.AppsAddIn24, "Sekmeli uygulama rafı; tek tıkla açar", WidgetGroup.Tool,
            () => AppHost.Widgets.Add(WidgetKind.Launcher), Matches: c => c.Kind == WidgetKind.Launcher),
    ];

    /// <summary>"Yeni bölme…" kutucuğunun anahtarı (AutomationId "Add.NewFence"): ad, kaynak ve simge soran pencereyi açar.</summary>
    public const string NewFenceKey = "NewFence";

    /// <summary>
    /// Bölmeler: önce "Yeni bölme…" (adı, kaynağı ve simgesiyle), sonra masaüstü türleri (Klasörler, Kısayollar, Dosyalar,
    /// Tüm masaüstü), sonra klasör bölmeleri (<see cref="WidgetManager.FolderFenceChoices"/> sırasıyla; masaüstünde olmayan
    /// kural klasörü "yeni klasör" rozetli). Her çağrıda yeniden kurulur: klasörler değişebilir.
    /// </summary>
    public static List<WidgetChoice> Fences()
    {
        var list = new List<WidgetChoice>
        {
            new(NewFenceKey, L.T("Yeni bölme…"), SymbolRegular.AddSquare24, L.T("Adını, ne göstereceğini ve simgesini seçerek bölme ekle"),
                WidgetGroup.Fence, () => NewFenceDialog.Ask(null)),
            Filter("Folders", DesktopFilter.Folders, "Klasörler", SymbolRegular.Folder24, "Masaüstündeki klasörler"),
            Filter("Shortcuts", DesktopFilter.Shortcuts, "Kısayollar", SymbolRegular.Apps24, "Uygulama kısayolları, Bu Bilgisayar, Geri Dönüşüm Kutusu"),
            Filter("Files", DesktopFilter.Files, "Dosyalar", SymbolRegular.DocumentMultiple24, "Masaüstünde duran dosyalar"),
            Filter("All", DesktopFilter.All, "Tüm masaüstü", SymbolRegular.Desktop24, "Masaüstündeki her şey tek bölmede"),
        };
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
    /// onların arkasında kaybolurdu). Add() çağrılır; FocusAfterAdd ise FocusNote. Klasör açılamazsa null.
    /// renameAfterAdd: yeni bölme/kutu/not başlığı düzenlenir hâlde gelir (Gezgin'deki "Yeni klasör" gibi; bölme ve kutuda
    /// simgesi tıklanınca seçici açılır, notta Enter ile yazı alanına geçilir). "Widget ekle" penceresi ve Widget'lar sayfası
    /// ister; karşılama ve toplu eklemeler istemez.
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
        if (renameAfterAdd && !named) AppHost.Widgets.BeginRename(config.Id);
        else if (choice.FocusAfterAdd) AppHost.Widgets.FocusNote(config.Id);
        return config;
    }
}
