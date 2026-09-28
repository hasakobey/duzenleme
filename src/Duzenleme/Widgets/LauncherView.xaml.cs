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

    public LauncherView(WidgetConfig config)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab());
        InitializeComponent();

        Header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };
        Header.SizeChanged += (_, e) => { if (e.WidthChanged) ApplyParts(); };
        // Pencereye bağlanınca (ölçeği artık kesin) simgeler o ekranın piksel boyutunda istenir.
        Loaded += (_, _) => UpdateIconSizes();

        Items.PreviewMouseLeftButtonUp += OnItemClick;
        Menus.AttachItemMenu(Items, FillItemMenu);

        Menus.EnableDragOut(Items, DragDropEffects.Copy | DragDropEffects.Link);
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        // Öğeler masaüstünden kutuya taşınınca ya da geri konunca yolları değişir.
        BoxMover.Changed += Render;
        Render();
    }

    public bool Resizable => true;
    public bool Collapsible => _config.Shows("header");
    public Thickness CardPadding => new(14, 12, 14, 12);
    public event Action? CollapseToggleRequested;
    public event Action? MenuRequested;
    public event Action? LayoutChanged;

    private static readonly (string Key, string Label)[] LauncherParts =
        [("header", "Başlık satırı"), ("count", "Öğe sayısı"), ("tabs", "Sekmeler"), Menus.ClosePart];

    /// <summary>
    /// Kullanıcının kapattığı parçaları gizler. Dar kutuda başlık okunsun diye öğe sayısı ve başlık simgesi (bu sırayla)
    /// geçici olarak gizlenir; kaydedilmez, genişleyince döner.
    /// </summary>
    private void ApplyParts()
    {
        Header.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        HeaderIcon.Visibility = Visibility.Visible;
        CountText.Visibility = _config.Shows("count") ? Visibility.Visible : Visibility.Collapsed;
        TabStrip.Visibility = AddTab.Visibility = _config.Shows("tabs") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        HeaderFitter.Fit(Header, TitleText, [CountText, HeaderIcon], [RemoveButton]);
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

    private const string DefaultTitle = "Kısayol kutusu";

    private void Render()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? DefaultTitle : _config.Title;
        CountText.Text = $"{_config.Tabs.Sum(t => t.Items.Count)} öğe";
        RenderTabs();
        ApplyParts();

        if (TileItem.PanelKey(_config) != _panelKey)
        {
            _panelKey = TileItem.PanelKey(_config);
            Items.ItemsPanel = TileItem.Panel(_config);
        }
        // Yollar arka planda denetlenir (ağ yolları süre sınırıyla): kapalı bir NAS'taki öğe açılışı ya da sekme
        // değiştirmeyi bekletmez; sonuç gelince öğe soluklaşır ya da simgesini alır.
        // Simgeler bu ekranın gerçek piksel boyutunda istenir (yol denetimi bitince).
        var dpi = IconDpi;
        _tiles = Current.Items.Select(p => TileItem.CreateUnchecked(p, _config, pixelsPerDip: dpi)).ToList();
        Items.ItemsSource = _tiles;
        EmptyState.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                ToolTip = "Sağ tık: yeniden adlandır, taşı, sil",
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

    private void FillTabMenu(ContextMenu menu, int index)
    {
        var tab = _config.Tabs[index];
        menu.Items.Add(Menus.Item("Yeniden adlandır…", () =>
        {
            if (InputDialog.Ask("Sekme adı", "Ad", tab.Name) is { Length: > 0 } name) Change(() => tab.Name = name);
        }));
        if (index > 0)
            menu.Items.Add(Menus.Item("Sola taşı", () => Change(() => Swap(index, index - 1))));
        if (index < _config.Tabs.Count - 1)
            menu.Items.Add(Menus.Item("Sağa taşı", () => Change(() => Swap(index, index + 1))));
        if (_config.Tabs.Count > 1)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item("Sekmeyi sil", () =>
            {
                Change(() =>
                {
                    _config.Tabs.RemoveAt(index);
                    _config.ActiveTab = Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
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
        if (InputDialog.Ask("Yeni sekme", "Sekme adı", $"Sekme {_config.Tabs.Count + 1}") is not { Length: > 0 } name) return;
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

    /// <summary>Dock gibi: tek tıkla açılır.</summary>
    private (LauncherTab Tab, int Index, string Path)? _lastRemoved;

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
        _lastRemoved = (tab, index, tab.Items[index]);
        Change(() => tab.Items.RemoveAt(index));
        if (!moved) return;
        BoxMover.Reconcile();
        if (!BoxPlan.Referenced(AppHost.Settings.Widgets).Contains(item.Path))
            Views.Notice.Show($"\"{item.Name}\" kutudan çıkarıldı ve masaüstüne geri konuyor.", Views.NoticeKind.Info, "Geri al", UndoRemove,
                "Geri almak için buraya tıkla.");
    }

    private void UndoRemove()
    {
        if (_lastRemoved is not { } last || !_config.Tabs.Contains(last.Tab)) return;
        _lastRemoved = null;
        Change(() => last.Tab.Items.Insert(Math.Min(last.Index, last.Tab.Items.Count), last.Path));
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
        menu.Items.Add(Menus.Item("Aç", () => TileItem.Launch(item.Path)));
        var moved = BoxMover.IsMoved(item.Path);
        if (moved)
        {
            // Masaüstünden kutuya taşınmış öğe (NestDesk klasöründe).
            var back = Menus.Item("Masaüstüne geri koy", () => BoxMover.Return([item.Path]));
            back.ToolTip = "Öğe masaüstüne döner ve kutuda kalır.";
            menu.Items.Add(back);
            var takeOut = Menus.Item("Kutudan çıkar (masaüstüne döner)", () => RemoveItem(item));
            takeOut.ToolTip = "Öğe masaüstüne geri konur ve kutudan çıkar.\nGeri almak için: kutuya sağ tık → Geri al";
            menu.Items.Add(takeOut);
        }
        else
        {
            var remove = Menus.Item("Widget'tan kaldır", () => RemoveItem(item));
            remove.ToolTip = "Yalnızca kısayol kutudan çıkar; dosyaya dokunulmaz.\nGeri almak için: kutuya sağ tık → Geri al";
            menu.Items.Add(remove);
            // Kip sonradan açıldıysa önceden eklenen masaüstü öğesi de tek tek kutuya alınabilir.
            if (BoxMover.Active && !item.Missing && BoxPlan.PinnedDesktopPaths([_config], AppHost.DesktopDirectories).Contains(item.Path))
            {
                var claim = Menus.Item("Masaüstünden kaldır (kutuya taşı)", () => BoxMover.Claim(_config, [item.Path], justAdded: false));
                claim.ToolTip = $"Öğe {BoxMover.Root} klasörüne taşınır ve kutuda durur.";
                menu.Items.Add(claim);
            }
        }
        var ext = System.IO.Path.GetExtension(item.Path).ToLowerInvariant();
        if (ext is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc")
            menu.Items.Add(Menus.Item("Yönetici olarak çalıştır", () => TileItem.Launch(item.Path, asAdmin: true)));
        menu.Items.Add(Menus.Item("Dosya konumunu aç", () => TileItem.Reveal(item.Path)));

        if (_config.Tabs.Count > 1)
        {
            var move = new MenuItem { Header = "Sekmeye taşı" };
            foreach (var tab in _config.Tabs.Where(t => t != Current))
                move.Items.Add(Menus.Item(tab.Name, () => Change(() =>
                {
                    Current.Items.Remove(item.Path);
                    tab.Items.Add(item.Path);
                })));
            menu.Items.Add(move);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Kutu ayarları…", () => MenuRequested?.Invoke()));
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
            menu.Primary.Add(Menus.Item($"Geri al: \"{TileItem.DisplayName(last.Path)}\" listeye dönsün", UndoRemove));
            menu.Primary.Add(new Separator());
        }
        menu.Primary.Add(Menus.Item("Uygulama ya da dosya ekle…", AddFiles));
        menu.Primary.Add(Menus.Item("Sekme ekle…", NewTab));
        menu.Primary.Add(Menus.Item("Başlığı değiştir…", () =>
        {
            if (InputDialog.Ask("Kutu başlığı", "Başlık", TitleText.Text) is { } title)
                Change(() => _config.Title = string.IsNullOrWhiteSpace(title) || title == DefaultTitle ? null : title);
        }));
        menu.Primary.Add(Menus.TileOptions(_config, Change, singleClickOption: false));
        menu.Appearance.Add(Menus.Parts(_config, LauncherParts, () => { ApplyParts(); LayoutChanged?.Invoke(); }));
        // Ayarlar'daki "Windows masaüstü simgeleri" seçimiyle aynı yol (Views.DesktopModes).
        var leaves = Views.DesktopModes.Current == Views.IconMode.BoxItemsLeave;
        var toggle = Menus.Toggle("Kutuya eklediklerim masaüstünden kalksın", leaves,
            () => Views.DesktopModes.Set(leaves ? Views.IconMode.ShowAll : Views.IconMode.BoxItemsLeave, null, null));
        toggle.ToolTip = $"Açıkken kutuya eklenen masaüstü öğeleri {BoxMover.Root} klasörüne taşınır ve kutuda durur.";
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
            Title = "Kısayol kutusuna ekle",
            Multiselect = true,
            DereferenceLinks = false,
            Filter = "Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*",
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() != true) return;
        AddItems(dialog.FileNames);
    }

    public void Detach() => BoxMover.Changed -= Render;
}
