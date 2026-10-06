using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>Masaüstündeki bir widget'ın liste satırı: simge, ad ve türü. Düğme adı ekran okuyucu ve UI Automation için ("Düzenle: Saat").</summary>
public sealed class WidgetRow(WidgetConfig config)
{
    public WidgetConfig Config { get; } = config;
    public string Id => Config.Id;
    public string Name { get; } = WidgetText.DisplayName(config);
    public string Kind { get; } = WidgetEditor.KindName(config);
    public string EditName => L.F("Düzenle: {0}", Name);

    /// <summary>Tür satırı yalnızca addan farklıysa ("Saat / Saat" yazmasın).</summary>
    public Visibility KindVisibility => string.Equals(Kind, Name, StringComparison.Ordinal) ? Visibility.Collapsed : Visibility.Visible;

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
/// Widget'lar: widget'ların yönetim yeri. Üstte "Widget ekle" (her yerdeki gibi tek pencere); "Tüm widget'lar" şeridi (Düzenle
/// panelindeki galeri ve beş renk, bütün widget'lara uygulanır); "Masaüstündekiler" listesi ve yanında seçilen widget'ın
/// gömülü Düzenle paneli (<see cref="WidgetEditor"/>; dar pencerede satırdaki "Düzenle" paneli kendi penceresinde açar);
/// katlı "Daha fazla" (hizala, öne getir, kayıtlı yerleşimler). Satır seçmek widget'ı öne getirir ve bir kez nabız attırır.
/// Her eylemin sonucu bildirim şeridinde; görünüm değişikliği ve silme oradan geri alınır.
/// </summary>
public partial class WidgetsPage : Page
{
    /// <summary>Panelin yanda sığdığı en dar sayfa (DIP; yaklaşık 880 DIP'lik pencere): liste en az 200 + 16 + 360 + iç boşluk.</summary>
    private const double WideWidth = 680;

    /// <summary>Galerinin tek sıraya (8 kart) sığdığı en dar sayfa (DIP).</summary>
    private const double GalleryOneRow = 700;

    private readonly WidgetEditor _editor = new() { ShowDone = false };
    private LookGalleryControl? _gallery;
    private readonly List<(WidgetAccent Accent, RadioButton Button, Ellipse Fill, Ellipse Ring)> _dots = [];

    private bool _wide = true;
    private bool _shown;
    private string? _selectedId;

    // Liste ve seçim koddan kurulurken SelectionChanged widget'ı öne getirmesin; görünüm denetimleri koddan güncellenirken
    // seçim bütün widget'ları yeniden boyamasın.
    private bool _syncingList;
    private bool _loading;

    public WidgetsPage()
    {
        InitializeComponent();
        EditorHost.Content = _editor;
        AutomationProperties.SetAutomationId(_editor, "Widgets.Editor");
        Fold.Attach(MoreFold, MoreContent);
        BuildLook();
        Loaded += (_, _) => Reload();
        // Ana pencere gizliyken sayfa widget değişikliklerine ve kayıtlara tepki vermez, gömülü panel widget'ı bırakır;
        // yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                _shown = true;
                AppHost.Widgets.Changed += OnWidgetsChanged;
                AppHost.SettingsChanged += OnSettingsChanged;
                EditSelected();
            },
            detach: () =>
            {
                _shown = false;
                AppHost.Widgets.Changed -= OnWidgetsChanged;
                AppHost.SettingsChanged -= OnSettingsChanged;
                _editor.Release();
            },
            refresh: Reload);
    }

    /// <summary>Liste, görünüm şeridi ve yerleşimler baştan.</summary>
    private void Reload()
    {
        _listed = null; // baştan kurulan liste oynamaz (sayfa zaten girerek gelir)
        RefreshWidgets();
        RefreshLayouts();
        RefreshLook();
    }

    /// <summary>"Widget ekle": tepsideki, Ctrl+Alt+B'deki ve halkadaki pencerenin aynısı (yeni widget ayardaki yere gelir).</summary>
    private void AddWidget_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowQuickAdd();

    /// <summary>Widget eklendi, kaldırıldı, düzen uygulandı ya da yerleştirildi: liste, panel, yerleşimler ve görünüm şeridi.</summary>
    private void OnWidgetsChanged()
    {
        RefreshWidgets();
        RefreshLayouts(); // düzenli yerleştirme ve düzen uygulama yedek düzen ekler
        RefreshLook();
        _editor.Refresh();
    }

    /// <summary>
    /// Ayarlar kaydedildi (widget sürüklemek de kaydeder): görünüm şeridi ve panel tazelenir. Liste yalnızca bir widget'ın adı,
    /// türü ya da simgesi değiştiyse (başlık, not, yapılacaklar) yeniden kurulur.
    /// </summary>
    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(OnSettingsChanged);
            return;
        }
        if (!_shown) return;
        RefreshLook();
        if (ActiveList.ItemsSource is IEnumerable<WidgetRow> rows &&
            !rows.Select(r => (r.Id, r.Name, r.Kind, r.Icon)).SequenceEqual(AppHost.Widgets.Configs.Select(w =>
                (w.Id, WidgetText.DisplayName(w), WidgetEditor.KindName(w), WidgetIcons.For(w)))))
            RefreshWidgets();
        else EditSelected(); // açılışta ya da düzen uygulanınca pencereler sırayla açılır
        _editor.Refresh();
    }

    // ------------------------------------------------------------------ liste ve panel

    private void RefreshWidgets()
    {
        var rows = AppHost.Widgets.Configs.Select(c => new WidgetRow(c)).ToList();
        var before = _listed;
        _listed = rows.Select(r => r.Id).ToHashSet();
        _syncingList = true;
        try
        {
            ActiveList.ItemsSource = rows;
            // Seçili widget kaldırıldıysa ilk satır (geniş pencerede panel boş kalmasın); dar pencerede seçim yalnızca kullanıcıdan.
            var selected = rows.FirstOrDefault(r => r.Id == _selectedId) ?? (_wide ? rows.FirstOrDefault() : null);
            ActiveList.SelectedItem = selected;
            _selectedId = selected?.Id;
        }
        finally { _syncingList = false; }
        ActiveHeader.Text = L.F("Masaüstündekiler ({0})", rows.Count);
        var any = rows.Count > 0;
        ManageGrid.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        EmptyCard.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        RevealAllButton.IsEnabled = ArrangeButton.IsEnabled = any;
        EditSelected();
        if (before is not null) EnterNewRows(rows.Where(r => !before.Contains(r.Id)).ToList());
    }

    /// <summary>Kullanıcı bir satır seçti: widget öne gelir ve bir kez nabız atar; geniş pencerede panel ona geçer.</summary>
    private void ActiveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingList || ActiveList.SelectedItem is not WidgetRow row) return;
        _selectedId = row.Id;
        EditSelected();
        AppHost.Widgets.Reveal(row.Id);
    }

    /// <summary>Gömülü panel seçili widget'ı düzenler (yalnızca geniş pencerede ve sayfa görünürken; penceresi henüz yoksa boş).</summary>
    private void EditSelected()
    {
        if (!_shown || !_wide) return;
        var window = _selectedId is null ? null : AppHost.Widgets.WindowOf(_selectedId);
        if (window is null)
        {
            if (_editor.Target is not null) _editor.Release();
            return;
        }
        if (!ReferenceEquals(_editor.Target, window)) _editor.Edit(window);
    }

    /// <summary>Dar pencere: satırdaki "Düzenle" paneli kendi penceresinde, kartın yanında açar.</summary>
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WidgetRow row) return;
        _selectedId = row.Id;
        AppHost.EnsureWidgetsShown();
        if (AppHost.Widgets.WindowOf(row.Id) is { } window) WidgetEditorWindow.ShowFor(window);
    }

    /// <summary>
    /// Pencerenin genişliğine göre: panel yanda (geniş) ya da satırdaki "Düzenle" (dar); galeri tek ya da iki sıra. Genişlik
    /// pencereden gelir (sütun yıldızlı), içerikten değil: karar geri beslenmez.
    /// </summary>
    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        if (_gallery is not null) _gallery.Columns = width >= GalleryOneRow ? 8 : 4;
        var wide = width >= WideWidth;
        if (wide == _wide && e.PreviousSize.Width > 0) return;
        _wide = wide;
        GapColumn.Width = new GridLength(wide ? 16 : 0);
        EditorColumn.Width = wide ? new GridLength(WidgetEditorWindow.PanelWidth) : new GridLength(0);
        EditorCard.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        ActiveList.Tag = wide ? Visibility.Collapsed : Visibility.Visible;
        if (!wide)
        {
            _editor.Release();
            return;
        }
        if (_selectedId is null && ActiveList.Items.Count > 0)
        {
            _syncingList = true;
            try { ActiveList.SelectedIndex = 0; }
            finally { _syncingList = false; }
            _selectedId = (ActiveList.SelectedItem as WidgetRow)?.Id;
        }
        EditSelected();
    }

    /// <summary>
    /// Panelin içi kendi kaydırıcısıyla kurulur; sayfanın içinde boyu sınırsız olduğundan kaymaz, ama tekerleği yine yutardı.
    /// Panel kayamıyorsa tekerlek sayfaya geçer.
    /// </summary>
    private void EditorCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || FindScroller(_editor) is { ScrollableHeight: > 0 }) return;
        e.Handled = true;
        var again = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = sender };
        ManageGrid.RaiseEvent(again);
    }

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scroller) return scroller;
            if (FindScroller(child) is { } nested) return nested;
        }
        return null;
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
        }, DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ Tüm widget'lar

    /// <summary>Görünüm şeridinin hedefleri: notlar kendi kağıt rengini kullanır.</summary>
    private static List<WidgetConfig> LookTargets() => AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList();

    /// <summary>Kartları ve noktaları çizmek için örnek widget (ilk hedef; yoksa varsayılan görünüş).</summary>
    private static WidgetConfig Sample() => LookTargets().FirstOrDefault() ?? new WidgetConfig();

    /// <summary>Hepsinde aynıysa o değer, değilse (ya da hedef yoksa) null: seçim görünmez.</summary>
    private static T? Common<T>(IEnumerable<T> values) where T : class
    {
        var distinct = values.Distinct().Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private void BuildLook()
    {
        _gallery = new LookGalleryControl("Widgets.Look.",
            id => WidgetPalette.Resolve(LookGallery.Probe(Sample(), id, WidgetPalette.SystemDark), null, WidgetPalette.SystemDark, highContrast: false),
            () => Common(LookTargets().Select(LookGallery.Current)),
            id =>
            {
                if (_loading) return;
                ApplyToAll(c => LookGallery.Apply(c, id, WidgetPalette.SystemDark));
            })
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        GalleryHost.Content = _gallery;

        (WidgetAccent Accent, string Label)[] named =
        [
            (WidgetAccent.Violet, L.T("Mor")), (WidgetAccent.Blue, L.T("Mavi")), (WidgetAccent.Green, L.T("Yeşil")),
            (WidgetAccent.Orange, L.T("Turuncu")), (WidgetAccent.Pink, L.T("Pembe")),
        ];
        var group = "WidgetsAccent" + Guid.NewGuid().ToString("N");
        for (var i = 0; i < named.Length; i++)
        {
            var (accent, label) = named[i];
            var fill = new Ellipse { Width = 22, Height = 22 };
            var ring = new Ellipse { Width = 28, Height = 28, StrokeThickness = 2 };
            // Düz şablon: WPF-UI'nin geçişli RadioButton şablonu yok; Sekme grupta bir kez durur, oklar gezer ve seçer.
            var button = new RadioButton
            {
                Content = new Grid { Width = 28, Height = 28, Children = { ring, fill } }, GroupName = group, Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand, MinWidth = 0, MinHeight = 0, // WPF-UI'nin örtük RadioButton stili 120 DIP genişlik verirdi
                Template = new ControlTemplate(typeof(RadioButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            };
            AutomationProperties.SetAutomationId(button, "Widgets.Accent." + (i + 1));
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, L.T("Renk"));
            button.Checked += (_, _) =>
            {
                if (_loading) return;
                ApplyToAll(c => WidgetLooks.SetAccent(c, accent));
            };
            button.GotKeyboardFocus += (_, e) =>
            {
                var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
                if (arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true) button.IsChecked = true;
            };
            _dots.Add((accent, button, fill, ring));
            AccentDots.Children.Add(button);
        }
    }

    /// <summary>Görünüş değişikliği bütün widget'lara; bildirimdeki "Geri al" her birini önceki görünüşüne döndürür.</summary>
    private void ApplyToAll(Action<WidgetConfig> change)
    {
        var before = AppHost.Widgets.ChangeLookForAll(change);
        RefreshLook();
        if (before.Count == 0) return;
        Notice.Show(L.T("Görünüm bütün widget'lara uygulandı."), NoticeKind.Success, L.T("Geri al"),
            () => AppHost.Widgets.UndoLookCopy(before));
    }

    /// <summary>Kartlar ve noktalar gerçek paletle yeniden çizilir; hepsinde aynı olan seçim işaretlenir.</summary>
    private void RefreshLook()
    {
        var targets = LookTargets();
        var highContrast = SystemParameters.HighContrast;
        LookCard.Visibility = targets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        GalleryHost.Visibility = AccentPanel.Visibility = highContrast ? Visibility.Collapsed : Visibility.Visible;
        LookHint.Text = highContrast
            ? L.T("Yüksek karşıtlık açık: widget'lar Windows'un renklerini kullanır.")
            : L.T("Seçtiğin görünüm bütün widget'lara uygulanır. Notlar kendi rengini korur.");
        if (highContrast || targets.Count == 0) return;
        _loading = true;
        try
        {
            _gallery?.Refresh();
            var sample = Sample();
            var shown = targets.Select(WidgetLooks.ShownAccent).Distinct().Take(2).ToList();
            var common = shown.Count == 1 ? shown[0] : null;
            var selection = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
            foreach (var (accent, button, fill, ring) in _dots)
            {
                fill.Fill = WidgetPalette.Resolve(LookGallery.ProbeAccent(sample, accent), null, WidgetPalette.SystemDark, highContrast: false).Accent;
                var on = common == accent;
                ring.Stroke = on ? selection : Brushes.Transparent;
                button.IsChecked = on;
            }
        }
        finally { _loading = false; }
    }

    // ------------------------------------------------------------------ Daha fazla

    private void RevealAll_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RevealAll();

    private void Arrange_Click(object sender, RoutedEventArgs e)
    {
        var count = AppHost.Widgets.ArrangeAll(out var moves);
        // "Geri al" yalnızca konumları geri koyar (yedeği bütünüyle uygulamak bildirim açıkken eklenen widget'ı silerdi);
        // önceki yerleşim ayrıca Otomatik yedekler'de durur.
        if (count == 0) Notice.Show(L.T("Hizalanacak widget yok."), NoticeKind.Info);
        else Notice.Show(L.P(count, "{0} widget hizalandı."), NoticeKind.Success, L.T("Geri al"), () => AppHost.Widgets.UndoArrange(moves));
    }

    /// <summary>Otomatik yedekler: uygulamanın düzen uygulamadan ya da hizalamadan önce aldığı kopyalar.</summary>
    private static bool IsBackup(LayoutSnapshot layout) => LayoutBackup.IsApply(layout.Name) || LayoutBackup.IsArrange(layout.Name);

    private void RefreshLayouts()
    {
        var all = AppHost.Settings.Layouts.OrderByDescending(l => l.Created).ToList();
        var named = all.Where(l => !IsBackup(l)).Select(l => new LayoutRow(l)).ToList();
        var backups = all.Where(IsBackup).Select(l => new LayoutRow(l)).ToList();
        LayoutList.ItemsSource = named;
        BackupList.ItemsSource = backups;
        LayoutsTitle.Text = L.F("Kayıtlı yerleşimler ({0})", named.Count);
        LayoutEmpty.Visibility = named.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BackupsFold.Content = L.F("Otomatik yedekler ({0})", backups.Count);
        BackupsFold.Visibility = backups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (backups.Count == 0) BackupList.Visibility = Visibility.Collapsed;
    }

    private void BackupsFold_Click(object sender, RoutedEventArgs e) =>
        BackupList.Visibility = BackupList.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

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
