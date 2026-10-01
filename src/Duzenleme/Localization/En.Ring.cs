using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Halka menü: widget, kısayolla açılan halka, öğe türleri ve ayarları. Terim: "halka menü" = "ring menu".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddRing() => Add(
    [
        // Tohum başlığı (2.0/2.1'de "Kısayol kutusu · Halka menü")
        new("Halka menü", "Ring menu"),
    ]);
}
