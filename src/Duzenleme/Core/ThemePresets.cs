namespace Duzenleme.Core;

/// <summary>
/// Bir temanın tek bir açık ya da koyu hâli: iki duraklı zemin degradesi, kenarlık, yazı ve ikincil yazı. Yalnızca renk
/// verisi (resim, yazı tipi, betik yok). Vurgu rengi temanın kendisindedir (<see cref="ThemePreset.Accent"/>).
/// </summary>
public sealed record ThemeColors(uint BackgroundFrom, uint BackgroundTo, uint Border, uint Text, uint Secondary, bool TextShadow = false)
{
    /// <summary>Zeminin iki durağı da (neredeyse) opak mı? Opak zeminde yazı ClearType ile çizilebilir.</summary>
    public bool IsOpaque => BackgroundFrom >> 24 >= 0xE0 && BackgroundTo >> 24 >= 0xE0;

    /// <summary>Zemin açık renk mi (koyu yazı)?</summary>
    public bool IsLight => Stops.Average(ThemeMath.Luminance) > 0.5;

    /// <summary>Karşıtlık hesabındaki zemin durakları (saydam zemin koyu cam gibi, siyahın üstünde okunur).</summary>
    public IReadOnlyList<uint> Stops => [ThemeMath.Opaque(BackgroundFrom), ThemeMath.Opaque(BackgroundTo)];
}

/// <summary>
/// Paketlenen tema: özgün renkler, adı <see cref="L.N"/> anahtarı (<see cref="Label"/> çevirir). İki hâli olan tema
/// Windows'un açık/koyu ayarına uyar. Vurgu rengi iki hâlde de aynıdır: tema seçilince widget'ın vurgu rengi olarak
/// yazılır (<see cref="WidgetLooks.SetTheme"/>), sonra kullanıcı başka bir renk seçebilir.
/// </summary>
public sealed record ThemePreset(string Id, string Name, uint Accent, ThemeColors? Light, ThemeColors? Dark)
{
    /// <summary>Görünen ad (arayüz dilinde).</summary>
    public string Label => L.Dyn(Name);

    /// <summary>Windows'un açık/koyu ayarına uyar mı (iki hâli de var mı)?</summary>
    public bool FollowsWindows => Light is not null && Dark is not null;

    /// <summary>Gösterilecek hâl: iki hâli varsa Windows'unkine göre.</summary>
    public ThemeColors Variant(bool systemDark) =>
        Light is null ? Dark! : Dark is null ? Light : systemDark ? Dark : Light;

    public IEnumerable<ThemeColors> Variants => new[] { Light, Dark }.OfType<ThemeColors>();
}

/// <summary>
/// Widget temaları (<see cref="WidgetConfig.LookTheme"/>): null = klasik (Arka plan + Vurgu rengi), "auto" = Koyu/Açık
/// Windows'a uyar, ötekiler paketlenen hazır temalar. Tanınmayan ad (daha yeni bir sürümün teması) klasik çizilir ama
/// ayarda aynen kalır. Temalar yalnızca widget'ların görünüşünü değiştirir; uygulamanın adı, simgesi ve logosu değişmez.
/// </summary>
public static class ThemePresets
{
    public const string Auto = "auto", Neon = "neon", Gece = "gece", Kagit = "kagit", Okyanus = "okyanus";

    /// <summary>Paketlenen temalar (sıra menüdeki sıradır). Hepsi <see cref="Validate"/>'ten geçer (ThemeTests).</summary>
    public static readonly IReadOnlyList<ThemePreset> All =
    [
        // Neon: siyaha yakın mor zemin, camgöbeği vurgu.
        new(Neon, L.N("Neon"), 0xFF22D3EE, Light: null,
            Dark: new ThemeColors(0xFF17112A, 0xFF0B0816, 0x3D8B5CF6, 0xFFF5F3FF, 0xFFB4ACD6)),
        // Gece: lacivert zemin, ay ışığı sarısı vurgu.
        new(Gece, L.N("Gece"), 0xFFFCD34D, Light: null,
            Dark: new ThemeColors(0xFF1A2642, 0xFF0F172A, 0x2693C5FD, 0xFFE8EEF8, 0xFF9AA8BD)),
        // Kâğıt: sıcak kâğıt zemin, kehribar vurgu.
        new(Kagit, L.N("Kâğıt"), 0xFFB45309,
            Light: new ThemeColors(0xFFFBF6EA, 0xFFF3EAD6, 0x1F5C4A2E, 0xFF2B2418, 0xFF6B5E4B), Dark: null),
        // Okyanus: deniz yeşili; açık ve koyu hâli var, Windows'a uyar.
        new(Okyanus, L.N("Okyanus"), 0xFF0D9488,
            Light: new ThemeColors(0xFFEFFBF9, 0xFFDDF3EF, 0x1F0F766E, 0xFF0B2E2B, 0xFF3F6B66),
            Dark: new ThemeColors(0xFF0B3B3F, 0xFF072A2E, 0x265EEAD4, 0xFFE6FFFB, 0xFF8FC9C2)),
    ];

    /// <summary>Paketlenen tema (büyük/küçük harf fark etmez); "auto", null ve tanınmayan ad null.</summary>
    public static ThemePreset? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(p => string.Equals(p.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Bu sürümün tanıdığı tema adı ("auto" ya da paketlenen tema, küçük harfle); klasik ve tanınmayan null.</summary>
    public static string? Known(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (string.Equals(id.Trim(), Auto, StringComparison.OrdinalIgnoreCase)) return Auto;
        return Find(id)?.Id;
    }

    /// <summary>Temanın görünüşü Windows'un açık/koyu ayarına göre değişir mi ("auto" ya da iki hâlli tema)?</summary>
    public static bool FollowsWindows(string? id) => Known(id) is { } known && (known == Auto || Find(known)!.FollowsWindows);

    /// <summary>2.0/2.1'in göreceği en yakın arka plan: açık zemin Açık, opak koyu zemin Koyu, saydam zemin Cam.</summary>
    public static WidgetStyle NearestStyle(ThemeColors colors) =>
        colors.IsLight ? WidgetStyle.Light : colors.IsOpaque ? WidgetStyle.Dark : WidgetStyle.Glass;

    /// <summary>
    /// Temanın okunurluk kuralları; sorun yoksa boş liste. Opak zeminde yazı her iki durağa karşı en az 4,5; saydam zeminde
    /// yazı açık renk ve gölgeli olmalı (Cam gibi). İkincil yazı ve vurgu en az 3; vurgunun üstündeki yazı en az 4,5.
    /// </summary>
    public static IReadOnlyList<string> Validate(ThemePreset preset)
    {
        var problems = new List<string>();
        if (preset.Light is null && preset.Dark is null) problems.Add($"{preset.Id}: no variant");
        foreach (var variant in preset.Variants)
        {
            var name = $"{preset.Id}/{(variant.IsLight ? "light" : "dark")}";
            var stops = variant.Stops;
            if (variant.IsOpaque)
            {
                if (stops.Any(s => ThemeMath.Contrast(variant.Text, s) < ThemeMath.TextContrast)) problems.Add($"{name}: text contrast");
            }
            else if (!variant.TextShadow || ThemeMath.Luminance(variant.Text) < 0.7) problems.Add($"{name}: translucent background needs light, shadowed text");
            if (variant.Text >> 24 != 0xFF || variant.Secondary >> 24 != 0xFF) problems.Add($"{name}: text must be opaque");
            if (stops.Any(s => ThemeMath.Contrast(variant.Secondary, s) < ThemeMath.AccentContrast)) problems.Add($"{name}: secondary contrast");
            if (stops.Any(s => ThemeMath.Contrast(preset.Accent, s) < ThemeMath.AccentContrast)) problems.Add($"{name}: accent contrast");
        }
        if (preset.Accent >> 24 != 0xFF) problems.Add($"{preset.Id}: accent must be opaque");
        if (ThemeMath.Contrast(ThemeMath.TextOn(preset.Accent), preset.Accent) < ThemeMath.TextContrast)
            problems.Add($"{preset.Id}: text on accent");
        return problems;
    }
}
