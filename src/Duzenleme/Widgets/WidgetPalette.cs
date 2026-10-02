using System.Windows;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public sealed record WidgetPalette(Brush Background, Brush BorderBrush, Brush Foreground, Brush Secondary, Brush Accent, Brush AccentForeground, bool TextShadow, bool IsLight)
{
    public static SolidColorBrush Solid(uint argb)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        b.Freeze();
        return b;
    }

    public static LinearGradientBrush Gradient(uint from, uint to)
    {
        var b = new LinearGradientBrush(
            Color.FromArgb((byte)(from >> 24), (byte)(from >> 16), (byte)(from >> 8), (byte)from),
            Color.FromArgb((byte)(to >> 24), (byte)(to >> 16), (byte)(to >> 8), (byte)to),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        b.Freeze();
        return b;
    }

    /// <summary>Koyu, yarı saydam cam: her duvar kağıdında okunur.</summary>
    public static readonly WidgetPalette Glass = new(
        Gradient(0x70141420, 0x4A0C0C14), Solid(0x38FFFFFF), Solid(0xFFFFFFFF), Solid(0xC8FFFFFF),
        Solid(0xFFC4B5FD), Solid(0xFF1E1B2E), TextShadow: true, IsLight: false);

    // Koyu, Açık ve Grafit tam opaktır: katmanlı pencerede yazı ancak opak zemin üstünde ClearType ile (renkli alt
    // piksellerle) çizilebilir; %95 opak zemin gözle fark edilmiyordu ama yazıyı gri tonlamalı (daha bulanık) bırakıyordu.
    public static readonly WidgetPalette Dark = new(
        Gradient(0xFF202029, 0xFF18181F), Solid(0x1FFFFFFF), Solid(0xFFF4F4F6), Solid(0xFF9D9DA8),
        Solid(0xFFA78BFA), Solid(0xFF16131F), TextShadow: false, IsLight: false);

    public static readonly WidgetPalette Light = new(
        Gradient(0xFFFFFFFF, 0xFFF5F3FF), Solid(0x18000000), Solid(0xFF1C1C21), Solid(0xFF6B6B76),
        Solid(0xFF7C3AED), Solid(0xFFFFFFFF), TextShadow: false, IsLight: true);

    /// <summary>Vurgu renkleri (<see cref="ThemeMath.AccentArgb"/>): koyu zeminde açık ton, açık zeminde koyu ton.</summary>
    public static uint AccentArgb(WidgetAccent accent, bool light) => ThemeMath.AccentArgb(accent, light);

    public static WidgetPalette For(WidgetStyle style, WidgetAccent accent = WidgetAccent.Violet)
    {
        var basePalette = style switch
        {
            WidgetStyle.Dark => Dark,
            WidgetStyle.Light => Light,
            _ => Glass,
        };
        return basePalette with { Accent = Solid(AccentArgb(accent, basePalette.IsLight)) };
    }

    /// <summary>Yapışkan not renkleri (SlideSlide'daki yüzen notlar gibi).</summary>
    public static WidgetPalette ForNote(NoteColor color) => color switch
    {
        NoteColor.Pink => NotePalette(0xFFFFD6E7, 0xFFFFC2DA, 0xFFBE185D),
        NoteColor.Green => NotePalette(0xFFD9F7D2, 0xFFC4EFBA, 0xFF15803D),
        NoteColor.Blue => NotePalette(0xFFD6E8FF, 0xFFC0DBFF, 0xFF1D4ED8),
        NoteColor.Purple => NotePalette(0xFFE9DEFF, 0xFFDACBFF, 0xFF6D28D9),
        NoteColor.Graphite => new(Gradient(0xFF2A2A33, 0xFF20202A), Solid(0x24FFFFFF), Solid(0xFFF4F4F6), Solid(0xFFA1A1AA),
            Solid(0xFFFDE68A), Solid(0xFF1C1917), TextShadow: false, IsLight: false),
        _ => NotePalette(0xFFFFF4B8, 0xFFFFEC99, 0xFFA16207),
    };

    private static WidgetPalette NotePalette(uint top, uint bottom, uint accent) =>
        new(Gradient(top, bottom), Solid(0x14000000), Solid(0xFF26221C), Solid(0x99302A20),
            Solid(accent), Solid(0xFFFFFFFF), TextShadow: false, IsLight: true);

    /// <summary>Zemin tamamen opak mı (ClearType yalnızca opak zeminde çalışır)? Cam yarı saydamdır.</summary>
    public bool IsOpaqueBackground => Background switch
    {
        SolidColorBrush s => s.Color.A == 255 && s.Opacity >= 1,
        GradientBrush g => g.Opacity >= 1 && g.GradientStops.Count > 0 && g.GradientStops.All(x => x.Color.A == 255),
        _ => false,
    };

    // ---------------------------------------------------------------- 2.2 Tema

    /// <summary>
    /// Windows'un yüksek karşıtlık renkleriyle kurulmuş palet (<see cref="HighContrast()"/>): gölge, cam saydamlığı, yazı
    /// gölgesi ve yarı saydam katmanlar kullanılmaz; widget'lar Windows'un renkleriyle çizilir.
    /// </summary>
    public bool IsHighContrast { get; init; }

    /// <summary>
    /// Windows'un uygulama kipi koyu mu ("Otomatik" tema ve iki hâlli temalar buna uyar). <c>App.ApplyTheme</c> günceller;
    /// değişince <see cref="WidgetManager.RestyleAll"/> ilgili widget'ları yeniden çizer.
    /// </summary>
    public static bool SystemDark { get; set; }

    /// <summary>
    /// Widget'ın paleti için tek çözücü: yüksek karşıtlık her şeyi (temaları, notları) ezer; sonra tema ("auto" ya da hazır
    /// tema), yoksa klasik arka plan; vurgu rengi özel renk ya da adlı renk. <paramref name="preview"/> menüde ya da renk
    /// seçicide üstüne gelinen (kaydedilmemiş) seçimdir: arka plan önizlemesi temayı, adlı renk önizlemesi özel rengi yok sayar
    /// (seçilseler öyle olacağı gibi). Not kağıdı burada değil, görünümün <c>AdjustPalette</c>'inde.
    /// </summary>
    internal static WidgetPalette Resolve(WidgetConfig config, LookOverride? preview, bool systemDark, bool highContrast)
    {
        if (highContrast) return HighContrast();
        string? theme = config.LookTheme, hex = config.LookAccentColor;
        var style = config.Style;
        var accent = config.Accent;
        if (preview is { } p)
        {
            if (p.Theme is { } t) theme = t;
            if (p.Style is { } s)
            {
                style = s;
                theme = null;
            }
            if (p.Accent is { } a)
            {
                accent = a;
                hex = null;
            }
            if (p.AccentColor is { } h) hex = h;
        }
        var key = new PaletteKey(ThemePresets.Known(theme), style, accent, ThemeMath.NormalizeHex(hex), systemDark);
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var palette = Build(key);
            if (Cache.Count > 256) Cache.Clear();
            Cache[key] = palette;
            return palette;
        }
    }

    /// <summary>
    /// Bir widget'ın kartı: <see cref="Resolve"/>, sonra görünümün düzeltmesi (<paramref name="adjust"/>: notun kağıt rengi) ya
    /// da menüde önizlenen not rengi. Yüksek karşıtlıkta düzeltme yapılmaz: Windows'un renkleri notları da ezer.
    /// </summary>
    internal static WidgetPalette ForWidget(WidgetConfig config, LookOverride? preview, bool systemDark, bool highContrast,
        Func<WidgetPalette, WidgetPalette> adjust)
    {
        var palette = Resolve(config, preview, systemDark, highContrast);
        if (palette.IsHighContrast) return palette;
        return preview?.Note is { } note ? ForNote(note) : adjust(palette);
    }

    private readonly record struct PaletteKey(string? Theme, WidgetStyle Style, WidgetAccent Accent, string? Hex, bool SystemDark);

    private static readonly Dictionary<PaletteKey, WidgetPalette> Cache = [];

    private static WidgetPalette Build(PaletteKey key)
    {
        uint? custom = ThemeMath.TryParse(key.Hex, out var argb) ? argb : null;
        if (ThemePresets.Find(key.Theme) is { } preset)
        {
            var colors = preset.Variant(key.SystemDark);
            // Temanın adlı vurgu rengi de onun zeminine göre okunur kılınır (kendi rengi doğrulanmıştır).
            return FromColors(colors, custom ?? AccentArgb(key.Accent, colors.IsLight));
        }
        var basePalette = key.Theme == ThemePresets.Auto ? (key.SystemDark ? Dark : Light) : key.Style switch
        {
            WidgetStyle.Dark => Dark,
            WidgetStyle.Light => Light,
            _ => Glass,
        };
        if (custom is not { } color) return basePalette with { Accent = Solid(AccentArgb(key.Accent, basePalette.IsLight)) };
        var shown = ThemeMath.EnsureContrast(color, StopsOf(basePalette.Background), ThemeMath.AccentContrast);
        return basePalette with { Accent = Solid(shown), AccentForeground = Solid(ThemeMath.TextOn(shown)) };
    }

    /// <summary>
    /// Tema renklerinden palet: iki duraklı dondurulmuş degrade zemin (resim yok, efekt yok). Vurgu rengi zemine karşı en az
    /// 3:1 olacak biçimde gerekirse açılır/koyulaştırılır; üstündeki yazı beyaz ya da koyu, hangisi okunursa.
    /// </summary>
    public static WidgetPalette FromColors(ThemeColors colors, uint accent)
    {
        var shown = ThemeMath.EnsureContrast(accent, colors.Stops, ThemeMath.AccentContrast);
        return new WidgetPalette(Gradient(colors.BackgroundFrom, colors.BackgroundTo), Solid(colors.Border), Solid(colors.Text),
            Solid(colors.Secondary), Solid(shown), Solid(ThemeMath.TextOn(shown)), colors.TextShadow, colors.IsLight);
    }

    private static uint[] StopsOf(Brush background) => background switch
    {
        SolidColorBrush s => [Argb(s.Color)],
        GradientBrush g => g.GradientStops.Select(x => Argb(x.Color)).ToArray(),
        _ => [],
    };

    private static uint Argb(Color c) => (uint)c.A << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;

    /// <summary>
    /// Widget'ların baktığı Windows durumu: açık/koyu kip, yüksek karşıtlık ve (yüksek karşıtlıkta) Windows'un renkleri.
    /// Bir yüksek karşıtlık temasından ötekine geçerken (Gece gökyüzü → Çöl) ilk ikisi değişmez, yalnızca renkler değişir.
    /// </summary>
    internal readonly record struct SystemLook(bool Dark, bool HighContrast, string HighContrastColors)
    {
        /// <summary>Şu anki durum (<see cref="SystemColors"/> okunur; yüksek karşıtlık kapalıyken renkler yok sayılır).</summary>
        public static SystemLook Current(bool dark, bool highContrast) => new(dark, highContrast, highContrast
            ? string.Join(",", new[]
            {
                SystemColors.WindowColor, SystemColors.WindowTextColor, SystemColors.HighlightColor, SystemColors.HighlightTextColor,
                SystemColors.HotTrackColor, SystemColors.GrayTextColor,
            }.Select(c => c.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            : "");

        /// <summary>
        /// <paramref name="before"/>'dan <paramref name="now"/>'a geçişte widget'lar yeniden çizilmeli mi, hepsi mi? İlk bakışta
        /// hiçbiri (pencereler zaten şu anki durumla açılır). Yüksek karşıtlık açılıp kapanınca ya da açıkken renkleri değişince
        /// hepsi; yalnızca açık/koyu değişince Windows'a uyanlar (<see cref="WidgetManager.RestyleAll"/>).
        /// </summary>
        public static (bool Restyle, bool All) Change(SystemLook? before, SystemLook now)
        {
            if (before is not { } b || b == now) return (false, false);
            var all = b.HighContrast != now.HighContrast || now.HighContrast && b.HighContrastColors != now.HighContrastColors;
            return (true, all);
        }
    }

    private static WidgetPalette? _highContrast;
    private static (Color, Color, Color, Color) _highContrastKey;

    /// <summary>
    /// Yüksek karşıtlık paleti, Windows'un renklerinden (Pencere, Pencere yazısı, Vurgulama, Vurgulanan yazı): temaları ve
    /// not renklerini ezer. Zemin opak ve düz; gölge ve yazı gölgesi yok.
    /// </summary>
    public static WidgetPalette HighContrast()
    {
        var key = (SystemColors.WindowColor, SystemColors.WindowTextColor, SystemColors.HighlightColor, SystemColors.HighlightTextColor);
        if (_highContrast is { } cached && _highContrastKey == key) return cached;
        var (window, text, highlight, highlightText) = key;
        var palette = new WidgetPalette(Frozen(window), Frozen(text), Frozen(text), Frozen(text), Frozen(highlight), Frozen(highlightText),
            TextShadow: false, IsLight: ThemeMath.Luminance(Argb(window)) > 0.5) { IsHighContrast = true };
        (_highContrast, _highContrastKey) = (palette, key);
        return palette;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    // ---------------------------------------------------------------- widget içi küçük yüzeyler

    private static readonly Brush ClearBrush = Solid(0x00000000);
    private static readonly Brush OverlayDefault = Solid(0x55100C20);

    /// <summary>Hafif beyaz katman (sekme, simge düğmesi); yüksek karşıtlıkta yok (saydam).</summary>
    public Brush Tint(byte alpha) => IsHighContrast ? ClearBrush : Solid((uint)alpha << 24 | 0xFFFFFF);

    /// <summary>Arama kutusu gibi alanların zemini; yüksek karşıtlıkta kartın kendi zemini.</summary>
    public Brush Field(byte alpha) => IsHighContrast ? Background : Solid((uint)alpha << 24 | 0xFFFFFF);

    /// <summary>"Bırak, …" katmanının zemini; yüksek karşıtlıkta kartın opak zemini (yazı okunur kalır).</summary>
    public Brush Overlay => IsHighContrast ? Background : OverlayDefault;
}
