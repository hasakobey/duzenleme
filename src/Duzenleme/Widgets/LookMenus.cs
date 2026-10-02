using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Tema seçenekleri (Widget'lar sayfası). Widget'ın kendi görünüşü Kolay NestDesk'ten beri Düzenle panelinin galerisinde
/// (<see cref="LookGallery"/>); sağ tık menüsünde "Görünüm ▸" yoktur. Seçimler <see cref="WidgetLooks"/>'un öncelik
/// kurallarıyla yazılır.
/// </summary>
internal static class LookMenus
{
    /// <summary>Tema seçenekleri (Widget'lar sayfası): "" Klasik, "auto" Otomatik, sonra paketlenen temalar.</summary>
    public static IReadOnlyList<(string Id, string Label)> ThemeChoices =>
    [
        ("", L.T("Klasik")),
        (ThemePresets.Auto, L.T("Otomatik (Windows'a uyar)")),
        .. ThemePresets.All.Select(p => (p.Id, p.Label)),
    ];

    /// <summary>Tema ve onunla gelecek vurgu rengi (seçilince <see cref="WidgetLooks.SetTheme"/> yazacağı gibi) önizlemesi.</summary>
    public static LookOverride ThemePreview(string theme) => new(Theme: theme, AccentColor: WidgetLooks.ThemeAccent(theme) ?? "");
}
