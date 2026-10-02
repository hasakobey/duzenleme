using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, 2. dalga: "Düzenle" paneli (Views/WidgetEditor, WidgetEditorWindow, EditorRows, LookGalleryControl;
// Core/LookGallery, SizePresets). "Düzenle" yalnızca bu paneldir = "Edit". Terim: "bölme" = "panel".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddEditor() => Add(
    [
        new("Düzenle", "Edit"),
        new("Düzenle…", "Edit…"),
        new("Klasör bölmesi", "Folder panel"),
        new("Widget'ı panel açıldığındaki hâline döndürür", "Returns the widget to how it was when you opened this panel"),

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
    ]);
}
