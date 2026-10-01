using System.Windows.Controls;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget menüsünün "Görünüm ▸" renk seçimleri: Tema, Arka plan ve Vurgu rengi. Hepsi fareyle üstüne gelince önizlenir
/// (<see cref="LookOverride"/>, kaydedilmez) ve seçilince <see cref="WidgetLooks"/>'un öncelik kurallarıyla yazılır.
/// <paramref name="update"/> değişikliği uygulayıp kaydeder (WidgetWindow.Update).
/// </summary>
internal static class LookMenus
{
    /// <summary>Tema seçenekleri (menü ve Widget'lar sayfası): "" Klasik, "auto" Otomatik, sonra paketlenen temalar.</summary>
    public static IReadOnlyList<(string Id, string Label)> ThemeChoices =>
    [
        ("", L.T("Klasik")),
        (ThemePresets.Auto, L.T("Otomatik (Windows'a uyar)")),
        .. ThemePresets.All.Select(p => (p.Id, p.Label)),
    ];

    /// <summary>Önizleme: tema ve onunla gelecek vurgu rengi (seçilince <see cref="WidgetLooks.SetTheme"/> yazacağı gibi).</summary>
    public static LookOverride ThemePreview(string theme) => new(Theme: theme, AccentColor: WidgetLooks.ThemeAccent(theme) ?? "");

    /// <summary>Görünüm ▸ Tema ▸. Yüksek karşıtlıkta temalar etkisizdir; bunu söyleyen bir satır eklenir.</summary>
    public static MenuItem Theme(WidgetConfig c, Action<Action> update, Action<LookOverride> preview, Action endPreview, bool highContrast)
    {
        // Tema adları menü başlığında veri gibi (Menus.Literal): "_" erişim tuşu olmasın.
        var item = Menus.Choice(L.T("Tema"), () => WidgetLooks.ActiveTheme(c) ?? "",
            ThemeChoices.Select(t => (t.Id, Menus.Literal(t.Label))),
            v => update(() => WidgetLooks.SetTheme(c, v, WidgetPalette.SystemDark)), v => preview(ThemePreview(v)), endPreview);
        if (highContrast)
        {
            item.Items.Add(new Separator());
            item.Items.Add(Menus.Hint(L.T("Yüksek karşıtlık açık: widget'lar Windows'un renklerini kullanır.")));
        }
        return item;
    }

    /// <summary>Görünüm ▸ Arka plan ▸: klasik arka planlar; seçmek temayı kaldırır, tema açıkken hiçbiri işaretli değildir.</summary>
    public static MenuItem Background(WidgetConfig c, Action<Action> update, Action<LookOverride> preview, Action endPreview) =>
        Menus.Choice<WidgetStyle?>(L.T("Arka plan"), () => WidgetLooks.ShownStyle(c),
            [(WidgetStyle.Glass, L.T("Cam")), (WidgetStyle.Dark, L.T("Koyu")), (WidgetStyle.Light, L.T("Açık"))],
            v => update(() => WidgetLooks.SetStyle(c, v!.Value)), v => preview(new LookOverride(Style: v)), endPreview);

    /// <summary>
    /// Görünüm ▸ Vurgu rengi ▸: beş adlı renk ve en sonda "Özel renk…" (renk seçiciyi açar; menü kapanır). Özel renk seçiliyken
    /// adlı renklerin hiçbiri işaretli değildir, "Özel renk…" işaretlidir.
    /// </summary>
    public static MenuItem Accent(WidgetConfig c, Action<Action> update, Action<LookOverride> preview, Action endPreview, Action pickCustom)
    {
        var item = Menus.Choice<WidgetAccent?>(L.T("Vurgu rengi"), () => WidgetLooks.ShownAccent(c),
            [(WidgetAccent.Violet, L.T("Mor")), (WidgetAccent.Blue, L.T("Mavi")), (WidgetAccent.Green, L.T("Yeşil")),
             (WidgetAccent.Orange, L.T("Turuncu")), (WidgetAccent.Pink, L.T("Pembe"))],
            v => update(() => WidgetLooks.SetAccent(c, v!.Value)), v => preview(new LookOverride(Accent: v)), endPreview);
        item.Items.Add(new Separator());
        item.Items.Add(Menus.Toggle(L.T("Özel renk…"), () => WidgetLooks.CustomAccent(c) is not null, pickCustom, staysOpen: false));
        return item;
    }
}
