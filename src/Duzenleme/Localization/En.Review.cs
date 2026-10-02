using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 inceleme düzeltmeleri: küçük widget'lara ad verme (WidgetNameLine) ve incelemede eklenen/değişen metinler.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddReview() => Add(
    [
        // Zamanlayıcı, takvim, dünya saati, sistem durumu: "Göster ▸ Ad" gizliyken adı soran pencere
        new("Ad (boş bırakırsan adı kalkar)", "Name (leave empty to remove it)"),
    ]);
}
