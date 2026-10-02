namespace Duzenleme.Core;

/// <summary>Düzenle panelindeki görünüm kartı: kimlik (AutomationId'de de) ve adı (<see cref="L.N"/> anahtarı; <see cref="Label"/> çevirir).</summary>
public sealed record LookEntry(string Id, string Name)
{
    /// <summary>Görünen ad (arayüz dilinde).</summary>
    public string Label => L.Dyn(Name);
}

/// <summary>
/// Kolay NestDesk "Düzenle" panelinin görünüm galerisi: eski üç seçim (Tema, Arka plan, Vurgu rengi) tek sıra karta iner.
/// Sekiz kart: Otomatik · Cam · Koyu · Açık · Neon · Gece · Kâğıt · Okyanus. Cam/Koyu/Açık klasik arka plandır
/// (<see cref="WidgetLooks.SetClassicLook"/>), öbürleri tema (<see cref="WidgetLooks.SetTheme"/>); yazma kuralları yine yalnızca
/// <see cref="WidgetLooks"/>'ta (2.1 için en yakın Style/Accent her seçimde yazılır). Saf, testlenir; yeni kayıtlı veri yok.
/// </summary>
public static class LookGallery
{
    /// <summary>Klasik arka planların kimlikleri (tema kimlikleri <see cref="ThemePresets"/>'tekilerdir).</summary>
    public const string Glass = "cam", Dark = "koyu", Light = "acik";

    /// <summary>Kartlar, galerideki sırasıyla. Tema adları <see cref="ThemePresets"/>'in kendi anahtarlarıdır.</summary>
    public static readonly IReadOnlyList<LookEntry> All =
    [
        new(ThemePresets.Auto, L.N("Otomatik")),
        new(Glass, L.N("Cam")),
        new(Dark, L.N("Koyu")),
        new(Light, L.N("Açık")),
        .. ThemePresets.All.Select(p => new LookEntry(p.Id, p.Name)),
    ];

    /// <summary>Kartın klasik arka planı (Cam/Koyu/Açık); tema kartında ve tanınmayan kimlikte null.</summary>
    public static WidgetStyle? StyleOf(string? id) => id switch
    {
        Glass => WidgetStyle.Glass,
        Dark => WidgetStyle.Dark,
        Light => WidgetStyle.Light,
        _ => null,
    };

    /// <summary>Galeride bu kimlikte bir kart var mı?</summary>
    public static bool IsKnown(string? id) => All.Any(e => e.Id == id);

    /// <summary>
    /// Kartı seçer: klasik arka plan ya da tema (vurgu rengi temanınki olur). Tanınmayan kimlik hiçbir şeyi değiştirmez (false).
    /// </summary>
    public static bool Apply(WidgetConfig c, string id, bool systemDark)
    {
        if (!IsKnown(id)) return false;
        if (StyleOf(id) is { } style) WidgetLooks.SetClassicLook(c, style);
        else WidgetLooks.SetTheme(c, id, systemDark);
        return true;
    }

    /// <summary>
    /// Seçili kart: tanınan tema, yoksa klasik arka plan. Bu sürümün tanımadığı tema adı (daha yeni bir sürümün) klasik çizilir;
    /// kart da öyle görünür (ad ayarda korunur).
    /// </summary>
    public static string Current(WidgetConfig c) => WidgetLooks.ActiveTheme(c) ?? c.Style switch
    {
        WidgetStyle.Dark => Dark,
        WidgetStyle.Light => Light,
        _ => Glass,
    };

    /// <summary>
    /// Kart seçilirse widget'ın görünüş alanları ne olur (kartı gerçek paletle çizmek için): yalnızca görünüş alanları kopyalanır,
    /// asıl ayara dokunulmaz.
    /// </summary>
    public static WidgetConfig Probe(WidgetConfig c, string id, bool systemDark)
    {
        var probe = LookFields.Of(c).ToConfig();
        Apply(probe, id, systemDark);
        return probe;
    }

    /// <summary>Adlı vurgu rengi seçilirse görünüş alanları ne olur (renk noktalarını gerçek paletle çizmek için).</summary>
    public static WidgetConfig ProbeAccent(WidgetConfig c, WidgetAccent accent)
    {
        var probe = LookFields.Of(c).ToConfig();
        WidgetLooks.SetAccent(probe, accent);
        return probe;
    }

    /// <summary>
    /// "Bu görünümü tüm widget'lara uygula": kaynağın görünüşünü (tema, arka plan, vurgu rengi, özel renk) hedeflere yazar ve
    /// her hedefin önceki görünüşünü döner (bildirimdeki "Geri al" için; <see cref="Undo"/>). Kaynağın kendisi atlanır.
    /// </summary>
    public static IReadOnlyList<(string Id, LookFields Before)> CopyToAll(WidgetConfig source, IEnumerable<WidgetConfig> targets)
    {
        var look = LookFields.Of(source);
        var before = new List<(string, LookFields)>();
        foreach (var target in targets)
        {
            if (ReferenceEquals(target, source) || target.Id == source.Id) continue;
            before.Add((target.Id, LookFields.Of(target)));
            look.ApplyTo(target);
        }
        return before;
    }

    /// <summary><see cref="CopyToAll"/>'u geri alır: hâlâ duran widget'lar önceki görünüşüne döner. Dönenlerin kimlikleri.</summary>
    public static IReadOnlyList<string> Undo(IReadOnlyList<(string Id, LookFields Before)> before, IEnumerable<WidgetConfig> widgets)
    {
        var byId = widgets.GroupBy(w => w.Id).ToDictionary(g => g.Key, g => g.First());
        var restored = new List<string>();
        foreach (var (id, look) in before)
        {
            if (!byId.TryGetValue(id, out var config)) continue;
            look.ApplyTo(config);
            restored.Add(id);
        }
        return restored;
    }
}

/// <summary>Widget'ın görünüş alanları (tema, arka plan, adlı vurgu rengi, özel renk): kopyalanır ve geri yazılır.</summary>
public sealed record LookFields(WidgetStyle Style, WidgetAccent Accent, string? Theme, string? AccentColor)
{
    public static LookFields Of(WidgetConfig c) => new(c.Style, c.Accent, c.LookTheme, c.LookAccentColor);

    public void ApplyTo(WidgetConfig c)
    {
        c.Style = Style;
        c.Accent = Accent;
        c.LookTheme = Theme;
        c.LookAccentColor = AccentColor;
    }

    /// <summary>Yalnızca bu alanları taşıyan geçici ayar (paleti çözmek için).</summary>
    public WidgetConfig ToConfig()
    {
        var c = new WidgetConfig();
        ApplyTo(c);
        return c;
    }
}
