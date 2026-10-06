using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Halka görünümü: halka menünün biçimleri (Düzenle ▸ Halka biçimi). "Klasik" En.Looks.cs'te.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddRingLook() => Add(
    [
        new("Halka biçimi", "Ring style"),
        new("Daire", "Orbit"),
        new("Etiketli", "Labeled"),
        new("Işıltı", "Glow"),
        new("Yüksek karşıtlık açık: halka sade çizilir.", "High contrast is on: the ring is drawn plainly."),
    ]);
}
