using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Tema önizlemesi. Görünüş Kolay NestDesk'ten beri galeride seçilir (<see cref="LookGallery"/>: Düzenle paneli ve Widget'lar
/// sayfasının "Tüm widget'lar" şeridi); sağ tık menüsünde "Görünüm ▸" yoktur. Seçimler <see cref="WidgetLooks"/>'un öncelik
/// kurallarıyla yazılır.
/// </summary>
internal static class LookMenus
{
    /// <summary>Tema ve onunla gelecek vurgu rengi (seçilince <see cref="WidgetLooks.SetTheme"/> yazacağı gibi) önizlemesi.</summary>
    public static LookOverride ThemePreview(string theme) => new(Theme: theme, AccentColor: WidgetLooks.ThemeAccent(theme) ?? "");
}
