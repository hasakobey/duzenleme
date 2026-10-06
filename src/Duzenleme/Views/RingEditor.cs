using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Halka düzenleyicisi (Kolay NestDesk; AutomationId <c>RingEditor</c>): bir halkanın (masaüstündeki halka widget'ı ya da NestDesk
/// halkası, <see cref="AppSettings.Hub"/>) öğelerini düzenler. Halka widget'ının Düzenle panelinde widget'ın ayarları olarak
/// (<see cref="RingWidgetView.AddEditorContent"/>), NestDesk halkası için kendi penceresinde (<see cref="RingEditorWindow"/>,
/// imlecin ekranının ortasında) durur; ikisine de <see cref="Open"/> götürür.
/// <para>Üstte halkanın canlı önizlemesi (≈220 DIP, gerçek <see cref="RingView"/>, düzenleme kipinde: öğeler çalışmaz, alt halkaya
/// girilmez): bir yere tıklamak (ya da oklar + Enter, 1–9) onu seçer (<c>RingEditor.Slot.N</c>). Seçili öğenin altında simgesi,
/// adı ve ne olduğu; "◀" / "▶" (Alt+← / Alt+→: ters/saat yönünde taşır), "Değiştir…", "Kaldır" (Delete; "Geri al" bildirimde)
/// ve alt halkada "Aç" (alt halkanın içini düzenler; "Ana halkaya dön" ya da ortaya tıklamak geri getirir). Ekleme çubuğu:
/// Uygulama ("Uygulama seç") · Klasör · Web adresi · NestDesk eylemi · "Daha fazla ▾" (Komut…, Tuşlara bas…, Metin yapıştır…,
/// kökteyken Alt halka…); yeni öğe seçilinin arkasına, yoksa sona girer. Altta "Tuşla imlecin yanında açılsın" (bkz.
/// <see cref="RingShortcut"/>) ve uygulama geneli "Kutuya koyduğum simgeler masaüstünden kalksın" (<see cref="DesktopModes.SetBoxLeaves"/>).</para>
/// <para>Bütün değişiklikler kutuyla aynı yoldan (<see cref="LauncherEdits"/>, sıra <see cref="LauncherEntries"/>): kaydedilir,
/// kutudaki masaüstü dosyaları kurallardan korunur. Alt halkalar ve <see cref="AppSettings.RingWidgetId"/> korunur;
/// <c>Tabs[0]</c> hep kök halkadır. Fareyle hiçbir şey belirmez ya da kaymaz; hareket yok.</para>
/// </summary>
internal sealed class RingEditor : UserControl
{
    /// <summary>Önizlemenin en büyük kenarı (DIP).</summary>
    public const double PreviewDip = 220;

    /// <summary>Seçili yer: yol (harf duyarsız), eylem kimliği ya da alt halkanın sekmesi.</summary>
    private sealed record SlotKey(string? Path, string? ActionId, LauncherTab? Tab);

    private readonly WidgetConfig _config;
    private readonly LauncherEdits _edits;
    private readonly Func<WidgetWindow?> _anchor;
    private readonly Func<NativeMethods.POINT?> _near;
    private readonly Action<string, Action>? _hostNotice;
    private readonly Action? _changed;
    private readonly RingView _ring = new() { EditMode = true, StableSize = true, ItemIdPrefix = "RingEditor.Slot.", Focusable = true };
    private readonly RingBackdrop _backdrop = new();
    private readonly List<Action> _refreshers = [];
    private readonly TextBlock _selName, _selTarget, _subRingLine, _shortcutText;
    private readonly Image _selIcon;
    private readonly FrameworkElement _selection, _empty, _subRingRow;
    private readonly Button _left, _right, _change, _remove, _open;
    private readonly Border _notice;
    private readonly TextBlock _noticeText;
    private Action? _noticeUndo;
    private SlotKey? _selected;
    private int _tab;
    private bool _refreshing;

    /// <param name="config">Düzenlenen halka.</param>
    /// <param name="edits">Halka widget'ının kendi düzenlemeleri ("Geri al" widget menüsüyle ortak); yoksa düzenleyici kendisininkini kurar.</param>
    /// <param name="anchor">Öğe pencerelerinin (ve simge seçicinin) yanında açılacağı widget; NestDesk halkasında yok.</param>
    /// <param name="near">"Uygulama seç" ve soruların açılacağı ekrandaki nokta; yoksa imleç.</param>
    /// <param name="notice">Bildirim ve "Geri al" (Düzenle panelinin bildirim satırı); yoksa düzenleyicinin kendi satırı.</param>
    /// <param name="changed">Bir şey değişti (Düzenle paneli tazelensin; o da <see cref="Refresh"/>'i çağırır); yoksa kendisi tazelenir.</param>
    /// <param name="tab">Başta gösterilen halka (0 kök, 1… alt halka).</param>
    public RingEditor(WidgetConfig config, LauncherEdits? edits, Func<WidgetWindow?> anchor, Func<NativeMethods.POINT?> near,
        Action<string, Action>? notice = null, Action? changed = null, int tab = 0)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab { Name = L.T("Uygulamalar") });
        _anchor = anchor;
        _near = near;
        _hostNotice = notice;
        _changed = changed;
        _tab = Math.Clamp(tab, 0, _config.Tabs.Count - 1);
        _edits = edits ?? new LauncherEdits(config, Refresh, near);
        AutomationProperties.SetAutomationId(this, "RingEditor");
        AutomationProperties.SetName(this, L.T("Halkayı düzenle"));
        AutomationProperties.SetName(_ring, L.T("Halka"));

        var root = new StackPanel();
        root.Children.Add(EditorRows.Muted(L.T("Bir öğeyi seçmek için halkada ona tıkla. Alt+← ve Alt+→ seçileni taşır.")));

        // Önizleme: açılır halkadaki zemin ve halka, düzenleyicinin genişliğine sığdırılır.
        var stage = new Grid();
        _backdrop.HorizontalAlignment = HorizontalAlignment.Center;
        _backdrop.VerticalAlignment = VerticalAlignment.Center;
        _ring.HorizontalAlignment = HorizontalAlignment.Center;
        _ring.VerticalAlignment = VerticalAlignment.Center;
        stage.Children.Add(_backdrop);
        stage.Children.Add(_ring);
        root.Children.Add(new Viewbox
        {
            Child = stage, MaxWidth = PreviewDip, MaxHeight = PreviewDip, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4),
        });
        _ring.ItemInvoked += Select;
        _ring.Navigated += () =>
        {
            _tab = _ring.TabIndex;
            _selected = null;
            Refresh();
        };

        // Alt halkadayken: hangisi olduğu ve "Ana halkaya dön".
        _subRingLine = EditorRows.Muted("");
        _subRingLine.VerticalAlignment = VerticalAlignment.Center;
        var back = EditorRows.Link(L.T("Ana halkaya dön"), "RingEditor.Back", () => ShowTab(0));
        var subRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(back, Dock.Right);
        back.Margin = new Thickness(8, 0, -4, 0);
        subRow.Children.Add(back);
        subRow.Children.Add(_subRingLine);
        _subRingRow = subRow;
        root.Children.Add(subRow);

        // Seçili öğe: simge, ad, ne olduğu; altında işleri.
        _selIcon = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(_selIcon, BitmapScalingMode.HighQuality);
        _selName = new TextBlock { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        _selName.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _selTarget = EditorRows.Muted("");
        _selTarget.TextWrapping = TextWrapping.NoWrap;
        _selTarget.TextTrimming = TextTrimming.CharacterEllipsis;
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_selName);
        texts.Children.Add(_selTarget);
        var head = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
        DockPanel.SetDock(_selIcon, Dock.Left);
        head.Children.Add(_selIcon);
        head.Children.Add(texts);

        _left = Small("◀", "RingEditor.Left", L.T("Ters yönde taşı"), () => Step(clockwise: false));
        _right = Small("▶", "RingEditor.Right", L.T("Saat yönünde taşı"), () => Step(clockwise: true));
        _change = Small(L.T("Değiştir…"), "RingEditor.Change", L.T("Değiştir…"), Change);
        _remove = Small(L.T("Kaldır"), "RingEditor.Remove", L.T("Kaldır"), Remove);
        _open = Small(L.T("Aç"), "RingEditor.Open", L.T("Alt halkayı aç"), OpenSubRing);
        var actions = new WrapPanel();
        foreach (var button in new[] { _left, _right, _change, _remove, _open }) actions.Children.Add(button);
        var selection = new StackPanel();
        selection.Children.Add(head);
        selection.Children.Add(actions);
        _selection = selection;
        root.Children.Add(selection);
        _empty = EditorRows.Muted(L.T("Seçili öğe yok."), top: 4);
        root.Children.Add(_empty);

        // Düzenleyicinin kendi bildirim satırı (panelin dışında, NestDesk halkasının penceresinde).
        _noticeText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        _noticeText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        AutomationProperties.SetLiveSetting(_noticeText, AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(_noticeText, "RingEditor.Notice");
        var undo = EditorRows.Link(L.T("Geri al"), "RingEditor.Notice.Undo", () =>
        {
            var action = _noticeUndo;
            HideNotice();
            action?.Invoke();
        });
        undo.Margin = new Thickness(8, 0, -4, 0);
        var noticeRow = new DockPanel();
        DockPanel.SetDock(undo, Dock.Right);
        noticeRow.Children.Add(undo);
        noticeRow.Children.Add(_noticeText);
        _notice = new Border
        {
            Child = noticeRow, Padding = new Thickness(10, 6, 6, 6), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _notice.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        root.Children.Add(_notice);

        // Ekleme çubuğu.
        root.Children.Add(EditorRows.Section(L.T("Öğe ekle"), top: 14));
        var add = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        add.Children.Add(Small(L.T("+ Uygulama"), "RingEditor.Add.App", L.T("Uygulama ekle"), AddApps));
        add.Children.Add(Small(L.T("+ Klasör"), "RingEditor.Add.Folder", L.T("Klasör ekle"), AddFolders));
        add.Children.Add(Small(L.T("+ Web adresi"), "RingEditor.Add.Web", L.T("Web adresi ekle"), () => AddAction(RingActionTypes.Url)));
        Button? actionButton = null;
        actionButton = Small(L.F("+ {0} eylemi", AppInfo.Name), "RingEditor.Add.Action", L.F("{0} eylemi ekle", AppInfo.Name),
            () => ShowMenu(actionButton!, AppActionsMenu()));
        add.Children.Add(actionButton);
        Button? more = null;
        more = Small(L.T("Daha fazla ▾"), "RingEditor.Add.More", L.T("Daha fazla öğe türü"), () => ShowMenu(more!, MoreMenu()));
        add.Children.Add(more);
        root.Children.Add(add);

        // Halkanın açılışı ve uygulama geneli kutu seçeneği.
        root.Children.Add(EditorRows.Switch(() => L.T("Tuşla imlecin yanında açılsın"), "RingEditor.Shortcut", () => RingShortcut.Opens(_config), on =>
        {
            if (_refreshing || on == RingShortcut.Opens(_config)) return;
            RingShortcut.ToggleFromMenu(_config);
            Changed();
        }, _refreshers));
        _shortcutText = EditorRows.Muted("");
        _shortcutText.Margin = new Thickness(0, -2, 0, 4);
        root.Children.Add(_shortcutText);
        root.Children.Add(EditorRows.Switch(() => L.T("Kutuya koyduğum simgeler masaüstünden kalksın"), "RingEditor.BoxLeaves", () => BoxMover.On, on =>
        {
            if (_refreshing || on == BoxMover.On) return;
            DesktopModes.SetBoxLeaves(on, _near(), Window.GetWindow(this));
            Changed();
        }, _refreshers));

        Content = root;
        PreviewKeyDown += OnKey;
        Refresh();
    }

    // ---------------------------------------------------------------- dışarıya açık

    /// <summary>
    /// Halkayı düzenlemeye açar: masaüstündeki halka widget'ıysa widget öne gelir ve Düzenle paneli (içinde bu düzenleyici)
    /// yanında açılır; değilse (NestDesk halkası) düzenleyicinin kendi penceresi imlecin ya da <paramref name="near"/> noktasının
    /// ekranında ortalanır.
    /// </summary>
    /// <remarks>
    /// Halka iş anında kimliğiyle bulunur (<see cref="RingTargets.EditPlace"/>): kaldırılmış (ya da "Geri al"la yeni nesneyle geri
    /// gelmiş) widget'ın eski kopyası düzenlenmez; kaldırıldıysa yalnızca söylenir. Widget'ın penceresi henüz açılmadıysa (açılış,
    /// gizli masaüstü) widget'lar gösterilir ve birkaç kez boşta yeniden denenir; olmazsa söylenir. İstenen halka (alt halka)
    /// Düzenle panelinde de açılır.
    /// </remarks>
    public static void Open(WidgetConfig config, int tab = 0, NativeMethods.POINT? near = null) => Open(config, tab, near, attempt: 0);

    private const int OpenAttempts = 4;

    private static void Open(WidgetConfig config, int tab, NativeMethods.POINT? near, int attempt)
    {
        var (place, ring) = RingTargets.EditPlace(AppHost.Settings, config);
        switch (place)
        {
            case RingEditPlace.Hub:
                RingEditorWindow.ShowFor(ring!, tab, near);
                return;
            case RingEditPlace.Gone:
                Views.Notice.Show(L.T("Bu halka artık masaüstünde değil."), NoticeKind.Info);
                return;
        }
        if (attempt == 0) AppHost.EnsureWidgetsShown();
        var view = AppHost.Widgets.ViewOf(ring!.Id) as RingWidgetView;
        if (view is not null) view.EditorTab = tab;
        if (AppHost.Widgets.RevealAndEdit(ring.Id))
        {
            // Panel zaten bu widget'taydı ve yeniden kurulmadıysa istenen halka sonraki bir kurulumda beklenmedik açılmasın.
            if (view is not null) view.Dispatcher.BeginInvoke(() => view.EditorTab = null, DispatcherPriority.ContextIdle);
            return;
        }
        if (attempt + 1 >= OpenAttempts)
        {
            Views.Notice.Show(L.T("Halka henüz açılmadı. Biraz sonra yeniden dene."), NoticeKind.Info);
            return;
        }
        // Pencereler sırayla açılır (RestoreAll) ya da yeni gösteriliyor: boşta yeniden denenir (yine kimlikle bulunur).
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Open(ring, tab, near, attempt + 1);
        };
        timer.Start();
    }

    /// <summary>Düzenlenen halka.</summary>
    public WidgetConfig Config => _config;

    /// <summary>
    /// Önizlemeyi, seçimi ve anahtarları ayardan tazeler (halka değişti, panel yeniden etkinleşti). Önizleme yalnızca halkanın
    /// parmak izi (<see cref="RingFingerprint"/>), gösterilen halka ya da palet değiştiyse yeniden kurulur: ilgisiz kayıtlar
    /// (widget sürüklemek) ve aynı düzenlemenin ikinci bildirimi halkayı yeniden kurmaz.
    /// </summary>
    public void Refresh()
    {
        _refreshing = true;
        _refreshCount++;
        try
        {
            if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab { Name = L.T("Uygulamalar") });
            _tab = Math.Clamp(_tab, 0, _config.Tabs.Count - 1);
            var palette = RingView.PaletteFor(_config);
            var print = RingFingerprint.Of(_config, SystemParameters.HighContrast, WidgetPalette.SystemDark);
            if (_built is not { } last || last.Print != print || last.Tab != _tab || !ReferenceEquals(last.Palette, palette))
            {
                _built = (print, _tab, palette);
                _ring.ApplyPalette(palette, rebuild: false);
                _ring.CenterShowsIcon = !_config.Shows(WidgetNameLine.PartKey);
                _ring.SetRing(_config, _tab);
                _backdrop.Apply(palette, _ring.Metrics.OuterRadius);
            }

            var index = SelectedIndex();
            if (index is null) _selected = null;
            _ring.SelectedIndex = index;
            UpdateSelection(index is { } i ? _ring.Slots[i] : null);

            _subRingRow.Visibility = _tab > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_tab > 0) _subRingLine.Text = L.F("Alt halka: {0}", RingSlot.SubRingName(_config.Tabs[_tab]));
            _shortcutText.Text = RingShortcut.Opens(_config) && RingShortcut.Current is { } hotkey ? L.F("Kısayol: {0}", hotkey) : "";
            _shortcutText.Visibility = _shortcutText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var refresh in _refreshers) refresh();
        }
        finally { _refreshing = false; }
    }

    /// <summary>Düzenleyici kapanıyor: önizlemenin yerleri kutucuklarının olaylarından çıkar.</summary>
    public void Release()
    {
        _ring.Release();
        _built = null;
    }

    /// <summary>Önizlemenin en son kurulduğu hâl (bkz. <see cref="Refresh"/>).</summary>
    private (ulong Print, int Tab, WidgetPalette Palette)? _built;

    /// <summary>Kaç kez tazelendi: düzenleme kendi yolundan (kutunun çizim geri çağırması) zaten tazelediyse ikinci kez bildirilmez.</summary>
    private int _refreshCount;

    // ---------------------------------------------------------------- seçim

    private LauncherTab CurrentTab => _config.Tabs[Math.Clamp(_tab, 0, _config.Tabs.Count - 1)];

    private static SlotKey KeyOf(RingSlot slot, WidgetConfig config) => slot.Kind switch
    {
        RingSlotKind.Path => new SlotKey(slot.Path, null, null),
        RingSlotKind.Action => new SlotKey(null, slot.Action?.Id, null),
        RingSlotKind.SubRing when slot.TabIndex > 0 && slot.TabIndex < config.Tabs.Count => new SlotKey(null, null, config.Tabs[slot.TabIndex]),
        _ => new SlotKey(null, null, null),
    };

    private int? SelectedIndex()
    {
        if (_selected is not { } key) return null;
        for (var i = 0; i < _ring.Slots.Count; i++)
        {
            var slot = _ring.Slots[i];
            if (key.Path is { } path && slot.Path is { } p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) return i;
            if (key.ActionId is { } id && slot.Action?.Id == id) return i;
            if (key.Tab is { } tab && slot.Kind == RingSlotKind.SubRing && slot.TabIndex > 0 && slot.TabIndex < _config.Tabs.Count &&
                ReferenceEquals(_config.Tabs[slot.TabIndex], tab)) return i;
        }
        return null;
    }

    private RingSlot? Selected => SelectedIndex() is { } i ? _ring.Slots[i] : null;

    private void Select(RingSlot slot)
    {
        if (slot.Kind == RingSlotKind.Add) return;
        _selected = KeyOf(slot, _config);
        var index = SelectedIndex();
        _ring.SelectedIndex = index;
        UpdateSelection(index is { } i ? _ring.Slots[i] : null);
    }

    private void UpdateSelection(RingSlot? slot)
    {
        _selection.Visibility = slot is null ? Visibility.Collapsed : Visibility.Visible;
        _empty.Visibility = slot is null ? Visibility.Visible : Visibility.Collapsed;
        if (slot is null) return;
        _selIcon.Source = slot.Icon;
        _selIcon.Visibility = slot.Icon is null ? Visibility.Collapsed : Visibility.Visible;
        // Simge arka planda gelir: gelince satır da tazelensin.
        slot.Changed -= OnSelectedIconChanged;
        slot.Changed += OnSelectedIconChanged;
        _selName.Text = slot.Name;
        _selTarget.Text = TargetText(slot);
        _selTarget.Visibility = _selTarget.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var sub = slot.Kind == RingSlotKind.SubRing;
        var count = sub ? _config.Tabs.Count - 1 : LauncherEntries.Count(CurrentTab);
        _left.IsEnabled = _right.IsEnabled = count > 1;
        _open.Visibility = sub ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(_selection, slot.Name);
    }

    private void OnSelectedIconChanged()
    {
        if (Selected is { } slot) _selIcon.Source = slot.Icon;
        _selIcon.Visibility = _selIcon.Source is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Seçili öğenin ne olduğu (yalnızca ekranda; hiçbir yere yazılmaz). Metin öğesinin içeriği gösterilmez.</summary>
    private static string TargetText(RingSlot slot) => slot.Kind switch
    {
        RingSlotKind.SubRing => L.P(slot.ChildCount, "({0} öğe)"),
        RingSlotKind.Path when slot.Path is { } path => StartApps.IsApp(path) ? L.T("Uygulama") : TileItem.IsShellObject(path) ? "" : path,
        RingSlotKind.Action when slot.Action is { } action => action.Type switch
        {
            RingActionTypes.Url => action.Url ?? "",
            RingActionTypes.Command => action.Command ?? "",
            RingActionTypes.Keys => L.T("Tuşlara basar (yalnızca tuşla açılan halkada)"),
            RingActionTypes.Text => L.T("Metin yapıştırır (yalnızca tuşla açılan halkada)"),
            RingActionTypes.App => L.F("{0} eylemi", AppInfo.Name),
            _ => "",
        },
        _ => "",
    };

    /// <summary>Öğenin sekmedeki şimdiki sırası (iş anında yolu ya da kimliğiyle yeniden bulunur); yoksa -1.</summary>
    private int EntryNow(LauncherTab tab, RingSlot slot) =>
        !_config.Tabs.Contains(tab) ? -1
        : slot.Path is { } path ? LauncherEntries.IndexOfPath(tab, path)
        : slot.Action is { } action ? LauncherEntries.IndexOfAction(tab, action.Id)
        : -1;

    /// <summary>Yeni öğenin girdiği sıra: seçili öğenin arkası (alt halka seçiliyse ya da seçim yoksa sona).</summary>
    private int? InsertAt() =>
        Selected is { Kind: RingSlotKind.Path or RingSlotKind.Action } slot && EntryNow(CurrentTab, slot) is >= 0 and var i ? i + 1 : null;

    // ---------------------------------------------------------------- işler

    private void Change(Action change)
    {
        var before = _refreshCount;
        _edits.Change(change);
        // Düzenlemenin çizim geri çağırması (widget'ın Render'ı → panel, ya da düzenleyicinin kendi Refresh'i) zaten tazelediyse yeter.
        if (_refreshCount == before) Changed();
    }

    private void Changed()
    {
        if (_changed is not null) _changed();
        else Refresh();
    }

    private void ShowTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, _config.Tabs.Count - 1);
        _selected = null;
        Refresh();
    }

    private void OpenSubRing()
    {
        if (Selected is not { Kind: RingSlotKind.SubRing } slot) return;
        ShowTab(slot.TabIndex);
    }

    /// <summary>"◀" / "▶", Alt+← / Alt+→: seçili öğe bir yer ters ya da saat yönünde (alt halkalar kendi aralarında) taşınır.</summary>
    private void Step(bool clockwise)
    {
        if (Selected is not { } slot) return;
        if (slot.Kind == RingSlotKind.SubRing)
        {
            var from = slot.TabIndex;
            var to = clockwise ? from + 1 : from - 1;
            if (from < 1 || to < 1 || to >= _config.Tabs.Count) return;
            Change(() => (_config.Tabs[from], _config.Tabs[to]) = (_config.Tabs[to], _config.Tabs[from]));
            return;
        }
        var tab = CurrentTab;
        var index = EntryNow(tab, slot);
        if (index < 0 || RingInput.StepTarget(index, LauncherEntries.Count(tab), clockwise) is not { } target) return;
        Change(() => LauncherEntries.Move(tab, index, target));
    }

    /// <summary>"Değiştir…": öğenin kendi penceresi (yol: ad ve simge; eylem: alanları; alt halka: adı).</summary>
    private void Change()
    {
        if (Selected is not { } slot) return;
        switch (slot.Kind)
        {
            case RingSlotKind.Action when slot.Action is { } action:
                if (RingActionDialog.Edit(action, _config, _anchor())) Change(() => { });
                break;
            case RingSlotKind.Path when slot.Path is { } path:
                if (RingActionDialog.EditPath(slot, _config, _anchor()) is not { } look) return;
                Change(() =>
                {
                    ItemLooks.SetName(_config, path, look.Name, RingSlot.DefaultPathName(path));
                    ItemLooks.SetIcon(_config, path, look.Icon);
                });
                break;
            case RingSlotKind.SubRing when slot.TabIndex > 0 && slot.TabIndex < _config.Tabs.Count:
                var tab = _config.Tabs[slot.TabIndex];
                if (RingActionDialog.AskSubRing(tab.Name, _anchor()) is { } name) Change(() => tab.Name = name);
                break;
        }
    }

    /// <summary>"Kaldır" (Delete): öğe ya da alt halka (içindekilerle) kalkar; "Geri al" bildirimde.</summary>
    private void Remove()
    {
        if (Selected is not { } slot) return;
        _selected = null;
        if (slot.Kind == RingSlotKind.SubRing)
        {
            RemoveSubRing(slot);
            return;
        }
        var tab = CurrentTab;
        var index = EntryNow(tab, slot);
        if (index < 0) return;
        var moved = slot.Path is { } path && BoxMover.IsMoved(path);
        var returning = _edits.Remove(tab, index);
        Changed();
        if (returning is null) return;
        Notice(returning == true
                ? L.F("\"{0}\" halkadan kaldırıldı ve masaüstüne geri konuyor.", slot.Name)
                : moved ? L.F("\"{0}\" halkadan kaldırıldı; başka bir kutuda ya da halkada da durduğu için masaüstüne dönmedi.", slot.Name)
                : L.F("\"{0}\" halkadan kaldırıldı.", slot.Name),
            () =>
            {
                if (_edits.UndoRemove()) Changed();
            });
    }

    private void RemoveSubRing(RingSlot slot)
    {
        var index = slot.TabIndex;
        if (index <= 0 || index >= _config.Tabs.Count) return;
        var tab = _config.Tabs[index];
        var returning = BoxMover.ReturningIf(_config.Id, tab, null);
        if (_tab == index) _tab = 0;
        _edits.ForgetRemoved();
        LauncherEdits.RemovedTab? removed = null;
        Change(() => removed = LauncherEdits.TakeTab(_config, index));
        if (removed is null) return;
        // Alt halkadaki, masaüstünden taşınmış öğeler başka kutuda yoksa masaüstüne döner.
        BoxMover.Reconcile(interactive: true);
        Notice(WidgetManager.RemovedText(L.F("\"{0}\" alt halkası kaldırıldı.", slot.Name), returning, modeOff: false), () =>
        {
            var put = false;
            Change(() => put = LauncherEdits.PutTab(_config, removed));
            if (put) BoxMover.Reconcile(interactive: true);
        });
    }

    // ---------------------------------------------------------------- ekleme

    private LauncherTab Live(LauncherTab tab) => _config.Tabs.Contains(tab) ? tab : CurrentTab;

    /// <summary>"+ Uygulama": "Uygulama seç" (dosya ve klasör altındaki "Dosya ya da klasör seç…"te).</summary>
    private void AddApps()
    {
        var tab = CurrentTab;
        if (AppPicker.Ask(_near(), tab.Items, L.T("Halkaya ekle"), L.T("Halkaya ekle")) is not { } pick) return;
        AddPaths(tab, pick.Paths, InsertAt(), pick.Names);
    }

    private void AddFolders()
    {
        var tab = CurrentTab;
        if (AppPicker.AskFolders(Window.GetWindow(this), L.T("Halkaya ekle")) is not { Count: > 0 } folders) return;
        AddPaths(tab, folders, InsertAt(), null);
    }

    private void AddPaths(LauncherTab tab, IReadOnlyList<string> paths, int? at, IReadOnlyDictionary<string, string>? names)
    {
        // Ekleme arka planda biter (varlık denetimi); bitince düzenleyici tazelenir (kendi düzenlemeleri ya da panel).
        _edits.AddItems(() => Live(tab), paths, at: at, names: names,
            duplicate: () => Notice(paths.Count == 1
                ? L.F("\"{0}\" zaten halkada.", RingSlot.DefaultPathName(paths[0])) : L.T("Bu öğeler zaten halkada."), null),
            then: () => Dispatcher.BeginInvoke(Changed, DispatcherPriority.Background));
    }

    private void AddAction(string type)
    {
        var tab = CurrentTab;
        if (RingActionDialog.Create(type, _config, _anchor()) is not { } action) return;
        Insert(Live(tab), InsertAt(), action);
    }

    private void AddApp(string id)
    {
        var tab = CurrentTab;
        if (LauncherEntries.Ordered(tab).Any(e => e.Action is { Type: RingActionTypes.App } a && a.App == id))
        {
            Notice(L.F("\"{0}\" zaten halkada.", RingAppIds.Label(id) ?? id), null);
            return;
        }
        Insert(tab, InsertAt(), RingActions.ForApp(id));
    }

    private void Insert(LauncherTab tab, int? at, LauncherAction action)
    {
        Change(() => LauncherEntries.Insert(tab, at ?? int.MaxValue, LauncherEntry.Of(action)));
        // Eklenen seçili olur: hemen taşınabilsin.
        _selected = new SlotKey(null, action.Id, null);
        Refresh();
    }

    private void AddSubRing()
    {
        if (RingActionDialog.AskSubRing(null, _anchor()) is not { } name) return;
        var tab = new LauncherTab { Name = name };
        Change(() => _config.Tabs.Add(tab));
        _selected = new SlotKey(null, null, tab);
        Refresh();
    }

    private ContextMenu AppActionsMenu()
    {
        var menu = new ContextMenu();
        foreach (var id in RingAppIds.All)
            menu.Items.Add(Menus.Item(Menus.Literal(RingAppIds.Label(id) ?? id), () => AddApp(id)));
        return menu;
    }

    private ContextMenu MoreMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Menus.Item(L.T("Komut…"), () => AddAction(RingActionTypes.Command)));
        menu.Items.Add(Menus.Item(L.T("Tuşlara bas…"), () => AddAction(RingActionTypes.Keys)));
        menu.Items.Add(Menus.Item(L.T("Metin yapıştır…"), () => AddAction(RingActionTypes.Text)));
        // Alt halka tek düzey: yalnızca kök halkaya eklenir.
        if (_tab == 0)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item(L.T("Alt halka…"), AddSubRing));
        }
        return menu;
    }

    private static void ShowMenu(FrameworkElement owner, ContextMenu menu)
    {
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // ---------------------------------------------------------------- bildirim ve klavye

    private void Notice(string text, Action? undo)
    {
        if (_hostNotice is not null && undo is not null)
        {
            _hostNotice(text, undo);
            return;
        }
        _noticeText.Text = text;
        _noticeUndo = undo;
        if (_notice.Child is DockPanel row && row.Children[0] is UIElement undoLink)
            undoLink.Visibility = undo is null ? Visibility.Collapsed : Visibility.Visible;
        _notice.Visibility = Visibility.Visible;
    }

    private void HideNotice()
    {
        _noticeUndo = null;
        _notice.Visibility = Visibility.Collapsed;
    }

    /// <summary>Alt+← / Alt+→ seçileni taşır; halkada Delete kaldırır.</summary>
    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Left or Key.Right && Selected is not null)
        {
            Step(clockwise: key == Key.Right);
            e.Handled = true;
            return;
        }
        if (key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && _ring.IsKeyboardFocusWithin && Selected is not null)
        {
            Remove();
            e.Handled = true;
        }
    }

    /// <summary>Küçük düğme (seçili öğenin işleri, ekleme çubuğu): adı ve AutomationId'si var.</summary>
    private static Button Small(string label, string automationId, string name, Action click)
    {
        var button = EditorRows.ActionButton(label, automationId, click);
        button.Margin = new Thickness(0, 0, 6, 6);
        AutomationProperties.SetName(button, name);
        return button;
    }
}

/// <summary>
/// NestDesk halkasının (masaüstünde widget'ı olmayan halka) düzenleyici penceresi: 360 DIP, imlecin ya da istenen noktanın
/// ekranında ortalanır, ekrandan uzunsa içi kayar. Tek örnek; kip değildir, odak gidince kapanmaz. Esc, "Bitti" ve ×
/// kapatır. Girişte yalnızca kısa bir solma (<see cref="Motion.Enter"/>). Ayar dışarıdan değişince (kutu taşıması, ad)
/// önizleme tazelenir.
/// </summary>
internal sealed class RingEditorWindow : FluentWindow
{
    private static RingEditorWindow? _current;

    private readonly RingEditor _editor;
    private readonly Grid _root;
    private bool _closing;

    public static void ShowFor(WidgetConfig config, int tab, NativeMethods.POINT? near)
    {
        if (_current is { _closing: false } open)
        {
            if (ReferenceEquals(open._editor.Config, config))
            {
                open.Activate();
                return;
            }
            open.Close();
        }
        var window = new RingEditorWindow(config, tab, near);
        _current = window;
        window.Closed += (_, _) => { if (_current == window) _current = null; };
        window.Show();
    }

    private RingEditorWindow(WidgetConfig config, int tab, NativeMethods.POINT? near)
    {
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu küçük pencereye bağlanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        var title = string.IsNullOrWhiteSpace(config.Title) ? WidgetText.DisplayName(config) : config.Title.Trim();
        Title = title;
        Width = WidgetEditorWindow.PanelWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        MinWidth = 0;
        MinHeight = 0;
        AutomationProperties.SetAutomationId(this, "RingEditor.Window");
        WindowFit.FitChromeToContent(this);
        NativeMethods.POINT point;
        if (near is { } p) point = p;
        else NativeMethods.GetCursorPos(out point);
        MaxHeight = Math.Max(320, NativeMethods.WorkAreaAt(point).Height / NativeMethods.ScaleAt(point) - 16);
        WindowFit.CenterOn(this, point);

        _editor = new RingEditor(config, edits: null, anchor: () => null, near: () => point, tab: tab);
        var done = new Button { Content = L.T("Bitti"), Appearance = ControlAppearance.Primary, IsDefault = false, MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        AutomationProperties.SetAutomationId(done, "RingEditor.Done");
        done.Click += (_, _) => Close();
        var body = new StackPanel { Margin = new Thickness(20, 4, 20, 20) };
        body.Children.Add(_editor);
        body.Children.Add(done);
        var scroller = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition());
        _root.Children.Add(new TitleBar { Title = title, ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(scroller, 1);
        _root.Children.Add(scroller);
        Content = _root;

        Loaded += (_, _) =>
        {
            Motion.Enter(_root, MotionKind.Fade);
            Dispatcher.BeginInvoke(() => _editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)), DispatcherPriority.Input);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            Close();
        };
        Activated += (_, _) => _editor.Refresh();
        AppHost.SettingsChanged += OnOutsideChange;
        BoxMover.Changed += OnOutsideChange;
        Closing += (_, _) => _closing = true;
        Closed += (_, _) =>
        {
            AppHost.SettingsChanged -= OnOutsideChange;
            BoxMover.Changed -= OnOutsideChange;
            _editor.Release();
        };
    }

    private void OnOutsideChange()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnOutsideChange);
            return;
        }
        if (!_closing && IsLoaded) _editor.Refresh();
    }
}
