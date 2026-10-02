using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Masaüstündeki halka menü (<see cref="WidgetVariants.Ring"/>; temel türü Launcher: 2.0/2.1 onu yollarıyla bir kısayol kutusu
/// olarak gösterir, 2.1 yol olmayan öğeleri korur). Kart kare kalır; adı ortadaki dairede yazar (F2 / "Yeniden adlandır"
/// sürerken üstte bir satırda düzenlenir: <see cref="WidgetNameLine"/>; "Göster ▸ Ad" kapalıyken ortada simge). Tek tıkla
/// açılır; 1–9 tuşları, oklar ve Enter da seçer.
/// <para>Öğe menüsü (sağ tık, Menü tuşu/Shift+F10 vurgulanan öğede): Aç, Öğe ekle ▸, saat yönünde/ters yönde taşı, alt
/// halkaya taşı ▸, Düzenle…, Halkadan kaldır ("Geri al" bildirimde ve widget menüsünde). Halkanın ortasına ya da dışına sağ
/// tık widget'ın menüsünü açar; oraya basıp sürüklemek widget'ı taşır. Yol öğeleri dışarı sürüklenebilir (veride yalnızca
/// FileDrop); içeri dosya, klasör ya da http(s) adresi bırakılabilir (bırakılan yere girer; alt halkanın üstüne bırakılan
/// onun içine). Ekleme/çıkarma kısayol kutusuyla aynı yoldan (<see cref="LauncherEdits"/>): kutudaki masaüstü dosyaları
/// kurallardan korunur, "masaüstünden kalksın" kipinde kutunun klasörüne taşınır, çıkınca masaüstüne döner.</para>
/// </summary>
internal sealed class RingWidgetView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly RingView _ring = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid _nameBox;
    private readonly TextBlock _nameText;
    private readonly Button _remove;
    private readonly WidgetNameLine _name;
    private readonly LauncherEdits _edits;
    private readonly ContextMenu _itemMenu;
    private RingSlot? _menuSlot;
    private bool _live, _rendered, _stale;

    /// <summary>Son kaldırılan alt halka ("Geri al" için): sekmesi, yeri ve içindeki yolların adları/simgeleri.</summary>
    private (LauncherTab Tab, int Index, Dictionary<string, ItemLook> Looks)? _removedSubRing;

    public RingWidgetView(WidgetConfig config)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab { Name = L.T("Uygulamalar") });
        _edits = new LauncherEdits(config, Render);

        _nameText = new TextBlock
        {
            FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        };
        _nameBox = new Grid { Margin = new Thickness(22, -2, 22, 6), Visibility = Visibility.Collapsed };
        _nameBox.Children.Add(_nameText);
        Grid.SetRow(_ring, 1);
        _remove = new Button { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -10, -12, 0) };
        _remove.SetResourceReference(StyleProperty, "WidgetCloseButton");
        _remove.Click += (_, _) => AppHost.Widgets.RemoveWithUndo(_config.Id);
        Grid.SetRowSpan(_remove, 2);
        // Saydam zemin: halkanın dışına da bırakılabilsin (basıp sürüklemek yine widget'ı taşır).
        var root = new Grid { Background = Brushes.Transparent };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(_nameBox);
        root.Children.Add(_ring);
        root.Children.Add(_remove);
        Content = root;

        // Ad halkanın ortasında yazılır; satır yalnızca F2 / "Yeniden adlandır" sürerken halkanın üstünde açılır.
        _name = new WidgetNameLine(this, config, _nameBox, _nameText, shadow: null, Render, editOnly: true);
        _ring.PixelsPerDipHint = WidgetWindow.ExpectedPixelsPerDip(config);
        _ring.SetRing(config);
        _rendered = true;
        _ring.ItemInvoked += RingRunner.RunFromWidget;
        // Yalnızca yol öğeleri dışarı sürüklenir; öbürlerinde biraz kayan tıklama yine seçer.
        _ring.CanDragOut = CanDragOut;
        _ring.DragOutRequested += DragOut;
        _ring.Navigated += ApplyParts;
        AutomationProperties.SetAutomationId(_ring, "Ring.View");

        // Tıklanınca klavye halkada: 1–9, oklar, Enter (ad yazılırken değil).
        PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (!_name.IsEditing) _ring.Focus();
        };
        _itemMenu = Menus.Dynamic(menu => { if (_menuSlot is { } slot) FillItemMenu(menu, slot); });
        _ring.PreviewMouseRightButtonDown += OnRightButtonDown;
        _ring.PreviewKeyDown += OnRingKey;

        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => EndDrag();
        Drop += OnDrop;
        // Öğeler masaüstünden kutuya taşınınca ya da geri konunca yolları değişir; bir bölmede yeniden adlandırılınca da.
        BoxMover.Changed += OnOutsideChange;
        AppHost.PathRenamed += OnPathRenamed;
        Loaded += (_, _) => _ring.UpdateIconSizes();
        ApplyParts();
    }

    public bool Resizable => false;

    public Thickness CardPadding => new(14, 12, 14, 14);

    // Kaldırma düğmesi (×) negatif kenar boşluğuyla kartın dolgusuna taşar: kırpılmasın.
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    private LauncherTab Tab(int index) => _config.Tabs[Math.Clamp(index, 0, _config.Tabs.Count - 1)];

    private LauncherTab CurrentTab => Tab(_ring.TabIndex);

    private NativeMethods.POINT? DialogPoint => Owner?.CenterPoint;

    private WidgetWindow? Owner => Window.GetWindow(this) as WidgetWindow;

    // ---------------------------------------------------------------- çizim

    /// <summary>Ayardan yeniden kurar. Görünmezken dışarıdan gelen değişiklik (kutu taşıması, ad değişimi) görünür olunca uygulanır.</summary>
    private void Render()
    {
        if (!_live && _rendered)
        {
            _stale = true;
            return;
        }
        _ring.Rebuild();
        ApplyParts();
    }

    private void OnOutsideChange() => Render();

    private void OnPathRenamed(string oldPath, string newPath, bool isDirectory)
    {
        // AppHost kutuların yollarını zaten güncelledi; bu halkada etkilenen öğe varsa yeniden çizilir.
        if (_config.Tabs.Any(t => t.Items.Any(p => PathRenames.Map(p, newPath, newPath, isDirectory) is not null))) Render();
    }

    public void SetLive(bool live)
    {
        _live = live;
        if (!live || !_stale) return;
        _stale = false;
        Render();
    }

    private void ApplyParts()
    {
        _remove.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        _name.Render();
        // "Göster ▸ Ad" kapalıyken boştaki ortada adın yerine widget'ın simgesi.
        _ring.CenterShowsIcon = !_config.Shows(WidgetNameLine.PartKey);
        _ring.Redraw();
        AutomationProperties.SetName(this, WidgetText.DisplayName(_config));
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _name.ApplyPalette(palette);
        _ring.ApplyPalette(palette);
        _remove.Foreground = palette.Foreground;
        ClearTypeText.Follow(this, _ring);
        ApplyParts(); // kilit kaldırma düğmesini gizleyebilir
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _ring.UpdateIconSizes();
    }

    /// <summary>F2 ve "Yeniden adlandır": halkanın adı (bkz. <see cref="WidgetNameLine"/>).</summary>
    public bool TryBeginRename() => _name.Begin();

    public void Detach()
    {
        _name.Cancel();
        _ring.Release();
        BoxMover.Changed -= OnOutsideChange;
        AppHost.PathRenamed -= OnPathRenamed;
    }

    // ---------------------------------------------------------------- widget menüsü

    public void AddMenuItems(WidgetMenu menu)
    {
        if (UndoText is { } undo)
        {
            menu.Primary.Add(Menus.Item(undo, UndoRemove));
            menu.Primary.Add(new Separator());
        }
        menu.Primary.Add(AddMenu(CurrentTab, () => null));
        if (_ring.InSubRing) menu.Primary.Add(Menus.Item(L.T("Ana halkaya dön"), _ring.GoBack, KeyNames.Backspace));
        menu.Primary.Add(_name.MenuItem());
        menu.Primary.Add(Menus.Item(L.T("Simgeyi değiştir…"), PickIcon));
        menu.Appearance.Add(Menus.Parts(_config, [WidgetNameLine.Part, (RingView.PartKeys, L.N("Tuş numaraları")), Menus.ClosePart], ApplyParts));
        // Kısayolla imleçte açılan halka (bkz. RingShortcut): yalnızca kısayolun açtığı halkada işaretli; açarken ne yaptığı
        // bildirimde anlatılır, başka halkayı açıyorsa bu halka hedef olur; menü kapanır.
        menu.More.Add(Menus.Toggle(L.T("Kısayolla imleçte açılsın"), () => RingShortcut.Opens(_config), () => RingShortcut.ToggleFromMenu(_config),
            staysOpen: false));
    }

    /// <summary>
    /// "Öğe ekle ▸": <paramref name="tab"/> sekmesine, <paramref name="where"/> sırasına (yoksa sona). Sıra seçilince
    /// hesaplanır (menü açıkken halka yeniden kurulmuş olabilir).
    /// </summary>
    private MenuItem AddMenu(LauncherTab tab, Func<int?> where)
    {
        var add = new MenuItem { Header = L.T("Öğe ekle") };
        add.Items.Add(Menus.Item(L.T("Uygulama ya da dosya…"), () => AddFiles(tab, where)));
        add.Items.Add(Menus.Item(L.T("Klasör…"), () => AddFolders(tab, where)));
        add.Items.Add(new Separator());
        add.Items.Add(Menus.Item(L.T("Web adresi…"), () => AddAction(RingActionTypes.Url, tab, where)));
        add.Items.Add(Menus.Item(L.T("Komut…"), () => AddAction(RingActionTypes.Command, tab, where)));
        add.Items.Add(Menus.Item(L.T("Tuş kısayolu gönder…"), () => AddAction(RingActionTypes.Keys, tab, where)));
        add.Items.Add(Menus.Item(L.T("Metin yapıştır…"), () => AddAction(RingActionTypes.Text, tab, where)));
        var app = new MenuItem { Header = L.F("{0} eylemi", AppInfo.Name) };
        foreach (var id in RingAppIds.All)
            app.Items.Add(Menus.Item(Menus.Literal(RingAppIds.Label(id) ?? id), () => AddApp(id, tab, where())));
        add.Items.Add(app);
        // Alt halka tek düzey: yalnızca kök halkaya eklenir.
        if (ReferenceEquals(tab, _config.Tabs[0]))
        {
            add.Items.Add(new Separator());
            add.Items.Add(Menus.Item(L.T("Alt halka…"), AddSubRing));
        }
        return add;
    }

    private void PickIcon()
    {
        if (Owner is not { } window) return;
        Views.IconPicker.ForWidget(window, _config, WidgetText.DisplayName(_config),
            preview: icon => _ring.PreviewIcon(icon, active: !string.Equals(icon, _config.Icon, StringComparison.Ordinal)),
            commit: icon =>
            {
                _config.Icon = icon;
                AppHost.SaveSettings();
                _ring.PreviewIcon(null, active: false);
            });
    }

    // ---------------------------------------------------------------- öğe menüsü

    private void OnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _menuSlot = _ring.SlotAt(e.GetPosition(_ring));
        // Öğe yoksa (orta, dış) menü olayı karta geçer: widget'ın menüsü.
        _ring.ContextMenu = _menuSlot is null ? null : _itemMenu;
        _ring.ClearValue(ContextMenuService.PlacementTargetProperty); // fareyle: imlecin yanında
    }

    private void OnRingKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Apps || (key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift))
        {
            // Klavyeyle açılan menü vurgulanan öğenin ortasında; vurgu yoksa widget'ın menüsü.
            _menuSlot = _ring.HighlightedSlot;
            _ring.ContextMenu = _menuSlot is null ? null : _itemMenu;
            if (_menuSlot is not null && _ring.ItemElement(IndexOf(_menuSlot)) is { } element)
                ContextMenuService.SetPlacementTarget(_ring, element);
            else _ring.ClearValue(ContextMenuService.PlacementTargetProperty);
            return;
        }
        if (key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && _ring.HighlightedSlot is { } slot)
        {
            Remove(CurrentTab, slot);
            e.Handled = true;
        }
    }

    private int IndexOf(RingSlot slot) => _ring.Slots.ToList().IndexOf(slot);

    /// <summary>
    /// Öğenin <paramref name="tab"/> sekmesindeki şimdiki sırası (yoksa -1): menü ya da pencere açıkken halka yeniden kurulmuş
    /// olabilir (kutu taşıması, ad değişikliği), iş anında yolu ya da eylemin kimliğiyle yeniden bulunur.
    /// </summary>
    private int EntryNow(LauncherTab tab, RingSlot slot) =>
        !_config.Tabs.Contains(tab) ? -1
        : slot.Path is { } path ? LauncherEntries.IndexOfPath(tab, path)
        : slot.Action is { } action ? LauncherEntries.IndexOfAction(tab, action.Id)
        : -1;

    /// <summary>Sekme hâlâ halkadaysa kendisi, değilse gösterilen sekme.</summary>
    private LauncherTab Live(LauncherTab tab) => _config.Tabs.Contains(tab) ? tab : CurrentTab;

    private void FillItemMenu(ContextMenu menu, RingSlot slot)
    {
        // Menü açıkken halka yeniden kurulmuş olabilir (dışarıdan değişiklik): iş, menü açılırken yakalanan sekme ve öğenin
        // kendisiyle (yolu ya da kimliği) iş anında yeniden bulunarak yapılır.
        var tab = CurrentTab;
        var sub = slot.Kind == RingSlotKind.SubRing;
        var subTab = sub ? Tab(slot.TabIndex) : null;
        menu.Items.Add(Menus.Item(L.T("Aç"), () =>
        {
            if (subTab is not null) { if (_config.Tabs.IndexOf(subTab) is > 0 and var t) _ring.OpenSubRing(t); }
            else RingRunner.RunFromWidget(slot);
        }, KeyNames.Enter));
        // Öğe: arkasına eklenir; alt halka: içine.
        menu.Items.Add(subTab is not null ? AddMenu(subTab, () => null)
            : AddMenu(tab, () => EntryNow(tab, slot) is >= 0 and var i ? i + 1 : null));
        menu.Items.Add(new Separator());
        if (!sub)
        {
            if (LauncherEntries.Count(tab) > 1)
            {
                menu.Items.Add(Menus.Item(L.T("Saat yönünde taşı"), () => Step(tab, slot, clockwise: true)));
                menu.Items.Add(Menus.Item(L.T("Ters yönde taşı"), () => Step(tab, slot, clockwise: false)));
            }
            var targets = new List<(LauncherTab Tab, string Name)>();
            if (_ring.InSubRing) targets.Add((_config.Tabs[0], L.T("Ana halka")));
            for (var t = 1; t < _config.Tabs.Count; t++)
                if (t != _ring.TabIndex) targets.Add((_config.Tabs[t], Menus.Literal(RingSlot.SubRingName(_config.Tabs[t]))));
            if (targets.Count > 0)
            {
                var move = new MenuItem { Header = _ring.InSubRing ? L.T("Başka halkaya taşı") : L.T("Alt halkaya taşı") };
                foreach (var (target, name) in targets) move.Items.Add(Menus.Item(name, () => MoveToTab(tab, slot, target)));
                menu.Items.Add(move);
            }
        }
        menu.Items.Add(Menus.Item(L.T("Düzenle…"), () => Edit(slot, subTab)));
        if (slot is { Kind: RingSlotKind.Path, Path: { } path } && !TileItem.IsShellObject(path))
        {
            if (BoxMover.IsMoved(path))
            {
                var back = Menus.Item(L.T("Masaüstüne geri koy"), () => BoxMover.Return([path]));
                back.ToolTip = L.T("Öğe masaüstüne döner ve halkada kalır.");
                menu.Items.Add(back);
            }
            if (RingRunner.CanRunAsAdmin(path))
                menu.Items.Add(Menus.Item(L.T("Yönetici olarak çalıştır"), () => TileItem.Launch(path, asAdmin: true)));
            menu.Items.Add(Menus.Item(L.T("Dosya konumunu aç"), () => TileItem.Reveal(path)));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item(L.T("Halkadan kaldır"), () => Remove(tab, slot, subTab), KeyNames.Delete));
    }

    private void Step(LauncherTab tab, RingSlot slot, bool clockwise)
    {
        var from = EntryNow(tab, slot);
        if (from < 0 || RingInput.StepTarget(from, LauncherEntries.Count(tab), clockwise) is not { } to) return;
        _edits.Change(() => LauncherEntries.Move(tab, from, to));
        // Taşınan öğe vurgulu kalır (vurgu yeni yerine geçer): art arda taşınabilsin.
        if (ReferenceEquals(tab, CurrentTab)) _ring.Highlight(to);
    }

    private void MoveToTab(LauncherTab from, RingSlot slot, LauncherTab to)
    {
        var index = EntryNow(from, slot);
        if (index < 0 || !_config.Tabs.Contains(to)) return;
        var moved = false;
        _edits.Change(() => moved = LauncherEntries.MoveTo(from, index, to, int.MaxValue));
        if (!moved) Views.Notice.Show(L.F("\"{0}\" o halkada zaten var.", slot.Name), Views.NoticeKind.Info);
    }

    private void Edit(RingSlot slot, LauncherTab? subTab)
    {
        switch (slot.Kind)
        {
            case RingSlotKind.Action when slot.Action is { } action:
                if (Views.RingActionDialog.Edit(action, _config, Owner)) _edits.Change(() => { });
                break;
            case RingSlotKind.Path when slot.Path is { } path:
                if (Views.RingActionDialog.EditPath(slot, _config, Owner) is not { } look) return;
                _edits.Change(() =>
                {
                    ItemLooks.SetName(_config, path, look.Name, RingSlot.DefaultPathName(path));
                    ItemLooks.SetIcon(_config, path, look.Icon);
                });
                break;
            case RingSlotKind.SubRing when (subTab ?? Tab(slot.TabIndex)) is { } tab && _config.Tabs.IndexOf(tab) > 0:
                if (Views.RingActionDialog.AskSubRing(tab.Name, Owner) is { } name) _edits.Change(() => tab.Name = name);
                break;
        }
    }

    // ---------------------------------------------------------------- ekleme

    private void AddFiles(LauncherTab tab, Func<int?> where)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.T("Halkaya ekle"),
            Multiselect = true,
            DereferenceLinks = false,
            Filter = L.T("Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*"),
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() == true) AddPaths(tab, dialog.FileNames, null, null, where());
    }

    private void AddFolders(LauncherTab tab, Func<int?> where)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("Halkaya ekle"),
            Multiselect = true,
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() == true) AddPaths(tab, dialog.FolderNames, null, null, where());
    }

    /// <summary>Yolları ekler (kısayol kutusuyla aynı yoldan); hepsi zaten oradaysa söyler.</summary>
    private void AddPaths(LauncherTab tab, IReadOnlyList<string> paths, ItemLook? look, Action? then, int? at) =>
        _edits.AddItems(() => _config.Tabs.Contains(tab) ? tab : CurrentTab, paths, look, then, at,
            duplicate: () => Views.Notice.Show(paths.Count == 1
                ? L.F("\"{0}\" zaten halkada.", RingSlot.DefaultPathName(paths[0])) : L.T("Bu öğeler zaten halkada."), Views.NoticeKind.Info));

    private void AddAction(string type, LauncherTab tab, Func<int?> where)
    {
        // Sıra pencere kapanınca hesaplanır (pencere açıkken halka değişmiş olabilir).
        if (Views.RingActionDialog.Create(type, _config, Owner) is { } action) Insert(Live(tab), where(), action);
    }

    private void AddApp(string id, LauncherTab tab, int? at)
    {
        var target = Live(tab);
        if (LauncherEntries.Ordered(target).Any(e => e.Action is { Type: RingActionTypes.App } a && a.App == id))
        {
            Views.Notice.Show(L.F("\"{0}\" zaten halkada.", RingAppIds.Label(id) ?? id), Views.NoticeKind.Info);
            return;
        }
        Insert(target, at, RingActions.ForApp(id));
    }

    private void Insert(LauncherTab tab, int? at, LauncherAction action) =>
        _edits.Change(() => LauncherEntries.Insert(tab, at ?? int.MaxValue, LauncherEntry.Of(action)));

    private void AddSubRing()
    {
        if (Views.RingActionDialog.AskSubRing(null, Owner) is { } name)
            _edits.Change(() => _config.Tabs.Add(new LauncherTab { Name = name }));
    }

    // ---------------------------------------------------------------- kaldırma ve geri alma

    /// <summary>Öğeyi <paramref name="tab"/> sekmesinden çıkarır (iş anında yeniden bulunur); alt halkayı kaldırır.</summary>
    private void Remove(LauncherTab tab, RingSlot slot, LauncherTab? subTab = null)
    {
        if (slot.Kind == RingSlotKind.SubRing)
        {
            if ((subTab ?? Tab(slot.TabIndex)) is { } sub) RemoveSubRing(sub, slot.Name);
            return;
        }
        var index = EntryNow(tab, slot);
        if (index < 0) return;
        var returning = _edits.Remove(tab, index);
        if (returning is null) return;
        _removedSubRing = null;
        Views.Notice.Show(returning == true
                ? L.F("\"{0}\" halkadan kaldırıldı ve masaüstüne geri konuyor.", slot.Name)
                : L.F("\"{0}\" halkadan kaldırıldı.", slot.Name),
            Views.NoticeKind.Info, L.T("Geri al"), UndoRemove, L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>
    /// Alt halkayı (sekmeyi) içindekilerle birlikte kaldırır; doluysa önce sorar. Masaüstünden taşınmış öğeleri başka kutuda
    /// yoksa masaüstüne döner; bildirimdeki ve menüdeki "Geri al" alt halkayı yerine koyar.
    /// </summary>
    private void RemoveSubRing(LauncherTab tab, string name)
    {
        if (_config.Tabs.IndexOf(tab) <= 0) return;
        var count = LauncherEntries.Count(tab);
        if (count > 0 && !Views.Confirm.Ask(null, L.F("\"{0}\" alt halkası kaldırılsın mı?", name),
                L.P(count, "İçindeki {0} öğe de halkadan kalkar. Dosyalara dokunulmaz; bildirimden geri alabilirsin."), L.T("Kaldır"),
                near: DialogPoint))
            return;
        // Soru açıkken değişmiş olabilir: yeri şimdi bulunur.
        var index = _config.Tabs.IndexOf(tab);
        if (index <= 0) return;
        if (_ring.TabIndex == index) _ring.GoBack();
        var looks = new Dictionary<string, ItemLook>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in tab.Items)
            if (!LauncherEdits.Elsewhere(_config, tab, path) && ItemLooks.Get(_config, path) is { } look) looks[path] = look;
        _removedSubRing = (tab, index, looks);
        _edits.ForgetRemoved();
        _edits.Change(() =>
        {
            _config.Tabs.Remove(tab);
            _config.ActiveTab = Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
            foreach (var path in looks.Keys) ItemLooks.Reset(_config, path);
        });
        // Alt halkadaki, masaüstünden taşınmış öğeler başka kutuda yoksa masaüstüne döner.
        BoxMover.Reconcile();
        Views.Notice.Show(L.F("\"{0}\" alt halkası kaldırıldı.", name), Views.NoticeKind.Info, L.T("Geri al"), UndoRemove,
            L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>Menüdeki "Geri al" satırı; geri alınacak bir şey yoksa null.</summary>
    private string? UndoText
    {
        get
        {
            if (_removedSubRing is { } sub) return L.F("Geri al: \"{0}\" halkaya dönsün", Menus.Literal(RingSlot.SubRingName(sub.Tab)));
            if (_edits.LastRemoved is not { } last) return null;
            var name = last.Entry.Path is { } path
                ? (last.Look?.Name is { Length: > 0 } given ? given : RingSlot.DefaultPathName(path))
                : last.Entry.Action is { } action ? (string.IsNullOrWhiteSpace(action.Name) ? RingSlot.DefaultName(action) : action.Name) : "";
            return L.F("Geri al: \"{0}\" halkaya dönsün", Menus.Literal(name));
        }
    }

    private void UndoRemove()
    {
        if (_removedSubRing is { } sub)
        {
            _removedSubRing = null;
            if (_config.Tabs.Contains(sub.Tab)) return;
            _edits.Change(() =>
            {
                _config.Tabs.Insert(Math.Clamp(sub.Index, 1, _config.Tabs.Count), sub.Tab);
                foreach (var (path, look) in sub.Looks) ItemLooks.Restore(_config, path, look);
            });
            // Öğeler bu arada masaüstüne geri konduysa halka onları yeniden bulur (kip açıksa yeniden taşınır).
            BoxMover.Reconcile();
            return;
        }
        _edits.UndoRemove();
    }

    // ---------------------------------------------------------------- sürükle-bırak

    /// <summary>Dışarı sürüklenebilen öğe: bulunabilen yol (kabuk nesnesi değil). Eylemler ve alt halkalar sürüklenmez.</summary>
    private static bool CanDragOut(RingSlot slot) =>
        slot is { Kind: RingSlotKind.Path, Path: { } path } && !slot.Dimmed && !TileItem.IsShellObject(path);

    /// <summary>Yol öğesi dışarı (Gezgin'e, bölmeye, başka kutuya) sürüklenir: yalnızca bağlantıdır, dosya yerinden oynamaz.</summary>
    private void DragOut(RingSlot slot)
    {
        if (!CanDragOut(slot) || slot.Path is not { } path) return;
        var tab = CurrentTab;
        var source = new WidgetDragSource(_config.Id, path, FromBox: true, () => _edits.TakeOut(tab, path), ItemLooks.Get(_config, path));
        var native = TileItem.NativePath(path);
        WidgetDrag.Run(source, () => DragDrop.DoDragDrop(_ring, new DataObject(DataFormats.FileDrop, new[] { native }),
            DragDropEffects.Copy | DragDropEffects.Link));
    }

    /// <summary>
    /// Bırakılınca ne olur: kendi öğesi yer değiştirir (alt halkanın üstüne bırakılırsa oraya taşınır); başka kutudan gelen
    /// buraya taşınır (Ctrl: kopya); bölmeden, Gezgin'den ya da başka uygulamadan gelen eklenir; http(s) adresi web adresi olur.
    /// </summary>
    private enum DropKind { None, Reorder, IntoSubRing, MoveHere, Add, Url }

    private (DropKind Kind, int Index, RingSlot? SubRing, string? Url) PlanDrop(DragEventArgs e)
    {
        var point = e.GetPosition(_ring);
        var sub = _ring.SlotAt(point) is { Kind: RingSlotKind.SubRing } s ? s : null;
        var index = _ring.DropIndexAt(point);
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var source = WidgetDrag.For(e.Data);
            if (source is null) return (DropKind.Add, index, sub, null);
            if (source.WidgetId == _config.Id)
            {
                if (LauncherEntries.IndexOfPath(CurrentTab, source.Path) < 0) return (DropKind.None, index, null, null);
                return sub is not null ? (DropKind.IntoSubRing, index, sub, null) : (DropKind.Reorder, index, null, null);
            }
            if (!source.FromBox) return (DropKind.Add, index, sub, null);
            return (e.KeyStates.HasFlag(DragDropKeyStates.ControlKey) ? DropKind.Add : DropKind.MoveHere, index, sub, null);
        }
        return UrlOf(e.Data) is { } url ? (DropKind.Url, index, sub, url) : (DropKind.None, index, null, null);
    }

    // Sürüklenen metin her fare hareketinde başka süreçten yeniden okunmasın: sürükleme başına bir kez.
    private IDataObject? _urlData;
    private string? _url;

    private string? UrlOf(IDataObject data)
    {
        if (!ReferenceEquals(data, _urlData))
        {
            _urlData = data;
            _url = DroppedUrl(data);
        }
        return _url;
    }

    private void EndDrag()
    {
        _ring.ShowDrop(null, null, null);
        _urlData = null;
        _url = null;
    }

    /// <summary>Bırakılan metin bir http(s) adresiyse o (ilk satır); değilse null.</summary>
    private static string? DroppedUrl(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.UnicodeText) || data.GetData(DataFormats.UnicodeText) is not string text) return null;
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return RingActions.TryParseUrl(line, out var uri, out _) && uri.Scheme is "http" or "https" ? line : null;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var (kind, index, sub, _) = PlanDrop(e);
        // Kaynağın izin verdiği bir etki seçilmeli; yoksa Windows bırakmayı reddeder (yalnızca kopyalamaya izin veren uygulama).
        var allowed = e.AllowedEffects;
        e.Effects = kind == DropKind.None ? DragDropEffects.None
            : allowed.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link
            : allowed.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy
            : allowed & DragDropEffects.Move;
        var hint = kind switch
        {
            DropKind.None => null,
            DropKind.Reorder => null,
            _ when sub is not null => L.F("Bırak, \"{0}\" alt halkasına eklensin", sub.Name),
            DropKind.MoveHere => L.T("Bırak, bu halkaya taşınsın"),
            _ => L.T("Bırak, halkaya eklensin"),
        };
        var subIndex = sub is not null ? IndexOf(sub) : (int?)null;
        _ring.ShowDrop(kind is DropKind.None || sub is not null ? null : index, kind is DropKind.None ? null : subIndex, hint);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var (kind, index, sub, url) = PlanDrop(e);
        EndDrag();
        var source = WidgetDrag.For(e.Data);
        if (source is not null) WidgetDrag.MarkDropped();
        e.Handled = true;
        var target = sub is not null ? Tab(sub.TabIndex) : CurrentTab;
        int? at = sub is not null ? null : index;
        switch (kind)
        {
            case DropKind.Reorder when source is not null:
                var from = LauncherEntries.IndexOfPath(CurrentTab, source.Path);
                var to = index > from ? index - 1 : index;
                if (from >= 0 && to != from) _edits.Change(() => LauncherEntries.Move(CurrentTab, from, to));
                break;
            case DropKind.IntoSubRing when source is not null && sub is not null:
                var entry = LauncherEntries.IndexOfPath(CurrentTab, source.Path);
                var moved = false;
                if (entry >= 0) _edits.Change(() => moved = LauncherEntries.MoveTo(CurrentTab, entry, target, int.MaxValue));
                if (entry >= 0 && !moved) Views.Notice.Show(L.F("\"{0}\" o halkada zaten var.", RingSlot.DefaultPathName(source.Path)), Views.NoticeKind.Info);
                break;
            case DropKind.MoveHere when source is not null:
                AddPaths(target, [source.Path], source.Look, source.RemoveFromSource, at);
                break;
            case DropKind.Add:
                if (source is not null) AddPaths(target, [source.Path], source.Look, null, at);
                else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(target, paths, null, null, at);
                break;
            case DropKind.Url when url is not null:
                Insert(target, at, new LauncherAction { Type = RingActionTypes.Url, Url = url });
                break;
        }
    }
}
