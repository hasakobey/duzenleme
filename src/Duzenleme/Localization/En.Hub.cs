using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, Dalga 2b: NestDesk halkası (AppSettings.Hub), kısayolsuz açılış (çift tıklama, tepsi), açılır halkanın
// "Halkayı düzenle" düğmesi ve halka düzenleyicisi (Views/RingEditor). Terim: Halka menü = ring menu, alt halka = sub-ring.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddHub() => Add(
    [
        // NestDesk halkası ve açılışlar
        new("{0} halkası", "{0} ring"),
        new("Halkayı aç", "Open the ring"),
        new("Boş masaüstüne çift tıkla.", "Double-click the empty desktop."),
        new("Halka imlecin yanında açılır; kısayol gerekmez. Simgeye çift tıklamak yine dosyayı açar.",
            "The ring opens next to the pointer; no shortcut needed. Double-clicking an icon still opens the file."),
        new("Bu öğe yalnızca tuşla açılan halkada çalışır", "This item only works when you open the ring with its shortcut"),
        new("Halka menün boş.", "Your ring menu is empty."),
        new("Halkaya öğe eklemek için buraya tıkla.", "Click here to add items to the ring."),

        // Halka düzenleyicisi
        new("Bir öğeyi seçmek için halkada ona tıkla. Alt+← ve Alt+→ seçileni taşır.",
            "Click an item on the ring to select it. Alt+← and Alt+→ move the selected item."),
        new("Ana halkaya dön", "Back to the main ring"),
        new("Alt halka: {0}", "Sub-ring: {0}"),
        new("Alt halkayı aç", "Open the sub-ring"),
        new("Seçili öğe yok.", "No item selected."),
        new("Klasör ekle", "Add folder"),
        new("+ Web adresi", "+ Web address"),
        new("Web adresi ekle", "Add a web address"),
        new("+ {0} eylemi", "+ {0} action"),
        new("{0} eylemi ekle", "Add a {0} action"),
        new("Daha fazla ▾", "More ▾"),
        new("Daha fazla öğe türü", "More item types"),
        new("Uygulama", "App"),
        new("Tuşlara basar (yalnızca tuşla açılan halkada)", "Presses keys (only when the ring is opened with its shortcut)"),
        new("Metin yapıştırır (yalnızca tuşla açılan halkada)", "Pastes text (only when the ring is opened with its shortcut)"),
    ]);
}
