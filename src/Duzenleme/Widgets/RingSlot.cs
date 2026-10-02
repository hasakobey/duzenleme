using System.ComponentModel;
using System.Windows.Media;
using Duzenleme.Core;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Duzenleme.Widgets;

/// <summary>Halkadaki bir yerin türü (kalıcı değil).</summary>
internal enum RingSlotKind { Path, Action, SubRing }

/// <summary>
/// Halkadaki bir yer: sekmenin bir öğesi (yol ya da yol olmayan öğe; <see cref="LauncherEntries"/> sırasıyla) ya da kök halkada
/// bir alt halka (<c>Tabs[1..]</c>, kökün sonunda; içindeki öğeler Kando'daki gibi küçük noktalarla gösterilir). Model saftır
/// (<see cref="Build"/> testlenir); simge görünüm bağlanınca yüklenir (<see cref="LoadIcon"/>): yol <see cref="TileItem"/> ile
/// (durumu ve simgesi arka planda), yol olmayan öğe kendi simgesiyle ya da türünün Fluent simgesiyle (hepsi BMP içinde).
/// Çalıştırılamayan öğe (tanınmayan tür, onaylanmamış içe aktarma, bulunamayan yol) soluk çizilir.
/// </summary>
internal sealed class RingSlot
{
    public required RingSlotKind Kind { get; init; }

    /// <summary>Sekmedeki görünen sırası (yol ve eylem); alt halkada -1.</summary>
    public int EntryIndex { get; init; } = -1;

    /// <summary>Alt halka: <see cref="WidgetConfig.Tabs"/> içindeki sırası (1…); öğede -1.</summary>
    public int TabIndex { get; init; } = -1;

    public string? Path { get; init; }
    public LauncherAction? Action { get; init; }

    /// <summary>Görünen ad (kullanıcının verdiği ya da türün/dosyanın adı).</summary>
    public required string Name { get; init; }

    /// <summary>Alt halkadaki öğe sayısı (küçük noktalar; en çok <see cref="MaxDots"/>).</summary>
    public int ChildCount { get; init; }

    /// <summary>Kullanıcının seçtiği simge (<see cref="IconRef"/>); yoksa null.</summary>
    public string? IconRef { get; init; }

    /// <summary>Türün varsayılan Fluent simgesi (yol olmayan öğe ve alt halka); yolda null (dosyanın kendi simgesi).</summary>
    public SymbolRegular? Glyph { get; init; }

    /// <summary>Yol olmayan öğe çalıştırılabilir mi (<see cref="RingActions.IsRunnable"/>); yol ve alt halkada true.</summary>
    public bool Runnable { get; init; } = true;

    /// <summary>Alt halkanın noktaları en çok bu kadar.</summary>
    public const int MaxDots = 8;

    /// <summary>Yol: kutucuğu (durumu ve simgesi); görünüm bağlanınca kurulur.</summary>
    public TileItem? Tile { get; private set; }

    /// <summary>Çizilecek simge (yüklenene dek null).</summary>
    public ImageSource? Icon { get; private set; }

    /// <summary>Soluk mu: çalıştırılamayan öğe ya da bulunamayan / ulaşılamayan yol.</summary>
    public bool Dimmed => !Runnable || Tile is { Missing: true };

    /// <summary>Simge ya da durum değişti: görünüm bu yeri yeniden çizer.</summary>
    public event Action? Changed;

    // ---------------------------------------------------------------- saf kurulum

    /// <summary>
    /// Sekmenin yerleri: öğeler görünen sırasıyla; kök halkada (<paramref name="tabIndex"/> 0) arkasından alt halkalar. Sekme
    /// yoksa boş.
    /// </summary>
    public static List<RingSlot> Build(WidgetConfig config, int tabIndex)
    {
        var slots = new List<RingSlot>();
        if (tabIndex < 0 || tabIndex >= config.Tabs.Count || config.Tabs[tabIndex] is not { } tab) return slots;
        var looks = ItemLooks.Lookup(config);
        var entries = LauncherEntries.Ordered(tab);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Path is { } path)
            {
                looks.TryGetValue(path, out var look);
                slots.Add(new RingSlot
                {
                    Kind = RingSlotKind.Path, EntryIndex = i, Path = path,
                    Name = string.IsNullOrWhiteSpace(look?.Name) ? DefaultPathName(path) : look.Name.Trim(), IconRef = look?.Icon,
                });
            }
            else if (entry.Action is { } action)
                slots.Add(new RingSlot
                {
                    Kind = RingSlotKind.Action, EntryIndex = i, Action = action,
                    Name = string.IsNullOrWhiteSpace(action.Name) ? DefaultName(action) : action.Name.Trim(), IconRef = action.Icon,
                    Glyph = DefaultGlyph(action), Runnable = RingActions.IsRunnable(action),
                });
        }
        if (tabIndex != 0) return slots;
        for (var t = 1; t < config.Tabs.Count; t++)
        {
            if (config.Tabs[t] is not { } sub) continue;
            slots.Add(new RingSlot
            {
                Kind = RingSlotKind.SubRing, TabIndex = t, Name = SubRingName(sub), Glyph = SubRingGlyph,
                ChildCount = LauncherEntries.Count(sub),
            });
        }
        return slots;
    }

    /// <summary>Alt halkanın görünen adı (boşsa "Alt halka").</summary>
    public static string SubRingName(LauncherTab tab) => string.IsNullOrWhiteSpace(tab.Name) ? L.T("Alt halka") : tab.Name.Trim();

    /// <summary>Yolun görünen adı: dosya adı (kısayolda uzantısız) ya da kabuk nesnesinin adı.</summary>
    public static string DefaultPathName(string path) => TileItem.IsShellObject(path) ? TileItem.ShellName(path) : TileItem.DisplayName(path);

    /// <summary>Yol olmayan öğenin varsayılan adı (kullanıcı ad vermediyse), arayüz dilinde.</summary>
    public static string DefaultName(LauncherAction action)
    {
        switch (action.Type)
        {
            case RingActionTypes.Url:
                if (!RingActions.TryParseUrl(action.Url, out var uri, out _)) return L.T("Web adresi");
                if (uri.Scheme == "mailto") return Shorten(Uri.UnescapeDataString(uri.AbsoluteUri["mailto:".Length..]));
                return Shorten(string.IsNullOrEmpty(uri.Host) ? uri.AbsoluteUri : uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host);
            case RingActionTypes.Command:
                // Komut satırının program parçası (diske dokunmadan): "ping 8.8.8.8" → "ping".
                var program = RingActions.SplitCommand(action.Command).Program;
                var file = program.Length == 0 ? "" : System.IO.Path.GetFileNameWithoutExtension(program.Replace('/', '\\').Split('\\')[^1]);
                return file.Length > 0 ? Shorten(file) : L.T("Komut");
            case RingActionTypes.Keys:
                return action.Keys is { Count: > 0 } keys ? Shorten(string.Join(", ", keys)) : L.T("Tuş kısayolu");
            case RingActionTypes.Text:
                // Metnin kendisi (ilk satırı bile) ad olmaz: masaüstünde, ekran okuyucuda ve bildirimde görünürdü.
                return L.T("Metin");
            case RingActionTypes.App:
                return RingAppIds.Label(action.App) ?? L.F("{0} eylemi", AppInfo.Name);
            default:
                return L.T("Bilinmeyen öğe");
        }
    }

    private static string Shorten(string text) => text.Length > 32 ? text[..31] + "…" : text;

    /// <summary>Türün varsayılan simgesi (hepsi BMP içinde: <see cref="WidgetIcons.IsDrawable"/>).</summary>
    public static SymbolRegular DefaultGlyph(LauncherAction action) => action.Type switch
    {
        RingActionTypes.Url => SymbolRegular.Globe24,
        RingActionTypes.Command => SymbolRegular.WindowConsole20,
        RingActionTypes.Keys => SymbolRegular.Keyboard24,
        RingActionTypes.Text => SymbolRegular.ClipboardPaste24,
        RingActionTypes.App => AppGlyph(action.App),
        _ => SymbolRegular.QuestionCircle24,
    };

    /// <summary>NestDesk eyleminin simgesi.</summary>
    public static SymbolRegular AppGlyph(string? id) => id switch
    {
        RingAppIds.NewNote => SymbolRegular.NoteAdd24,
        RingAppIds.PeekDesktop => SymbolRegular.Eye24,
        RingAppIds.QuickAdd => SymbolRegular.AddSquare24,
        RingAppIds.OpenApp => SymbolRegular.Home24,
        _ => SymbolRegular.QuestionCircle24,
    };

    /// <summary>Alt halkanın simgesi.</summary>
    public const SymbolRegular SubRingGlyph = SymbolRegular.AppFolder24;

    /// <summary>Alt halkadaki "geri" yerinin simgesi.</summary>
    public const SymbolRegular BackGlyph = SymbolRegular.ArrowLeft24;

    /// <summary>Bütün varsayılan simgeler (test: hepsi çizilebilir).</summary>
    public static IEnumerable<SymbolRegular> AllGlyphs =>
        RingActionTypes.All.Select(t => DefaultGlyph(new LauncherAction { Type = t }))
            .Concat(RingAppIds.All.Select(AppGlyph))
            .Concat([SymbolRegular.QuestionCircle24, SubRingGlyph, BackGlyph]);

    // ---------------------------------------------------------------- simge

    private int _pixels;
    private double _pixelsPerDip;
    private double _scale = 1;
    private Color _glyphColor;
    private bool _custom;

    /// <summary>
    /// Simgeyi ekrandaki piksel boyutunda ister (değişmediyse bir şey yapmaz). Yol: kutucuk kurulur, durumu ve simgesi
    /// arka planda gelir. Yol olmayan öğe: seçilen simge ("sym:" vektör, "res:"/"img:" arka planda), yoksa programı gösteren
    /// komutta programın simgesi (yol arka planda denetlenir), yoksa türün Fluent simgesi <paramref name="glyphColor"/>'da.
    /// </summary>
    public void LoadIcon(WidgetConfig config, double iconDip, double pixelsPerDip, double widgetScale, Color glyphColor)
    {
        var dpi = pixelsPerDip > 0 ? pixelsPerDip : NativeMethods.SystemPixelsPerDip;
        var pixels = IconSizing.DevicePixels(iconDip, dpi, widgetScale);
        var colorChanged = glyphColor != _glyphColor;
        _pixelsPerDip = dpi;
        _scale = widgetScale;
        _glyphColor = glyphColor;
        if (Kind == RingSlotKind.Path && Path is { } path)
        {
            if (Tile is null)
            {
                Tile = TileItem.CreateUnchecked(path, config, Name, dpi, IconRef);
                Tile.PropertyChanged += OnTileChanged;
                Icon = Tile.Icon;
            }
            else Tile.UpdateIconSize(dpi, widgetScale);
            return;
        }
        if (pixels == _pixels && !colorChanged && Icon is not null) return;
        _pixels = pixels;
        if (Core.IconRef.Parse(IconRef) is { } custom && LoadCustom(custom, pixels)) return;
        _custom = false;
        SetIcon(Glyph is { } glyph ? GlyphImage.For(glyph, glyphColor) : null);
        if (Action is { Type: RingActionTypes.Command, Command: { } command } && ProgramPath(command) is { } program)
            LoadProgramIcon(program, pixels);
    }

    private void OnTileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(TileItem.Icon) or nameof(TileItem.Missing))) return;
        Icon = Tile?.Icon;
        Changed?.Invoke();
    }

    private void SetIcon(ImageSource? icon)
    {
        if (ReferenceEquals(Icon, icon)) return;
        Icon = icon;
        Changed?.Invoke();
    }

    /// <summary>Seçilen simge; yüklenemeyecekse (dosya yok, simge değil) false: türün simgesine dönülür.</summary>
    private bool LoadCustom(IconRef custom, int pixels)
    {
        switch (custom.Kind)
        {
            case IconRefKind.Symbol:
                if (WidgetIcons.Symbol(custom.ToString()) is not { } symbol || GlyphImage.For(symbol, _glyphColor) is not { } glyph) return false;
                _custom = true;
                SetIcon(glyph);
                return true;
            case IconRefKind.Resource:
                _custom = true;
                if (ShellIcons.TryCachedResource(custom.Value, custom.Index, pixels, out var cached)) ApplyCustom(cached, pixels);
                else ShellIcons.RequestResource(custom.Value, custom.Index, pixels, icon => ApplyCustom(icon, pixels));
                return true;
            default:
                if (IconFiles.PathOf(custom.Value, AppHost.IconsDirectory) is not { } file) return false;
                _custom = true;
                if (ShellIcons.TryCachedImage(file, pixels, out var image)) ApplyCustom(image, pixels);
                else ShellIcons.RequestImage(file, pixels, icon => ApplyCustom(icon, pixels));
                return true;
        }
    }

    private void ApplyCustom(ImageSource? icon, int pixels)
    {
        if (!_custom || pixels != _pixels) return;
        if (icon is not null)
        {
            SetIcon(icon);
            return;
        }
        // Yüklenemedi (dosya silinmiş, kaynak yok): sessizce türün simgesine dönülür.
        _custom = false;
        SetIcon(Glyph is { } glyph ? GlyphImage.For(glyph, _glyphColor) : null);
    }

    /// <summary>Komut tam yollu bir programı gösteriyorsa onun yolu (simgesi için); değilse null. Diske bakmaz.</summary>
    public static string? ProgramPath(string command)
    {
        var text = command.Trim().Trim('"');
        if (text.Length < 4 || !System.IO.Path.IsPathFullyQualified(text) || text.StartsWith(@"\\", StringComparison.Ordinal)) return null;
        var ext = System.IO.Path.GetExtension(text).ToLowerInvariant();
        return ext is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc" or ".com" ? text : null;
    }

    /// <summary>Programın simgesi: yol arka planda denetlenir (ulaşılamayan sürücüde beklemez), varsa kabuktan istenir.</summary>
    private void LoadProgramIcon(string program, int pixels)
    {
        var native = TileItem.NativePath(program);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        PathProbe.Shared.CheckAsync(native).ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully || t.Result is not { Exists: true, IsDirectory: false }) return;
            dispatcher?.BeginInvoke(() =>
            {
                if (_custom || pixels != _pixels) return;
                if (ShellIcons.TryCached(native, pixels, false, false, out var cached) && cached is not null) SetIcon(cached);
                else ShellIcons.Request(native, pixels, false, false, icon =>
                {
                    if (icon is not null && !_custom && pixels == _pixels) SetIcon(icon);
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }, TaskScheduler.Default);
    }

    // ---------------------------------------------------------------- önizleme (iletişim penceresi)

    /// <summary>
    /// Yol olmayan öğenin simge önizlemesi: verilen simgeyle (null: türün simgesi) geçici bir yer kurulur, simgesi gelince
    /// bildirilir. Dönen nesne bırakılınca (pencere kapanınca, simge değişince) bir daha bildirmez.
    /// </summary>
    public static Func<string?, double, Color, Action<ImageSource?>, IDisposable?> Preview(LauncherAction action, WidgetConfig config) =>
        (icon, size, color, show) => Watch(new RingSlot
        {
            Kind = RingSlotKind.Action, Action = action, Name = "", IconRef = icon, Glyph = DefaultGlyph(action),
        }, config, size, color, show);

    /// <summary>Yol öğesinin simge önizlemesi (seçilen simge ya da dosyanın kendi simgesi).</summary>
    public static Func<string?, double, Color, Action<ImageSource?>, IDisposable?> Preview(string path, WidgetConfig config) =>
        (icon, size, color, show) => Watch(new RingSlot { Kind = RingSlotKind.Path, Path = path, Name = "", IconRef = icon }, config, size, color, show);

    private static IDisposable Watch(RingSlot slot, WidgetConfig config, double size, Color color, Action<ImageSource?> show)
    {
        slot.Changed += () => show(slot.Icon);
        slot.LoadIcon(config, size, 0, 1, color);
        show(slot.Icon);
        return new Releaser(slot);
    }

    private sealed class Releaser(RingSlot slot) : IDisposable
    {
        public void Dispose() => slot.Release();
    }

    /// <summary>Görünüm bırakıldı: kutucuğun olaylarından çıkar (eski yer bir daha çizilmez).</summary>
    public void Release()
    {
        if (Tile is not null) Tile.PropertyChanged -= OnTileChanged;
        Changed = null;
        _custom = false;
        _pixels = -1;
    }
}
