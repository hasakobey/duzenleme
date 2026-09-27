using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>Masaüstündeki bir widget'ın liste satırı. Düğme adları ekran okuyucu ve UI Automation için ("Kaldır: Saat").</summary>
public sealed class WidgetRow(WidgetConfig config)
{
    public WidgetConfig Config { get; } = config;
    public string Name { get; } = WidgetText.DisplayName(config);
    public string RevealName => $"Bul: {Name}";
    public string RemoveName => $"Kaldır: {Name}";

    /// <summary>Ekleme kutucuklarındaki simgenin aynısı (bkz. <see cref="WidgetCatalog"/>).</summary>
    public SymbolRegular Icon => Config.Kind switch
    {
        WidgetKind.Clock => SymbolRegular.Clock24,
        WidgetKind.Date => SymbolRegular.CalendarLtr24,
        WidgetKind.Note => Config.NoteChecklist ? SymbolRegular.TaskListLtr24 : SymbolRegular.Note24,
        WidgetKind.Launcher => SymbolRegular.AppsAddIn24,
        _ => Config.Filter switch
        {
            DesktopFilter.Folders => SymbolRegular.Folder24,
            DesktopFilter.Shortcuts => SymbolRegular.Apps24,
            DesktopFilter.Files => SymbolRegular.DocumentMultiple24,
            DesktopFilter.All => SymbolRegular.Desktop24,
            _ => string.Equals(Config.FolderName, "PDF", StringComparison.OrdinalIgnoreCase)
                ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24,
        },
    };
}

/// <summary>Kayıtlı düzen satırı.</summary>
public sealed record LayoutRow(LayoutSnapshot Layout)
{
    public string Name => Layout.Name;
    public string Detail => $"{Layout.Widgets.Count} widget · {UiText.When(Layout.Created)}";
    public string ApplyName => $"Uygula: {Name}";
    public string DeleteName => $"Düzeni sil: {Name}";
}

/// <summary>
/// Widget'lar: "yalnızca bölmelerde" modu, ekleme kutucukları, masaüstündekilerin listesi (Bul / Kaldır), toplu görünüm ve
/// kayıtlı düzenler. Her eylemin sonucu ana penceredeki bildirim şeridinde görünür; kaldırma ve silme oradan geri alınır.
/// </summary>
public partial class WidgetsPage : Page
{
    // "Arka plan" ve "Vurgu rengi" kutularındaki sıra.
    private static readonly WidgetStyle[] Styles = [WidgetStyle.Glass, WidgetStyle.Dark, WidgetStyle.Light];
    private static readonly WidgetAccent[] Accents =
        [WidgetAccent.Violet, WidgetAccent.Blue, WidgetAccent.Green, WidgetAccent.Orange, WidgetAccent.Pink];

    private const string FolderKeyPrefix = "Folder:";

    // Kutular koddan güncellenirken SelectionChanged bütün widget'ları yeniden boyamasın.
    private bool _loading;

    public WidgetsPage()
    {
        InitializeComponent();
        Fold.Attach(LookFold, LookContent);
        Fold.Attach(LayoutsFold, LayoutsContent);
        // Araçlar sabit; bölmeler (klasörler değişebilir) her açılışta yeniden kurulur.
        WidgetCatalog.AddTiles(ToolTiles, WidgetCatalog.Tools, AddWidget);
        Loaded += (_, _) =>
        {
            AppHost.Widgets.Changed += OnWidgetsChanged;
            AppHost.SettingsChanged += OnSettingsChanged;
            AppHost.DesktopVisibilityChanged += OnSettingsChanged;
            FenceTiles.Children.Clear();
            WidgetCatalog.AddTiles(FenceTiles, WidgetCatalog.Fences(), AddWidget);
            OnWidgetsChanged();
            OnSettingsChanged();
        };
        Unloaded += (_, _) =>
        {
            AppHost.Widgets.Changed -= OnWidgetsChanged;
            AppHost.SettingsChanged -= OnSettingsChanged;
            AppHost.DesktopVisibilityChanged -= OnSettingsChanged;
        };
    }

    /// <summary>Widget eklendi, kaldırıldı, düzen uygulandı ya da yerleştirildi: liste, sayılar ve görünüm kutuları.</summary>
    private void OnWidgetsChanged()
    {
        RefreshWidgets();
        RefreshLayouts(); // düzenli yerleştirme ve düzen uygulama yedek düzen ekler
        RefreshSettings();
    }

    /// <summary>
    /// Ayarlar kaydedildi (widget sürüklemek de kaydeder): yalnızca anahtarlar ve görünüm kutuları güncellenir. Liste
    /// yalnızca bir widget'ın adı değiştiyse (not başlığı, yapılacaklar ilerlemesi) yeniden kurulur.
    /// </summary>
    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(OnSettingsChanged);
            return;
        }
        RefreshSettings();
        if (ActiveList.ItemsSource is IEnumerable<WidgetRow> rows &&
            !rows.Select(r => r.Name).SequenceEqual(AppHost.Settings.Widgets.Select(WidgetText.DisplayName)))
            RefreshWidgets();
    }

    private void RefreshWidgets()
    {
        var rows = AppHost.Widgets.Configs.Select(c => new WidgetRow(c)).ToList();
        ActiveList.ItemsSource = rows;
        ActiveHeader.Text = $"Masaüstündekiler ({rows.Count})";
        ActiveEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RevealAllButton.IsEnabled = ArrangeButton.IsEnabled = rows.Count > 0;
    }

    private void RefreshLayouts()
    {
        var rows = AppHost.Settings.Layouts.OrderByDescending(l => l.Created).Select(l => new LayoutRow(l)).ToList();
        LayoutList.ItemsSource = rows;
        LayoutsTitle.Text = $"Kayıtlı düzenler ({rows.Count})";
        LayoutEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Mod anahtarı ve toplu görünüm denetimleri ayarlardaki değeri gösterir.</summary>
    private void RefreshSettings()
    {
        _loading = true;
        try
        {
            FencesModeToggle.IsChecked = AppHost.Settings.FencesReplaceIcons;
            // Notlar kendi kağıt rengini kullanır: toplu görünüm onları saymaz.
            var targets = AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList();
            StyleBox.SelectedIndex = Common(targets.Select(w => Array.IndexOf(Styles, w.Style)));
            AccentBox.SelectedIndex = Common(targets.Select(w => Array.IndexOf(Accents, w.Accent)));
            StyleBox.IsEnabled = AccentBox.IsEnabled = targets.Count > 0;
            SnapToggle.IsChecked = AppHost.Settings.SnapWidgets;
            OverlapToggle.IsChecked = AppHost.Settings.PreventOverlap;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Değerlerin hepsi aynıysa o değer; farklıysa ya da hiç yoksa -1 (kutuda seçim görünmez).</summary>
    private static int Common(IEnumerable<int> values)
    {
        var distinct = values.Distinct().Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : -1;
    }

    /// <summary>Ana pencerenin ortası: yeni widget'lar bu pencerenin bulunduğu ekrana yerleşir.</summary>
    private NativeMethods.POINT? WindowCenter()
    {
        if (Window.GetWindow(this) is not { } window) return null;
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var r)
            ? new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 }
            : null;
    }

    private void FencesMode_Click(object sender, RoutedEventArgs e)
    {
        if (FencesModeToggle.IsChecked == true)
        {
            var ids = DesktopFences.TurnOn(allStarters: false, WindowCenter());
            Notice.Show(DesktopFences.Describe(ids.Count), NoticeKind.Success, "Geri al", () => DesktopFences.Undo(ids));
        }
        else
        {
            DesktopFences.TurnOff();
            Notice.Show("Masaüstü simgeleri yeniden gösteriliyor. Bölmelerin yerinde duruyor.", NoticeKind.Info);
        }
        RefreshSettings();
    }

    /// <summary>Kutucuktaki widget'ı ana pencerenin ekranına ekler; bildirimdeki "Bul" onu öne getirir.</summary>
    private void AddWidget(WidgetChoice choice)
    {
        var folder = choice.Key.StartsWith(FolderKeyPrefix, StringComparison.Ordinal) ? choice.Key[FolderKeyPrefix.Length..] : null;
        // Klasör eklerken açılacak mı? Kutucuktaki rozete değil diske bakılır (kutucuk kurulduktan sonra değişmiş olabilir).
        var creates = folder is not null && !AppHost.Organizer.ExistingFolders().Any(f => FolderName.Equal(f, folder));
        if (WidgetCatalog.Invoke(choice, WindowCenter()) is not { } config) return; // klasör açılamadı; kullanıcı uyarıldı
        // Pencere açılamadıysa WidgetManager widget'ı geri çıkarıp uyardı: "eklendi" denmesin.
        if (!AppHost.Settings.Widgets.Any(w => w.Id == config.Id)) return;

        var text = choice.Group == WidgetGroup.Tool ? $"{choice.Label} masaüstüne eklendi."
            : creates ? $"\"{folder}\" bölmesi eklendi; masaüstünde \"{folder}\" klasörü de oluşturuldu."
            : $"\"{choice.Label}\" bölmesi masaüstüne eklendi.";
        Notice.Show(text, NoticeKind.Success, "Bul", () => AppHost.Widgets.Reveal(config.Id));
        if (creates) RefreshTile(choice.Key);
    }

    /// <summary>Klasör açıldı: kutucuğun "yeni klasör" rozeti kalksın. Yerinde değiştirilir ("Diğer klasörler" açık kalır).</summary>
    private void RefreshTile(string key)
    {
        var old = FenceTiles.Children.OfType<FrameworkElement>()
            .FirstOrDefault(t => System.Windows.Automation.AutomationProperties.GetAutomationId(t) == "Add." + key);
        if (old is null || WidgetCatalog.Fences().FirstOrDefault(c => c.Key == key) is not { } fresh) return;
        var tile = WidgetCatalog.Tile(fresh, AddWidget);
        tile.Visibility = old.Visibility;
        FenceTiles.Children[FenceTiles.Children.IndexOf(old)] = tile;
    }

    private void RevealAll_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RevealAll();

    private void Arrange_Click(object sender, RoutedEventArgs e)
    {
        var count = AppHost.Widgets.ArrangeAll();
        // Geri al yok: bildirim açık kalırken eklenen widget yedekle değiştirilince sessizce silinirdi. Yedek Kayıtlı düzenler'de.
        if (count == 0) Notice.Show("Yerleştirilecek widget yok.", NoticeKind.Info);
        else Notice.Show($"{count} widget düzenli yerleştirildi. Önceki yerleşim Kayıtlı düzenler'de \"{WidgetManager.ArrangeBackupName}\" adıyla duruyor.");
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WidgetRow row) AppHost.Widgets.Reveal(row.Config.Id);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WidgetRow row) return;
        if (!AppHost.Settings.Widgets.Any(w => w.Id == row.Config.Id)) return;
        var name = WidgetText.DisplayName(row.Config); // kaldırmadan önce
        var modeOff = AppHost.Widgets.RemoveWithUndo(row.Config.Id, notify: false);
        Notice.Show($"{name} kaldırıldı." + (modeOff ? " Masaüstü simgeleri yeniden gösteriliyor." : ""),
            NoticeKind.Info, "Geri al", AppHost.Widgets.UndoRemove);
    }

    private void StyleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || StyleBox.SelectedIndex < 0) return;
        AppHost.Widgets.SetLookForAll(Styles[StyleBox.SelectedIndex], null);
    }

    private void AccentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AccentBox.SelectedIndex < 0) return;
        AppHost.Widgets.SetLookForAll(null, Accents[AccentBox.SelectedIndex]);
    }

    private void Snap_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.SnapWidgets = SnapToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void Overlap_Click(object sender, RoutedEventArgs e)
    {
        // Yalnızca ayar yazılır: sonraki bırakma/büyütmede uygulanır, var olan widget'lar yerinden oynamaz.
        AppHost.Settings.PreventOverlap = OverlapToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void LayoutName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SaveLayout();
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e) => SaveLayout();

    private void SaveLayout()
    {
        // Boş düzen uygulanınca bütün widget'lar kalkardı.
        if (AppHost.Settings.Widgets.Count == 0)
        {
            Notice.Show("Kaydedilecek widget yok. Önce masaüstüne bir widget ekle.", NoticeKind.Info);
            return;
        }
        var layouts = AppHost.Settings.Layouts;
        var sameName = StringComparer.Create(UiText.Tr, ignoreCase: true);
        var name = LayoutName.Text.Trim();
        if (name.Length == 0)
        {
            var n = layouts.Count + 1;
            while (layouts.Any(l => sameName.Equals(l.Name, $"Düzen {n}"))) n++;
            name = $"Düzen {n}";
        }
        // Aynı adlı düzen varsa üzerine yazılır.
        layouts.RemoveAll(l => sameName.Equals(l.Name, name));
        layouts.Add(LayoutSnapshot.Capture(name, AppHost.Settings.Widgets));
        AppHost.SaveSettings();
        LayoutName.Text = "";
        RefreshLayouts();
        Notice.Show($"\"{name}\" düzeni kaydedildi.");
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LayoutRow row) return;
        // Yedeğin kendisi uygulanırken yeni yedek alınmaz (WidgetManager.ApplyLayout).
        var backedUp = row.Name != WidgetManager.ApplyBackupName;
        AppHost.Widgets.ApplyLayout(row.Layout);
        Notice.Show(backedUp
            ? $"\"{row.Name}\" düzeni uygulandı. Önceki yerleşim \"{WidgetManager.ApplyBackupName}\" adıyla kaydedildi."
            : $"\"{row.Name}\" düzeni uygulandı.");
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LayoutRow row) return;
        var layout = row.Layout;
        var index = AppHost.Settings.Layouts.IndexOf(layout);
        if (index < 0) return;
        AppHost.Settings.Layouts.RemoveAt(index);
        AppHost.SaveSettings();
        RefreshLayouts();
        Notice.Show($"\"{layout.Name}\" düzeni silindi.", NoticeKind.Info, "Geri al", () =>
        {
            if (AppHost.Settings.Layouts.Contains(layout)) return;
            AppHost.Settings.Layouts.Insert(Math.Min(index, AppHost.Settings.Layouts.Count), layout);
            AppHost.SaveSettings();
            RefreshLayouts();
        });
    }
}
