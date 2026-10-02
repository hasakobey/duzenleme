using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// P4 (2.1, widget menüleri): Widgets/{Menus, WidgetWindow.FillMenu} ve görünümlerin AddMenuItems/parça etiketleri.
// Terim: "bölme" = panel, "Kısayol kutusu" = shortcut box (En.Common).
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddP4() => Add(
    [
        // Widget menüsünün iskeleti (WidgetWindow.FillMenu)
        new("Windows masaüstüne göz at", "Peek at the Windows desktop"),
        new("{0}'e dön", "Back to {0}"),
        new("Görünüm", "Appearance"),
        new("Ölçek", "Scale"),
        new("Saydamlık", "Transparency"),
        new("Köşeler", "Corners"),
        new("Yuvarlak", "Round"),
        new("Köşeli", "Square"),
        new("Gölge", "Shadow"),
        new("Fare üstünde değilken soluk dursun", "Fade when the pointer isn't on it"),
        new("Başlığa katla", "Collapse to title"),
        new("Fare üstünde değilken başlığa katla", "Collapse to title when the pointer isn't on it"),
        new("Konumu kilitle", "Lock position"),
        new("Konum kilitli.", "Position locked."),
        new("Kilidi aç", "Unlock"),
        new("Kilidi açmak için buraya tıkla.", "Click here to unlock."),
        new("Kenarlara yapışsın (mıknatıs)", "Snap to edges (magnet)"),
        new("Widget'lar üst üste binmesin", "Don't let widgets overlap"),
        new("Çoğalt", "Duplicate"),

        // Menus: parçalar ve Simgeler ▸
        new("Kaldır düğmesi (×)", "Remove button (×)"),
        new("Simgeler", "Icons"),
        new("Izgara", "Grid"),
        new("Liste", "List"),
        new("Boyut", "Size"),
        new("Küçük", "Small"),
        new("Orta", "Medium"),
        new("Büyük", "Large"),
        new("Çok büyük", "Extra large"),
        new("Hizalama", "Alignment"),
        new("Sola", "Left"),
        new("Ortaya", "Center"),
        new("Sağa", "Right"),
        new("Aralık", "Spacing"),
        new("Sık", "Compact"),
        new("Normal", "Normal"),
        new("Geniş", "Wide"),
        new("Yazı boyutu", "Text size"),
        new("Adları gizle", "Hide names"),
        new("Önizlemeleri göster (resim, video, PDF)", "Show previews (pictures, videos, PDFs)"),
        new("Tek tıkla aç", "Open with a single click"),

        // Bölme (FenceView)
        new("Klasörü aç", "Open folder"),
        new("Ne gösterilsin?", "What to show"),
        new("Sırala", "Sort by"),
        new("En yeni üstte", "Newest first"),
        new("Ada göre", "Name"),
        new("Türe göre", "Type"),
        new("Gizlenen öğeler ({0})", "Hidden items ({0})"),
        new("Hepsini yeniden göster", "Show all again"),
        new("{0} — göster", "{0} — show"),
        new("Başlık satırı", "Title bar"),
        new("Öğe sayısı", "Item count"),
        new("Arama düğmesi", "Search button"),
        new("Klasörü aç düğmesi", "Open folder button"),
        new("Ayraç çizgisi", "Divider line"),
        new("Klasör simgesi…", "Folder icon…"),
        new("Yenile", "Refresh"),

        // Kısayol kutusu (LauncherView)
        new("Sekmeler", "Tabs"),

        // Not, saat, tarih
        new("Sarı", "Yellow"),
        new("Grafit", "Graphite"),
        new("Saniyeyi göster", "Show seconds"),
        new("Selam ve gün", "Greeting and day"),
        new("Yıl ve gün adı", "Year and weekday"),
    ]);
}
