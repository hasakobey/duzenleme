using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, 2. dalga: "Düzenle" paneli (Views/WidgetEditor, WidgetEditorWindow, EditorRows, EditorContent,
// LookGalleryControl; Core/LookGallery, SizePresets), kısa widget menüsü (WidgetWindow.BuildMenu) ve kartın köşe düğmeleri
// (Widgets/CardButtons). "Düzenle" yalnızca bu paneldir = "Edit". Terim: "bölme" = "panel".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddEditor() => Add(
    [
        new("Düzenle", "Edit"),
        new("Düzenle…", "Edit…"),
        new("Klasör bölmesi", "Folder panel"),
        new("Widget'ı panel açıldığındaki hâline döndürür", "Returns the widget to how it was when you opened Edit"),

        // Görünüm: galeri, renk ve boyut
        new("Otomatik", "Auto"),
        new("Özel ({0})", "Custom ({0})"),
        new("Seçili", "Selected"),
        new("Bu görünümü tüm widget'lara uygula", "Use this look for all widgets"),
        new("Görünüm bütün widget'lara uygulandı.", "Look applied to all widgets."),

        // Daha fazla
        new("Daha fazla", "More"),
        new("Çoğu zaman varsayılanlar yeterli.", "The defaults are usually enough."),
        new("Hafif", "Soft"),

        // Menü ve kartın köşesi (⋯ Düzenle, ×, kilit)
        new("Düzenle düğmesi (⋯)", "Edit button (⋯)"),
        new("Görünüm ve diğer ayarlar artık \"Düzenle\"de.", "Look and other settings are now in \"Edit\"."),
        new("Listeye çevir", "Convert to list"),
        new("Bazı masaüstü öğeleri artık hiçbir bölmede görünmüyordu: Windows'un masaüstü simgeleri yeniden gösteriliyor.",
            "Some desktop items no longer showed in any panel, so Windows desktop icons are shown again."),
        new("\"{0}\" kutudan çıkarıldı.", "\"{0}\" was removed from the box."),
        new("{0} biten madde temizlendi.", "Cleared {0} done item.", "Cleared {0} done items."),

        // Widget'ların kendi ayarları
        new("Görünüş", "View"),
        new("Simge boyutu", "Icon size"),
        new("Yazı", "Text size"),
        new("Tümü", "All"),
        new("Bir klasör seç…", "Choose a folder…"),
        new("Bölme artık \"{0}\" gösteriyor.", "The panel now shows \"{0}\"."),
        new("Sekme", "Tab"),
        new("+ Uygulama", "+ App"),
        new("+ Klasör", "+ Folder"),
        new("+ Dosya", "+ File"),
        new("+ Sekme", "+ Tab"),
        new("{0}: kaldır", "Remove {0}"),
    ]);
}
