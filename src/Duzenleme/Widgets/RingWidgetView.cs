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
/// halkaya taşı ▸, Değiştir…, Halkadan kaldır ("Geri al" bildirimde ve widget menüsünde). Halkanın ortasına ya da dışına sağ
/// tık widget'ın menüsünü açar; oraya basıp sürüklemek widget'ı taşır. Yol öğeleri dışarı sürüklenebilir (veride yalnızca
/// FileDrop); içeri dosya, klasör ya da http(s) adresi bırakılabilir (bırakılan yere girer; alt halkanın üstüne bırakılan
/// onun içine). Ekleme/çıkarma kısayol kutusuyla aynı yoldan (<see cref="LauncherEdits"/>): kutudaki masaüstü dosyaları
/// kurallardan korunur, "Kutuya koyduğum simgeler masaüstünden kalksın" açıksa masaüstünden kalkar, çıkınca geri döner; öğe
/// menüsünün kutu satırları kutuyla aynıdır (<see cref="BoxChoices.MenuRows"/>).</para>
/// </summary>
internal sealed class RingWidgetView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly RingView _ring = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid _nameBox;
    private readonly TextBlock _nameText;
    private readonly CardButtons _corner;
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
        _edits = new LauncherEdits(config, Render, () => Owner?.CenterPoint);

        _nameText = new TextBlock
        {
            FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        };
        _nameBox = new Grid { Margin = new Thickness(44, -2, 44, 6), Visibility = Visibility.Collapsed };
        _nameBox.Children.Add(_nameText);
        Grid.SetRow(_ring, 1);
        // Sağ üst köşede hep yerinde, sönük: ⋯ Düzenle ve × (kilitliyken kilit).
        _corner = new CardButtons { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -10, -12, 0) };
        Grid.SetRowSpan(_corner, 2);
        // Saydam zemin: halkanın dışına da bırakılabilsin (basıp sürüklemek yine widget'ı taşır).
        var root = new Grid { Background = Brushes.Transparent };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(_nameBox);
        root.Children.Add(_ring);
        root.Children.Add(_corner);
        Content = root;

        // Ad halkanın ortasında yazılır; satır yalnızca F2 / "Yeniden adlandır" sürerken halkanın üstünde açılır.
        _name = new WidgetNameLine(this, config, _nameBox, _nameText, shadow: null, Render, editOnly: true);
        _ring.PixelsPerDipHint = WidgetWindow.ExpectedPixelsPerDip(config);
        // 8'den az yerli halkada sonda "+" ("Uygulama ekle"): "Uygulama seç" açılır, seçilen gösterilen halkaya eklenir.
        _ring.ShowAddSlot = true;
        _ring.AddRequested += () => AddApps(CurrentTab, () => null);
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
        // Açık Düzenle panelindeki halka düzenleyicisi de tazelensin (sürükleyip bırakma, arka planda biten ekleme).
        _editorChanged?.Invoke();
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
        _corner.Update(_config);
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
        _corner.Paint(palette);
        ClearTypeText.Follow(this, _ring);
        ApplyParts(); // kilit kaldırma düğmesini gizleyebilir
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _ring.UpdateIconSizes();
    }

    /// <summary>F2: halkanın adı (bkz. <see cref="WidgetNameLine"/>; Düzenle panelinde ad kutusu).</summary>
    public bool TryBeginRename() => _name.Begin();

    public bool RefreshTitle()
    {
        _name.Refresh();
        return true;
    }

    public void Detach()
    {
        _editorChanged = null;
        _name.Cancel();
        _ring.Release();
        BoxMover.Changed -= OnOutsideChange;
        AppHost.PathRenamed -= OnPathRenamed;
    }

    // ---------------------------------------------------------------- widget menüsü

    /// <summary>
    /// Menüde: "Uygulama ekle…" (Başlat menüsündeki uygulamalar, gösterilen halkaya) ve "Tuşla imlecin yanında açılsın" (açıkken
    /// tuşuyla). Öbür öğe türleri (web adresi, komut, tuşlar…) Düzenle panelindeki halka düzenleyicisinde; kaldırılanın "Geri al"ı bildirimde.
    /// </summary>
    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item(L.T("Uygulama ekle…"), () => AddApps(CurrentTab, () => null)));
        // Kısayolla imleçte açılan halka (bkz. RingShortcut): yalnızca kısayolun açtığı halkada işaretli; açarken ne yaptığı
        // bildirimde anlatılır, başka halkayı açıyorsa bu halka hedef olur; menü kapanır (panel yeniden kurulur).
        var key = Menus.Toggle(L.T("Tuşla imlecin yanında açılsın"), () => RingShortcut.Opens(_config), () => RingShortcut.ToggleFromMenu(_config),
            staysOpen: false);
        if (RingShortcut.Opens(_config) && RingShortcut.Current is { } hotkey) key.InputGestureText = hotkey;
        menu.Primary.Add(key);
        if (!menu.ForEditor) return;
        menu.Settings.Add(Menus.Parts(_config,
            [WidgetNameLine.Part, (RingView.PartKeys, L.N("Tuş numaraları")), Menus.EditPart, Menus.ClosePart], ApplyParts));
    }

    // Açık Düzenle panelinin halka düzenleyicisi: halka değişince panel (ve düzenleyici) tazelenir.
    private Action? _editorChanged;

    /// <summary>
    /// Düzenle paneli: halka düzenleyicisi (<see cref="Views.RingEditor"/>: önizleme, seçili öğenin işleri, ekleme çubuğu,
    /// "Tuşla imlecin yanında açılsın", "Kutuya koyduğum simgeler masaüstünden kalksın"). Düzenlemeler widget'ın kendi
    /// <see cref="LauncherEdits"/>'iyle (menüdeki ve bildirimdeki "Geri al" ortak); kaldırılanın "Geri al"ı panelin bildiriminde.
    /// </summary>
    public bool AddEditorContent(Views.EditorContent content)
    {
        var editor = new Views.RingEditor(_config, _edits, () => Owner, () => Owner?.CenterPoint, content.Notice, () => content.Changed(),
            _ring.TabIndex);
        _editorChanged = () => content.Changed();
        content.Custom(editor, editor.Refresh);
        return true;
    }

    /// <summary>
    /// "Öğe ekle ▸": <paramref name="tab"/> sekmesine, <paramref name="where"/> sırasına (yoksa sona). Sıra seçilince
    /// hesaplanır (menü açıkken halka yeniden kurulmuş olabilir). <paramref name="withApps"/>: başta "Uygulama ya da dosya…"
    /// (öğe menüsünde; widget menüsünde hemen üstteki "Uygulama ekle…" aynı işi yaptığı için yok).
    /// </summary>
    private MenuItem AddMenu(LauncherTab tab, Func<int?> where, bool withApps = true)
    {
        var add = new MenuItem { Header = L.T("Öğe ekle") };
        if (withApps)
        {
            // Uygulama, dosya ya da klasör: "Uygulama seç" (dosya ve klasör altındaki "Dosya ya da klasör seç…"te).
            add.Items.Add(Menus.Item(L.T("Uygulama ya da dosya…"), () => AddApps(tab, where)));
            add.Items.Add(new Separator());
        }
        add.Items.Add(Menus.Item(L.T("Web adresi…"), () => AddAction(RingActionTypes.Url, tab, where)));
        add.Items.Add(Menus.Item(L.T("Komut…"), () => AddAction(RingActionTypes.Command, tab, where)));
        add.Items.Add(Menus.Item(L.T("Tuşlara bas…"), () => AddAction(RingActionTypes.Keys, tab, where)));
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

    // ---------------------------------------------------------------- öğe menüsü

    /// <summary>Menüsü olan yer ("+" yerinin menüsü yok: oraya sağ tık widget'ın menüsünü açar).</summary>
    private static RingSlot? WithMenu(RingSlot? slot) => slot is { Kind: RingSlotKind.Add } ? null : slot;

    private void OnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _menuSlot = WithMenu(_ring.SlotAt(e.GetPosition(_ring)));
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
            _menuSlot = WithMenu(_ring.HighlightedSlot);
            _ring.ContextMenu = _menuSlot is null ? null : _itemMenu;
            if (_menuSlot is not null && _ring.ItemElement(IndexOf(_menuSlot)) is { } element)
                ContextMenuService.SetPlacementTarget(_ring, element);
            else _ring.ClearValue(ContextMenuService.PlacementTargetProperty);
            return;
        }
        if (key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && WithMenu(_ring.HighlightedSlot) is { } slot)
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
        menu.Items.Add(Menus.Item(L.T("Değiştir…"), () => Edit(slot, subTab)));
        var rows = BoxChoices.MenuRows(BoxItemState.Link, ring: true, heldElsewhere: false);
        if (slot is { Kind: RingSlotKind.Path, Path: { } path } && !TileItem.IsShellObject(path))
        {
            if (RingRunner.CanRunAsAdmin(path))
                menu.Items.Add(Menus.Item(L.T("Yönetici olarak çalıştır"), () => TileItem.Launch(path, asAdmin: true)));
            menu.Items.Add(Menus.Item(L.T("Dosya konumunu aç"), () => TileItem.Reveal(path)));
            // Kutuyla aynı satırlar (BoxChoices.MenuRows): tek kaldırma fiili; istisna durumunda "Masaüstünden kaldır".
            // Bulunamayan (soluk) öğede geri koyma satırı yok.
            var state = BoxMover.ItemState(path, missing: slot.Dimmed);
            rows = BoxChoices.MenuRows(state, ring: true,
                heldElsewhere: state == BoxItemState.Moved && BoxPlan.HeldElsewhere(Launchers.All(AppHost.Settings), path));
        }
        menu.Items.Add(new Separator());
        foreach (var row in rows)
        {
            var item = row.Action switch
            {
                BoxMenuAction.Remove => Menus.Item(row.Label, () => Remove(tab, slot, subTab), KeyNames.Delete),
                BoxMenuAction.LeaveDesktopAsAdmin when slot.Path is { } leave =>
                    Menus.Item(row.Label, () => BoxMover.LeaveDesktop(_config, leave, Live(tab), asAdmin: true)),
                _ when slot.Path is { } leave => Menus.Item(row.Label, () => BoxMover.LeaveDesktop(_config, leave, Live(tab), asAdmin: false)),
                _ => null,
            };
            if (item is null) continue;
            // Alt halka ve eylem öğesi bir dosya değildir: ipucu yok.
            if (slot.Kind == RingSlotKind.Path) item.ToolTip = row.ToolTip;
            menu.Items.Add(item);
        }
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

    /// <summary>
    /// "Uygulama ekle…", "+" yeri ve öğe menüsündeki "Öğe ekle ▸ Uygulama ya da dosya…": "Uygulama seç" widget'ın
    /// monitöründe açılır; seçilen uygulama (ya da altındaki "Dosya ya da klasör seç…" ile seçilenler) <paramref name="tab"/> sekmesine, <paramref name="where"/>
    /// sırasına (yoksa sona; sıra pencere kapanınca hesaplanır) eklenir. Sekmede zaten olan "Zaten ekli"dir.
    /// </summary>
    private void AddApps(LauncherTab tab, Func<int?> where)
    {
        if (Views.AppPicker.Ask(Owner?.CenterPoint, Live(tab).Items, L.T("Halkaya ekle"), L.T("Halkaya ekle")) is not { } pick) return;
        AddPaths(tab, pick.Paths, null, null, where(), pick.Names);
    }

    /// <summary>Yolları ekler (kısayol kutusuyla aynı yoldan); hepsi zaten oradaysa söyler.</summary>
    private void AddPaths(LauncherTab tab, IReadOnlyList<string> paths, ItemLook? look, Action? then, int? at,
        IReadOnlyDictionary<string, string>? names = null) =>
        _edits.AddItems(() => _config.Tabs.Contains(tab) ? tab : CurrentTab, paths, look, then, at,
            duplicate: () => Views.Notice.Show(paths.Count == 1
                ? L.F("\"{0}\" zaten halkada.", RingSlot.DefaultPathName(paths[0])) : L.T("Bu öğeler zaten halkada."), Views.NoticeKind.Info),
            names: names);

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
        var moved = slot.Path is { } path && BoxMover.IsMoved(path);
        var returning = _edits.Remove(tab, index);
        if (returning is null) return;
        _removedSubRing = null;
        Views.Notice.Show(returning == true
                ? L.F("\"{0}\" halkadan kaldırıldı ve masaüstüne geri konuyor.", slot.Name)
                : moved ? L.F("\"{0}\" halkadan kaldırıldı; başka bir kutuda ya da halkada da durduğu için masaüstüne dönmedi.", slot.Name)
                : L.F("\"{0}\" halkadan kaldırıldı.", slot.Name),
            Views.NoticeKind.Info, L.T("Geri al"), UndoRemove, L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>
    /// Alt halkayı (sekmeyi) içindekilerle birlikte kaldırır (soru yok). Masaüstünden kalkmış öğeleri başka bir kutuda ya da
    /// halkada yoksa masaüstüne döner (bildirim kaç tanesinin döndüğünü söyler); bildirimdeki ve menüdeki "Geri al" alt halkayı
    /// yerine koyar (öğeler yeniden kalkar).
    /// </summary>
    private void RemoveSubRing(LauncherTab tab, string name)
    {
        var index = _config.Tabs.IndexOf(tab);
        if (index <= 0) return;
        var returning = BoxMover.ReturningIf(_config.Id, tab, null);
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
        BoxMover.Reconcile(interactive: true);
        Views.Notice.Show(WidgetManager.RemovedText(L.F("\"{0}\" alt halkası kaldırıldı.", name), returning, modeOff: false),
            Views.NoticeKind.Info, L.T("Geri al"), UndoRemove, L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>Bildirimdeki "Geri al": son kaldırılan öğeyi ya da alt halkayı yerine koyar.</summary>
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
            BoxMover.Reconcile(interactive: true);
            return;
        }
        _edits.UndoRemove();
    }

    // ---------------------------------------------------------------- sürükle-bırak

    /// <summary>Dışarı sürüklenebilen öğe: bulunabilen yol (kabuk nesnesi değil). Eylemler ve alt halkalar sürüklenmez.</summary>
    private static bool CanDragOut(RingSlot slot) =>
        slot is { Kind: RingSlotKind.Path, Path: { } path } && !slot.Dimmed && !TileItem.IsShellObject(path);

    /// <summary>
    /// Yol öğesi dışarı (Gezgin'e, bölmeye, başka kutuya) sürüklenir: yalnızca bağlantıdır, dosya yerinden oynamaz. Masaüstünden
    /// kalkmış öğe (NestDesk klasöründe) ise taşınabilir: Windows masaüstüne ya da Gezgin'e bırakılınca halkadan çıkmış sayılır
    /// (<see cref="BoxMover.DraggedOut"/>; kopyalanırsa bir şey olmaz).
    /// </summary>
    private void DragOut(RingSlot slot)
    {
        if (!CanDragOut(slot) || slot.Path is not { } path) return;
        var tab = CurrentTab;
        var source = new WidgetDragSource(_config.Id, path, FromBox: true, () => _edits.TakeOut(tab, path), ItemLooks.Get(_config, path));
        var native = TileItem.NativePath(path);
        var moved = BoxMover.IsMoved(path);
        var effects = moved ? DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link : DragDropEffects.Copy | DragDropEffects.Link;
        var inside = WidgetDrag.Run(source, () => DragDrop.DoDragDrop(_ring, new DataObject(DataFormats.FileDrop, new[] { native }), effects));
        if (!inside && moved) BoxMover.DraggedOut(_config.Id, tab, path);
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
    // Bırakılanlar masaüstünden kalkar mı ("Kutuya koyduğum simgeler masaüstünden kalksın"; sürükleme başına bir kez).
    private IDataObject? _leavesData;
    private BoxDropEffect _leaves;

    private string? UrlOf(IDataObject data)
    {
        if (!ReferenceEquals(data, _urlData))
        {
            _urlData = data;
            _url = DroppedUrl(data);
        }
        return _url;
    }

    private BoxDropEffect LeavesOf(IDataObject data)
    {
        if (!ReferenceEquals(data, _leavesData))
        {
            _leavesData = data;
            _leaves = WidgetDrag.For(data) is not { FromBox: true } && data.GetData(DataFormats.FileDrop) is string[] dropped
                ? BoxMover.DropEffect(dropped)
                : BoxDropEffect.None;
        }
        return _leaves;
    }

    private void EndDrag()
    {
        _ring.ShowDrop(null, null, null);
        _urlData = null;
        _url = null;
        _leavesData = null;
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
        var leaves = kind == DropKind.Add ? LeavesOf(e.Data) : BoxDropEffect.None;
        var hint = kind switch
        {
            DropKind.None => null,
            DropKind.Reorder => null,
            _ when sub is not null => L.F("Bırak, \"{0}\" alt halkasına eklensin", sub.Name),
            DropKind.MoveHere => L.T("Bırak, bu halkaya taşınsın"),
            _ when leaves == BoxDropEffect.Moves => L.T("Bırak, halkaya girsin; masaüstünden kalkar"),
            _ when leaves == BoxDropEffect.Hides => L.T("Bırak, halkaya girsin; bölmeden kalkar"),
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
