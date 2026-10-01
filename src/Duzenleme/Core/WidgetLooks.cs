namespace Duzenleme.Core;

/// <summary>
/// Widget'ın görünüş seçimlerinin öncelik kuralları (menü, Widget'lar sayfası ve renk seçici aynı yoldan yazar; saf, testli):
/// <list type="bullet">
/// <item>Tema seçmek vurgu rengini de temanınkine çevirir: hazır temada temanın rengi özel renk olarak yazılır; Klasik ve
/// Otomatik'te özel renk silinir (adlı vurgu rengi geçerli olur).</item>
/// <item>Klasik bir "Arka plan" (Cam/Koyu/Açık) seçmek temayı kaldırır; tema açıkken "Arka plan"da hiçbiri işaretli değildir.</item>
/// <item>Adlı bir vurgu rengi seçmek özel rengi siler; özel renk seçmek adlı rengi en yakın değere yazar.</item>
/// <item>2.0/2.1 temayı ve özel rengi tanımaz: <see cref="WidgetConfig.Style"/> ve <see cref="WidgetConfig.Accent"/> her
/// seçimde en yakın değerde tutulur (SortBy → Sort gibi).</item>
/// </list>
/// </summary>
public static class WidgetLooks
{
    /// <summary>Şu an geçerli tema (tanınan ad, küçük harfle); klasik ya da tanınmayan ad null.</summary>
    public static string? ActiveTheme(WidgetConfig c) => ThemePresets.Known(c.LookTheme);

    /// <summary>Menüdeki "Arka plan" işareti: tema açıkken hiçbiri.</summary>
    public static WidgetStyle? ShownStyle(WidgetConfig c) => ActiveTheme(c) is null ? c.Style : null;

    /// <summary>Menüdeki adlı vurgu rengi işareti: özel renk seçiliyse hiçbiri.</summary>
    public static WidgetAccent? ShownAccent(WidgetConfig c) => CustomAccent(c) is null ? c.Accent : null;

    /// <summary>Geçerli özel vurgu rengi ("#RRGGBB"); yoksa ya da okunamıyorsa null.</summary>
    public static string? CustomAccent(WidgetConfig c) => ThemeMath.NormalizeHex(c.LookAccentColor);

    /// <summary>Temanın kendi vurgu rengi ("#RRGGBB"): yalnızca hazır temalarda; Klasik ve Otomatik'te null.</summary>
    public static string? ThemeAccent(string? theme) =>
        ThemePresets.Find(theme) is { } preset ? ThemeMath.ToHex(preset.Accent) : null;

    /// <summary>
    /// Tema seçer (null ya da "" = Klasik). Vurgu rengi temanınki olur; Style ve Accent 2.1 için en yakın değere yazılır
    /// (Otomatik'te Windows'un şimdiki hâline göre).
    /// </summary>
    public static void SetTheme(WidgetConfig c, string? theme, bool systemDark)
    {
        var known = ThemePresets.Known(theme);
        c.LookTheme = known;
        c.LookAccentColor = ThemeAccent(known);
        if (known == ThemePresets.Auto) c.Style = systemDark ? WidgetStyle.Dark : WidgetStyle.Light;
        else if (ThemePresets.Find(known) is { } preset)
        {
            c.Style = ThemePresets.NearestStyle(preset.Variant(systemDark));
            c.Accent = ThemeMath.NearestAccent(preset.Accent);
        }
    }

    /// <summary>Klasik arka plan: temayı kaldırır (vurgu rengi olduğu gibi kalır).</summary>
    public static void SetStyle(WidgetConfig c, WidgetStyle style)
    {
        c.Style = style;
        c.LookTheme = null;
    }

    /// <summary>Adlı vurgu rengi: özel rengi siler.</summary>
    public static void SetAccent(WidgetConfig c, WidgetAccent accent)
    {
        c.Accent = accent;
        c.LookAccentColor = null;
    }

    /// <summary>Özel vurgu rengi ("#RRGGBB"); okunamayan renk hiçbir şeyi değiştirmez. Accent en yakın adlı renge yazılır.</summary>
    public static bool SetAccentColor(WidgetConfig c, string? hex)
    {
        if (ThemeMath.NormalizeHex(hex) is not { } normalized) return false;
        c.LookAccentColor = normalized;
        ThemeMath.TryParse(normalized, out var argb);
        c.Accent = ThemeMath.NearestAccent(argb);
        return true;
    }

    /// <summary>Görünüşü Windows'un açık/koyu ayarına göre değişir mi (Windows teması değişince yeniden çizilmeli)?</summary>
    public static bool FollowsWindows(WidgetConfig c) => ThemePresets.FollowsWindows(c.LookTheme);
}
