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
    private readonly LauncherEdits _edits;
    private bool _renderDeferred;
    private string? _selectAfterRender;

    public LauncherView(WidgetConfig config)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab { Name = L.T("Uygulamalar") });
        InitializeComponent();
        _edits = new LauncherEdits(_config, Render);

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

        // Kutudaki öğe bağlantıdır: dışarı (Gezgin'e) sürüklenince asıl dosya yerinden oynamaz. Başka bir kutuya ya da bölmeye
        // bırakılınca hedef onu WidgetDrag'den tanır ve bu kutudan çıkarır (Ctrl ile kutuya kopyalanır).
        Menus.EnableDragOut(Items, DragDropEffects.Copy | DragDropEffects.Link, item =>
        {
            var tab = Current;
            var path = item.Path;
            return new WidgetDragSource(_config.Id, path, FromBox: true, () => TakeOut(tab, path), ItemLooks.Get(_config, path));
        });
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
        Corner.Update(_config);
        // Başlık düzenlenirken kutuya yer açılır, simge düğmesi (seçiciyi açar) gizlenmez.
        _titleEditor.Fit([CountText, HeaderIconButton], [Corner]);
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

    public UIElement BodyElement => Body;

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
        EmptyText.Text = _config.Tabs.Count > 1 ? L.T("Bu sekme boş.") : L.T("Kutu boş.");
        if (_selectAfterRender is { } path)
        {
            _selectAfterRender = null;
            if (_tiles.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase)) is { } item) SelectAndFocus(item);
        }
        WatchRecycleBin(_tiles.Any(t => TileItem.IsRecycleBin(t.Path)));
        _editorChanged?.Invoke();
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
                Background = active ? _palette.Accent : _palette.Tint(0x1E),
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
            menu.Items.Add(Menus.Item(L.T("Sekmeyi sil"), () => DeleteTab(tab)));
        }
    }

    /// <summary>
    /// Sekmeyi siler (soru yok): başka sekmede kalmayan öğelerin adları ve simgeleri de gider (son çıkarılan öğe "Geri al" için
    /// tutulur), masaüstünden taşınmış öğeler başka kutuda yoksa masaüstüne döner. Bildirimdeki "Geri al" sekmeyi içindekiler,
    /// adlar ve simgelerle yerine koyar (kip açıksa öğeler yeniden kutunun klasörüne taşınır).
    /// </summary>
    private void DeleteTab(LauncherTab tab)
    {
        LauncherEdits.RemovedTab? removed = null;
        Change(() => removed = LauncherEdits.TakeTab(_config, _config.Tabs.IndexOf(tab),
            _edits.LastRemoved?.Entry.Path is { } last ? [last] : null));
        if (removed is not { } taken) return;
        BoxMover.Reconcile();
        // Geri alma kutuyu kimliğiyle bulur: o arada kaldırılıp geri getirildiyse yeni görünüm ve ayarın kopyası vardır;
        // kaldırılmış kaldıysa bir şey yapmaz.
        var id = _config.Id;
        Views.Notice.Show(L.F("\"{0}\" sekmesi silindi.", tab.Name), Views.NoticeKind.Info, L.T("Geri al"),
            () => (AppHost.Widgets.ViewOf(id) as LauncherView)?.PutBack(taken), L.T("Geri almak için buraya tıkla."));
    }

    private void PutBack(LauncherEdits.RemovedTab taken)
    {
        if (!AppHost.Settings.Widgets.Contains(_config)) return;
        var restored = false;
        Change(() => restored = LauncherEdits.PutTab(_config, taken));
        if (restored) BoxMover.Reconcile();
    }

    private void Swap(int a, int b)
    {
        (_config.Tabs[a], _config.Tabs[b]) = (_config.Tabs[b], _config.Tabs[a]);
        if (_config.ActiveTab == a) _config.ActiveTab = b;
        else if (_config.ActiveTab == b) _config.ActiveTab = a;
    }

    private void AddTab_Click(object sender, RoutedEventArgs e) => NewTab();

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

    // Kaydet, kutudaki masaüstü dosyalarını kurallardan koru, yeniden çiz (bkz. LauncherEdits).
    private void Change(Action change) => _edits.Change(change);

    /// <summary>
    /// Öğeyi listeden çıkarır; kutunun menüsündeki "Geri al" ile yerine döner. Masaüstünden kutuya taşınmış öğe (başka kutuda
    /// yoksa) masaüstüne geri konur; "Geri al" onu yeniden kutuya taşır.
    /// </summary>
    private void RemoveItem(TileItem item)
    {
        if (_edits.Remove(Current, LauncherEntries.IndexOfPath(Current, item.Path)) == true)
            Views.Notice.Show(L.F("\"{0}\" kutudan çıkarıldı ve masaüstüne geri konuyor.", item.Name), Views.NoticeKind.Info, L.T("Geri al"),
                UndoRemove, L.T("Geri almak için buraya tıkla."));
    }

    private void UndoRemove() => _edits.UndoRemove();

    /// <summary>
    /// Öğeleri etkin sekmeye ekler (varlık denetimi arka planda). "Kutulara eklediklerim masaüstünden kalksın" açıksa
    /// masaüstündekiler kutunun klasörüne taşınır.
    /// </summary>
    /// <param name="look">Başka kutudan taşınan öğenin orada verilmiş adı ve simgesi.</param>
    /// <param name="then">Ekleme bittikten sonra (ör. kaynak kutudan çıkarmak): öğe önce burada olmalı, yoksa kutunun
    /// klasörüne taşınmış öğe arada masaüstüne geri konur.</param>
    /// <param name="names">Bırakılan bağlantının sayfa başlığı, uygulamanın adı (yola göre; öğenin adı yoksa yazılır).</param>
    private void AddItems(IReadOnlyList<string> paths, ItemLook? look = null, Action? then = null, IReadOnlyDictionary<string, string>? names = null) =>
        _edits.AddItems(() => Current, paths, look, then, names: names);

    /// <summary>
    /// Öğe sürüklenerek başka bir widget'a taşındı: bu sekmeden çıkar (başka sekmede de yoksa adı ve simgesi de). Kutunun
    /// klasörüne taşınmış öğe başka kutuda yoksa masaüstüne döner (BoxMover.Reconcile).
    /// </summary>
    private void TakeOut(LauncherTab tab, string path) => _edits.TakeOut(tab, path);

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (Menus.IsClick(Items, e) && Menus.ItemAt(Items, e.OriginalSource) is { Missing: false } item)
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
        // Başlat menüsü uygulamasının ve bırakılan bağlantının adı yalnızca kayıttadır (silinirse kimlik/özetli ad görünürdü):
        // onlarda yalnızca simge varsayılana döner.
        var keepName = TileItem.NameIsStored(item.Path);
        if (ItemLooks.Get(_config, item.Path) is { IsEmpty: false } look && (!keepName || look.Icon is not null))
            menu.Items.Add(keepName
                ? Menus.Item(L.T("Varsayılan simge"), () => Change(() => ItemLooks.SetIcon(_config, item.Path, null)))
                : Menus.Item(L.T("Varsayılan ad ve simge"), () => Change(() => ItemLooks.Reset(_config, item.Path))));
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
            var remove = Menus.Item(L.T("Kutudan kaldır"), () => RemoveItem(item));
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
            // Bırakılan bağlantının dosyası uygulamanın veri klasöründedir (paketli sürümde Gezgin o yolu göremez).
            if (!TileItem.IsDroppedLink(item.Path))
                menu.Items.Add(Menus.Item(L.T("Dosya konumunu aç"), () => TileItem.Reveal(item.Path)));
        }

        if (_config.Tabs.Count > 1)
        {
            var move = new MenuItem { Header = L.T("Sekmeye taşı") };
            foreach (var tab in _config.Tabs.Where(t => t != Current))
                move.Items.Add(Menus.Item(Menus.Literal(tab.Name), () => Change(() =>
                {
                    Current.Items.RemoveAll(p => string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase));
                    if (!tab.Items.Contains(item.Path, StringComparer.OrdinalIgnoreCase)) tab.Items.Add(item.Path);
                })));
            menu.Items.Add(move);
        }
    }

    /// <summary>
    /// Bırakılınca ne olur: kendi öğesi yer değiştirir; başka kutudan gelen buraya taşınır (Ctrl basılıysa kopyalanır);
    /// bölmeden, Gezgin'den ya da başka uygulamadan gelen kısayol olarak eklenir. 2.2: Başlat menüsünden bırakılan uygulama
    /// (AddShell), tarayıcıdan bırakılan bağlantı (AddLink) da eklenir; eklenemeyen türde bağlantı (file:, javascript:…)
    /// bırakılamaz ve nedeni gösterilir (BlockedLink).
    /// </summary>
    private enum DropKind { None, Reorder, MoveHere, Add, AddShell, AddLink, BlockedLink }

    // 2.2 Kutu: bırakılan verinin türü sürükleme başına bir kez (ilk DragEnter'da) belirlenir; DragOver her fare hareketinde
    // gelir ve bağlantı biçimlerini yeniden okumaz. WPF bir sürükleme boyunca aynı veri nesnesini verir (zayıf başvuru: biten
    // sürüklemenin verisi tutulmaz).
    private WeakReference<IDataObject>? _dropData;
    private DropContent _dropContent;

    private DropContent ContentOf(IDataObject data)
    {
        if (_dropData is null || !_dropData.TryGetTarget(out var known) || !ReferenceEquals(known, data))
        {
            _dropData = new WeakReference<IDataObject>(data);
            _dropContent = LauncherDrop.Classify(data);
        }
        return _dropContent;
    }

    private DropKind PlanDrop(DragEventArgs e)
    {
        switch (ContentOf(e.Data))
        {
            case DropContent.None: return DropKind.None;
            case DropContent.Apps or DropContent.ShellItems: return DropKind.AddShell;
            case DropContent.Link: return DropKind.AddLink;
            case DropContent.BlockedLink: return DropKind.BlockedLink;
        }
        var source = WidgetDrag.For(e.Data);
        if (source is null) return DropKind.Add;
        if (source.WidgetId == _config.Id)
            return Current.Items.Contains(source.Path, StringComparer.OrdinalIgnoreCase) ? DropKind.Reorder : DropKind.Add;
        if (!source.FromBox) return DropKind.Add;
        return e.KeyStates.HasFlag(DragDropKeyStates.ControlKey) ? DropKind.Add : DropKind.MoveHere;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var kind = PlanDrop(e);
        // Kaynağın izin verdiği bir etki seçilmeli; yoksa Windows bırakmayı reddeder (yalnızca kopyalamaya izin veren uygulama).
        var allowed = e.AllowedEffects;
        e.Effects = kind is DropKind.None or DropKind.BlockedLink ? DragDropEffects.None
            : allowed.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link
            : allowed.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy
            // Bağlantı ve uygulama yalnızca okunur: kaynak (tarayıcı, metin) "taşındı" sanıp kendi verisini silmesin.
            : kind is DropKind.AddShell or DropKind.AddLink ? DragDropEffects.None
            : allowed & DragDropEffects.Move;
        var text = kind switch
        {
            DropKind.Add => L.T("Bırak, kısayol olarak eklensin"),
            DropKind.MoveHere => L.T("Bırak, bu kutuya taşınsın"),
            DropKind.AddShell when _dropContent == DropContent.Apps => L.T("Bırak, uygulama olarak eklensin"),
            DropKind.AddShell => L.T("Bırak, kısayol olarak eklensin"),
            DropKind.AddLink => L.T("Bırak, bağlantı olarak eklensin"),
            DropKind.BlockedLink => L.T("Bu bağlantı türü eklenemez (yalnızca http, https, mailto)"),
            _ => null,
        };
        if (text is not null) DropText.Text = text;
        DropOverlay.Visibility = text is not null ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        var kind = PlanDrop(e);
        _dropData = null; // sonraki sürükleme yeniden belirlenir
        // Veri yalnızca bırakma sırasında geçerlidir: kabuk listesi ve bağlantı burada kopyalanır, işleri arka planda sürer.
        switch (kind)
        {
            case DropKind.AddShell:
                e.Handled = true;
                e.Effects = e.AllowedEffects.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link : DragDropEffects.Copy;
                AddShellItems(LauncherDrop.ReadShellIdList(e.Data));
                return;
            case DropKind.AddLink:
                e.Handled = true;
                e.Effects = e.AllowedEffects.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link : DragDropEffects.Copy;
                AddLink(LauncherDrop.ReadLink(e.Data, withTitle: true));
                return;
            case DropKind.None or DropKind.BlockedLink:
                return;
        }
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var source = WidgetDrag.For(e.Data);
        if (source is not null) WidgetDrag.MarkDropped();
        e.Handled = true;
        switch (kind)
        {
            case DropKind.Reorder when source is not null:
                Reorder(source.Path, e);
                break;
            case DropKind.MoveHere when source is not null:
                AddItems([source.Path], source.Look, source.RemoveFromSource);
                break;
            case DropKind.Add:
                AddItems(source is not null ? [source.Path] : paths, source?.Look);
                break;
        }
    }

    /// <summary>
    /// Başlat menüsünden (ya da Gezgin'in sanal bir klasöründen) bırakılan kabuk öğeleri: uygulamalar adlarıyla, dosya sistemi
    /// öğeleri yollarıyla eklenir. Kabuk kendi STA iş parçacığında çözer; eklenecek bir şey yoksa söylenir.
    /// </summary>
    private async void AddShellItems(byte[]? shellIdList)
    {
        var found = await LauncherDrop.ResolveAsync(shellIdList);
        if (found.Count == 0)
        {
            Views.Notice.Show(L.T("Bu öğe kısayol kutusuna eklenemez."), Views.NoticeKind.Info);
            return;
        }
        var names = found.Where(f => f.Name is not null)
            .ToDictionary(f => f.Path, f => f.Name!, StringComparer.OrdinalIgnoreCase);
        AddItems(found.Select(f => f.Path).ToList(), names: names);
    }

    /// <summary>
    /// Bırakılan bağlantı: veri klasöründe küçük bir .url dosyası olur (arka planda; aynı adres aynı dosya) ve kutuya sayfa
    /// başlığıyla (yoksa alan adıyla) eklenir. Açınca adres yeniden denetlenip tarayıcıyla açılır (TileItem.Launch).
    /// </summary>
    private async void AddLink((LinkKind Kind, string? Url, string? Title) link)
    {
        if (link is not { Kind: LinkKind.Allowed, Url: { } url }) return;
        LinkFile file;
        try
        {
            var directory = AppHost.LinksDirectory;
            file = await Task.Run(() => LinkFiles.Create(url, link.Title, directory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            DebugLog.Write($"bağlantı dosyası yazılamadı: {ex.Message}");
            Views.Notice.Show(L.T("Bağlantı kaydedilemedi."), Views.NoticeKind.Error);
            return;
        }
        AddItems([file.Path], names: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [file.Path] = file.Name });
    }

    /// <summary>Kendi öğesini etkin sekmede bırakıldığı öğenin önüne (sağ/alt yarısına bırakılınca arkasına) taşır.</summary>
    private void Reorder(string path, DragEventArgs e)
    {
        var tab = Current;
        var target = Menus.ItemAt(Items, e.OriginalSource);
        var items = tab.Items;
        var from = items.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (from < 0) return;
        int to;
        if (target is null) to = items.Count;   // boş alana: sona
        else
        {
            to = items.FindIndex(p => string.Equals(p, target.Path, StringComparison.OrdinalIgnoreCase));
            if (to < 0) return;
            if (Items.ItemContainerGenerator.ContainerFromItem(target) is FrameworkElement box)
            {
                var at = e.GetPosition(box);
                if (_config.View == ItemView.List ? at.Y > box.ActualHeight / 2 : at.X > box.ActualWidth / 2) to++;
            }
        }
        if (to > from) to--;
        if (to == from) return;
        Change(() =>
        {
            var moving = items[from];
            items.RemoveAt(from);
            items.Insert(Math.Clamp(to, 0, items.Count), moving);
        });
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
        EmptyText.Foreground = palette.Foreground;
        EmptyHint.Foreground = palette.Secondary;
        EmptyAddIcon.Foreground = palette.Accent;
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = palette.Overlay;
        Corner.Paint(palette);
        ApplyParts(); // kilit kaldırma düğmesini gizleyebilir: başlık yeniden sığdırılır
        RenderTabs();
        UpdateIconSizes(); // ölçek değiştiyse simgeler yeni piksel boyutunda
    }

    /// <summary>
    /// Menüde: son çıkarılan öğe varsa onun "Geri al"ı ve "Uygulama ekle…" (Başlat menüsündeki uygulamalar; dosya ve klasör aynı
    /// pencerenin altındaki "Dosya ya da klasör seç…"te). Öğeler, sekmeler ve görünüş Düzenle panelinde (<see cref="AddEditorContent"/>).
    /// </summary>
    public void AddMenuItems(WidgetMenu menu)
    {
        if (_edits.LastRemoved?.Entry.Path is { } lastPath)
            menu.Primary.Add(Menus.Item(L.F("Geri al: \"{0}\" listeye dönsün", Menus.Literal(TileItem.DisplayName(lastPath))), UndoRemove));
        menu.Primary.Add(Menus.Item(L.T("Uygulama ekle…"), () => AddApps()));
        if (!menu.ForEditor) return;
        menu.Settings.Add(Menus.Parts(_config, LauncherParts, () => { ApplyParts(); LayoutChanged?.Invoke(); }));
        menu.More.AddRange(Menus.TileOptions(_config, Change, singleClickOption: false));
        // "Kutuya eklediklerim masaüstünden kalksın" uygulama genelidir: widget menüsünde değil, Ayarlar'da (Views.DesktopModes).
    }

    // Açık Düzenle panelinin öğe listesi kutu değişince (sürükleyip bırakma, arka planda biten ekleme) hemen tazelensin.
    private Action? _editorChanged;

    /// <summary>
    /// Düzenle paneli: birden çok sekme varsa "Sekme", etkin sekmenin öğeleri (yalnızca seçili satırda "Kaldır" · "↑" · "↓";
    /// çıkarma <see cref="LauncherEdits.Remove"/> ile, "Geri al" bildirimde ve menüde), "+ Uygulama" ("Uygulama seç") · "+ Klasör"
    /// · "+ Dosya" · "+ Sekme" ve görünüş (Izgara/Liste, simge boyutu).
    /// </summary>
    public bool AddEditorContent(Views.EditorContent content)
    {
        _editorChanged = () => content.Changed();
        if (_config.Tabs.Count > 1)
            content.Dropdown(L.T("Sekme"), "Editor.Launcher.Tab", _config.Tabs.Select(t => t.Name).ToList(),
                () => Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1), index =>
                {
                    if (index == _config.ActiveTab) return;
                    _config.ActiveTab = index;
                    // Seçili sekme önemsiz bir durum: bir sonraki kayıtla gider.
                    AppHost.SaveSettingsLater();
                    Render();
                });
        content.Items(L.T("Öğeler"), _config.Tabs.Count > 1 ? L.T("Bu sekme boş.") : L.T("Kutu boş."), () => _tiles,
            path =>
            {
                if (_tiles.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase)) is { } item) RemoveItem(item);
            },
            MoveItem);
        content.Buttons(
        [
            (L.T("+ Uygulama"), "Editor.Launcher.AddApp", () => AddApps()),
            (L.T("+ Klasör"), "Editor.Launcher.AddFolder", () => AddPicked(Views.AppPicker.AskFolders(content.Owner, L.T("Kısayol kutusuna klasör ekle")))),
            (L.T("+ Dosya"), "Editor.Launcher.AddFile", () => AddPicked(Views.AppPicker.AskFiles(content.Owner, L.T("Kısayol kutusuna ekle")))),
            (L.T("+ Sekme"), "Editor.Launcher.AddTab", NewTab),
        ], rebuild: true);
        content.TileLook(_config, Change);
        return true;
    }

    /// <summary>Panelden seçilen dosya ya da klasörler etkin sekmeye (varlık denetimi arka planda).</summary>
    private void AddPicked(IReadOnlyList<string>? paths)
    {
        if (paths is not { Count: > 0 }) return;
        var tab = Current;
        _edits.AddItems(() => _config.Tabs.Contains(tab) ? tab : Current, paths);
    }

    /// <summary>Etkin sekmedeki öğeyi bir yukarı (-1) ya da aşağı (+1) taşır (Düzenle panelinin "↑" / "↓", Alt+↑/↓).</summary>
    private void MoveItem(string path, int delta)
    {
        var tab = Current;
        var from = LauncherEntries.IndexOfPath(tab, path);
        if (from >= 0) Change(() => LauncherEntries.Move(tab, from, from + delta));
    }

    public bool OnCtrlWheel(int delta)
    {
        Menus.StepIconSize(_config, delta, Change);
        return true;
    }

    private void AddApp_Click(object sender, RoutedEventArgs e) => AddApps();

    /// <summary>
    /// "Uygulama ekle…" (menü ve boş kutudaki düğme): "Uygulama seç" penceresi widget'ın monitöründe açılır; seçilen uygulama
    /// (ya da altındaki "Dosya ya da klasör seç…" ile seçilenler) etkin sekmeye eklenir. Sekmede zaten olan "Zaten ekli"dir.
    /// </summary>
    private void AddApps()
    {
        var tab = Current;
        if (Views.AppPicker.Ask(DialogPoint, tab.Items, L.T("Kısayol kutusuna ekle"), L.T("Kısayol kutusuna klasör ekle")) is not { } pick) return;
        _edits.AddItems(() => _config.Tabs.Contains(tab) ? tab : Current, pick.Paths, names: pick.Names);
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

    // 2.2 Kutu
    /// <summary>1–9: etkin sekmede görünen n'inci öğeyi açar; seçenek açıksa (Düzenle ▸ Daha fazla ▸ Numara tuşlarıyla aç).</summary>
    public bool TryQuickOpen(KeyEventArgs e) =>
        Menus.TryQuickOpen(e, _config, Items, editing: _titleEditor.IsEditing || _rename is { IsActive: true });

    public void Detach()
    {
        _editorChanged = null;
        _rename?.Cancel();
        _titleEditor.Cancel();
        BoxMover.Changed -= Render;
        AppHost.PathRenamed -= OnPathRenamed;
        WatchRecycleBin(false);
    }
}
