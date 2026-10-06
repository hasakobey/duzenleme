using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// Düzenle panelinin düzenlediği widget: <see cref="WidgetWindow"/> (testte sahte bir hedef). Her iş widget'a hemen uygulanır ve
/// kısa süre içinde kaydedilir (<see cref="AppHost.SaveSettings"/>); panelde "Kaydet" yoktur.
/// </summary>
internal interface IEditorTarget
{
    WidgetConfig Config { get; }

    /// <summary>Kartın görünümü (ad, simge ve "Geri al" onu yeniden kurabilir: her seferinde güncelini oku).</summary>
    IWidgetView View { get; }

    /// <summary>Görünüş değişikliği (görünüm, renk, boyut, saydamlık, köşeler, gölge): uygular ve kaydettirir.</summary>
    void Update(Action change);

    void SetCollapsed(bool collapsed);

    void SetLocked(bool locked);

    void SetAutoRollup(bool on);

    /// <summary>Ad (null: türün varsayılan adı); kartın başlığı yerinde tazelenir (yapamayan görünüm yeniden kurulur).</summary>
    void Rename(string? title);

    /// <summary>Simge seçici (kartın ve panelin yanında): seçerken <paramref name="preview"/>, kaydedince <paramref name="saved"/>.</summary>
    void PickIcon(Action<string?> preview, Action saved);

    /// <summary>Özel vurgu rengi seçici; kaydedince <paramref name="saved"/>.</summary>
    void PickAccentColor(Action saved);

    /// <summary>"Geri al": ayar panel açıldığındaki hâline döner (<see cref="ConfigSnapshot"/>).</summary>
    void Restore(ConfigSnapshot snapshot);

    /// <summary>"Bu görünümü tüm widget'lara uygula"; dönen liste bildirimdeki "Geri al" içindir.</summary>
    IReadOnlyList<(string Id, LookFields Before)> ApplyLookToAll();

    void UndoLookToAll(IReadOnlyList<(string Id, LookFields Before)> before);

    void Duplicate();

    /// <summary>Kaldırır ("Geri al" bildirimiyle; <see cref="WidgetManager.RemoveWithUndo"/>).</summary>
    void Remove();

    /// <summary>Kartın ekrandaki dikdörtgeni (fiziksel piksel); görünmüyorsa null.</summary>
    Box? CardBox { get; }

    /// <summary>Kartın ortası (fiziksel piksel): panel ve açtığı pencereler bu monitörde açılır.</summary>
    NativeMethods.POINT CenterPoint { get; }
}

/// <summary>
/// "Düzenle" paneli (Kolay NestDesk 2. dalga): bir widget'ın bütün ayarları tek yerde, sabit sırayla. Üstte simge, ad ve türü;
/// sonra widget'ın kendi ayarları (görünümün kendi denetimleri, <see cref="IWidgetView.AddEditorContent"/>; yoksa menü öğeleri,
/// <see cref="MenuRows"/>), "Görünüm" (galeri, renk, boyut),
/// "Göster" (parçalar) ve kapalı "Daha fazla" (saydamlık, köşeler, gölge, kilit…); altta "Geri al" · "Kaldır" · "Bitti".
/// Her denetim widget'a hemen uygulanır (widget'ın kendisi önizlemedir; fareyle önizleme yok), "Geri al" widget'ı panel
/// açıldığındaki hâline döndürür (Ctrl+Z de). Kendi penceresinde (<see cref="WidgetEditorWindow"/>) ya da bir sayfanın içinde
/// durabilir: pencere yönetimi yok, hedef <see cref="Edit"/> ile verilir ve değiştirilebilir.
/// </summary>
internal sealed class WidgetEditor : UserControl
{
    private readonly ScrollViewer _scroller;
    private readonly StackPanel _body;
    private readonly Border _notice;
    private readonly TextBlock _noticeText;
    private readonly Button _undo;
    private readonly Button _done;
    private readonly List<Action> _refreshers = [];

    private IEditorTarget? _target;
    private IWidgetView? _builtView;
    private ConfigSnapshot? _snapshot;
    private TextBox? _name;
    private SymbolIcon? _icon;
    private bool _moreOpen;
    private bool _rebuildQueued;
    private bool _rebuildAfterRelease;
    private bool _syncing;
    private Action? _noticeUndo;

    /// <summary>"Bitti" (ya da Enter): barındıran kapatır.</summary>
    public event Action? DoneRequested;

    public WidgetEditor()
    {
        AutomationProperties.SetName(this, L.T("Düzenle"));

        _body = new StackPanel { Margin = new Thickness(20, 4, 20, 12) };
        _scroller = new ScrollViewer
        {
            Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
        };

        // Bildirim satırı (ör. "Görünüm bütün widget'lara uygulandı." + "Geri al"): panelin içinde, alt düğmelerin üstünde.
        _noticeText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        _noticeText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var noticeAction = new Button { Content = L.T("Geri al"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(noticeAction, "Editor.Notice.Undo");
        noticeAction.Click += (_, _) =>
        {
            var undo = _noticeUndo;
            HideNotice();
            undo?.Invoke();
            // Geri alınan iş panelin yapısını da değiştirmiş olabilir (bölmenin kaynağı): yeniden kurulur.
            QueueRebuild();
        };
        var noticeRow = new DockPanel();
        DockPanel.SetDock(noticeAction, Dock.Right);
        noticeRow.Children.Add(noticeAction);
        noticeRow.Children.Add(_noticeText);
        _notice = new Border
        {
            Child = noticeRow, Padding = new Thickness(12, 8, 8, 8), Margin = new Thickness(20, 0, 20, 8), CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed,
        };
        _notice.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        _notice.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        AutomationProperties.SetAutomationId(_notice, "Editor.Notice");
        AutomationProperties.SetLiveSetting(_noticeText, AutomationLiveSetting.Polite);

        // Alt satır: Geri al · Kaldır (solda) · Bitti (sağda, birincil).
        _undo = new Button { Content = L.T("Geri al"), Appearance = ControlAppearance.Transparent, IsEnabled = false };
        AutomationProperties.SetAutomationId(_undo, "Editor.Undo");
        AutomationProperties.SetHelpText(_undo, L.T("Widget'ı panel açıldığındaki hâline döndürür"));
        _undo.Click += (_, _) => Undo();
        var removeText = new TextBlock { Text = L.T("Kaldır") };
        removeText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        var remove = new Button { Content = removeText, Appearance = ControlAppearance.Transparent, Margin = new Thickness(4, 0, 0, 0) };
        AutomationProperties.SetAutomationId(remove, "Editor.Remove");
        AutomationProperties.SetName(remove, L.T("Kaldır"));
        remove.Click += (_, _) => _target?.Remove();
        _done = new Button { Content = L.T("Bitti"), Appearance = ControlAppearance.Primary, MinWidth = 90 };
        AutomationProperties.SetAutomationId(_done, "Editor.Done");
        _done.Click += (_, _) => Done();
        var footer = new DockPanel { Margin = new Thickness(16, 4, 20, 18), LastChildFill = false };
        DockPanel.SetDock(_done, Dock.Right);
        footer.Children.Add(_undo);
        footer.Children.Add(remove);
        footer.Children.Add(_done);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(_scroller);
        Grid.SetRow(_notice, 1);
        root.Children.Add(_notice);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;

        PreviewKeyDown += OnKey;
        // Fare panelde basılıyken ertelenen yeniden kurulum bırakınca (tıklama işlendikten sonra) yapılır (QueueRebuild).
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, _) =>
        {
            if (!_rebuildAfterRelease || IsMouseCaptureWithin) return;
            _rebuildAfterRelease = false;
            QueueRebuild();
        }), handledEventsToo: true);
    }

    /// <summary>Düzenlenen widget (yoksa null).</summary>
    public IEditorTarget? Target => _target;

    /// <summary>"Bitti" Enter'la basılsın mı (kendi penceresinde evet; bir sayfanın içinde barındıran seçer).</summary>
    public bool DoneIsDefault
    {
        get => _done.IsDefault;
        set => _done.IsDefault = value;
    }

    /// <summary>
    /// "Bitti" görünsün mü (kendi penceresinde evet; Widget'lar sayfasına gömülü panelde kapatılacak bir şey yok, gizlenir).
    /// </summary>
    public bool ShowDone
    {
        get => _done.Visibility == Visibility.Visible;
        set => _done.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Bu widget'ı düzenlemeye başlar (başka bir widget düzenleniyorsa ona geçer): "Geri al"ın döneceği hâl şimdi kopyalanır.
    /// Önceki widget'ın yazılmakta olan adı kaydedilir.
    /// </summary>
    public void Edit(IEditorTarget target)
    {
        CommitName();
        _target = target;
        _snapshot = ConfigSnapshot.Take(target.Config);
        _moreOpen = false;
        HideNotice();
        Build();
        _scroller.ScrollToTop();
    }

    /// <summary>Panel kapanıyor: yazılmakta olan ad kaydedilir, widget'la bağ kopar.</summary>
    public void Release()
    {
        CommitName();
        _target = null;
        _builtView = null;
        _snapshot = null;
        _rebuildAfterRelease = false;
        _refreshers.Clear();
        _body.Children.Clear();
        _name = null;
        _icon = null;
    }

    /// <summary>Yazılmakta olan adı kaydeder (panel kapanırken, Bitti'de).</summary>
    public void CommitPending() => CommitName();

    /// <summary>Panel açıldığından beri değişen bir şey var mı ("Geri al" etkin mi)?</summary>
    public bool CanUndo => _target is { } target && _snapshot is { } snapshot && snapshot.Differs(target.Config);

    /// <summary>"Geri al" / Ctrl+Z: widget panel açıldığındaki hâline döner (yer, boyut ve notun metni hariç).</summary>
    public void Undo()
    {
        if (_target is not { } target || _snapshot is not { } snapshot || !CanUndo) return;
        _name = null; // yazılan ad da geri alınır (kaydedilmez)
        target.Restore(snapshot);
        Rebuild();
    }

    /// <summary>Satırları modelden tazeler (bir şey değişti, ya da panel yeniden etkinleşti).</summary>
    public void Refresh()
    {
        if (_target is null) return;
        // Kartın görünümü yeniden kuruldu (ad, simge, "Geri al"): satırlar eski görünümün öğelerine bağlıydı.
        if (!ReferenceEquals(_target.View, _builtView))
        {
            QueueRebuild();
            return;
        }
        _syncing = true;
        try
        {
            foreach (var refresh in _refreshers.ToList()) refresh();
        }
        finally { _syncing = false; }
        _undo.IsEnabled = CanUndo;
    }

    /// <summary>Paneli baştan kurar (görünümün menüsü değişti): kaydırma yeri ve odaktaki denetim korunur.</summary>
    public void Rebuild()
    {
        if (_target is null) return;
        var focusedId = Keyboard.FocusedElement is Visual focused && IsAncestorOf(focused) ? AutomationProperties.GetAutomationId(focused) : null;
        var offset = _scroller.VerticalOffset;
        Build();
        Dispatcher.BeginInvoke(() =>
        {
            _scroller.ScrollToVerticalOffset(offset);
            if (!string.IsNullOrEmpty(focusedId) && Find(_body, focusedId) is UIElement again) again.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void QueueRebuild()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        // Komut pencere açtıysa (ad, süre…) kapandıktan sonra; menünün yapısı değişmiş olabilir. Fare paneldeki bir denetime
        // basılıyken (ör. ad kutusu odağı bırakıp kaydedildi, kart yeniden kuruldu) beklenir: basılan düğme kaldırılırsa
        // tıklama kaybolurdu. Bırakılınca kurulur.
        Dispatcher.BeginInvoke(() =>
        {
            _rebuildQueued = false;
            if (_target is null) return;
            if (IsMouseCaptureWithin)
            {
                _rebuildAfterRelease = true;
                return;
            }
            _rebuildAfterRelease = false;
            Rebuild();
        }, DispatcherPriority.Background);
    }

    /// <summary>Bir satır bir şey değiştirdi; <paramref name="structural"/>: menünün yapısı değişebilir (yeniden kur).</summary>
    private void Changed(bool structural)
    {
        if (_syncing) return;
        if (structural) QueueRebuild();
        else Refresh();
    }

    private void Done()
    {
        CommitName();
        DoneRequested?.Invoke();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        // Ctrl+Z: yazı kutusunda yazının geri alması kalır; başka her yerde paneldeki "Geri al".
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBoxBase)
        {
            Undo();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ kurulum

    private void Build()
    {
        // Ad kutusu kaldırılırken odağı kaybedip eski adı kaydetmeye kalkmasın (yazılan ad önceden kaydedildi).
        _name = null;
        _icon = null;
        _refreshers.Clear();
        _body.Children.Clear();
        if (_target is not { } target) return;

        var menu = new WidgetMenu { ForEditor = true };
        target.View.AddMenuItems(menu);
        _builtView = target.View;

        _body.Children.Add(Header(target));

        // Widget'ın kendi ayarları: görünümün kendi denetimleri (bölme, kutu, not, zamanlayıcı, geri sayım) ya da menü öğeleri
        // (menünün üst bölümündeki seçenekler; işleri menüde kalır) ve Settings'teki öbür öğeler.
        var content = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (!target.View.AddEditorContent(new EditorContent(content, _refreshers, Changed, ShowNotice)))
        {
            MenuRows.Add(content, menu.Primary, optionsOnly: true, "Editor.Option.P", _refreshers, Changed);
            MenuRows.Add(content, menu.Settings, optionsOnly: false, "Editor.Option.S", _refreshers, Changed);
        }
        if (content.Children.Count > 0) _body.Children.Add(content);

        AddLook(target);

        var parts = MenuRows.PartsIn([.. menu.Primary, .. menu.Settings, .. menu.More]).ToList();
        var shown = parts.Where(p => !IsChromePart(p.Key)).ToList();
        if (shown.Count > 0)
        {
            _body.Children.Add(EditorRows.Section(L.T("Göster")));
            foreach (var (option, key) in shown)
                _body.Children.Add(MenuRows.SwitchRow(option, "Editor.Part." + key, _refreshers, Changed));
        }

        AddMore(target, parts.Where(p => IsChromePart(p.Key)).ToList(), menu.More);

        _undo.IsEnabled = CanUndo;
    }

    /// <summary>Kartın kendi düğmeleri (× ve ⋯): "Göster"de değil, "Daha fazla"da.</summary>
    private static bool IsChromePart(string key) => key is "close" or "edit";

    private FrameworkElement Header(IEditorTarget target)
    {
        var config = target.Config;
        _icon = new SymbolIcon { Symbol = WidgetIcons.For(config), Filled = true, FontSize = 22 };
        var icon = new Button
        {
            Content = _icon, Width = 40, Height = 40, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(icon, "Editor.Icon");
        AutomationProperties.SetName(icon, L.T("Simgeyi değiştir…"));
        var shownIcon = _icon;
        icon.Click += (_, _) => target.PickIcon(
            preview: chosen => shownIcon.Symbol = WidgetIcons.Symbol(chosen) ?? WidgetIcons.DefaultFor(config),
            saved: Refresh);

        var column = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        if (HasName(config))
        {
            var original = config.Title ?? "";
            var name = new TextBox { Text = original, PlaceholderText = L.T("Ad"), MaxLength = TitleEditor.MaxLength };
            AutomationProperties.SetAutomationId(name, "Editor.Name");
            AutomationProperties.SetName(name, L.T("Ad"));
            // Enter kaydeder (panel açık kalır), Esc yazılanı geri alır (değişiklik yoksa paneli kapatır), odak gidince kaydedilir.
            name.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitName();
                    name.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape && !string.Equals(name.Text, _target?.Config.Title ?? "", StringComparison.Ordinal))
                {
                    name.Text = _target?.Config.Title ?? "";
                    name.SelectAll();
                    e.Handled = true;
                }
            };
            name.LostKeyboardFocus += (_, _) => CommitName();
            _name = name;
            column.Children.Add(name);
        }
        var kind = EditorRows.Muted(KindName(config), top: _name is null ? 0 : 4);
        column.Children.Add(kind);
        _refreshers.Add(() =>
        {
            kind.Text = KindName(config);
            if (!ReferenceEquals(shownIcon, _icon)) return;
            shownIcon.Symbol = WidgetIcons.For(config);
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(icon);
        Grid.SetColumn(column, 1);
        row.Children.Add(column);
        return row;
    }

    /// <summary>
    /// Yazılan ad değiştiyse kaydeder (boş: türün varsayılan adı). Kartın başlığı yerinde tazelenir; panel yalnızca kartın görünümü
    /// yeniden kurulduysa yeniden kurulur (<see cref="Refresh"/>): ad kutusu odağı panelde bir denetime basılırken bırakır, basılan
    /// denetim yerinde kalmalı.
    /// </summary>
    private void CommitName()
    {
        if (_name is not { } name || _target is not { } target) return;
        var text = name.Text.Trim();
        var title = text.Length == 0 ? null : text;
        if (string.Equals(title, target.Config.Title, StringComparison.Ordinal)) return;
        target.Rename(title);
        Refresh();
    }

    /// <summary>Adı olan widget: saat ve tarihin başlığı yoktur.</summary>
    internal static bool HasName(WidgetConfig c) => WidgetVariants.Of(c) is not null || c.Kind is not (WidgetKind.Clock or WidgetKind.Date);

    /// <summary>Başlığın altındaki gri satır: widget'ın türü.</summary>
    internal static string KindName(WidgetConfig c) => WidgetVariants.Of(c) switch
    {
        WidgetVariants.Month => L.T("Takvim"),
        WidgetVariants.Countdown => L.T("Geri sayım"),
        WidgetVariants.Timer => TimerModes.Normalize(c.Timer?.Mode) switch
        {
            TimerModes.Pomodoro => L.T("Pomodoro"),
            TimerModes.Stopwatch => L.T("Kronometre"),
            _ => L.T("Zamanlayıcı"),
        },
        WidgetVariants.World => L.T("Dünya saati"),
        WidgetVariants.System => L.T("Sistem durumu"),
        WidgetVariants.Recycle => L.T("Geri Dönüşüm Kutusu"),
        WidgetVariants.Ring => L.T("Halka menü"),
        _ => c.Kind switch
        {
            WidgetKind.Clock => L.T("Saat"),
            WidgetKind.Date => L.T("Tarih"),
            WidgetKind.Note => c.NoteChecklist ? L.T("Yapılacaklar") : L.T("Not"),
            WidgetKind.Launcher => L.T("Kısayol kutusu"),
            _ => c.Filter == DesktopFilter.None ? L.T("Klasör bölmesi") : L.T("Bölme"),
        },
    };

    // ------------------------------------------------------------------ Görünüm

    private void AddLook(IEditorTarget target)
    {
        var c = target.Config;
        _body.Children.Add(EditorRows.Section(L.T("Görünüm")));
        var themed = target.View.UsesThemeColors;
        if (themed && SystemParameters.HighContrast)
            _body.Children.Add(EditorRows.Muted(L.T("Yüksek karşıtlık açık: widget'lar Windows'un renklerini kullanır.")));
        else if (themed)
        {
            var gallery = new LookGalleryControl("Editor.Look.",
                id => WidgetPalette.Resolve(LookGallery.Probe(c, id, WidgetPalette.SystemDark), null, WidgetPalette.SystemDark, highContrast: false),
                () => LookGallery.Current(c),
                id =>
                {
                    if (_syncing) return;
                    target.Update(() => LookGallery.Apply(c, id, WidgetPalette.SystemDark));
                    Refresh();
                });
            _body.Children.Add(gallery);
            _refreshers.Add(gallery.Refresh);
            _body.Children.Add(AccentRow(target));
        }

        // Boyut: Küçük · Orta · Büyük; kayıtlı başka bir ölçek seçili "Özel (%150)" olarak görünür (kaybolmaz).
        var custom = SizePresets.CustomLabel(c.Scale);
        var sizes = SizePresets.All.Select(p => new EditorRows.Segment(p.Id, p.Label, "Editor.Size." + p.Id)).ToList();
        if (custom is not null) sizes.Add(new EditorRows.Segment("Custom", custom, "Editor.Size.Custom", SelectsOnArrow: false));
        _body.Children.Add(EditorRows.Segments(() => L.T("Boyut"), sizes, () => SizePresets.Match(c.Scale)?.Id ?? "Custom", id =>
        {
            if (_syncing || SizePresets.All.FirstOrDefault(p => p.Id == id) is not { } preset) return;
            target.Update(() => c.Scale = preset.Scale);
            Changed(structural: custom is not null); // "Özel" seçeneği kalkar
        }, _refreshers));
        // Ölçek başka yerden (Ctrl + tekerlek) hazır olmayan bir değere geçtiyse ya da ondan döndüyse "Özel" seçeneği kurulur/kalkar.
        _refreshers.Add(() =>
        {
            if (!string.Equals(SizePresets.CustomLabel(c.Scale), custom, StringComparison.Ordinal)) QueueRebuild();
        });

        if (themed && !SystemParameters.HighContrast)
            _body.Children.Add(EditorRows.Link(L.T("Bu görünümü tüm widget'lara uygula"), "Editor.ApplyAll", () =>
            {
                var before = target.ApplyLookToAll();
                if (before.Count == 0) return;
                ShowNotice(L.T("Görünüm bütün widget'lara uygulandı."), () => target.UndoLookToAll(before));
            }));
    }

    /// <summary>"Renk": beş adlı renk (gerçek paletle, seçili görünümün zemininde okunur tonuyla) ve "Özel…" (renk seçici).</summary>
    private FrameworkElement AccentRow(IEditorTarget target)
    {
        var c = target.Config;
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        var title = new TextBlock { Text = L.T("Renk"), Margin = new Thickness(0, 0, 0, 6) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(title);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var dots = new StackPanel { Orientation = Orientation.Horizontal };
        KeyboardNavigation.SetTabNavigation(dots, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(dots, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(dots, L.T("Renk"));
        var group = "EditorAccent" + Guid.NewGuid().ToString("N");
        (WidgetAccent Accent, string Label)[] named =
        [
            (WidgetAccent.Violet, L.T("Mor")), (WidgetAccent.Blue, L.T("Mavi")), (WidgetAccent.Green, L.T("Yeşil")),
            (WidgetAccent.Orange, L.T("Turuncu")), (WidgetAccent.Pink, L.T("Pembe")),
        ];
        var faces = new List<(WidgetAccent Accent, RadioButton Button, Ellipse Fill, Ellipse Ring)>();
        var syncing = false; // işaretler modelden konurken seçim uygulanmaz
        for (var i = 0; i < named.Length; i++)
        {
            var (accent, label) = named[i];
            var fill = new Ellipse { Width = 22, Height = 22 };
            var ring = new Ellipse { Width = 28, Height = 28, StrokeThickness = 2 };
            var button = new RadioButton
            {
                Content = new Grid { Width = 28, Height = 28, Children = { ring, fill } }, GroupName = group, Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Template = new ControlTemplate(typeof(RadioButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            };
            AutomationProperties.SetAutomationId(button, "Editor.Accent." + (i + 1));
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, L.T("Renk"));
            button.Checked += (_, _) =>
            {
                if (_syncing || syncing) return;
                target.Update(() => WidgetLooks.SetAccent(c, accent));
                Refresh();
            };
            button.GotKeyboardFocus += (_, e) =>
            {
                var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
                if (arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true) button.IsChecked = true;
            };
            faces.Add((accent, button, fill, ring));
            dots.Children.Add(button);
        }
        row.Children.Add(dots);

        var customDot = new Ellipse { Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var customText = new TextBlock { Text = L.T("Özel…"), VerticalAlignment = VerticalAlignment.Center };
        var customButton = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { customDot, customText } },
            Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(2),
        };
        AutomationProperties.SetAutomationId(customButton, "Editor.Accent.Custom");
        AutomationProperties.SetName(customButton, L.T("Özel renk…"));
        customButton.Click += (_, _) => target.PickAccentColor(Refresh);
        row.Children.Add(customButton);
        panel.Children.Add(row);

        void Sync()
        {
            var selection = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
            var shown = WidgetLooks.ShownAccent(c);
            syncing = true;
            try
            {
                foreach (var (accent, button, fill, ring) in faces)
                {
                    fill.Fill = WidgetPalette.Resolve(LookGallery.ProbeAccent(c, accent), null, WidgetPalette.SystemDark, highContrast: false).Accent;
                    var on = shown == accent;
                    ring.Stroke = on ? selection : Brushes.Transparent;
                    button.IsChecked = on;
                }
            }
            finally { syncing = false; }
            var chosen = WidgetLooks.CustomAccentChosen(c);
            customDot.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
            if (chosen) customDot.Fill = WidgetPalette.Resolve(c, null, WidgetPalette.SystemDark, highContrast: false).Accent;
            if (chosen) customButton.BorderBrush = selection;
            else customButton.ClearValue(Control.BorderBrushProperty);
            AutomationProperties.SetItemStatus(customButton, chosen ? L.T("Seçili") : "");
        }
        Sync();
        _refreshers.Add(Sync);
        return panel;
    }

    // ------------------------------------------------------------------ Daha fazla

    private void AddMore(IEditorTarget target, List<(OptionMenuItem Option, string Key)> chromeParts, List<object> more)
    {
        var c = target.Config;
        var chevron = new SymbolIcon { Symbol = _moreOpen ? SymbolRegular.ChevronUp24 : SymbolRegular.ChevronDown24, FontSize = 14, Margin = new Thickness(8, 0, 0, 0) };
        var heading = new TextBlock { Text = L.T("Daha fazla"), FontSize = 14, FontWeight = FontWeights.SemiBold };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var toggle = new FoldButton
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { heading, chevron } },
            Appearance = ControlAppearance.Transparent, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(-10, 16, 0, 0), Padding = new Thickness(10, 6, 10, 6),
        };
        AutomationProperties.SetAutomationId(toggle, "Editor.More");
        AutomationProperties.SetName(toggle, L.T("Daha fazla"));
        var body = new StackPanel { Visibility = _moreOpen ? Visibility.Visible : Visibility.Collapsed };
        void Show(bool open)
        {
            _moreOpen = open;
            body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            chevron.Symbol = open ? SymbolRegular.ChevronUp24 : SymbolRegular.ChevronDown24;
            toggle.IsExpanded = open;
        }
        toggle.Click += (_, _) => Show(!_moreOpen);
        Show(_moreOpen);
        _body.Children.Add(toggle);
        _body.Children.Add(body);

        body.Children.Add(EditorRows.Muted(L.T("Çoğu zaman varsayılanlar yeterli."), top: 2));

        (double Value, string Label, string Id)[] opacities =
            [(1.0, L.T("Yok"), "None"), (0.85, L.Percent(0.15), "15"), (0.7, L.Percent(0.3), "30"), (0.55, L.Percent(0.45), "45")];
        body.Children.Add(EditorRows.Segments(() => L.T("Saydamlık"),
            opacities.Select(o => new EditorRows.Segment(o.Id, o.Label, "Editor.Opacity." + o.Id)).ToList(),
            () => opacities.FirstOrDefault(o => Math.Abs(o.Value - c.Opacity) < 0.005).Id,
            id =>
            {
                if (_syncing) return;
                var value = opacities.First(o => o.Id == id).Value;
                target.Update(() => c.Opacity = value);
                Refresh();
            }, _refreshers));

        (CornerStyle Value, string Label)[] corners =
            [(CornerStyle.Round, L.T("Yuvarlak")), (CornerStyle.Soft, L.T("Hafif")), (CornerStyle.Square, L.T("Köşeli"))];
        body.Children.Add(EditorRows.Segments(() => L.T("Köşeler"),
            corners.Select(o => new EditorRows.Segment(o.Value.ToString(), o.Label, "Editor.Corners." + o.Value)).ToList(),
            () => c.Corners.ToString(),
            id =>
            {
                if (_syncing || !Enum.TryParse<CornerStyle>(id, out var value)) return;
                target.Update(() => c.Corners = value);
                Refresh();
            }, _refreshers));

        body.Children.Add(EditorRows.Switch(() => L.T("Gölge"), "Editor.Shadow", () => c.Shadow, on =>
        {
            target.Update(() => c.Shadow = on);
            Refresh();
        }, _refreshers));
        body.Children.Add(EditorRows.Switch(() => L.T("Konumu kilitle"), "Editor.Lock", () => c.Locked, on =>
        {
            target.SetLocked(on);
            Refresh();
        }, _refreshers));
        var collapse = EditorRows.Switch(() => L.T("Başlığa katla"), "Editor.Collapse", () => c.Collapsed, on =>
        {
            target.SetCollapsed(on);
            Refresh();
        }, _refreshers);
        body.Children.Add(collapse);
        void FollowCollapsible() => collapse.Visibility = target.View.Collapsible ? Visibility.Visible : Visibility.Collapsed;
        FollowCollapsible();
        _refreshers.Add(FollowCollapsible);

        // Kartın kendi düğmeleri: "Kaldır düğmesi (×)", "Düzenle düğmesi (⋯)".
        foreach (var (option, key) in chromeParts)
            body.Children.Add(MenuRows.SwitchRow(option, "Editor.Part." + key, _refreshers, Changed));

        // Fareye bağlı seçenekler yalnızca açıkken (kapatılabilsin diye); panel açıkken kapatılırsa yine geri açılabilir.
        if (c.FadeUntilHover)
            body.Children.Add(EditorRows.Switch(() => L.T("Fare üstünde değilken soluk dursun"), "Editor.FadeUntilHover", () => c.FadeUntilHover, on =>
            {
                target.Update(() => c.FadeUntilHover = on);
                Refresh();
            }, _refreshers));
        if (c.AutoRollup && target.View.Collapsible)
            body.Children.Add(EditorRows.Switch(() => L.T("Fare üstünde değilken başlığa katla"), "Editor.AutoRollup", () => c.AutoRollup, on =>
            {
                target.SetAutoRollup(on);
                Refresh();
            }, _refreshers));

        // Widget'ın seyrek kullanılanları (Panoya kopyala, simge seçenekleri, Klasör simgesi…, Yenile).
        MenuRows.Add(body, more, optionsOnly: false, "Editor.Option.M", _refreshers, Changed);

        body.Children.Add(EditorRows.ActionButton(L.T("Çoğalt"), "Editor.Duplicate", target.Duplicate));
    }

    // ------------------------------------------------------------------ bildirim

    /// <summary>Panelin içindeki bildirim satırı: metin ve "Geri al" (basılınca satır kalkar; panel yeniden kurulur).</summary>
    private void ShowNotice(string text, Action undo)
    {
        _noticeText.Text = text;
        _noticeUndo = undo;
        _notice.Visibility = Visibility.Visible;
    }

    private void HideNotice()
    {
        _noticeUndo = null;
        _notice.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// "Daha fazla" düğmesi: ekran okuyucu bölümün açık mı kapalı mı olduğunu duyar ve açıp kapatabilir (ExpandCollapse; düz düğme
    /// bunu söylemez). Türetilmiş denetim örtük stili almaz: WPF-UI düğmesinin stili verilir.
    /// </summary>
    private sealed class FoldButton : Button
    {
        private bool _expanded;

        public FoldButton() => SetResourceReference(StyleProperty, typeof(Button));

        public bool IsExpanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                (UIElementAutomationPeer.FromElement(this) as Peer)?.RaiseStateChanged(!value, value);
            }
        }

        private void Toggle() => OnClick();

        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private sealed class Peer(FoldButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
        {
            public override object GetPattern(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

            public ExpandCollapseState ExpandCollapseState => State(owner.IsExpanded);

            public void Expand()
            {
                if (!owner.IsEnabled) throw new ElementNotEnabledException();
                if (!owner.IsExpanded) owner.Toggle();
            }

            public void Collapse()
            {
                if (!owner.IsEnabled) throw new ElementNotEnabledException();
                if (owner.IsExpanded) owner.Toggle();
            }

            internal void RaiseStateChanged(bool was, bool now) =>
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, State(was), State(now));

            private static ExpandCollapseState State(bool expanded) => expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        }
    }

    /// <summary>Görsel ağaçta AutomationId'si verilen ilk öğe (yeniden kurulduktan sonra odağı geri vermek için).</summary>
    private static DependencyObject? Find(DependencyObject root, string automationId)
    {
        if (AutomationProperties.GetAutomationId(root) == automationId) return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (Find(child, automationId) is { } found) return found;
        return null;
    }
}
