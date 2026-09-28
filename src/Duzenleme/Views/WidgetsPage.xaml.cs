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

    /// <summary>Widget'ın başlığındaki simgenin aynısı (kullanıcının seçtiği ya da türün varsayılanı, bkz. <see cref="WidgetIcons"/>).</summary>
    public SymbolRegular Icon { get; } = WidgetIcons.For(config);
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
        foreach (var mode in DesktopModes.Choices)
            IconModeBox.Items.Add(new ComboBoxItem { Content = DesktopModes.Label(mode), Tag = mode });
        foreach (var mode in PlaceModes.Choices)
            PlacementBox.Items.Add(new ComboBoxItem { Content = DesktopModes.PlaceLabel(mode), Tag = mode });
        // Araçlar sabit; bölmeler (klasörler değişebilir) her açılışta yeniden kurulur.
        WidgetCatalog.AddTiles(ToolTiles, WidgetCatalog.Tools, AddWidget);
        Loaded += (_, _) => Reload();
        // Ana pencere gizliyken sayfa widget değişikliklerine ve kayıtlara tepki vermez; yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.Widgets.Changed += OnWidgetsChanged;
                AppHost.SettingsChanged += OnSettingsChanged;
                AppHost.DesktopVisibilityChanged += OnSettingsChanged;
            },
            detach: () =>
            {
                AppHost.Widgets.Changed -= OnWidgetsChanged;
                AppHost.SettingsChanged -= OnSettingsChanged;
                AppHost.DesktopVisibilityChanged -= OnSettingsChanged;
            },
            refresh: Reload);
    }

    /// <summary>Bölme kutucukları (masaüstündeki klasörler değişebilir), liste ve ayarlar baştan.</summary>
    private void Reload()
    {
        FenceTiles.Children.Clear();
        WidgetCatalog.AddTiles(FenceTiles, WidgetCatalog.Fences(), AddWidget);
        OnWidgetsChanged();
        OnSettingsChanged();
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
    /// yalnızca bir widget'ın adı ya da simgesi değiştiyse (başlık, not, yapılacaklar ilerlemesi) yeniden kurulur.
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
            !rows.Select(r => (r.Name, r.Icon)).SequenceEqual(AppHost.Settings.Widgets.Select(w => (WidgetText.DisplayName(w), WidgetIcons.For(w)))))
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
            var mode = DesktopModes.Current;
            IconModeBox.SelectedIndex = Array.IndexOf(DesktopModes.Choices, mode);
            IconModeText.Text = DesktopModes.Description(mode);
            PlacementBox.SelectedIndex = Array.IndexOf(PlaceModes.Choices, PlaceModes.Parse(AppHost.Settings.NewWidgetPlacement));
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

    private void IconModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || IconModeBox.SelectedItem is not ComboBoxItem { Tag: IconMode mode }) return;
        DesktopModes.Set(mode, WindowCenter(), Window.GetWindow(this));
        // Soru penceresinden sonra ya da ayar değişmediyse de kutu gerçek durumu göstersin.
        RefreshSettings();
    }

    private void PlacementBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PlacementBox.SelectedItem is not ComboBoxItem { Tag: PlaceMode mode }) return;
        DesktopModes.SetPlacement(mode);
    }

    /// <summary>Kutucuktaki widget'ı ana pencerenin ekranına ekler; bildirimdeki "Bul" onu öne getirir.</summary>
    private void AddWidget(WidgetChoice choice)
    {
        var folder = choice.Key.StartsWith(FolderKeyPrefix, StringComparison.Ordinal) ? choice.Key[FolderKeyPrefix.Length..] : null;
        // Klasör eklerken açılacak mı? Kutucuktaki rozete değil diske bakılır (kutucuk kurulduktan sonra değişmiş olabilir).
        var creates = folder is not null && !AppHost.DesktopFolders().Any(f => FolderName.Equal(f, folder));
        // Bölme ve kutu başlığı düzenlenir hâlde gelir (ad yazılabilir, simgesi tıklanınca seçici açılır).
        if (WidgetCatalog.Invoke(choice, WindowCenter(), renameAfterAdd: true) is not { } config) return; // klasör açılamadı ya da vazgeçildi
        // Pencere açılamadıysa WidgetManager widget'ı geri çıkarıp uyardı: "eklendi" denmesin.
        if (!AppHost.Settings.Widgets.Any(w => w.Id == config.Id)) return;

        var text = choice.Key == WidgetCatalog.NewFenceKey ? L.F("\"{0}\" bölmesi masaüstüne eklendi.", FenceTitle(config))
            : choice.Group == WidgetGroup.Tool ? $"{choice.Label} masaüstüne eklendi."
            : creates ? $"\"{folder}\" bölmesi eklendi; masaüstünde \"{folder}\" klasörü de oluşturuldu."
            : $"\"{choice.Label}\" bölmesi masaüstüne eklendi.";
        Notice.Show(text, NoticeKind.Success, "Bul", () => AppHost.Widgets.Reveal(config.Id));
        if (creates) RefreshTile(choice.Key);
    }

    /// <summary>Bölmenin görünen başlığı (kullanıcının verdiği ya da kaynağının adı).</summary>
    private static string FenceTitle(WidgetConfig config) =>
        config.Title ?? (config.Filter != DesktopFilter.None ? DesktopItems.Label(config.Filter) : config.FolderName ?? "");

    /// <summary>Klasör açıldı: kutucuğun "yeni klasör" rozeti kalksın. Yerinde değiştirilir ("Diğer klasörler" açık kalır).</summary>
    private void RefreshTile(string key)
    {
        var old = FenceTiles.Children.OfType<FrameworkElement>()
            .FirstOrDefault(t => System.Windows.Automation.AutomationProperties.GetAutomationId(t) == "Add." + key);
        if (old is null || WidgetCatalog.Fences().FirstOrDefault(c => c.Key == key) is not { } fresh) return;
        var tile = WidgetCatalog.Tile(fresh, AddWidget);
        tile.Visibility = old.Visibility;
        // Dolu bir yuvaya doğrudan atamak (Children[i] = tile) WPF'te hata verir: önce eskisi çıkarılır.
        var index = FenceTiles.Children.IndexOf(old);
        FenceTiles.Children.RemoveAt(index);
        FenceTiles.Children.Insert(index, tile);
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
        var id = row.Config.Id;
        var returning = BoxMover.MovedOnlyIn(row.Config);
        var modeOff = AppHost.Widgets.RemoveWithUndo(id, notify: false);
        // "Geri al" bu widget'ı getirir: bildirim açıkken masaüstünden başka bir widget kaldırılsa da.
        Notice.Show($"{name} kaldırıldı." +
                    (returning > 0 ? $" Kutudaki {returning} öğe masaüstüne geri konuyor." : "") +
                    (modeOff ? " Masaüstü simgeleri yeniden gösteriliyor." : ""),
            NoticeKind.Info, "Geri al", () => AppHost.Widgets.UndoRemove(id));
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
        var sameName = L.Sorter;
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
        var backedUp = !LayoutBackup.IsApply(row.Name);
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
