using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, 3. dalga: Otomatik taşıma sayfasının kural kartları, "Kural ekle" penceresi (RuleDialog), kural
// kalıpları (Core/RuleTemplates) ve sade Ana sayfa (HomePage, HowToCards). Terim: Otomatik taşıma = Auto-move; uzantı =
// extension; Halka menü = ring menu.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddKolay3() => Add(
    [
        // Otomatik taşıma sayfası: kural kartı
        new("Oluştur", "Create"),
        new("Klasör adı yok", "No folder name"),
        new("Kural seçenekleri: {0}", "Rule options: {0}"),
        new("Henüz kural yok.", "No rules yet."),
        new("Varsayılan kurallara dön", "Go back to the default rules"),
        new("Hazır altı kural geri gelir; eklediğin kurallar silinir.", "The six default rules come back; rules you added are removed."),

        // "Kural ekle" / "Kuralı değiştir"
        new("Kuralı değiştir", "Change rule"),
        new("Hangi dosyalar?", "Which files?"),
        new("Bir tür seç ya da uzantıyı kendin yaz.", "Pick a type or type the extension yourself."),
        new("Başka uzantı…", "Other extension…"),
        new("Kendin yaz", "Type your own"),
        new("Uzantıyı yaz ve Enter'a bas. Birden çok yazabilirsin.", "Type the extension and press Enter. You can add several."),
        new("En az bir uzantı ekle.", "Add at least one extension."),
        new("Nereye?", "Where to?"),
        new("Uzantıları ya da klasörü değiştir.", "Change the extensions or the folder."),
        new("ör. psd", "e.g. psd"),
        new(".{0} dosyaları hiç taşınmaz.", ".{0} files are never moved."),
        new(".{0} zaten \"{1}\" kuralında.", ".{0} is already in the \"{1}\" rule."),
        new("\"{0}\" bir uzantı olamaz.", "\"{0}\" can't be an extension."),
        new("Yeni klasör:", "New folder:"),
        new("Masaüstündeki klasör:", "Folder on the desktop:"),
        new("Dosyalar masaüstündeki \"{0}\" klasörüne gider.", "Files go to the \"{0}\" folder on the desktop."),
        new("Masaüstünde \"{0}\" klasörü var; dosyalar oraya gider.", "There's a \"{0}\" folder on the desktop; files go there."),
        new("Masaüstünde \"{0}\" klasörü oluşturulur.", "A \"{0}\" folder will be created on the desktop."),
        new("Klasöre bir ad ver.", "Give the folder a name."),
        new("\"{0}\" kuralı eklendi.", "The \"{0}\" rule was added."),
        new("\"{0}\" kuralı eklendi; masaüstünde \"{0}\" klasörü de oluşturuldu.",
            "The \"{0}\" rule was added, and a \"{0}\" folder was created on the desktop."),
        new("\"{0}\" kuralı değiştirildi.", "The \"{0}\" rule was changed."),
        new("\"{0}\" kuralı değiştirildi; masaüstünde \"{0}\" klasörü de oluşturuldu.",
            "The \"{0}\" rule was changed, and a \"{0}\" folder was created on the desktop."),

        // Kural kalıpları (klasör adı; kural oluşturulurken bir kez arayüz dilinde yazılır)
        new("E-kitaplar", "E-books"),

        // Ana sayfa: büyük düğmeler ve otomatik taşıma satırı
        new("Halkayı düzenle", "Edit ring"),
        new("Halkadaki uygulamalar", "The apps in your ring"),
        new("Halkan öne geldi. Uygulama eklemek ya da çıkarmak için ona sağ tıkla.",
            "Your ring is in front now. Right-click it to add or remove apps."),
        new("Otomatik taşıma açık · masaüstünde hedef klasör yok", "Auto-move is on · no target folders on the desktop"),
        new("Otomatik taşıma açık · son: {0} → {1}", "Auto-move is on · last: {0} → {1}"),

        // Ana sayfa: "Nasıl kullanılır" kartları (Views/HowToCards). "Düzenle" Düzenle paneliyle aynı çeviriyi kullanır.
        new("Nasıl kullanılır", "How it works"),
        new("Anladım", "Got it"),
        new("Nasıl kullanılır kartlarını bir daha gösterme", "Don't show the How it works cards again"),
        new("Başlığından ya da boş yerinden sürükle.", "Drag it by its title or an empty spot."),
        new("Büyüt", "Resize"),
        new("Kenarından sürükle ya da ⋯ › Boyut.", "Drag an edge, or use ⋯ › Size."),
        new("Düzenle", "Edit"),
        new("⋯'ye bas ya da sağ tıkla.", "Click ⋯ or right-click it."),
        new("Halka", "Ring"),
        new("Widget ekle › Halka menü.", "Add widget › Ring menu."),
    ]);
}
