using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Sekmeli kısayol kutusu (ViPad'in sekmeleri + LaunchBar Commander'ın dock'u gibi).
/// Sürükle-bırakla uygulama/dosya/klasör eklenir; dosyalar taşınmaz, yalnızca kısayol olarak tutulur.
/// </summary>
public partial class LauncherView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private WidgetPalette _palette = WidgetPalette.Glass;

    // Yerinde yeniden adlandırma: başlık ve öğenin görünen adı (dosyaya dokunulmaz, bkz. ItemLooks).
    private readonly TitleEditor _titleEditor;
    private TileRename? _rename;
    private bool _renderDeferred;
    private string? _selectAfterRender;

    public LauncherView(WidgetConfig config)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab { Name = L.T("Uygulamalar") });
        InitializeComponent();

        _titleEditor = new TitleEditor(this, Header, TitleText, HeaderIconButton, () => DefaultTitle, CommitTitle, PickIcon, ApplyParts);
        Header.MouseLeftButtonDown += (_, e) =>
        {
            Items.SelectedItem = null; // F2 artık başlığı adlandırır
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };
        Header.SizeChanged += (_, e) => { if (e.WidthChanged) ApplyParts(); };
        // Pencereye bağlanınca (ölçeği artık kesin) simgeler o ekranın piksel boyutunda istenir.
        Loaded += (_, _) => UpdateIconSizes();

        Items.PreviewMouseLeftButtonUp += OnItemClick;
        Items.KeyDown += OnItemsKey;
        Menus.AttachItemMenu(Items, FillItemMenu);

        Menus.EnableDragOut(Items, DragDropEffects.Copy | DragDropEffects.Link);
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        // Öğeler masaüstünden kutuya taşınınca ya da geri konunca yolları değişir; bir bölmede yeniden adlandırılınca da.
        BoxMover.Changed += Render;
        AppHost.PathRenamed += OnPathRenamed;
        Render();
    }

    public bool Resizable => true;
    public bool Collapsible => _config.Shows("header");
    public Thickness CardPadding => new(14, 12, 14, 12);
    public event Action? CollapseToggleRequested;
    public event Action? MenuRequested;
    public event Action? LayoutChanged;

    // Etiketler Menus.Parts'ta çevrilir (L.Dyn).
    private static readonly (string Key, string Label)[] LauncherParts =
        [("header", L.N("Başlık satırı")), ("count", L.N("Öğe sayısı")), ("tabs", L.N("Sekmeler")), Menus.ClosePart];

    /// <summary>
    /// Kullanıcının kapattığı parçaları gizler. Dar kutuda başlık okunsun diye öğe sayısı ve başlık simgesi (bu sırayla)
    /// geçici olarak gizlenir; kaydedilmez, genişleyince döner.
    /// </summary>
    private void ApplyParts()
    {
        Header.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        HeaderIconButton.Visibility = Visibility.Visible;
        CountText.Visibility = _config.Shows("count") ? Visibility.Visible : Visibility.Collapsed;
        TabStrip.Visibility = AddTab.Visibility = _config.Shows("tabs") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        // Başlık düzenlenirken kutuya yer açılır, simge düğmesi (seçiciyi açar) gizlenmez.
        _titleEditor.Fit([CountText, HeaderIconButton], [RemoveButton]);
    }

    private List<TileItem> _tiles = [];

    /// <summary>
    /// Simgelerin istendiği ekran ölçeği: pencereye bağlıysa bulunduğu monitörünki, değilse (açılış) açılacağı monitörünki.
    /// </summary>
    private double IconDpi => PresentationSource.FromVisual(this) is not null
        ? VisualTreeHelper.GetDpi(this).PixelsPerDip
        : WidgetWindow.ExpectedPixelsPerDip(_config);

    /// <summary>Ekran ya da widget ölçeği değişince simgeler yeni piksel boyutunda istenir (değişmediyse bir şey yapmaz).</summary>
    private void UpdateIconSizes()
    {
        var dpi = IconDpi;
        foreach (var item in _tiles) item.UpdateIconSize(dpi, _config.Scale);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateIconSizes();
    }

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private LauncherTab Current => _config.Tabs[Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1)];

    private static string DefaultTitle => L.T("Kısayol kutusu");

    private void Render()
    {
        // Öğenin adı düzenlenirken liste yeniden kurulmaz (kutu ve yazılan kaybolurdu); düzenleme bitince yapılır.
        if (_rename is { IsActive: true })
        {
            _renderDeferred = true;
            return;
        }
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? DefaultTitle : _config.Title;
        HeaderIcon.Symbol = WidgetIcons.For(_config);
        CountText.Text = L.P(_config.Tabs.Sum(t => t.Items.Count), "{0} öğe");
        RenderTabs();
        ApplyParts();

        if (TileItem.PanelKey(_config) != _panelKey)
        {
            _panelKey = TileItem.PanelKey(_config);
            Items.ItemsPanel = TileItem.Panel(_config);
        }
        // Yollar arka planda denetlenir (ağ yolları süre sınırıyla): kapalı bir NAS'taki öğe açılışı ya da sekme
        // değiştirmeyi bekletmez; sonuç gelince öğe soluklaşır ya da simgesini alır.
        // Simgeler bu ekranın gerçek piksel boyutunda istenir (yol denetimi bitince). Kullanıcının verdiği ad ve simge
        // (ItemLooks) öğenin yoluna bağlıdır: sekme değişse de gider.
        var dpi = IconDpi;
        var looks = ItemLooks.Lookup(_config);
        _glyphColor = TileItem.GlyphColor(_config);
        _tiles = Current.Items.Select(p =>
        {
            looks.TryGetValue(p, out var look);
            return TileItem.CreateUnchecked(p, _config, look?.Name, pixelsPerDip: dpi, icon: look?.Icon);
        }).ToList();
        Items.ItemsSource = _tiles;
        EmptyState.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_selectAfterRender is { } path)
        {
            _selectAfterRender = null;
            if (_tiles.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase)) is { } item) SelectAndFocus(item);
        }
        WatchRecycleBin(_tiles.Any(t => TileItem.IsRecycleBin(t.Path)));
    }

    private bool _watchingBin;

    /// <summary>Kutuda Geri Dönüşüm Kutusu varsa dolup boşalınca simgesi yenilenir (önbellekte eskisi kalmasın).</summary>
    private void WatchRecycleBin(bool watch)
    {
        if (watch == _watchingBin) return;
        _watchingBin = watch;
        if (watch) Desktop.RecycleBin.Changed += OnRecycleBinChanged;
        else Desktop.RecycleBin.Changed -= OnRecycleBinChanged;
    }

    private void OnRecycleBinChanged()
    {
        ShellIcons.ForgetShell(Desktop.RecycleBin.ShellName);
        foreach (var tile in _tiles.Where(t => TileItem.IsRecycleBin(t.Path))) tile.ReloadIcon();
    }

    /// <summary>Fluent simgeli öğelerin çizildiği vurgu rengi (değişince öğeler yeniden kurulur).</summary>
    private System.Windows.Media.Color _glyphColor;

    /// <summary>Öğeyi seçer ve (widget etkinse) odaklar: Gezgin gibi F2 ve yön tuşları hemen çalışır.</summary>
    private void SelectAndFocus(TileItem item)
    {
        Items.SelectedItem = item;
        Items.ScrollIntoView(item);
        if (Window.GetWindow(this)?.IsActive != true) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (Items.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container) container.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnPathRenamed(string oldPath, string newPath, bool isDirectory)
    {
        // AppHost kutuların yollarını zaten güncelledi; bu kutuda etkilenen öğe varsa yeniden çizilir.
        if (_config.Tabs.Any(t => t.Items.Any(p => PathRenames.Map(p, newPath, newPath, isDirectory) is not null))) Render();
    }

    private string _panelKey = "";

    private void RenderTabs()
    {
        TabStrip.Items.Clear();
        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var index = i;
            var active = i == Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
            var pill = new Button
            {
                Style = (Style)FindResource("PillButton"),
                ToolTip = L.T("Sağ tık: yeniden adlandır, taşı, sil"),
                Background = active ? _palette.Accent : new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                Content = new TextBlock
                {
                    Text = _config.Tabs[i].Name,
                    FontSize = 12.5,
                    FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = active ? _palette.AccentForeground : _palette.Secondary,
                },
                ContextMenu = Menus.Dynamic(menu => FillTabMenu(menu, index)),
            };
            pill.Click += (_, _) =>
            {
                _config.ActiveTab = index;
                // Seçili sekme önemsiz bir durum: her tıklamada diske yazdırmaz, bir sonraki kayıtla gider.
                AppHost.SaveSettingsLater();
                Render();
            };
            TabStrip.Items.Add(pill);
        }
    }

    /// <summary>Sekme adı soran küçük pencere widget'ın monitöründe açılsın (birincil monitörde değil).</summary>
    private NativeMethods.POINT? DialogPoint => (Window.GetWindow(this) as WidgetWindow)?.CenterPoint;

    private void FillTabMenu(ContextMenu menu, int index)
    {
        var tab = _config.Tabs[index];
        menu.Items.Add(Menus.Item(L.T("Yeniden adlandır…"), () =>
        {
            if (InputDialog.Ask(L.T("Sekmeyi yeniden adlandır"), L.T("Sekme adı"), tab.Name, DialogPoint) is { Length: > 0 } name)
                Change(() => tab.Name = name);
        }));
        if (index > 0)
            menu.Items.Add(Menus.Item(L.T("Sola taşı"), () => Change(() => Swap(index, index - 1))));
        if (index < _config.Tabs.Count - 1)
            menu.Items.Add(Menus.Item(L.T("Sağa taşı"), () => Change(() => Swap(index, index + 1))));
        if (_config.Tabs.Count > 1)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item(L.T("Sekmeyi sil"), () =>
            {
                Change(() =>
                {
                    _config.Tabs.RemoveAt(index);
                    _config.ActiveTab = Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
                    // Başka sekmede kalmayan öğelerin adları ve simgeleri de gider (son silinen öğe "Geri al" için tutulur).
                    ItemLooks.Prune(_config, _lastRemoved is { } last ? [last.Path] : null);
                });
                // Sekmedeki, masaüstünden taşınmış öğeler başka kutuda yoksa masaüstüne döner.
                BoxMover.Reconcile();
            }));
        }
    }

    private void Swap(int a, int b)
    {
        (_config.Tabs[a], _config.Tabs[b]) = (_config.Tabs[b], _config.Tabs[a]);
        if (_config.ActiveTab == a) _config.ActiveTab = b;
        else if (_config.ActiveTab == b) _config.ActiveTab = a;
    }

    private void AddTab_Click(object sender, RoutedEventArgs e) => NewTab();

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    private void NewTab()
    {
        if (InputDialog.Ask(L.T("Yeni sekme"), L.T("Sekme adı"), L.F("Sekme {0}", _config.Tabs.Count + 1), DialogPoint) is not { Length: > 0 } name)
            return;
        Change(() =>
        {
            _config.Tabs.Add(new LauncherTab { Name = name });
            _config.ActiveTab = _config.Tabs.Count - 1;
        });
    }

    private void Change(Action change)
    {
        change();
        AppHost.SaveSettings();
        // Kutudaki masaüstü dosyaları kurallarla taşınmasın (bkz. DesktopOrganizer.Pinned).
        AppHost.RefreshPinnedPaths();
        Render();
    }

    /// <summary>Son kaldırılan öğe (menüdeki "Geri al" için); kullanıcının verdiği ad ve simgesiyle.</summary>
    private (LauncherTab Tab, int Index, string Path, ItemLook? Look)? _lastRemoved;

    /// <summary>
    /// Öğeyi listeden çıkarır; kutunun menüsündeki "Geri al" ile yerine döner. Masaüstünden kutuya taşınmış öğe (başka kutuda
    /// yoksa) masaüstüne geri konur; "Geri al" onu yeniden kutuya taşır.
    /// </summary>
    private void RemoveItem(TileItem item)
    {
        var tab = Current;
        var index = tab.Items.FindIndex(p => string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        var moved = BoxMover.IsMoved(item.Path);
        var path = tab.Items[index];
        // Öğe başka sekmede de duruyorsa adı ve simgesi orada kalır.
        var elsewhere = _config.Tabs.Where(t => t != tab).Any(t => t.Items.Contains(path, StringComparer.OrdinalIgnoreCase));
        _lastRemoved = (tab, index, path, elsewhere ? null : ItemLooks.Get(_config, path));
        Change(() =>
        {
            tab.Items.RemoveAt(index);
            if (!elsewhere) ItemLooks.Reset(_config, path);
        });
        if (!moved) return;
        BoxMover.Reconcile();
        if (!BoxPlan.Referenced(AppHost.Settings.Widgets).Contains(item.Path))
            Views.Notice.Show(L.F("\"{0}\" kutudan çıkarıldı ve masaüstüne geri konuyor.", item.Name), Views.NoticeKind.Info, L.T("Geri al"),
                UndoRemove, L.T("Geri almak için buraya tıkla."));
    }

    private void UndoRemove()
    {
        if (_lastRemoved is not { } last || !_config.Tabs.Contains(last.Tab)) return;
        _lastRemoved = null;
        Change(() =>
        {
            last.Tab.Items.Insert(Math.Min(last.Index, last.Tab.Items.Count), last.Path);
            ItemLooks.Restore(_config, last.Path, last.Look);
        });
        // Öğe bu arada masaüstüne geri konduysa kutu onu yeniden bulur (kip açıksa yeniden taşınır).
        BoxMover.Reconcile();
    }

    /// <summary>
    /// Öğeleri etkin sekmeye ekler (varlık denetimi arka planda). "Kutulara eklediklerim masaüstünden kalksın" açıksa
    /// masaüstündekiler kutunun klasörüne taşınır.
    /// </summary>
    private async void AddItems(IReadOnlyList<string> paths)
    {
        var tab = Current;
        var existing = await Task.Run(() => paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList());
        if (!_config.Tabs.Contains(tab)) tab = Current;
        var added = new List<string>();
        Change(() =>
        {
            foreach (var path in existing)
                if (!tab.Items.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    tab.Items.Add(path);
                    added.Add(path);
                }
        });
        if (added.Count > 0 && BoxMover.Active) BoxMover.Claim(_config, added, justAdded: true);
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (Menus.ItemAt(Items, e.OriginalSource) is { Missing: false } item)
        {
            TileItem.Launch(item.Path);
            // Açılan öğe seçili kalmasın (vurgusu ve menüsü sonraki tıklamaları karıştırmasın).
            Items.SelectedItem = null;
        }
    }

    private void FillItemMenu(ContextMenu menu, TileItem item)
    {
        var open = Menus.Item(L.T("Aç"), () => TileItem.Launch(item.Path));
        open.InputGestureText = KeyNames.Enter;
        menu.Items.Add(open);
        // Kutudaki ad ve simge yalnızca bu kutuda görünür; dosyanın kendisine dokunulmaz.
        var rename = Menus.Item(L.T("Yeniden adlandır"), () => BeginLabelRename(item));
        rename.InputGestureText = KeyNames.F2;
        rename.ToolTip = L.T("Yalnızca kutuda görünen ad değişir; dosyanın adı değişmez.");
        menu.Items.Add(rename);
        menu.Items.Add(Menus.Item(L.T("Simgeyi değiştir…"), () => PickItemIcon(item)));
        if (ItemLooks.Get(_config, item.Path) is { IsEmpty: false })
            menu.Items.Add(Menus.Item(L.T("Varsayılan ad ve simge"), () => Change(() => ItemLooks.Reset(_config, item.Path))));
        menu.Items.Add(new Separator());
        var moved = BoxMover.IsMoved(item.Path);
        if (moved)
        {
            // Masaüstünden kutuya taşınmış öğe (NestDesk klasöründe).
            var back = Menus.Item(L.T("Masaüstüne geri koy"), () => BoxMover.Return([item.Path]));
            back.ToolTip = L.T("Öğe masaüstüne döner ve kutuda kalır.");
            menu.Items.Add(back);
            var takeOut = Menus.Item(L.T("Kutudan çıkar (masaüstüne döner)"), () => RemoveItem(item));
            takeOut.ToolTip = L.T("Öğe masaüstüne geri konur ve kutudan çıkar.\nGeri almak için: kutuya sağ tık → Geri al");
            menu.Items.Add(takeOut);
        }
        else
        {
            var remove = Menus.Item(L.T("Widget'tan kaldır"), () => RemoveItem(item));
            remove.InputGestureText = KeyNames.Delete;
            remove.ToolTip = L.T("Yalnızca kısayol kutudan çıkar; dosyaya dokunulmaz.\nGeri almak için: kutuya sağ tık → Geri al");
            menu.Items.Add(remove);
            // Kip sonradan açıldıysa önceden eklenen masaüstü öğesi de tek tek kutuya alınabilir.
            if (BoxMover.Active && !item.Missing && BoxPlan.PinnedDesktopPaths([_config], AppHost.DesktopDirectories).Contains(item.Path))
            {
                var claim = Menus.Item(L.T("Masaüstünden kaldır (kutuya taşı)"), () => BoxMover.Claim(_config, [item.Path], justAdded: false));
                claim.ToolTip = L.F("Öğe {0} klasörüne taşınır ve kutuda durur.", BoxMover.Root);
                menu.Items.Add(claim);
            }
        }
        if (TileItem.IsShellObject(item.Path))
        {
            // Bu Bilgisayar, Geri Dönüşüm Kutusu…: diskte konumu yoktur.
            if (TileItem.IsRecycleBin(item.Path))
                menu.Items.Add(Menus.Item(L.T("Geri Dönüşüm Kutusu'nu boşalt…"), RecycleBinActions.EmptyWithConfirm));
        }
        else
        {
            var ext = System.IO.Path.GetExtension(item.Path).ToLowerInvariant();
            if (ext is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc")
                menu.Items.Add(Menus.Item(L.T("Yönetici olarak çalıştır"), () => TileItem.Launch(item.Path, asAdmin: true)));
            menu.Items.Add(Menus.Item(L.T("Dosya konumunu aç"), () => TileItem.Reveal(item.Path)));
        }

        if (_config.Tabs.Count > 1)
        {
            var move = new MenuItem { Header = L.T("Sekmeye taşı") };
            foreach (var tab in _config.Tabs.Where(t => t != Current))
                move.Items.Add(Menus.Item(Menus.Literal(tab.Name), () => Change(() =>
                {
                    Current.Items.Remove(item.Path);
                    tab.Items.Add(item.Path);
                })));
            menu.Items.Add(move);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item(L.T("Kutu ayarları…"), () => MenuRequested?.Invoke()));
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Link : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        AddItems(paths);
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _titleEditor.ApplyPalette(palette);
        // Fluent simgeli öğeler vurgu renginde çizilir: renk değiştiyse öğeler yeniden kurulur.
        if (TileItem.GlyphColor(_config) != _glyphColor && _tiles.Any(t => t.HasCustomIcon)) Render();
        Foreground = palette.Foreground;
        HeaderIcon.Foreground = palette.Accent;
        CountText.Foreground = palette.Secondary;
        AddTabIcon.Foreground = palette.Secondary;
        EmptyText.Foreground = palette.Secondary;
        EmptyIcon.Foreground = palette.Secondary;
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x10, 0x0C, 0x20));
        RemoveButton.Foreground = palette.Foreground;
        ApplyParts(); // kilit kaldırma düğmesini gizleyebilir: başlık yeniden sığdırılır
        RenderTabs();
        UpdateIconSizes(); // ölçek değiştiyse simgeler yeni piksel boyutunda
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        if (_lastRemoved is { } last)
        {
            menu.Primary.Add(Menus.Item(L.F("Geri al: \"{0}\" listeye dönsün", Menus.Literal(TileItem.DisplayName(last.Path))), UndoRemove));
            menu.Primary.Add(new Separator());
        }
        var add = new MenuItem { Header = L.T("Öğe ekle") };
        add.Items.Add(Menus.Item(L.T("Uygulama ya da dosya…"), AddFiles));
        add.Items.Add(Menus.Item(L.T("Klasör…"), AddFolders));
        menu.Primary.Add(add);
        menu.Primary.Add(Menus.Item(L.T("Sekme ekle…"), NewTab));
        var rename = Menus.Item(L.T("Yeniden adlandır"), () => BeginTitleEdit());
        rename.InputGestureText = KeyNames.F2;
        menu.Primary.Add(rename);
        menu.Primary.Add(Menus.Item(L.T("Simgeyi değiştir…"), PickIcon));
        menu.Primary.Add(Menus.TileOptions(_config, Change, singleClickOption: false));
        menu.Appearance.Add(Menus.Parts(_config, LauncherParts, () => { ApplyParts(); LayoutChanged?.Invoke(); }));
        // Ayarlar'daki "Windows masaüstü simgeleri" seçimiyle aynı yol (Views.DesktopModes). Kip değişimi dosya taşımayı
        // sorabilir: menüyü kapatır.
        static bool Leaves() => Views.DesktopModes.Current == Views.IconMode.BoxItemsLeave;
        var toggle = Menus.Toggle(L.T("Kutuya eklediklerim masaüstünden kalksın"), Leaves,
            () => Views.DesktopModes.Set(Leaves() ? Views.IconMode.ShowAll : Views.IconMode.BoxItemsLeave, null, null), staysOpen: false);
        toggle.ToolTip = L.F("Açıkken kutuya eklenen masaüstü öğeleri {0} klasörüne taşınır ve kutuda durur.", BoxMover.Root);
        menu.More.Add(toggle);
    }

    public bool OnCtrlWheel(int delta)
    {
        Menus.StepIconSize(_config, delta, Change);
        return true;
    }

    /// <summary>Sürükle-bırak dışında da ekleyebilmek için dosya seçici.</summary>
    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.T("Kısayol kutusuna ekle"),
            Multiselect = true,
            DereferenceLinks = false,
            Filter = L.T("Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*"),
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() != true) return;
        AddItems(dialog.FileNames);
    }

    /// <summary>Klasör seçici (eskiden klasör yalnızca sürükleyerek eklenebiliyordu).</summary>
    private void AddFolders()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("Kısayol kutusuna klasör ekle"),
            Multiselect = true,
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() != true) return;
        AddItems(dialog.FolderNames);
    }

    // --- Yerinde yeniden adlandırma (F2) ve simgeler ---

    /// <summary>F2: seçili öğenin kutudaki adı (liste odaktayken), yoksa başlık.</summary>
    public bool TryBeginRename()
    {
        if (_titleEditor.IsEditing || _rename is { IsActive: true }) return true;
        if (Items.IsKeyboardFocusWithin && Items.SelectedItem is TileItem item) return BeginLabelRename(item);
        return BeginTitleEdit();
    }

    /// <summary>Başlığı yerinde düzenler; başlık satırı gizliyse küçük pencereyle sorar (widget'ın monitöründe).</summary>
    private bool BeginTitleEdit()
    {
        if (_titleEditor.Begin()) return true;
        if (InputDialog.Ask(L.T("Kutuyu yeniden adlandır"), L.T("Ad (boş bırakırsan varsayılan ad kullanılır)"), TitleText.Text, DialogPoint) is { } title)
            CommitTitle(string.IsNullOrWhiteSpace(title) || title == DefaultTitle ? null : title);
        return true;
    }

    private void CommitTitle(string? title)
    {
        if (string.Equals(_config.Title, title, StringComparison.Ordinal)) return;
        _config.Title = title;
        AppHost.SaveSettings();
        TitleText.Text = title ?? DefaultTitle;
        ApplyParts();
    }

    private void PickIcon()
    {
        if (Window.GetWindow(this) is not WidgetWindow window) return;
        Views.IconPicker.ForWidget(window, _config, TitleText.Text,
            preview: icon => HeaderIcon.Symbol = WidgetIcons.Symbol(icon) ?? WidgetIcons.DefaultFor(_config),
            commit: icon =>
            {
                _config.Icon = icon;
                AppHost.SaveSettings();
                HeaderIcon.Symbol = WidgetIcons.For(_config);
            });
    }

    /// <summary>Öğenin kutuda görünen adını yerinde düzenler (dosyanın adı değişmez; boş bırakmak asıl ada döndürür).</summary>
    private bool BeginLabelRename(TileItem item)
    {
        _rename?.Cancel();
        var path = item.Path;
        var text = item.Name;
        TileRename? rename = null;
        rename = TileRename.Begin(Items, item, text, 0, text.Length, ItemLooks.MaxName, fileName: false, _palette,
            commit: edited =>
            {
                if (ItemLooks.SetName(_config, path, edited, TileItem.DisplayName(path)))
                {
                    AppHost.SaveSettings();
                    _renderDeferred = true;
                    _selectAfterRender = path;
                }
                return true;
            },
            ended: () =>
            {
                if (_rename == rename) _rename = null;
                if (!_renderDeferred) return;
                _renderDeferred = false;
                Render();
            });
        _rename = rename;
        return true;
    }

    /// <summary>Öğenin simgesi: Fluent simgesi, Windows simgesi ya da resim; seçerken öğe hemen o simgeyle görünür.</summary>
    private void PickItemIcon(TileItem item)
    {
        if (Window.GetWindow(this) is not WidgetWindow window) return;
        var path = item.Path;
        var original = ItemLooks.Get(_config, path)?.Icon;
        void Show(string? icon)
        {
            var index = _tiles.FindIndex(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return;
            _tiles[index] = TileItem.CreateUnchecked(path, _config, _tiles[index].Renamed ? _tiles[index].Name : null, IconDpi, icon);
            Items.ItemsSource = null;
            Items.ItemsSource = _tiles;
        }
        Views.IconPicker.ForItem(window, item, original, preview: Show, commit: icon =>
        {
            if (ItemLooks.SetIcon(_config, path, icon)) AppHost.SaveSettings();
            Render();
        });
    }

    /// <summary>Liste odaktayken: Enter açar, Delete kutudan çıkarır ("Geri al" menüde).</summary>
    private void OnItemsKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ListBoxItem || Items.SelectedItem is not TileItem item || Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Enter when !item.Missing:
                TileItem.Launch(item.Path);
                e.Handled = true;
                break;
            case Key.Delete:
                RemoveItem(item);
                e.Handled = true;
                break;
        }
    }

    public void Detach()
    {
        _rename?.Cancel();
        _titleEditor.Cancel();
        BoxMover.Changed -= Render;
        AppHost.PathRenamed -= OnPathRenamed;
        WatchRecycleBin(false);
    }
}
