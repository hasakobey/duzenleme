using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>Masaüstündeki bir widget'ın liste satırı. Düğme adları ekran okuyucu ve UI Automation için ("Kaldır: Saat").</summary>
public sealed class WidgetRow(WidgetConfig config)
{
    public WidgetConfig Config { get; } = config;
    public string Name { get; } = WidgetText.DisplayName(config);
    public string RevealName => L.F("Bul: {0}", Name);
    public string RemoveName => L.F("Kaldır: {0}", Name);

    /// <summary>Widget'ın başlığındaki simgenin aynısı (kullanıcının seçtiği ya da türün varsayılanı, bkz. <see cref="WidgetIcons"/>).</summary>
    public SymbolRegular Icon { get; } = WidgetIcons.For(config);

    /// <summary>Liste öğesinin ekran okuyucudaki adı (yoksa sınıfın adı okunur).</summary>
    public override string ToString() => Name;
}

/// <summary>Kayıtlı düzen satırı.</summary>
public sealed record LayoutRow(LayoutSnapshot Layout)
{
    public string Name => Layout.Name;
    public string Detail => L.P(Layout.Widgets.Count, "{0} widget · {1}", UiText.When(Layout.Created));
    public string ApplyName => L.F("Uygula: {0}", Name);
    public string DeleteName => L.F("Düzeni sil: {0}", Name);

    /// <summary>Liste öğesinin ekran okuyucudaki adı (kaydın varsayılan ToString'i bütün alanları okurdu).</summary>
    public override string ToString() => Name;
}

/// <summary>
/// Widget'lar: "Widget ekle" (her yerdeki gibi tek pencere), masaüstündekilerin listesi (Bul / Kaldır), toplu görünüm ve
/// kayıtlı yerleşimler. Her eylemin sonucu ana penceredeki bildirim şeridinde görünür; kaldırma ve silme oradan geri alınır.
/// </summary>
public partial class WidgetsPage : Page
{
    // "Arka plan" ve "Vurgu rengi" kutularındaki sıra.
    private static readonly WidgetStyle[] Styles = [WidgetStyle.Glass, WidgetStyle.Dark, WidgetStyle.Light];
    private static readonly WidgetAccent[] Accents =
        [WidgetAccent.Violet, WidgetAccent.Blue, WidgetAccent.Green, WidgetAccent.Orange, WidgetAccent.Pink];

    // Kutular koddan güncellenirken SelectionChanged bütün widget'ları yeniden boyamasın.
    private bool _loading;

    public WidgetsPage()
    {
        InitializeComponent();
        Fold.Attach(LookFold, LookContent);
        Fold.Attach(LayoutsFold, LayoutsContent);
        foreach (var (id, label) in LookMenus.ThemeChoices)
            ThemeBox.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        Loaded += (_, _) => Reload();
        // Ana pencere gizliyken sayfa widget değişikliklerine ve kayıtlara tepki vermez; yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.Widgets.Changed += OnWidgetsChanged;
                AppHost.SettingsChanged += OnSettingsChanged;
            },
            detach: () =>
            {
                AppHost.Widgets.Changed -= OnWidgetsChanged;
                AppHost.SettingsChanged -= OnSettingsChanged;
            },
            refresh: Reload);
    }

    /// <summary>Liste ve ayarlar baştan.</summary>
    private void Reload()
    {
        _listed = null; // baştan kurulan liste oynamaz (sayfa zaten girerek gelir)
        OnWidgetsChanged();
        OnSettingsChanged();
    }

    /// <summary>"Widget ekle": tepsideki, Ctrl+Alt+B'deki ve halkadaki pencerenin aynısı (yeni widget ayardaki yere gelir).</summary>
    private void AddWidget_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowQuickAdd();

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
        var before = _listed;
        _listed = rows.Select(r => r.Config.Id).ToHashSet();
        ActiveList.ItemsSource = rows;
        ActiveHeader.Text = L.F("Masaüstündekiler ({0})", rows.Count);
        ActiveEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RevealAllButton.IsEnabled = ArrangeButton.IsEnabled = rows.Count > 0;
        if (before is not null) EnterNewRows(rows.Where(r => !before.Contains(r.Config.Id)).ToList());
    }

    // 2.2 Hareket
    /// <summary>Listedeki widget'ların kimlikleri (sayfa açıkken eklenenler ayırt edilsin); sayfa baştan kurulunca boş.</summary>
    private HashSet<string>? _listed;

    /// <summary>Bir güncellemede en çok bu kadar yeni satır girerek gelir; daha çoğunda (düzen uygulandı) hiçbiri oynamaz.</summary>
    private const int MaxRowEnter = 5;

    /// <summary>
    /// Sayfa açıkken eklenen (ya da geri getirilen) widget'ın satırı aşağıdan kısa bir yükselişle gelir (Views.Motion; Sakin'de
    /// yalnızca solma). Kaldırılan satır hemen gider.
    /// </summary>
    private void EnterNewRows(List<WidgetRow> added)
    {
        if (added.Count is 0 or > MaxRowEnter) return;
        Dispatcher.BeginInvoke(() =>
        {
            var rows = added.Select(r => ActiveList.ItemContainerGenerator.ContainerFromItem(r)).OfType<UIElement>().ToList();
            if (rows.Count > 0) Motion.Cascade(rows, MotionKind.Rise);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void RefreshLayouts()
    {
        var rows = AppHost.Settings.Layouts.OrderByDescending(l => l.Created).Select(l => new LayoutRow(l)).ToList();
        LayoutList.ItemsSource = rows;
        LayoutsTitle.Text = L.F("Kayıtlı yerleşimler ({0})", rows.Count);
        LayoutEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Toplu görünüm denetimleri ayarlardaki değeri gösterir.</summary>
    private void RefreshSettings()
    {
        _loading = true;
        try
        {
            // Notlar kendi kağıt rengini kullanır: toplu görünüm onları saymaz.
            var targets = AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList();
            // Tema açıkken "Arka plan" boş görünür (menüdeki gibi); özel vurgu rengi de adlı renklerden biri sayılmaz.
            var themes = LookMenus.ThemeChoices.Select(c => c.Id).ToList();
            ThemeBox.SelectedIndex = Common(targets.Select(w => themes.IndexOf(WidgetLooks.ActiveTheme(w) ?? "")));
            StyleBox.SelectedIndex = Common(targets.Select(w => WidgetLooks.ShownStyle(w) is { } s ? Array.IndexOf(Styles, s) : -1));
            AccentBox.SelectedIndex = Common(targets.Select(w => WidgetLooks.ShownAccent(w) is { } a ? Array.IndexOf(Accents, a) : -1));
            ThemeBox.IsEnabled = StyleBox.IsEnabled = AccentBox.IsEnabled = targets.Count > 0;
            ThemeNote.Visibility = SystemParameters.HighContrast ? Visibility.Visible : Visibility.Collapsed;
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

    private void RevealAll_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RevealAll();

    private void Arrange_Click(object sender, RoutedEventArgs e)
    {
        var count = AppHost.Widgets.ArrangeAll(out var moves);
        // "Geri al" yalnızca konumları geri koyar (yedeği bütünüyle uygulamak bildirim açıkken eklenen widget'ı silerdi);
        // önceki yerleşim ayrıca Kayıtlı yerleşimler'de durur.
        if (count == 0) Notice.Show(L.T("Hizalanacak widget yok."), NoticeKind.Info);
        else Notice.Show(L.P(count, "{0} widget hizalandı."), NoticeKind.Success, L.T("Geri al"), () => AppHost.Widgets.UndoArrange(moves));
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
        Notice.Show(WidgetManager.RemovedText(L.F("{0} kaldırıldı.", name), returning, modeOff),
            NoticeKind.Info, L.T("Geri al"), () => AppHost.Widgets.UndoRemove(id));
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string theme }) return;
        AppHost.Widgets.SetLookForAll(null, null, theme);
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

    // Checked/Unchecked (Click değil): ekran okuyucunun "Aç/Kapat"ı da ayarı değiştirsin; koddan atama _loading ile ayrılır.
    private void Snap_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.SnapWidgets = SnapToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void Overlap_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
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
            Notice.Show(L.T("Kaydedilecek widget yok. Önce masaüstüne bir widget ekle."), NoticeKind.Info);
            return;
        }
        var layouts = AppHost.Settings.Layouts;
        var sameName = L.Sorter;
        var name = LayoutName.Text.Trim();
        if (name.Length == 0)
        {
            // Varsayılan ad kayıtta arayüz dilinde kalır (sekme adları gibi, dil değişince yeniden adlandırılmaz).
            var n = layouts.Count + 1;
            while (layouts.Any(l => sameName.Equals(l.Name, L.F("Düzen {0}", n)))) n++;
            name = L.F("Düzen {0}", n);
        }
        // Aynı adlı düzen varsa üzerine yazılır.
        layouts.RemoveAll(l => sameName.Equals(l.Name, name));
        layouts.Add(LayoutSnapshot.Capture(name, AppHost.Settings.Widgets));
        AppHost.SaveSettings();
        LayoutName.Text = "";
        RefreshLayouts();
        Notice.Show(L.F("\"{0}\" düzeni kaydedildi.", name));
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LayoutRow row) return;
        // Yedeğin kendisi uygulanırken yeni yedek alınmaz (WidgetManager.ApplyLayout).
        var backedUp = !LayoutBackup.IsApply(row.Name);
        AppHost.Widgets.ApplyLayout(row.Layout);
        Notice.Show(backedUp
            ? L.F("\"{0}\" düzeni uygulandı. Önceki yerleşim \"{1}\" adıyla kaydedildi.", row.Name, WidgetManager.ApplyBackupName)
            : L.F("\"{0}\" düzeni uygulandı.", row.Name));
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
        Notice.Show(L.F("\"{0}\" düzeni silindi.", layout.Name), NoticeKind.Info, L.T("Geri al"), () =>
        {
            if (AppHost.Settings.Layouts.Contains(layout)) return;
            AppHost.Settings.Layouts.Insert(Math.Min(index, AppHost.Settings.Layouts.Count), layout);
            AppHost.SaveSettings();
            RefreshLayouts();
        });
    }
}
