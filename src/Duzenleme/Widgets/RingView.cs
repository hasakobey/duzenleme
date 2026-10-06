using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Halkanın ölçüleri (DIP; saf). Öğe dairesinin çapı simgeden 18 DIP büyüktür; öğe merkezlerinin yarıçapı
/// <see cref="RingGeometry.Radius"/> ile (en az öğe çapının 1,6 katı), orta daire öğelerin içinde, dış disk öğelerin dışında
/// kalır. <see cref="Places"/>: halkadaki yer sayısı (alt halkada "geri" yeri de sayılır).
/// </summary>
internal readonly record struct RingMetrics(int Places, double IconDip, double ItemDip, double Radius, double CenterRadius, double OuterRadius)
{
    /// <summary>Öğe dairesi ile orta daire ve dış kenar arasındaki boşluk.</summary>
    public const double Gap = 10;

    /// <summary>Orta dairenin en küçük yarıçapı (yazı sığsın).</summary>
    public const double MinCenterRadius = 34;

    public double Size => OuterRadius * 2;

    /// <summary>Orta (ölü) bölge: bunun içi hiçbir öğeyi seçmez.</summary>
    public double DeadZone => CenterRadius;

    /// <param name="style">Biçim (yoksa Klasik). Göstergeli biçimde (Daire, Etiketli) orta daire ile öğeler arasında gösterge
    /// çizgisine <see cref="PointerRoom"/> kadar yer kalır. Etiketli'de öğeler yan yana duran etiketleri kadar aralıklı, etiketler
    /// dışa bakar (<see cref="RingStyles.LabelAbove"/>), dış disk bütün etiketleri kapsar; orta daire en çok
    /// <see cref="MaxLabeledCenter"/> (büyük halkada ortası boşa büyümesin).</param>
    public static RingMetrics For(int places, double iconDip, RingStyle? style = null)
    {
        var item = iconDip + 18;
        var room = style?.Pointer == true ? PointerRoom : 0;
        if (style?.Labels == true)
        {
            var hang = item / 2 + Gap + room;
            // Orta daire etiketlerin de içinde kalır (yanlardaki öğelerin etiketi ortaya doğru uzanır).
            var (r, inner, reach) = RingStyles.LabeledLayout(places, item, Math.Max(item * 1.6, MinCenterRadius + hang));
            for (var i = 0; inner - LabelRoom < MinCenterRadius && i < 100; i++)
                (r, inner, reach) = RingStyles.LabeledLayout(places, item, r + 2);
            var c = Math.Clamp(Math.Min(r - hang, inner - LabelRoom), MinCenterRadius, MaxLabeledCenter);
            return new RingMetrics(places, iconDip, item, r, c, Math.Max(r + item / 2 + Gap, reach + 6));
        }
        var radius = RingGeometry.Radius(places, item, item * 1.6);
        var center = Math.Max(MinCenterRadius, radius - item / 2 - Gap - room);
        radius = Math.Max(radius, center + item / 2 + Gap + room);
        return new RingMetrics(places, iconDip, item, radius, center, radius + item / 2 + Gap);
    }

    /// <summary>Göstergeli biçimde orta daire ile öğe daireleri arasına eklenen yer (DIP).</summary>
    public const double PointerRoom = 12;

    /// <summary>Etiketli halkada orta daire ile en yakın etiket arasındaki en az boşluk (DIP).</summary>
    public const double LabelRoom = 6;

    /// <summary>Etiketli halkada orta dairenin en büyük yarıçapı (DIP).</summary>
    public const double MaxLabeledCenter = 52;
}

/// <summary>Halkada bir tuşun anlamı (kalıcı değil).</summary>
internal enum RingKey { None, Number, Arrow, Enter, Back, Escape }

/// <summary>Halkanın fare ve klavye eşlemesi (saf; testlenir).</summary>
internal static class RingInput
{
    /// <summary>
    /// Tuşun anlamı: 1–9 (üst sıra ve sayısal tuş takımı), oklar, Enter/Boşluk (seç), Backspace (geri), Esc. Ctrl, Alt, Win
    /// ya da Shift ile basılan tuş halkanın değildir.
    /// </summary>
    public static (RingKey Kind, int Number, RingGeometry.Arrow Arrow) Map(Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.None) return (RingKey.None, 0, default);
        return key switch
        {
            >= Key.D1 and <= Key.D9 => (RingKey.Number, key - Key.D0, default),
            >= Key.NumPad1 and <= Key.NumPad9 => (RingKey.Number, key - Key.NumPad0, default),
            Key.Left => (RingKey.Arrow, 0, RingGeometry.Arrow.Left),
            Key.Right => (RingKey.Arrow, 0, RingGeometry.Arrow.Right),
            Key.Up => (RingKey.Arrow, 0, RingGeometry.Arrow.Up),
            Key.Down => (RingKey.Arrow, 0, RingGeometry.Arrow.Down),
            Key.Enter or Key.Space => (RingKey.Enter, 0, default),
            Key.Back => (RingKey.Back, 0, default),
            Key.Escape => (RingKey.Escape, 0, default),
            _ => (RingKey.None, 0, default),
        };
    }

    /// <summary>1–9 numaralı öğenin sırası; o numarada öğe yoksa null.</summary>
    public static int? NumberIndex(int number, int count) => number is >= 1 and <= 9 && number <= count ? number - 1 : null;

    /// <summary>
    /// Merkeze göre (<paramref name="dx"/>, <paramref name="dy"/>) noktadaki yer: orta daire Center; <paramref name="limitToDisc"/>
    /// ise (masaüstündeki widget) dış diskin dışı None, açılır halkada açı yeter.
    /// </summary>
    public static RingGeometry.Hit HitAt(RingMetrics metrics, RingGeometry.WedgeLayout wedges, double dx, double dy, bool limitToDisc) =>
        RingGeometry.HitTest(dx, dy, wedges, metrics.DeadZone, limitToDisc ? metrics.OuterRadius : 0);

    /// <summary>Bırakılan öğenin sekmedeki sırası: alt halkaların arasına bırakılan da öğelerin sonuna eklenir.</summary>
    public static int DropIndex(IReadOnlyList<double> angles, int entryCount, double dx, double dy) =>
        Math.Clamp(RingGeometry.DropIndex(angles, RingGeometry.AngleOf(dx, dy)), 0, Math.Max(0, entryCount));

    /// <summary>
    /// "Saat yönünde / ters yönde taşı": <see cref="LauncherEntries.Move"/>'un hedef sırası (öğe çıkarıldıktan sonraki listede).
    /// Halka döner: sondaki saat yönünde başa, baştaki ters yönde sona geçer. Tek öğede null.
    /// </summary>
    public static int? StepTarget(int index, int count, bool clockwise)
    {
        if (count < 2 || index < 0 || index >= count) return null;
        return clockwise ? (index < count - 1 ? index + 1 : 0) : (index > 0 ? index - 1 : count - 1);
    }

    /// <summary>
    /// Basılı öğe (<paramref name="dx"/>, <paramref name="dy"/>) kadar gidince dışarı sürüklenmeye başlar mı: yalnızca
    /// masaüstündeki widget'ta, dışarı sürüklenebilen öğede (yol) ve Windows'un sürükleme eşiğini geçince. Sürüklenemeyen öğede
    /// (eylem, alt halka, soluk ya da kabuk nesnesi) basış sürer: biraz kayan tıklama (dokunmatik yüzey, kalem) yine seçer.
    /// </summary>
    public static bool StartsDragOut(bool isPopup, bool canDragOut, double dx, double dy, double minX, double minY) =>
        !isPopup && canDragOut && (Math.Abs(dx) >= minX || Math.Abs(dy) >= minY);

    /// <summary>
    /// Bırakınca seçilen yer: basılan yerin üstünde bırakılırsa o; açılır halkada basıp başka bir öğeye götürüp bırakmak o öğe
    /// (pasta menülerindeki gibi). Yoksa hiçbiri (null).
    /// </summary>
    public static RingGeometry.Hit? ReleaseTarget(RingGeometry.Hit pressed, RingGeometry.Hit released, bool isPopup) =>
        pressed.Kind == RingGeometry.HitKind.None ? null
        : released == pressed || (isPopup && released.Kind == RingGeometry.HitKind.Item) ? released : null;
}

/// <summary>
/// Halka menünün çizimi ve girdisi; masaüstündeki widget (<see cref="RingWidgetView"/>) ile kısayolla açılan halka paylaşır.
/// <para>Çizim (Kando'dan esinli, efektsiz): zemin diski, bütün dilimler hep (her biri dondurulmuş StreamGeometry halka
/// dilimi) ve dış kenarlarında ince yay, aralarında ince ayırıcılar; öğeler halka yarıçapında daire içinde simge; orta dairede
/// başlık ya da vurgulanan öğenin adı. Fareyle ya da klavyeyle yalnızca dolgular değişir: vurgulanan dilim vurgu renginde
/// içten dışa koyulaşır, yayı ve öğe dairesinin kenarı vurgu rengine döner (daire nötr kalır, simge okunsun; bkz.
/// <see cref="RingColors"/>). Hiçbir şey belirip kaybolmaz; yeniden çizim yalnızca vurgu
/// ya da yerleşim değişince. Yüksek karşıtlıkta sistem renkleri.</para>
/// <para>İsabet testi öğe kutularına değil açıya bakar (<see cref="RingGeometry.HitTest"/>, görünümün kendi koordinatında):
/// açılırken öğeler büyüyüp kayarken de doğrudur. Klavye: 1–9 (tuşa basınca vurgu, bırakınca seçim), oklar
/// (<see cref="RingGeometry.Navigate"/>), Enter/Boşluk, Backspace ya da ortaya tıklama (alt halkadan geri), Esc.</para>
/// <para>Erişilebilirlik: her öğe isabet testine katılmayan bir Button'dur (AutomationId <c>Ring.Item.N</c>, adı öğenin adı,
/// Invoke seçer); orta <c>Ring.Center</c>, alt halkadaki geri <c>Ring.Back</c>, widget'taki "+" yeri <c>Ring.AddApp</c>.</para>
/// </summary>
internal sealed class RingView : FrameworkElement
{
    private readonly VisualCollection _children;
    private readonly RingCenterVisual _center;
    private readonly List<RingItemVisual> _items = [];
    private readonly RingItemVisual _back;

    private WidgetConfig? _config;
    private int _tab;
    private List<RingSlot> _slots = [];
    private double[] _angles = [];
    private double? _parentAngle;
    private RingGeometry.WedgeLayout _wedges = new([], null);
    private RingMetrics _metrics = RingMetrics.For(0, 36);
    private RingColors _colors = RingColors.From(WidgetPalette.Glass, highContrast: false);
    private Color _glyphColor = Colors.White;
    private StreamGeometry[] _sectors = [], _rims = [];
    private StreamGeometry? _parentSector, _parentRim, _track;
    private Brush _disc = Brushes.Transparent, _hot = Brushes.Transparent;
    private WidgetPalette? _palette;
    private RingStyle _style = RingStyles.Classic;

    private RingGeometry.Hit _highlight = RingGeometry.Hit.None;
    private bool _keyboardHighlight;
    private RingGeometry.Hit _pressed = RingGeometry.Hit.None;
    private Point _pressPoint;
    private Key? _commitKey;
    private string? _hint;
    private int? _dropInsert, _dropSlot;

    public RingView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        _children = new VisualCollection(this);
        _center = new RingCenterVisual(this);
        // Klavyeyle vurgulanan öğenin adı ortada yazar: ekran okuyucusu da söylesin (bkz. Announce).
        AutomationProperties.SetLiveSetting(_center, AutomationLiveSetting.Polite);
        _children.Add(_center);
        _back = new RingItemVisual(this, -1);
        AutomationProperties.SetAutomationId(_back, "Ring.Back");
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    // ---------------------------------------------------------------- dışarıya açık

    /// <summary>Kısayolla açılan halka: isabet diskle sınırlı değil, ortaya basmak geri/kapat demektir, Esc kapatır.</summary>
    public bool IsPopup { get; set; }

    /// <summary>
    /// Boyut bütün halkalara göre: alt halkaya geçince halka büyüyüp küçülmez (masaüstündeki widget yerinde kalsın).
    /// Açılır halka kendi boyutunda çizilebilir.
    /// </summary>
    public bool StableSize { get; set; } = true;

    /// <summary>
    /// Öğe dışarı sürüklenebilir mi (masaüstündeki widget: yalnızca yol öğeleri). Yoksa ya da false dönerse basış sürer ve
    /// bırakınca öğe seçilir (biraz kayan tıklama kaybolmaz).
    /// </summary>
    public Func<RingSlot, bool>? CanDragOut { get; set; }

    /// <summary>
    /// Vurguyu sahibi (açılır halka, "tuşu bırakınca seç" sürerken) imlecin yönünden verir: görünüm fareyle vurgulamaz ve
    /// fare dışarı çıkınca vurguyu silmez.
    /// </summary>
    public bool PointerFromOwner { get; set; }

    /// <summary>
    /// Masaüstündeki widget: halkada 8'den az yer varken sonda "+" yeri ("Uygulama ekle", AutomationId <c>Ring.AddApp</c>)
    /// çizilir; seçilince <see cref="AddRequested"/>. Veri değildir; numarası, menüsü ve sürüklemesi yoktur. Açılır halkada yok.
    /// </summary>
    public bool ShowAddSlot { get; set; }

    /// <summary>"+" yeri seçildi (tık, Enter, UI Automation Invoke).</summary>
    public event Action? AddRequested;

    /// <summary>
    /// Öğenin simgesi soluk çizilsin mi (çalışmayan öğelerin yanında; ör. açılır halka çift tıklamayla açılınca tuş ve metin
    /// öğeleri). Yalnızca çizim; seçimi sahibi denetler.
    /// </summary>
    public Func<RingSlot, bool>? Dims { get; set; }

    /// <summary>
    /// Düzenleyicinin önizlemesi (<c>Views/RingEditor</c>): öğe seçilince çalışmaz, alt halkaya da girilmez; her yer (alt halka
    /// dahil) <see cref="ItemInvoked"/> ile seçilir. "+" yeri yine <see cref="AddRequested"/>.
    /// </summary>
    public bool EditMode { get; set; }

    /// <summary>Düzenleyicide seçili yer (sürekli vurgulu çizilir); yoksa null.</summary>
    public int? SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            var old = _selected;
            _selected = value;
            if (old is { } o && o < _items.Count) _items[o].InvalidateVisual();
            if (value is { } v && v < _items.Count) _items[v].InvalidateVisual();
        }
    }
    private int? _selected;

    /// <summary>Öğelerin AutomationId öneki ("Ring.Item." → Ring.Item.1…; düzenleyicinin önizlemesinde "RingEditor.Slot.").</summary>
    public string ItemIdPrefix { get; set; } = "Ring.Item.";

    /// <summary>Simgesi soluk çizilen öğe mi (çalışmıyor ya da <see cref="Dims"/>)?</summary>
    internal bool IsDimmed(RingSlot slot) => slot.Dimmed || Dims?.Invoke(slot) == true;

    /// <summary>Kökte boşken orta dairede ad yerine widget'ın simgesi ("Göster ▸ Ad" kapalıyken).</summary>
    public bool CenterShowsIcon
    {
        get => _centerShowsIcon;
        set
        {
            if (_centerShowsIcon == value) return;
            _centerShowsIcon = value;
            _center.InvalidateVisual();
        }
    }
    private bool _centerShowsIcon;

    public WidgetConfig? Config => _config;

    /// <summary>Gösterilen sekme: 0 kök halka, 1… alt halka.</summary>
    public int TabIndex => _tab;

    public bool InSubRing => _tab > 0;

    public IReadOnlyList<RingSlot> Slots => _slots;

    public RingMetrics Metrics => _metrics;

    /// <summary>Vurgulanan öğe; yoksa (orta, geri ya da hiçbiri) null.</summary>
    public RingSlot? HighlightedSlot =>
        _highlight.Kind == RingGeometry.HitKind.Item && _highlight.Index < _slots.Count ? _slots[_highlight.Index] : null;

    /// <summary>Orta daire (açılış hareketi ve erişilebilirlik).</summary>
    public FrameworkElement CenterElement => _center;

    /// <summary>Öğelerin görselleri, sırasıyla (açılış/geçiş hareketi).</summary>
    public IReadOnlyList<FrameworkElement> ItemElements => _items;

    /// <summary>Her öğeden halkanın ortasına vektör (DIP; açılış hareketi öğeleri ortadan getirir).</summary>
    public IReadOnlyList<Vector> TowardCenter =>
        _angles.Select(a => { var (x, y) = RingGeometry.Direction(a, _metrics.Radius); return new Vector(-x, -y); }).ToList();

    /// <summary>Öğe seçildi (yol ya da yol olmayan öğe; alt halkaya girmek burada değil: görünüm kendisi geçer).</summary>
    public event Action<RingSlot>? ItemInvoked;

    /// <summary>Kökteyken geri istendi (açılır halkada ortaya basmak, Backspace): açılır halka kapanır.</summary>
    public event Action? BackRequested;

    /// <summary>Esc (açılır halka kapanır).</summary>
    public event Action? CloseRequested;

    /// <summary>Alt halkaya girildi ya da köke dönüldü (açılır halkanın geçiş hareketi; widget menüsü buna göre kurulur).</summary>
    public event Action? Navigated;

    /// <summary>Basılı öğe sürüklenmeye başladı (yalnızca yol öğeleri dışarı sürüklenir; karar dinleyenin).</summary>
    public event Action<RingSlot>? DragOutRequested;

    /// <summary>Vurgulanan yer değişti.</summary>
    public event Action? HighlightChanged;

    /// <summary>Halkayı kurar (kök halka ya da <paramref name="tab"/>. alt halka). Simgeler <see cref="ApplyPalette"/> ile gelir.</summary>
    public void SetRing(WidgetConfig config, int tab = 0)
    {
        _config = config;
        _tab = tab;
        Rebuild();
    }

    /// <summary>Ayar değişti (öğe eklendi, taşındı, adı değişti…): yerler yeniden kurulur, vurgu (varsa) korunur.</summary>
    public void Rebuild()
    {
        if (_config is not { } config) return;
        if (_tab >= config.Tabs.Count) _tab = 0;
        foreach (var slot in _slots) slot.Release();
        _slots = RingSlot.Build(config, _tab);
        if (ShowAddSlot && RingSlot.WantsAddSlot(_slots.Count)) _slots.Add(RingSlot.AddSlot());

        _parentAngle = null;
        if (_tab > 0)
        {
            var root = RingSlot.Build(config, 0);
            var at = root.FindIndex(s => s.Kind == RingSlotKind.SubRing && s.TabIndex == _tab);
            // Kökün "+" yeri (varsa) en sondadır: alt halkanın yönü onunla birlikte hesaplanır.
            var rootAngles = RingGeometry.Angles(WithAddSlot(root.Count));
            _parentAngle = at >= 0 ? (rootAngles[at] + 180) % 360 : 180;
        }
        _angles = RingGeometry.Angles(_slots.Count, _parentAngle);
        _wedges = RingGeometry.Wedges(_angles, _parentAngle);
        var places = _slots.Count + (_parentAngle is null ? 0 : 1);
        if (StableSize) places = Math.Max(places, MaxPlaces(config));
        // Biçim ayardan (alan değişince widget görünümü yeniden kurulur; açılır halka da): simgenin rengi biçimin katmanlarına göre.
        var style = RingStyles.Effective(config.RingStyle, _colors.HighContrast);
        if (!ReferenceEquals(style, _style))
        {
            _style = style;
            if (_palette is not null)
            {
                _colors = RingColors.From(_palette, _colors.HighContrast, style);
                _glyphColor = _colors.Glyph;
            }
        }
        _metrics = RingMetrics.For(places, IconDip(config), _style);

        while (_items.Count > _slots.Count)
        {
            _children.Remove(_items[^1]);
            _items.RemoveAt(_items.Count - 1);
        }
        while (_items.Count < _slots.Count)
        {
            var item = new RingItemVisual(this, _items.Count);
            _items.Add(item);
            _children.Add(item);
        }
        if (_parentAngle is null)
        {
            if (_children.Contains(_back)) _children.Remove(_back);
        }
        else if (!_children.Contains(_back)) _children.Add(_back);
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var index = i;
            slot.Changed += () => { if (index < _items.Count && ReferenceEquals(_slots.ElementAtOrDefault(index), slot)) _items[index].InvalidateVisual(); };
            _items[i].Bind(slot);
        }
        _back.BindBack(L.F("Geri: {0}", RootTitle));

        if (_highlight.Kind == RingGeometry.HitKind.Item && _highlight.Index >= _slots.Count) _highlight = RingGeometry.Hit.None;
        if (_highlight.Kind == RingGeometry.HitKind.Parent && _parentAngle is null) _highlight = RingGeometry.Hit.None;
        _pressed = RingGeometry.Hit.None;
        _dropInsert = _dropSlot = null;
        BuildGeometry();
        LoadIcons();
        AutomationProperties.SetName(this, CenterTitle);
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateAll();
    }

    /// <summary>Palet (widget'ın arka planı ve vurgu rengi); yüksek karşıtlıkta sistem renkleri.</summary>
    /// <param name="rebuild">false: yalnızca renkler (hemen ardından <see cref="SetRing"/> çağrılacak; eski halka boşuna
    /// yeniden kurulmasın, simgeler yeni renkle SetRing'de istenir).</param>
    public void ApplyPalette(WidgetPalette palette, bool rebuild = true)
    {
        var highContrast = SystemParameters.HighContrast;
        _palette = palette;
        _style = RingStyles.Effective(_config?.RingStyle, highContrast);
        _colors = RingColors.From(palette, highContrast, _style);
        // Simgeler öğe dairesinin üstünde okunur kalsın (vurgu rengi gerekirse açılır/koyulaşır); yüksek karşıtlıkta pencere yazısı.
        var glyph = _colors.Glyph;
        var recolor = glyph != _glyphColor || !_paletteApplied;
        _glyphColor = glyph;
        _paletteApplied = true;
        BuildBrushes();
        if (!rebuild) return;
        // Fluent simgeli öğeler vurgu renginde çizilir: renk değiştiyse yerler yeniden kurulur (simgeler önbellekten gelir).
        if (recolor && _config is not null && _slots.Count > 0) Rebuild();
        else
        {
            LoadIcons();
            InvalidateAll();
        }
    }

    /// <summary>
    /// Halkanın paleti widget'ın ayarından (WidgetWindow'un kart paletiyle aynı çözüm; menüdeki önizleme hariç): kısayolla
    /// açılan halka da widget'ın arka planı ve vurgu rengiyle çizilir.
    /// </summary>
    public static WidgetPalette PaletteFor(WidgetConfig config) =>
        WidgetPalette.Resolve(config, null, WidgetPalette.SystemDark, SystemParameters.HighContrast);

    /// <summary>Ekran ya da widget ölçeği değişti: simgeler yeni piksel boyutunda istenir.</summary>
    public void UpdateIconSizes() => LoadIcons();

    /// <summary>Ayarlardaki görünüş değişti (parçalar, başlık, simge): yerleşim aynı, yalnızca yeniden çizilir.</summary>
    public void Redraw()
    {
        AutomationProperties.SetName(this, CenterTitle);
        InvalidateAll();
    }

    /// <summary>Widget'ın simgesi seçilirken ortadaki simge seçileni gösterir (kaydedilmez); null: önizleme biter.</summary>
    public void PreviewIcon(string? icon, bool active)
    {
        _iconPreview = active ? icon : null;
        _iconPreviewing = active;
        _center.InvalidateVisual();
    }

    private string? _iconPreview;
    private bool _iconPreviewing;

    /// <summary>Ortadaki simge (önizleme sürerken seçilen; boşsa türün varsayılanı).</summary>
    internal Wpf.Ui.Controls.SymbolRegular? CenterIcon => _config is not { } config ? null
        : _iconPreviewing ? WidgetIcons.Symbol(_iconPreview) ?? WidgetIcons.DefaultFor(config) : WidgetIcons.For(config);

    /// <summary>Görünüm kapanıyor: yerler kutucuklarının olaylarından çıkar.</summary>
    public void Release()
    {
        foreach (var slot in _slots) slot.Release();
        _slots = [];
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        LoadIcons();
        InvalidateAll();
    }

    /// <summary>Ölçek dışarıdan verilir (pencereye bağlanmadan önce açılacağı ekranınki).</summary>
    public double PixelsPerDipHint { get; set; }

    /// <summary>
    /// Pencereye bağlıysa pencerenin gerçek ölçeği (HwndSource dönüşümü: hiç gösterilmemiş açılır halkada
    /// <see cref="VisualTreeHelper.GetDpi"/> ilk gösterilişe dek 1 döner, simgeler yanlış boyutta istenirdi).
    /// </summary>
    private double PixelsPerDip => PresentationSource.FromVisual(this) is { } source
        ? source.CompositionTarget?.TransformToDevice.M11 is double m && m > 0 ? m : VisualTreeHelper.GetDpi(this).PixelsPerDip
        : PixelsPerDipHint > 0 ? PixelsPerDipHint : NativeMethods.SystemPixelsPerDip;

    /// <summary>Noktadaki yeri vurgular (görünümün kendi koordinatında).</summary>
    public void HighlightAt(Point point) => SetHighlight(HitAt(point), keyboard: false);

    public void ClearHighlight() => SetHighlight(RingGeometry.Hit.None, keyboard: false);

    /// <summary><paramref name="index"/>. öğeyi vurgular (klavye, menü).</summary>
    public void Highlight(int index)
    {
        if (index >= 0 && index < _slots.Count) SetHighlight(new RingGeometry.Hit(RingGeometry.HitKind.Item, index), keyboard: true);
    }

    /// <summary>Öğeyi seçer: alt halkaya girer ya da <see cref="ItemInvoked"/>.</summary>
    public void Activate(int index)
    {
        if (index < 0 || index >= _slots.Count) return;
        var slot = _slots[index];
        if (EditMode && slot.Kind != RingSlotKind.Add) ItemInvoked?.Invoke(slot);
        else if (slot.Kind == RingSlotKind.SubRing) OpenSubRing(slot.TabIndex);
        else if (slot.Kind == RingSlotKind.Add) AddRequested?.Invoke();
        else ItemInvoked?.Invoke(slot);
    }

    /// <summary>
    /// Vurgulanan yeri seçer (açılır halkada nişan alanına tıklama, "tuşu bırakınca seç"): öğe → <see cref="Activate"/>, geri
    /// yeri ya da ortası → <see cref="GoBack"/>; vurgu yoksa bir şey yapmaz.
    /// </summary>
    public void CommitHighlight() => Commit(_highlight);

    /// <summary>Alt halkaya girer.</summary>
    public void OpenSubRing(int tab)
    {
        if (_config is null || tab <= 0 || tab >= _config.Tabs.Count || tab == _tab) return;
        _tab = tab;
        _highlight = RingGeometry.Hit.None;
        Rebuild();
        Navigated?.Invoke();
    }

    /// <summary>Alt halkadan köke döner; kökteyse <see cref="BackRequested"/>.</summary>
    public void GoBack()
    {
        if (_tab == 0)
        {
            BackRequested?.Invoke();
            return;
        }
        _tab = 0;
        _highlight = RingGeometry.Hit.None;
        Rebuild();
        Navigated?.Invoke();
    }

    /// <summary>Noktadaki öğe (menü, bırakma); yoksa null.</summary>
    public RingSlot? SlotAt(Point point) =>
        HitAt(point) is { Kind: RingGeometry.HitKind.Item } hit && hit.Index < _slots.Count ? _slots[hit.Index] : null;

    /// <summary>Noktaya bırakılan öğenin sekmedeki sırası.</summary>
    public int DropIndexAt(Point point)
    {
        var c = CenterPoint;
        return RingInput.DropIndex(_angles, EntryCount, point.X - c.X, point.Y - c.Y);
    }

    /// <summary>Sekmenin öğeleri (yol ve eylem; alt halkalar ve "+" yeri hariç): hep yerlerin başındadır.</summary>
    private int EntryCount => _slots.Count(s => s.Kind is RingSlotKind.Path or RingSlotKind.Action);

    /// <summary>Numara alan yerler ("+" yeri hariç; o hep sondadır).</summary>
    private int NumberedCount => _slots.Count(s => s.Kind != RingSlotKind.Add);

    /// <summary>
    /// Sürükleme sırasında bırakılacak yeri gösterir: <paramref name="insertIndex"/> öğelerin arasına vurgu çizgisi,
    /// <paramref name="slotIndex"/> o yerin dilimi (alt halkaya ekleme), <paramref name="hint"/> orta dairede. Hepsi null: biter.
    /// </summary>
    public void ShowDrop(int? insertIndex, int? slotIndex, string? hint)
    {
        if (_dropInsert == insertIndex && _dropSlot == slotIndex && _hint == hint) return;
        _dropInsert = insertIndex;
        _dropSlot = slotIndex;
        _hint = hint;
        InvalidateAll();
    }

    /// <summary>Öğenin görseli (menünün klavyeyle açıldığı yer); yoksa null.</summary>
    public FrameworkElement? ItemElement(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

    // ---------------------------------------------------------------- iç durum

    private static double IconDip(WidgetConfig config) => TileItem.IconPixels(config.IconSize);

    /// <summary>
    /// Bütün halkaların en çok yeri (alt halkada "geri" yeri, widget'ta "+" yeri dahil): boyut halkalar arasında değişmesin.
    /// </summary>
    private int MaxPlaces(WidgetConfig config)
    {
        var max = WithAddSlot(RingSlot.Build(config, 0).Count);
        for (var t = 1; t < config.Tabs.Count; t++)
            if (config.Tabs[t] is { } tab) max = Math.Max(max, WithAddSlot(LauncherEntries.Count(tab)) + 1);
        return max;
    }

    /// <summary>Bu kadar yerli halkada "+" yeriyle birlikte kaç yer olur.</summary>
    private int WithAddSlot(int places) => ShowAddSlot && RingSlot.WantsAddSlot(places) ? places + 1 : places;

    private Point CenterPoint => new(_metrics.OuterRadius, _metrics.OuterRadius);

    internal RingColors Scheme => _colors;
    internal Color GlyphColor => _glyphColor;

    /// <summary>Çizilen biçim (yüksek karşıtlıkta Klasik).</summary>
    internal RingStyle Style => _style;

    /// <summary>
    /// Ortada adının üstünde gösterilen simge: vurgulanan öğeninki (bırakma ipucu sürerken yok). Kando'daki gibi orta, nereye
    /// gidileceğini simgesi ve adıyla söyler.
    /// </summary>
    internal ImageSource? CenterItemIcon => _hint is null ? HighlightedSlot?.Icon : null;

    /// <summary>Alt halkadaki "geri" dairesinin öğe dairesine oranı (Kando'daki gibi küçük üst öğe).</summary>
    public const double BackScale = 0.66;

    /// <summary>
    /// Kök halkanın başlığı: verilmiş ad; yoksa ya da tohumun yazdığı varsayılan adsa (oluşturulduğu dilde) arayüz dilinde
    /// "Halka menü" (bkz. WidgetText).
    /// </summary>
    internal string RootTitle =>
        _config is { Title: { } title } && !string.IsNullOrWhiteSpace(title) && !L.Variants("Halka menü").Contains(title.Trim())
            ? title.Trim() : L.T("Halka menü");

    /// <summary>Orta dairenin boştaki başlığı: kökte halkanın adı, alt halkada alt halkanın adı.</summary>
    internal string CenterTitle =>
        _config is { } config && _tab > 0 && _tab < config.Tabs.Count ? RingSlot.SubRingName(config.Tabs[_tab]) : RootTitle;

    /// <summary>Orta dairede şu an yazan: bırakma ipucu, vurgulanan öğenin adı, geri, ya da başlık; boş kökte ipucu.</summary>
    internal string? CenterText
    {
        get
        {
            if (_hint is not null) return _hint;
            if (HighlightedSlot is { } slot) return slot.Name;
            if (_highlight.Kind == RingGeometry.HitKind.Parent || (_highlight.Kind == RingGeometry.HitKind.Center && _tab > 0))
                return L.F("Geri: {0}", RootTitle);
            if (_tab == 0 && _slots.All(s => s.Kind == RingSlotKind.Add)) return L.T("Uygulama, dosya ya da klasörü buraya sürükle");
            if (_tab == 0 && CenterShowsIcon) return null;
            return CenterTitle;
        }
    }

    /// <summary>Orta daire vurgulu mu (alt halkada geri; açılır halkada kökte kapat)?</summary>
    internal bool CenterHot => _highlight.Kind == RingGeometry.HitKind.Center && (_tab > 0 || IsPopup);

    internal bool CenterPressed => _pressed.Kind == RingGeometry.HitKind.Center && (_tab > 0 || IsPopup);

    internal bool IsHot(int index) =>
        (index >= 0 && _selected == index) || (index >= 0 && _highlight.Kind == RingGeometry.HitKind.Item && _highlight.Index == index) ||
        (index < 0 && _highlight.Kind == RingGeometry.HitKind.Parent) || (index >= 0 && _dropSlot == index);

    internal bool IsPressed(int index) =>
        (index >= 0 && _pressed.Kind == RingGeometry.HitKind.Item && _pressed.Index == index) ||
        (index < 0 && _pressed.Kind == RingGeometry.HitKind.Parent);

    /// <summary>1–9 rozetleri gösterilsin mi ("Göster ▸ Tuş numaraları").</summary>
    internal bool ShowsKeys => _config?.Shows(PartKeys) ?? true;

    /// <summary>"Göster ▸" anahtarı: öğelerin 1–9 numaraları.</summary>
    public const string PartKeys = "keys";

    internal double ItemAngle(int index) => index < 0 ? _parentAngle ?? 0 : index < _angles.Length ? _angles[index] : 0;

    internal double PixelsPerDipForText => PixelsPerDip;

    private RingGeometry.Hit HitAt(Point point)
    {
        var c = CenterPoint;
        return RingInput.HitAt(_metrics, _wedges, point.X - c.X, point.Y - c.Y, limitToDisc: !IsPopup);
    }

    private void SetHighlight(RingGeometry.Hit hit, bool keyboard)
    {
        // Kökte orta yalnızca açılır halkada bir iştir (kapat); widget'ta ortası taşımaya açıktır, vurgulanmaz.
        if (hit.Kind == RingGeometry.HitKind.Center && _tab == 0 && !IsPopup) hit = RingGeometry.Hit.None;
        _keyboardHighlight = keyboard && hit.Kind != RingGeometry.HitKind.None;
        if (hit == _highlight) return;
        var old = _highlight;
        _highlight = hit;
        InvalidateFor(old);
        InvalidateFor(hit);
        InvalidateVisual();
        _center.InvalidateVisual();
        if (_keyboardHighlight) Announce();
        HighlightChanged?.Invoke();
    }

    /// <summary>
    /// Klavyeyle vurgulanan yeri ekran okuyucusu söyler: orta daire nazik canlı bölgedir (adı vurgulanan öğenin adı), değişince
    /// bildirilir. Dinleyen yoksa bir şey yapılmaz.
    /// </summary>
    private void Announce()
    {
        if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;
        var peer = UIElementAutomationPeer.FromElement(_center) ?? UIElementAutomationPeer.CreatePeerForElement(_center);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    // Odak çerçevesi (orta dairenin kenarı vurgu renginde): klavyeyle odaklanınca ya da tuşa basınca; fareyle basınca kalkar.
    private bool _focusCue;

    /// <summary>Orta dairenin kenarı odak çerçevesi olarak çizilsin mi (klavye odağı görünümde ve son girdi klavyeden)?</summary>
    internal bool ShowsFocusCue => _focusCue && IsKeyboardFocused;

    private void SetFocusCue(bool on)
    {
        if (_focusCue == on) return;
        _focusCue = on;
        _center.InvalidateVisual();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        // Fareyle tıklanınca odaklanan widget çerçeve göstermez; kısayolla açılan halka (fare basılı değil) gösterir.
        SetFocusCue(Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed);
        _center.InvalidateVisual();
    }

    private void InvalidateFor(RingGeometry.Hit hit)
    {
        if (hit.Kind == RingGeometry.HitKind.Item && hit.Index < _items.Count) _items[hit.Index].InvalidateVisual();
        else if (hit.Kind == RingGeometry.HitKind.Parent) _back.InvalidateVisual();
    }

    private void InvalidateAll()
    {
        InvalidateVisual();
        _center.InvalidateVisual();
        _back.InvalidateVisual();
        foreach (var item in _items) item.InvalidateVisual();
    }

    // Simgeler ilk palet gelince istenir (Fluent simgeleri vurgu renginde çizilir; önce beyaz istenip yeniden istenmesin).
    private bool _paletteApplied;

    private void LoadIcons()
    {
        if (_config is not { } config || !_paletteApplied) return;
        var dpi = PixelsPerDip;
        foreach (var slot in _slots) slot.LoadIcon(config, _metrics.IconDip, dpi, config.Scale, _glyphColor);
    }

    // ---------------------------------------------------------------- geometri ve fırçalar

    private void BuildGeometry()
    {
        var c = CenterPoint;
        var inner = _metrics.CenterRadius + 3;
        var outer = _metrics.OuterRadius - 1;
        var rim = _metrics.OuterRadius - 4;
        _sectors = _wedges.Items.Select(w => Sector(c, inner, outer, w.Start, w.End)).ToArray();
        _rims = _wedges.Items.Select(w => Arc(c, rim, w.Start, w.End)).ToArray();
        _parentSector = _wedges.Parent is { } p ? Sector(c, inner, outer, p.Start, p.End) : null;
        _parentRim = _wedges.Parent is { } q ? Arc(c, rim, q.Start, q.End) : null;
        _track = _style.Track ? Track(c, _metrics, _angles, _parentAngle) : null;
        BuildBrushes();
    }

    /// <summary>
    /// Yörünge (Daire): öğe merkezlerinin çemberi, öğe dairelerinin arasında kalan yaylar (çizgi simgenin altından geçmez;
    /// dairelerle arasında 4 DIP). Alt halkadaki küçük "geri" dairesi de yer kaplar. Dondurulmuş, tek biçim.
    /// </summary>
    internal static StreamGeometry Track(Point c, RingMetrics metrics, IReadOnlyList<double> angles, double? parentAngle)
    {
        var radius = metrics.Radius;
        double Half(double chipRadius) => Math.Asin(Math.Min(1, (chipRadius + 4) / radius)) * 180 / Math.PI;
        var stops = angles.Select(a => (Angle: a, Half: Half(metrics.ItemDip / 2))).ToList();
        if (parentAngle is { } parent) stops.Add((parent, Half(metrics.ItemDip / 2 * BackScale)));
        stops.Sort((a, b) => a.Angle.CompareTo(b.Angle));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (stops.Count == 0)
            {
                ctx.BeginFigure(At(c, radius, 0), isFilled: false, isClosed: true);
                ctx.ArcTo(At(c, radius, 180), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                ctx.ArcTo(At(c, radius, 360), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            }
            for (var i = 0; i < stops.Count; i++)
            {
                var (a, ha) = stops[i];
                var (b, hb) = stops[(i + 1) % stops.Count];
                var span = stops.Count == 1 ? 360 : ((b - a) % 360 + 360) % 360;
                var (from, to) = (a + ha, a + span - hb);
                if (to - from < 2) continue;
                ctx.BeginFigure(At(c, radius, from), isFilled: false, isClosed: false);
                ctx.ArcTo(At(c, radius, to), new Size(radius, radius), 0, to - from > 180, SweepDirection.Clockwise, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private void BuildBrushes()
    {
        (_disc, _hot) = LayerBrushes(_colors, _metrics);
        (_chipShadow, _chipShadowShape) = RingItemVisual.Shadow(_metrics, _colors.ChipShadowAlpha) is { } shadow
            ? (shadow.Brush, shadow.Shape) : ((Brush?)null, (Geometry?)null);
        var glow = _style.Glow && !_colors.HighContrast;
        (_chipHalo, _chipHaloShape) = glow && RingItemVisual.Halo(_metrics, _colors.Accent) is { } halo
            ? (halo.Brush, halo.Shape) : ((Brush?)null, (Geometry?)null);
        (_glowBand, _rimHalo) = glow ? GlowBrushes(_colors, _metrics) : ((Brush?)null, (Pen?)null);
    }

    /// <summary>
    /// Işıltı: dış kenarın içinde vurgu renginde ince bir hale (yalnızca öğe dairelerinin dışında kalan bantta) ve vurgulanan
    /// yayın altında genişçe, iki yana solan bir kalem. Degradeler halkanın ortasına göre (mutlak, dondurulmuş); efekt değil.
    /// </summary>
    internal static (Brush Band, Pen RimHalo) GlowBrushes(RingColors colors, RingMetrics metrics)
    {
        var c = new Point(metrics.OuterRadius, metrics.OuterRadius);
        var outer = metrics.OuterRadius;
        var rim = (outer - 4) / outer;
        var bandStart = Math.Min((metrics.Radius + metrics.ItemDip / 2 + 1) / outer, rim - 0.01);
        var band = Radial(c, outer, (0, WithAlpha(colors.Accent, 0)), (bandStart, WithAlpha(colors.Accent, 0)),
            (rim, WithAlpha(colors.Accent, GlowBandAlpha)), (1, WithAlpha(colors.Accent, 0x08)));
        const double width = 10;
        var inner = (outer - 4 - width / 2) / outer;
        var halo = Radial(c, outer, (0, WithAlpha(colors.Accent, 0)), (inner, WithAlpha(colors.Accent, 0)),
            (rim, WithAlpha(colors.Accent, RimHaloAlpha)), (1, WithAlpha(colors.Accent, 0)));
        var pen = new Pen(halo, width) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        pen.Freeze();
        return (band, pen);
    }

    /// <summary>Işıltının dış bandı ve vurgulanan yayın halesi en koyu yerinde (vurgu renginin saydamlığı).</summary>
    internal const byte GlowBandAlpha = 0x34, RimHaloAlpha = 0x60;

    private Brush? _chipHalo, _glowBand;
    private Geometry? _chipHaloShape;
    private Pen? _rimHalo;

    internal Brush? ChipHalo => _chipHalo;
    internal Geometry? ChipHaloShape => _chipHaloShape;

    /// <summary>
    /// Zemin diskinin ve vurgulanan dilimin degradeleri (görünümün koordinatında, ortası halkanın ortası; dondurulmuş). Disk
    /// orta dairenin kenarına dek <see cref="RingColors.DiscInner"/>, dış kenarda <see cref="RingColors.DiscOuter"/>; vurgulanan
    /// dilim içten dışa yumuşakça koyulaşır (orta dairenin yanında neredeyse saydam, dış kenarda en çok %28).
    /// </summary>
    internal static (Brush Disc, Brush Hot) LayerBrushes(RingColors colors, RingMetrics metrics)
    {
        if (colors.HighContrast) return (colors.Disc, colors.HotFill);
        var c = new Point(metrics.OuterRadius, metrics.OuterRadius);
        var outer = metrics.OuterRadius;
        var inner = Math.Clamp(metrics.CenterRadius / outer, 0, 1);
        var disc = Radial(c, outer, (0, colors.DiscInner), (inner, colors.DiscInner), (1, colors.DiscOuter));
        var hot = Radial(c, outer, (0, WithAlpha(colors.Accent, 0)), (inner, WithAlpha(colors.Accent, HotInnerAlpha)),
            (inner + (1 - inner) * 0.55, WithAlpha(colors.Accent, HotMidAlpha)), (1, WithAlpha(colors.Accent, HotOuterAlpha)));
        return (disc, hot);
    }

    internal static Brush Radial(Point center, double radius, params (double Offset, Color Color)[] stops)
    {
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute, Center = center, GradientOrigin = center, RadiusX = radius, RadiusY = radius,
        };
        foreach (var (offset, color) in stops) brush.GradientStops.Add(new GradientStop(color, offset));
        brush.Freeze();
        return brush;
    }

    /// <summary>Vurgulanan dilimin degradesi: orta dairenin yanında, ortada ve dış kenarda vurgu renginin saydamlığı.</summary>
    internal const byte HotInnerAlpha = 0x0C, HotMidAlpha = 0x26, HotOuterAlpha = 0x48;

    // Öğe dairelerinin gölgesi: bütün öğe kutuları aynı boyda, tek fırça ve tek biçim (ölçü ya da palet değişince yeniden).
    private Brush? _chipShadow;
    private Geometry? _chipShadowShape;

    internal Brush? ChipShadow => _chipShadow;
    internal Geometry? ChipShadowShape => _chipShadowShape;

    internal static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    internal static Point At(Point center, double radius, double angle)
    {
        var (x, y) = RingGeometry.Direction(angle, radius);
        return new Point(center.X + x, center.Y + y);
    }

    /// <summary>Halka dilimi (iç ve dış yarıçap arası); tam tur ise halka.</summary>
    private static StreamGeometry Sector(Point c, double inner, double outer, double start, double end)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (end - start >= 359.99)
            {
                Circle(ctx, c, outer);
                Circle(ctx, c, inner);
                geometry.FillRule = FillRule.EvenOdd;
            }
            else
            {
                var large = end - start > 180;
                ctx.BeginFigure(At(c, outer, start), isFilled: true, isClosed: true);
                ctx.ArcTo(At(c, outer, end), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, false);
                ctx.LineTo(At(c, inner, end), true, false);
                ctx.ArcTo(At(c, inner, start), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    internal static void Circle(StreamGeometryContext ctx, Point c, double r)
    {
        ctx.BeginFigure(new Point(c.X, c.Y - r), isFilled: true, isClosed: true);
        ctx.ArcTo(new Point(c.X, c.Y + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
        ctx.ArcTo(new Point(c.X, c.Y - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
    }

    /// <summary>Dilimin dış kenarındaki yay; komşusuyla arasında 4 DIP boşluk kalır.</summary>
    private static StreamGeometry Arc(Point c, double radius, double start, double end)
    {
        var geometry = new StreamGeometry();
        var gap = Math.Min((end - start) * 0.2, 2 * 180 / (Math.PI * radius));
        using (var ctx = geometry.Open())
        {
            if (end - start >= 359.99)
            {
                ctx.BeginFigure(At(c, radius, 0), isFilled: false, isClosed: true);
                ctx.ArcTo(At(c, radius, 180), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                ctx.ArcTo(At(c, radius, 360), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            }
            else
            {
                var (s, e) = (start + gap, end - gap);
                ctx.BeginFigure(At(c, radius, s), isFilled: false, isClosed: false);
                ctx.ArcTo(At(c, radius, e), new Size(radius, radius), 0, e - s > 180, SweepDirection.Clockwise, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    // ---------------------------------------------------------------- yerleşim ve çizim

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in _children) child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(_metrics.Size, _metrics.Size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var c = CenterPoint;
        var r = _metrics.CenterRadius;
        _center.Arrange(new Rect(c.X - r, c.Y - r, r * 2, r * 2));
        var box = RingItemVisual.BoxSize(_metrics);
        for (var i = 0; i < _items.Count; i++) _items[i].Arrange(ItemRect(c, ItemAngle(i), box));
        if (_parentAngle is { } parent) _back.Arrange(ItemRect(c, parent, box));
        return new Size(_metrics.Size, _metrics.Size);
    }

    private Rect ItemRect(Point c, double angle, double box)
    {
        var p = At(c, _metrics.Radius, angle);
        return new Rect(p.X - box / 2, p.Y - box / 2, box, box);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var c = CenterPoint;
        var style = _style;
        // Zemin diski: isabet testi de buna bakar (dışı widget'ı taşımaya kalır).
        dc.DrawEllipse(_disc, null, c, _metrics.OuterRadius, _metrics.OuterRadius);
        if (_glowBand is not null) dc.DrawEllipse(_glowBand, null, c, _metrics.OuterRadius, _metrics.OuterRadius);
        if (style.Wedges)
        {
            for (var i = 0; i < _sectors.Length; i++)
                dc.DrawGeometry(IsHot(i) ? _hot : _colors.Wedge, null, _sectors[i]);
            if (_parentSector is not null) dc.DrawGeometry(IsHot(-1) ? _hot : _colors.Wedge, null, _parentSector);
        }
        else if (_dropSlot is { } drop && drop < _sectors.Length) dc.DrawGeometry(_hot, null, _sectors[drop]); // alt halkaya bırakma

        // Dilimler arasındaki ince ayırıcılar (tek dilimde yok); Işıltı'da yerlerinde dış kenarda küçük noktalar.
        var boundaries = _wedges.Items.Select(w => w.Start).ToList();
        if (_wedges.Parent is { } parent) boundaries.Add(parent.Start);
        if (boundaries.Count > 1 && style.Separators)
            foreach (var angle in boundaries)
                dc.DrawLine(_colors.Separator, At(c, _metrics.CenterRadius + 8, angle), At(c, _metrics.OuterRadius - 9, angle));
        if (boundaries.Count > 1 && style.Glow)
            foreach (var angle in boundaries)
                dc.DrawEllipse(_colors.GlowDot, null, At(c, _metrics.OuterRadius - 4, angle), 1.6, 1.6);

        if (style.Rims)
        {
            var idle = style.Glow ? _colors.GlowRim : _colors.Rim;
            for (var i = 0; i < _rims.Length; i++)
            {
                if (IsHot(i) && _rimHalo is not null) dc.DrawGeometry(null, _rimHalo, _rims[i]);
                dc.DrawGeometry(null, IsHot(i) ? _colors.RimHot : idle, _rims[i]);
            }
            if (_parentRim is not null)
            {
                if (IsHot(-1) && _rimHalo is not null) dc.DrawGeometry(null, _rimHalo, _parentRim);
                dc.DrawGeometry(null, IsHot(-1) ? _colors.RimHot : idle, _parentRim);
            }
            // Boş halka: dilim yok; yine de bir halka gibi görünsün (bırakılacak yer).
            if (_rims.Length == 0 && _parentRim is null)
                dc.DrawEllipse(null, _colors.Separator, c, _metrics.OuterRadius - 4, _metrics.OuterRadius - 4);
        }
        else dc.DrawEllipse(null, _colors.Edge, c, _metrics.OuterRadius - 0.5, _metrics.OuterRadius - 0.5);
        if (_track is not null) dc.DrawGeometry(null, _colors.Track, _track);

        // Alt halkada: ortadan geldiğin yöndeki küçük üst daireye bağlantı çizgisi (Kando'daki gibi).
        if (_parentAngle is { } back)
        {
            var end = _metrics.Radius - _metrics.ItemDip / 2 * BackScale - 2;
            if (end > _metrics.CenterRadius + 6)
                dc.DrawLine(IsHot(-1) ? _colors.Pointer : _colors.Connector, At(c, _metrics.CenterRadius + 3, back), At(c, end, back));
        }
        // Gösterge: ortadan vurgulanan öğeye ince vurgu çizgisi (Daire, Etiketli).
        if (style.Pointer && _highlight.Kind == RingGeometry.HitKind.Item && _highlight.Index < _angles.Length)
        {
            var angle = _angles[_highlight.Index];
            var end = _metrics.Radius - _metrics.ItemDip / 2 - 3;
            if (end > _metrics.CenterRadius + 6)
            {
                dc.DrawLine(_colors.Pointer, At(c, _metrics.CenterRadius + 3, angle), At(c, end, angle));
                dc.DrawEllipse(_colors.PointerDot, null, At(c, _metrics.CenterRadius + 3, angle), 2.5, 2.5);
            }
        }

        // Bırakılacak yer: öğelerin arasında vurgu çizgisi.
        var entries = EntryCount;
        if (_dropInsert is { } insert && entries > 0)
        {
            insert = Math.Clamp(insert, 0, entries);
            var angle = insert < entries ? _wedges.Items[insert].Start : _wedges.Items[entries - 1].End;
            dc.DrawLine(_colors.Marker, At(c, _metrics.CenterRadius + 4, angle), At(c, _metrics.OuterRadius - 2, angle));
        }
    }

    // ---------------------------------------------------------------- fare

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        // Açılır halkadan dışarı sürüklenmez: basıp başka öğeye götürmek oradaki öğeyi seçer (bkz. OnMouseLeftButtonUp).
        // Sürüklenemeyen öğede basış sürer: biraz kayan tıklama yine seçer.
        if (_pressed.Kind == RingGeometry.HitKind.Item && e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured &&
            _pressed.Index < _slots.Count && _slots[_pressed.Index] is var pressedSlot)
        {
            var delta = point - _pressPoint;
            if (RingInput.StartsDragOut(IsPopup, CanDragOut?.Invoke(pressedSlot) == true, delta.X, delta.Y,
                    SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance))
            {
                EndPress();
                DragOutRequested?.Invoke(pressedSlot);
                return;
            }
        }
        if (_pressed.Kind == RingGeometry.HitKind.None && !PointerFromOwner) HighlightAt(point);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_keyboardHighlight && !PointerFromOwner && _pressed.Kind == RingGeometry.HitKind.None) ClearHighlight();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        SetFocusCue(false); // fareyle kullanan: odak çerçevesi yalnızca klavyeden sonra
        var hit = HitAt(e.GetPosition(this));
        // Widget'ta kökün ortası ve diskin dışı widget'ı taşımaya kalır (olay karta geçer).
        var actionable = hit.Kind is RingGeometry.HitKind.Item or RingGeometry.HitKind.Parent ||
                         (hit.Kind == RingGeometry.HitKind.Center && (_tab > 0 || IsPopup));
        if (!actionable || e.ClickCount > 1)
        {
            if (actionable) e.Handled = true; // çift tıklama: ikinci basış bir şey açmaz, widget'ı da taşımaz
            return;
        }
        _pressed = hit;
        _pressPoint = e.GetPosition(this);
        SetHighlight(hit, keyboard: false);
        CaptureMouse();
        InvalidateFor(hit);
        _center.InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressed.Kind == RingGeometry.HitKind.None) return;
        var pressed = _pressed;
        var hit = HitAt(e.GetPosition(this));
        EndPress();
        e.Handled = true;
        // Açılır halkada basıp bir öğeye doğru götürüp bırakmak o öğeyi seçer (pasta menülerindeki gibi).
        if (RingInput.ReleaseTarget(pressed, hit, IsPopup) is { } target) Commit(target);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_pressed.Kind != RingGeometry.HitKind.None) EndPress();
    }

    private void EndPress()
    {
        var pressed = _pressed;
        _pressed = RingGeometry.Hit.None;
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateFor(pressed);
        _center.InvalidateVisual();
    }

    /// <summary>Yeri seçer: öğe → <see cref="Activate"/>, geri ya da alt halkada orta → <see cref="GoBack"/>, açılır halkada kökün ortası → kapat.</summary>
    private void Commit(RingGeometry.Hit hit)
    {
        switch (hit.Kind)
        {
            case RingGeometry.HitKind.Item: Activate(hit.Index); break;
            case RingGeometry.HitKind.Parent: GoBack(); break;
            case RingGeometry.HitKind.Center when _tab > 0 || IsPopup: GoBack(); break;
        }
    }

    // ---------------------------------------------------------------- klavye

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var (kind, number, arrow) = RingInput.Map(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        if (kind != RingKey.None) SetFocusCue(true);
        switch (kind)
        {
            case RingKey.Number:
                if (!e.IsRepeat && RingInput.NumberIndex(number, NumberedCount) is { } index)
                {
                    Highlight(index);
                    _commitKey = e.Key;
                }
                e.Handled = true;
                break;
            case RingKey.Arrow:
                var current = _highlight.Kind switch
                {
                    RingGeometry.HitKind.Item => _highlight.Index,
                    RingGeometry.HitKind.Parent => RingGeometry.ParentIndex,
                    _ => (int?)null,
                };
                if (RingGeometry.Navigate(_angles, current, arrow, _parentAngle) is { } next)
                    SetHighlight(next == RingGeometry.ParentIndex ? new RingGeometry.Hit(RingGeometry.HitKind.Parent, next)
                        : new RingGeometry.Hit(RingGeometry.HitKind.Item, next), keyboard: true);
                _commitKey = null;
                e.Handled = true;
                break;
            case RingKey.Enter:
                if (!e.IsRepeat && _highlight.Kind != RingGeometry.HitKind.None) _commitKey = e.Key;
                e.Handled = _highlight.Kind != RingGeometry.HitKind.None;
                break;
            case RingKey.Back:
                if (!e.IsRepeat && (_tab > 0 || IsPopup)) GoBack();
                e.Handled = _tab > 0 || IsPopup;
                break;
            case RingKey.Escape:
                if (IsPopup)
                {
                    CloseRequested?.Invoke();
                    e.Handled = true;
                }
                else if (_highlight.Kind != RingGeometry.HitKind.None)
                {
                    ClearHighlight();
                    e.Handled = true;
                }
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_commitKey is not { } key || e.Key != key) return;
        _commitKey = null;
        e.Handled = true;
        Commit(_highlight);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _center.InvalidateVisual();
        _commitKey = null;
        if (_keyboardHighlight && !IsMouseOver) ClearHighlight();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ViewPeer(this);

    private sealed class ViewPeer(RingView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string GetClassNameCore() => nameof(RingView);
        protected override string GetLocalizedControlTypeCore() => L.T("Halka menü");
        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// Halkanın renkleri (paletten; yüksek karşıtlıkta sistem renkleri). Fırçalar ve kalemler dondurulmuştur.
/// <para>Cila (efektsiz): öğe daireleri yukarıdan hafif aydınlık bir degradeyle ve altlarında dışa doğru solan ince bir gölgeyle
/// (<see cref="ChipShadowAlpha"/>, <see cref="RingItemVisual.Shadow"/>: yalnızca dairenin dışında) çizilir. Vurgulanan ve
/// basılan daire (öğe ve orta) nötr ve neredeyse opaktır, vurgu renginde 2 DIP kenar alır: altındaki vurgu degradesi daireyi
/// ve vurgu renginde simgeyi boyamaz. Simgeler halkanın gerçek katmanlarının üstünde en az 3:1 okunur (<see cref="Glyph"/>,
/// <see cref="GlyphGrounds"/>). Yüksek karşıtlıkta vurgulanan öğe ve orta daire pencere renginde kalır (simge ve yazı
/// Windows'un pencere yazısı rengiyle okunur), vurgu kalın Vurgulama kenarı ve dilimin Vurgulama dolgusuyla gösterilir; gölge
/// yok.</para>
/// </summary>
internal sealed record RingColors(
    bool HighContrast, Color DiscInner, Color DiscOuter, Brush Disc, Brush Wedge, Brush HotFill, Color Accent,
    Pen Separator, Pen Rim, Pen RimHot, Pen Marker,
    Brush Chip, Pen ChipBorder, Brush ChipHot, Pen ChipBorderHot, Brush ChipPressed, byte ChipShadowAlpha,
    Brush Center, Pen CenterBorder, Brush CenterHot, Pen CenterBorderHot, Brush Text, Brush TextHot, Brush Secondary,
    Brush Badge, Pen BadgeBorder, Brush BadgeText, Brush BadgeHot, Brush BadgeTextHot, Brush Dot, Pen FocusBorder, Color Glyph,
    Pen Track, Pen Pointer, Brush PointerDot, Pen Connector, Pen Edge,
    Brush Label, Pen LabelBorder, Brush LabelHot, Brush LabelText, Brush LabelTextHot,
    Pen GlowRim, Brush GlowDot, Pen GlowChipBorder, Pen GlowCenterBorder)
{
    /// <summary>Simgenin altındaki renge karşı en az karşıtlığı (WCAG, anlamlı grafik).</summary>
    public const double GlyphContrast = 3.0;

    /// <summary>
    /// Açılır halkanın Cam'daki zemini (yarı saydam kartın yerine; <c>RingBackdrop</c>): koyu ve neredeyse opak, ortadan
    /// kenara degrade. Simgelerin rengi buna göre de denetlenir.
    /// </summary>
    internal const uint PopupPlateInner = 0xFA242232, PopupPlateOuter = 0xF713121B;

    /// <summary>Koyu temada vurgulanan ve basılan dairenin saydamlığı (neredeyse opak; açık temada opak).</summary>
    internal const byte SolidChipAlpha = 0xF0;

    /// <param name="style">Halkanın biçimi (<see cref="RingStyles"/>; yalnızca simge renginin denendiği katmanları değiştirir:
    /// dilimsiz biçimde simgenin altında dilim yoktur). Yüksek karşıtlıkta yok sayılır.</param>
    public static RingColors From(WidgetPalette palette, bool highContrast, RingStyle? style = null)
    {
        if (highContrast)
        {
            var window = SystemColors.WindowColor;
            var text = SystemColors.WindowTextColor;
            var highlight = SystemColors.HighlightColor;
            var highlightText = SystemColors.HighlightTextColor;
            // Vurgulanan öğe ve orta: zemin pencere rengi kalır (simge ve yazı pencere yazısı renginde okunur), kenarı kalın
            // Vurgulama; dilim Vurgulama ile dolar. Rozet Vurgulama / Vurgulanan yazı çiftidir.
            return new RingColors(true, window, window, Solid(window), Brushes.Transparent, Solid(highlight), highlight,
                Separator: Line(text, 1), Rim: Line(text, 2), RimHot: Line(highlight, 4), Marker: Line(highlight, 3),
                Chip: Solid(window), ChipBorder: Line(text, 1.5), ChipHot: Solid(window), ChipBorderHot: Line(highlight, 3),
                ChipPressed: Solid(window), ChipShadowAlpha: 0,
                Center: Solid(window), CenterBorder: Line(text, 1.5), CenterHot: Solid(window), CenterBorderHot: Line(highlight, 3),
                Text: Solid(text), TextHot: Solid(text), Secondary: Solid(text),
                Badge: Solid(window), BadgeBorder: Line(text, 1), BadgeText: Solid(text), BadgeHot: Solid(highlight),
                BadgeTextHot: Solid(highlightText), Dot: Solid(text), FocusBorder: Line(highlight, 2), Glyph: text,
                Track: Line(text, 1), Pointer: Line(highlight, 2), PointerDot: Solid(highlight), Connector: Line(text, 1.5),
                Edge: Line(text, 1), Label: Solid(window), LabelBorder: Line(text, 1), LabelHot: Solid(highlight), LabelText: Solid(text),
                LabelTextHot: Solid(highlightText), GlowRim: Line(text, 2), GlowDot: Solid(text), GlowChipBorder: Line(text, 1.5),
                GlowCenterBorder: Line(text, 1.5));
        }
        var fg = (palette.Foreground as SolidColorBrush)?.Color ?? Colors.White;
        var accent = (palette.Accent as SolidColorBrush)?.Color ?? Colors.White;
        var accentText = palette.AccentForeground;
        var light = palette.IsLight;
        var white = Colors.White;
        // Öğe dairesi yukarıdan hafif aydınlık: açık temada beyazdan serin griye, koyuda yazı renginin biraz daha saydamına.
        var chipTop = light ? Color.FromArgb(0xFA, 0xFF, 0xFF, 0xFF) : A(fg, 0x26);
        var chipBottom = light ? Color.FromArgb(0xF2, 0xF3, 0xF4, 0xF8) : A(fg, 0x14);
        // Vurgulanan ve basılan daire (öğe ve orta) nötr ve neredeyse opak, kenarı vurgu renginde: vurgu renginde simge, altındaki
        // vurgu degradesinin boyadığı dairede kaybolmasın (Okyanus gibi tek tonlu temalarda, koyu temalarda). Koyu temada
        // zeminin üstündeki yazı renginden (vurgulu boştakinden biraz aydınlık, basılı biraz sönük), açık temada beyaz; basılınca
        // yazı renginde hafif gri.
        var ground = Average(SurfaceGrounds(palette));
        var hotFill = Solid(light ? white : Solidish(ThemeMath.Composite(Argb(A(fg, 0x3E)), ground)));
        var pressedFill = Solid(light ? Rgb(ThemeMath.Composite(Argb(A(fg, 0x10)), Argb(white)))
            : Solidish(ThemeMath.Composite(Argb(A(fg, 0x2A)), ground)));
        var hotBorder = Line(accent, 2);
        var colors = new RingColors(false,
            DiscInner: A(fg, light ? 0x0E : 0x16), DiscOuter: A(fg, light ? 0x04 : 0x06), Disc: Brushes.Transparent,
            Wedge: Solid(A(fg, 0x04)), HotFill: Solid(A(accent, 0x40)), Accent: accent,
            Separator: Line(A(fg, light ? 0x18 : 0x22), 1), Rim: Line(A(fg, light ? 0x22 : 0x2C), 3), RimHot: Line(accent, 3.5),
            Marker: Line(accent, 3),
            Chip: Vertical(chipTop, chipBottom), ChipBorder: Line(A(fg, light ? 0x1E : 0x30), 1),
            ChipHot: hotFill, ChipBorderHot: hotBorder, ChipPressed: pressedFill, ChipShadowAlpha: light ? (byte)0x2E : (byte)0x50,
            Center: Solid(light ? A(white, 0xF2) : A(fg, 0x14)), CenterBorder: Line(A(fg, light ? 0x22 : 0x2E), 1),
            CenterHot: hotFill, CenterBorderHot: hotBorder,
            Text: palette.Foreground, TextHot: palette.Foreground, Secondary: palette.Secondary,
            Badge: Solid(light ? white : Color.FromArgb(0xEE, 0x1E, 0x1C, 0x28)), BadgeBorder: Line(A(fg, light ? 0x26 : 0x34), 1),
            BadgeText: palette.Foreground, BadgeHot: palette.Accent, BadgeTextHot: accentText, Dot: Solid(A(fg, light ? 0x70 : 0x8C)),
            FocusBorder: Line(accent, 2), Glyph: accent,
            // Yörünge ve bağlantı çizgisi yazı renginin saydamı; gösterge vurgu renginde. Etiket orta daire gibi neredeyse opak
            // ve nötr (yazı renginde ad), vurgulanınca vurgu renginde (vurgu yazısı rengiyle).
            Track: Line(A(fg, light ? 0x30 : 0x38), 1.5), Pointer: Line(A(accent, 0xD8), 2), PointerDot: Solid(accent),
            Connector: Line(A(fg, light ? 0x48 : 0x58), 1.5), Edge: Line(A(fg, light ? 0x1C : 0x24), 1),
            Label: Solid(light ? A(white, 0xF4) : Solidish(ThemeMath.Composite(Argb(A(fg, 0x1E)), ground))),
            LabelBorder: Line(A(fg, light ? 0x1E : 0x2A), 1), LabelHot: palette.Accent, LabelText: palette.Foreground,
            LabelTextHot: Solid(Rgb(ThemeMath.TextOn(Argb(accent)))),
            // Işıltı: kenarlar vurgu renginin saydamı (simgenin altına düşmez; halesi RingView/RingItemVisual'da degrade).
            GlowRim: Line(A(accent, light ? 0x80 : 0x70), 2), GlowDot: Solid(A(accent, 0xB0)),
            GlowChipBorder: Line(A(accent, light ? 0xA0 : 0x88), 1.25), GlowCenterBorder: Line(A(accent, light ? 0x90 : 0x78), 1.25));
        var glyph = GlyphOn(accent, colors.GlyphGrounds(Grounds(palette), wedges: style?.Wedges ?? true));
        // Alt halkanın noktaları simgenin renginde, biraz saydam (nereye gideceğini söyler; vurgu ailesinden).
        return colors with { Glyph = glyph, Dot = Solid(A(glyph, 0xC8)) };
    }

    /// <summary>
    /// Fluent simgelerin (öğeler, alt halkanın geri oku, ortadaki simge) rengi: vurgu rengi, altında görünebilecek her rengin
    /// (<paramref name="grounds"/>, bkz. <see cref="GlyphGrounds"/>) karşısında en az <see cref="GlyphContrast"/> okunacak
    /// biçimde gerekirse açılır ya da koyulaşır (ör. Okyanus'un deniz yeşili zemininde aynı tonda simge).
    /// </summary>
    internal static Color GlyphOn(Color accent, IReadOnlyList<uint> grounds)
    {
        var shown = ThemeMath.EnsureContrast(Argb(accent), grounds, GlyphContrast);
        return Color.FromRgb((byte)(shown >> 16), (byte)(shown >> 8), (byte)shown);
    }

    /// <summary>
    /// Simgelerin altında görünebilecek renkler (opak), halkanın gerçek katmanlarıyla: zemin (<paramref name="grounds"/>) →
    /// zemin diski → boştaki dilim ya da vurgulanan dilimin degradesi → öğe dairesi (boşta, vurgulu, basılı); ortada zemin →
    /// diskin içi → orta daire (boşta, vurgulu, basılı). Degradeler halkanın ölçüsünden bağımsız, en kötü durumla denenir: her
    /// biri uçlarında ve ortasında. Öğe gölgesi dairenin içine düşmez (<see cref="RingItemVisual.Shadow"/>).
    /// </summary>
    /// <param name="wedges">false: dilimsiz biçim (Daire, Etiketli): öğe dairesi doğrudan zemin diskinin üstünde, vurgulanınca
    /// da (altında dilim degradesi yok).</param>
    internal IReadOnlyList<uint> GlyphGrounds(IEnumerable<uint> grounds, bool wedges = true)
    {
        var discs = WithMidpoints([Argb(DiscOuter), Argb(DiscInner)]);
        var hots = new[] { RingView.HotInnerAlpha, RingView.HotMidAlpha, RingView.HotOuterAlpha }.Select(a => Argb(A(Accent, a))).ToList();
        var idleChips = Fills(Chip).Concat(Fills(ChipPressed)).ToList();
        var hotChips = Fills(ChipHot).Concat(Fills(ChipPressed)).ToList();
        var centers = Fills(Center).Concat(Fills(CenterHot)).Concat(Fills(ChipPressed)).ToList();
        var result = new HashSet<uint>();
        foreach (var ground in grounds)
        {
            foreach (var disc in discs)
            {
                var under = ThemeMath.Composite(disc, ground);
                if (!wedges)
                {
                    foreach (var chip in idleChips.Concat(hotChips)) result.Add(ThemeMath.Composite(chip, under));
                    continue;
                }
                foreach (var wedge in Fills(Wedge))
                {
                    var idle = ThemeMath.Composite(wedge, under);
                    foreach (var chip in idleChips) result.Add(ThemeMath.Composite(chip, idle));
                }
                foreach (var hot in hots)
                {
                    var highlighted = ThemeMath.Composite(hot, under);
                    foreach (var chip in hotChips) result.Add(ThemeMath.Composite(chip, highlighted));
                }
            }
            var middle = ThemeMath.Composite(Argb(DiscInner), ground);
            foreach (var center in centers) result.Add(ThemeMath.Composite(center, middle));
        }
        return [.. result];
    }

    /// <summary>
    /// Halkanın altındaki zemin (opak): kartın zemininin durakları ve aralarındaki ortalar; Cam'da (yarı saydam kart) açılır
    /// halkanın koyu zemini de (<see cref="PopupPlateInner"/>).
    /// </summary>
    internal static IReadOnlyList<uint> Grounds(WidgetPalette palette)
    {
        var grounds = WithMidpoints(Stops(palette.Background)).Select(ThemeMath.Opaque).ToList();
        if (!palette.IsOpaqueBackground) grounds.AddRange(PopupGrounds);
        return grounds;
    }

    private static IEnumerable<uint> PopupGrounds => WithMidpoints([PopupPlateInner, PopupPlateOuter]).Select(ThemeMath.Opaque);

    /// <summary>Neredeyse opak dairelerin üstünde durduğu zemin: kartın zemini; Cam'da açılır halkanın koyu zemini.</summary>
    private static IEnumerable<uint> SurfaceGrounds(WidgetPalette palette) =>
        palette.IsOpaqueBackground ? Stops(palette.Background) : PopupGrounds;

    private static IReadOnlyList<uint> Stops(Brush background) => background switch
    {
        SolidColorBrush s => [Argb(s.Color)],
        GradientBrush g => g.GradientStops.Select(x => Argb(x.Color)).ToList(),
        _ => [],
    };

    /// <summary>Fırçanın renkleri: düz renk ya da degradenin durakları ve aralarındaki ortalar.</summary>
    private static IReadOnlyList<uint> Fills(Brush brush) => WithMidpoints(Stops(brush));

    private static IReadOnlyList<uint> WithMidpoints(IReadOnlyList<uint> stops)
    {
        var all = new List<uint>(stops);
        for (var i = 1; i < stops.Count; i++) all.Add(Mix(stops[i - 1], stops[i]));
        return all;
    }

    private static uint Mix(uint a, uint b)
    {
        uint Half(int shift) => (((a >> shift) & 0xFF) + ((b >> shift) & 0xFF) + 1) / 2;
        return Half(24) << 24 | Half(16) << 16 | Half(8) << 8 | Half(0);
    }

    /// <summary>Opak renklerin kanal kanal ortalaması (saydam renkler önce siyahın üstüne konur).</summary>
    private static uint Average(IEnumerable<uint> colors)
    {
        var opaque = colors.Select(ThemeMath.Opaque).ToList();
        if (opaque.Count == 0) return 0xFF000000;
        uint Channel(int shift) => (uint)Math.Round(opaque.Average(c => (double)((c >> shift) & 0xFF)));
        return 0xFF000000 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    /// <summary>Opak rengin neredeyse opak hâli (<see cref="SolidChipAlpha"/>).</summary>
    private static Color Solidish(uint opaque) => Color.FromArgb(SolidChipAlpha, (byte)(opaque >> 16), (byte)(opaque >> 8), (byte)opaque);

    private static Color Rgb(uint argb) => Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static uint Argb(Color c) => (uint)c.A << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;

    private static Color A(Color color, int alpha) => RingView.WithAlpha(color, (byte)alpha);

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Vertical(Color top, Color bottom)
    {
        var brush = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1));
        brush.Freeze();
        return brush;
    }

    private static Pen Line(Color color, double thickness)
    {
        var pen = new Pen(Solid(color), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }
}

/// <summary>Halkadaki bir öğenin görseli: daire, simge, 1–9 rozeti ve (alt halkada) içindeki öğelerin noktaları.</summary>
internal sealed class RingItemVisual : FrameworkElement
{
    private readonly RingView _ring;
    private readonly int _index;
    private RingSlot? _slot;
    private string _backName = "";

    /// <param name="index">Öğenin sırası; -1 alt halkadaki "geri" yeri.</param>
    public RingItemVisual(RingView ring, int index)
    {
        _ring = ring;
        _index = index;
        IsHitTestVisible = false;
        Focusable = false;
        if (index >= 0) AutomationProperties.SetAutomationId(this, "Ring.Item." + (index + 1).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Öğe dairesi ve çevresindeki rozet/nokta/gölge payı.</summary>
    public static double BoxSize(RingMetrics metrics) => metrics.ItemDip + 24;

    /// <summary>Gölgenin daireden taşan payı (DIP) ve aşağı kayması: kutunun payının içinde kalır.</summary>
    public const double ShadowReach = 4.5, ShadowDrop = 1.5;

    /// <summary>Gölge biçiminin deliği dairenin kenarından bu kadar içeride (DIP): kenar çizgisinin altında kalır, arada ışık sızmaz.</summary>
    public const double ShadowHoleInset = 0.5;

    /// <summary>
    /// Öğe dairesinin gölgesi (efektsiz; öğe kutusunun kendi koordinatında, bütün öğelerde aynı, dondurulmuş): daireden
    /// <see cref="ShadowReach"/> büyük ve <see cref="ShadowDrop"/> aşağıdaki bir daireden öğe dairesi çıkarılmış biçim
    /// (EvenOdd) ve durakları yarıçapa göre konmuş degrade: dairenin alt kenarında saydam başlar, 1,5 DIP dışarıda en koyudur,
    /// dışa doğru solar. Dairenin içine düşmez (koyu temada yarı saydam daire ve simge koyulaşmaz); yalnızca kenar çizgisinin
    /// altına <see cref="ShadowHoleInset"/> kadar girer. <paramref name="alpha"/> 0: gölge yok (yüksek karşıtlık).
    /// </summary>
    internal static (Brush Brush, Geometry Shape)? Shadow(RingMetrics metrics, byte alpha)
    {
        if (alpha == 0) return null;
        var half = BoxSize(metrics) / 2;
        var c = new Point(half, half);
        var r = metrics.ItemDip / 2;
        var reach = r + ShadowReach;
        var at = new Point(c.X, c.Y + ShadowDrop);
        // Gölgenin ortasından uzaklık (DIP) → durak: dairenin alt kenarı r - kayma uzaklıktadır.
        var edge = r - ShadowDrop;
        double At(double distance) => Math.Clamp(distance / reach, 0, 1);
        var brush = RingView.Radial(at, reach,
            (At(edge), Color.FromArgb(0, 0, 0, 0)),
            (At(edge + 1.5), Color.FromArgb(alpha, 0, 0, 0)),
            (At(edge + 3.5), Color.FromArgb((byte)(alpha / 3), 0, 0, 0)),
            (1, Color.FromArgb(0, 0, 0, 0)));
        var shape = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = shape.Open())
        {
            RingView.Circle(ctx, at, reach);
            RingView.Circle(ctx, c, r - ShadowHoleInset);
        }
        shape.Freeze();
        return (brush, shape);
    }

    /// <summary>Işıltı halesinin daireden taşan payı (DIP): kutunun payının içinde kalır.</summary>
    public const double HaloReach = 7;

    /// <summary>
    /// Işıltı biçiminde vurgulanan dairenin halesi (efektsiz; gölge gibi yalnızca dairenin dışında, öğe kutusunun koordinatında,
    /// dondurulmuş): dairenin kenarında vurgu renginde en koyu, <see cref="HaloReach"/> dışarıda saydam.
    /// </summary>
    internal static (Brush Brush, Geometry Shape)? Halo(RingMetrics metrics, Color accent)
    {
        var half = BoxSize(metrics) / 2;
        var c = new Point(half, half);
        var r = metrics.ItemDip / 2;
        var reach = r + HaloReach;
        double At(double distance) => Math.Clamp(distance / reach, 0, 1);
        var brush = RingView.Radial(c, reach,
            (At(r - 1), RingView.WithAlpha(accent, 0x78)),
            (At(r + 2), RingView.WithAlpha(accent, 0x38)),
            (At(r + 4.5), RingView.WithAlpha(accent, 0x10)),
            (1, RingView.WithAlpha(accent, 0)));
        var shape = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = shape.Open())
        {
            RingView.Circle(ctx, c, reach);
            RingView.Circle(ctx, c, r - ShadowHoleInset);
        }
        shape.Freeze();
        return (brush, shape);
    }

    /// <summary>1–9 rozetinin yarıçapı (DIP): simgeyi örtmeyecek kadar küçük, rakam okunacak kadar büyük.</summary>
    public static double BadgeRadius(RingMetrics metrics) => Math.Max(7.5, metrics.ItemDip * 0.15);

    public string AccessibleName => _slot?.Name ?? _backName;

    public void Bind(RingSlot slot)
    {
        _slot = slot;
        _label = null; // yeni öğe: etiketin yazısı yeniden kurulur (rozetin rakamı sırayla aynı kalır)
        // "+" yerinin kendi kimliği var (yer sırası değişse de bulunsun); öbürleri sırasıyla.
        AutomationProperties.SetAutomationId(this, slot.Kind == RingSlotKind.Add ? "Ring.AddApp"
            : _ring.ItemIdPrefix + (_index + 1).ToString(CultureInfo.InvariantCulture));
        AutomationProperties.SetName(this, slot.Name);
        AutomationProperties.SetHelpText(this, slot.Kind == RingSlotKind.SubRing ? L.P(slot.ChildCount, "({0} öğe)")
            : !slot.Runnable ? L.T("Bu öğe bu sürümde çalışmıyor ya da kapalı.") : "");
        InvalidateVisual();
    }

    public void BindBack(string name)
    {
        _slot = null;
        _backName = name;
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    internal void Invoke()
    {
        if (_index < 0) _ring.GoBack();
        else _ring.Activate(_index);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var box = BoxSize(_ring.Metrics);
        return new Size(box, box);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var metrics = _ring.Metrics;
        var colors = _ring.Scheme;
        var style = _ring.Style;
        var c = new Point(RenderSize.Width / 2, RenderSize.Height / 2);
        var back = _index < 0;
        // Alt halkadaki "geri": geldiğin yönde küçük daire (Kando'daki üst öğe); ortadan ona bağlantı çizgisi RingView'de.
        var radius = metrics.ItemDip / 2 * (back ? RingView.BackScale : 1);
        var hot = _ring.IsHot(_index);
        var pressed = _ring.IsPressed(_index);

        // Alt halkanın içindeki öğeler: dairenin dışında, alt halkada duracakları yönlerde küçük noktalar (Kando'daki torunlar
        // gibi; durağan, fareyle belirmez).
        if (_slot is { Kind: RingSlotKind.SubRing, ChildCount: > 0 } sub)
        {
            var count = Math.Min(sub.ChildCount, RingSlot.MaxDots);
            var angle = _ring.ItemAngle(_index);
            foreach (var a in RingGeometry.Angles(count, (angle + 180) % 360))
                dc.DrawEllipse(colors.Dot, null, RingView.At(c, radius + DotGap, a), DotRadius, DotRadius);
        }

        // Dairenin altında dışa solan ince gölge, yalnızca dairenin dışında (yüksek karşıtlıkta yok); Işıltı'da vurgulanan
        // dairenin çevresinde vurgu renginde hale.
        if (!back)
        {
            if ((hot || pressed) && _ring.ChipHalo is { } halo && _ring.ChipHaloShape is { } haloShape) dc.DrawGeometry(halo, null, haloShape);
            else if (_ring.ChipShadow is { } shadow && _ring.ChipShadowShape is { } shape) dc.DrawGeometry(shadow, null, shape);
        }
        // Yüksek karşıtlıkta kalın Vurgulama kenarı daireyi taşırmasın: kalemin yarısı kadar içeride.
        var border = hot || pressed ? colors.ChipBorderHot : style.Glow ? colors.GlowChipBorder : colors.ChipBorder;
        var inset = border.Thickness / 2;
        dc.DrawEllipse(pressed ? colors.ChipPressed : hot ? colors.ChipHot : colors.Chip, border, c, radius - inset, radius - inset);

        var image = _slot?.Icon ?? (back ? GlyphImage.For(RingSlot.BackGlyph, _ring.GlyphColor) : null);
        if (image is not null)
        {
            var size = back ? metrics.IconDip * 0.55 : metrics.IconDip;
            var dpi = _ring.PixelsPerDipForText;
            var left = Math.Round((c.X - size / 2) * dpi) / dpi;
            var top = Math.Round((c.Y - size / 2) * dpi) / dpi;
            var dim = _slot is { } shown && _ring.IsDimmed(shown);
            if (dim) dc.PushOpacity(0.45);
            dc.DrawImage(image, new Rect(left, top, size, size));
            if (dim) dc.Pop();
        }

        // Etiketli: dairenin dışa bakan yanında (üst yarıda üstünde, öbürlerinde altında) adı; vurgulanınca vurgu renginde.
        if (style.Labels && !back && _slot is { } slot) DrawLabel(dc, slot, c, radius, hot || pressed, colors, metrics);

        // 1–9: hep yerinde (rozet fareyle belirmez), Kando'daki gibi öğenin ortaya bakan kenarında (alt halkanın noktaları hiç
        // o yönde değildir); Etiketli'de etiket alttadır, rozet sol üstte. Vurgulanınca yalnızca rengi değişir.
        if (_index is >= 0 and < 9 && _ring.ShowsKeys && _slot?.Kind != RingSlotKind.Add)
        {
            var badge = RingView.At(c, radius * 0.95, _ring.ItemAngle(_index) + 180);
            var r = BadgeRadius(metrics);
            dc.DrawEllipse(hot ? colors.BadgeHot : colors.Badge, colors.BadgeBorder, badge, r, r);
            var (digit, offset) = Digit(r * 1.2, hot ? colors.BadgeTextHot : colors.BadgeText, _ring.PixelsPerDipForText);
            dc.DrawText(digit, new Point(badge.X + offset.X, badge.Y + offset.Y));
        }
    }

    // Rozetin rakamı ve etiketin yazısı her çizimde yeniden kurulmaz (vurgu her fare hareketinde yeniden çizdirir): yazı, boyut,
    // fırça ve ölçekle anahtarlanır; biri değişince (yeniden kurma, palet, ekran ölçeği, vurgu) yenisi kurulur.
    private (string Text, double Size, Brush Brush, double Dpi, FormattedText Digit, Vector Offset)? _digit;
    private (string Text, bool Bold, Brush Brush, double Dpi, double Width, FormattedText Label)? _label;

    /// <summary>Rozetin rakamı ve rakamın çizilen kutusunu (satır yüksekliği değil) rozetin ortasına getiren kayma.</summary>
    private (FormattedText Digit, Vector Offset) Digit(double size, Brush brush, double dpi)
    {
        var text = (_index + 1).ToString(CultureInfo.InvariantCulture);
        if (_digit is { } d && d.Text == text && d.Size == size && ReferenceEquals(d.Brush, brush) && d.Dpi == dpi) return (d.Digit, d.Offset);
        var digit = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, RingCenterVisual.Bold, size, brush, dpi);
        var ink = digit.BuildGeometry(new Point(0, 0)).Bounds;
        var offset = ink.IsEmpty ? new Vector(-digit.Width / 2, -digit.Height / 2) : new Vector(-ink.X - ink.Width / 2, -ink.Y - ink.Height / 2);
        _digit = (text, size, brush, dpi, digit, offset);
        return (digit, offset);
    }

    /// <summary>Alt halka noktalarının daireden uzaklığı ve yarıçapı (DIP; öğe kutusunun payında kalır).</summary>
    public const double DotGap = 4.5, DotRadius = 2.6;

    /// <summary>Etiketin dikdörtgeni (öğe kutusunun koordinatında; kutunun dışına taşar, halka ona yer açar).</summary>
    internal static Rect LabelRect(Point center, RingMetrics metrics, double angle)
    {
        var (left, top, width, height) = RingStyles.LabelBox(angle, metrics.ItemDip);
        return new Rect(center.X + left, center.Y + top, width, height);
    }

    private void DrawLabel(DrawingContext dc, RingSlot slot, Point c, double radius, bool hot, RingColors colors, RingMetrics metrics)
    {
        var rect = LabelRect(c, metrics, _ring.ItemAngle(_index));
        var corner = rect.Height / 2;
        dc.DrawRoundedRectangle(hot ? colors.LabelHot : colors.Label, hot ? null : colors.LabelBorder, rect, corner, corner);
        var brush = hot ? colors.LabelTextHot : colors.LabelText;
        var dpi = _ring.PixelsPerDipForText;
        var width = Math.Max(1, rect.Width - 14);
        FormattedText text;
        if (_label is { } l && l.Text == slot.Name && l.Bold == hot && ReferenceEquals(l.Brush, brush) && l.Dpi == dpi && l.Width == width)
            text = l.Label;
        else
        {
            text = new FormattedText(slot.Name, L.Culture, FlowDirection.LeftToRight, hot ? RingCenterVisual.Bold : RingCenterVisual.Regular,
                11.5, brush, dpi)
            {
                MaxTextWidth = width, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
            };
            _label = (slot.Name, hot, brush, dpi, width, text);
        }
        var dim = slot.Dimmed;
        if (dim) dc.PushOpacity(0.6);
        dc.DrawText(text, new Point(rect.X + 7, rect.Y + (rect.Height - text.Height) / 2));
        if (dim) dc.Pop();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(RingItemVisual owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => "RingItem";
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override bool IsKeyboardFocusableCore() => false;
        protected override string GetNameCore() => owner.AccessibleName;

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        public void Invoke() => owner.Invoke();
    }
}

/// <summary>Halkanın ortası: daire, başlık ya da vurgulanan öğenin adı (daireye sığdırılır); alt halkada üstte geri oku.</summary>
internal sealed class RingCenterVisual : FrameworkElement
{
    private readonly RingView _ring;

    /// <summary>Odak halkasının orta daireden uzaklığı (DIP; dilimlerin iç kenarı orta dairenin 3 DIP dışında başlar).</summary>
    internal const double FocusGap = 2.5;

    internal static readonly Typeface Regular = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    internal static readonly Typeface Bold = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    public RingCenterVisual(RingView ring)
    {
        _ring = ring;
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAutomationId(this, "Ring.Center");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = _ring.Metrics.CenterRadius * 2;
        return new Size(size, size);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var colors = _ring.Scheme;
        var r = _ring.Metrics.CenterRadius;
        var c = new Point(r, r);
        var hot = _ring.CenterHot;
        var border = hot || _ring.CenterPressed ? colors.CenterBorderHot : _ring.Style.Glow ? colors.GlowCenterBorder : colors.CenterBorder;
        var inset = border.Thickness / 2;
        dc.DrawEllipse(_ring.CenterPressed ? colors.ChipPressed : hot ? colors.CenterHot : colors.Center, border, c, r - inset, r - inset);
        // Odak çerçevesi: klavye odağı halkadayken orta dairenin hemen dışında vurgu renginde ince bir halka (Windows'un odak
        // çerçevesi gibi, dairenin kendisi değişmez). Yalnızca klavyeden sonra; fareyle basınca kalkar.
        if (_ring.ShowsFocusCue) dc.DrawEllipse(null, colors.FocusBorder, c, r + FocusGap, r + FocusGap);

        var top = c.Y - r * 0.62;
        var bottom = c.Y + r * 0.62;
        if (_ring.CenterItemIcon is { } itemIcon)
        {
            // Vurgulanan öğe: üstte simgesi, altında adı (Kando'daki gibi orta nereye gidileceğini gösterir).
            var size = Math.Round(Math.Min(_ring.Metrics.IconDip * 0.8, r * 0.56));
            var y = c.Y - r * 0.70;
            var dim = _ring.HighlightedSlot?.Dimmed == true;
            if (dim) dc.PushOpacity(0.45);
            dc.DrawImage(itemIcon, new Rect(c.X - size / 2, y, size, size));
            if (dim) dc.Pop();
            top = y + size + 2;
        }
        else if (_ring.InSubRing && GlyphImage.For(RingSlot.BackGlyph, _ring.GlyphColor) is { } back)
        {
            // Alt halkada: üstte geri oku (ortaya basmak köke döner).
            var size = Math.Max(12, r * 0.28);
            dc.DrawImage(back, new Rect(c.X - size / 2, c.Y - r * 0.72, size, size));
            top = c.Y - r * 0.72 + size + 2;
        }

        if (_ring.CenterText is not { } text)
        {
            // Boştaki kök: widget'ın simgesi (ad üstteki satırda yazılı).
            if (_ring.CenterIcon is { } symbol && GlyphImage.For(symbol, _ring.GlyphColor) is { } icon)
            {
                var size = r * 0.85;
                dc.DrawImage(icon, new Rect(c.X - size / 2, c.Y - size / 2, size, size));
            }
            return;
        }
        var width = r * 2 * 0.80;
        var height = bottom - top;
        var formatted = Fit(text, width, height, hot ? colors.TextHot : colors.Text, _ring.PixelsPerDipForText);
        dc.DrawText(formatted, new Point(c.X - width / 2, top + (height - formatted.Height) / 2));
    }

    /// <summary>
    /// Yazıyı daireye sığdırır (HeaderFitter gibi): önce tek satırda (15'ten 12 DIP'e küçülterek), sonra sözcükler bölünmeden
    /// birkaç satırda (15'ten 10 DIP'e); hiçbiri sığmazsa en küçük boyutta üç noktayla kısaltılır.
    /// </summary>
    internal static FormattedText Fit(string text, double width, double height, Brush brush, double pixelsPerDip)
    {
        if (!text.Contains('\n'))
            for (var size = 15.0; size >= 12; size -= 1)
            {
                var line = Text(text, size, brush, pixelsPerDip, width);
                if (line.Height <= size * 1.5 && line.Height <= height) return line;
            }
        FormattedText? last = null;
        for (var size = 15.0; size >= 10; size -= 1)
        {
            var formatted = Text(text, size, brush, pixelsPerDip, width);
            if (formatted.Height <= height && formatted.MinWidth <= width + 0.5) return formatted;
            last = formatted;
        }
        last ??= Text(text, 10, brush, pixelsPerDip, width);
        last.MaxTextHeight = Math.Max(10, height);
        last.Trimming = TextTrimming.CharacterEllipsis;
        return last;
    }

    private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip, double width) =>
        new(text, L.Culture, FlowDirection.LeftToRight, size >= 13 ? Bold : Regular, size, brush, pixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, width), TextAlignment = TextAlignment.Center,
        };

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(RingCenterVisual owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        private bool Actionable => owner._ring.InSubRing || owner._ring.IsPopup;
        protected override AutomationControlType GetAutomationControlTypeCore() => Actionable ? AutomationControlType.Button : AutomationControlType.Text;
        protected override string GetClassNameCore() => "RingCenter";
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override bool IsKeyboardFocusableCore() => false;
        protected override string GetNameCore() => owner._ring.CenterText ?? owner._ring.CenterTitle;

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke && Actionable ? this : base.GetPattern(patternInterface);

        public void Invoke() => owner._ring.GoBack();
    }
}
