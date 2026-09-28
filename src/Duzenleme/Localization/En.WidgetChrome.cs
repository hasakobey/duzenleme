using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// WidgetChrome alanı: Views/WidgetsPage, Widgets/{WidgetWindow, WidgetManager, Menus}.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddWidgetChrome() => Add(
    [
        // Otomatik düzen yedeklerinin adı: kimlik olarak da karşılaştırılır (L.Variants), dil değişse de tanınır.
        new("Düzen uygulanmadan önce", "Before applying a layout"),
        new("Otomatik yerleştirmeden önce", "Before auto-arrange"),
    ]);
}
