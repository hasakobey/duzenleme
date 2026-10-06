using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 "Masaüstünden ekle": "Widget ekle" penceresinin "Masaüstünden" satırı, "Tümü…" sayfası ve masaüstünden bırakılan
// dosyalar (klasör → bölme, öbürleri → kısayol kutusu). Terimler: bölme = panel, Kısayol kutusu = shortcut box.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddFromDesktop() => Add(
    [
        new("Tümü…", "All…"),
        new("Masaüstündeki bütün öğeler; aranabilir, birden çok seçilebilir", "Everything on your desktop; search and pick several"),
        new("Bölme olarak eklenir", "Adds a panel"),
        new("Kısayol kutusuna eklenir", "Goes into a shortcut box"),
        new("Masaüstünden ekle", "Add from the desktop"),
        new("Klasör bölme olur; kısayollar ve dosyalar kısayol kutusuna girer. Birden çok seçebilirsin.",
            "A folder becomes a panel; shortcuts and files go into a shortcut box. You can pick several."),
        new("Ekle ({0})", "Add ({0})"),
        new("Masaüstünde eklenecek bir şey yok.", "There's nothing on your desktop to add."),
        new("Aramaya uyan öğe yok.", "Nothing matches your search."),
        new("Bırak: klasör bölme, öbürleri kısayol kutusu olur", "Drop: folders become panels, everything else a shortcut box"),

        // Bildirimler
        new("Yeni kısayol kutusu eklendi.", "New shortcut box added."),
        new("{0} öğe kısayol kutusuna eklendi.", "{0} item added to the shortcut box.", "{0} items added to the shortcut box."),
        new("\"{0}\" kısayol kutusuna eklendi.", "\"{0}\" added to the shortcut box."),
        new("Seçtiklerin zaten kısayol kutusunda.", "What you picked is already in the shortcut box."),
        new("{0} klasör eklenmedi: bir seferde en çok {1} bölme eklenir.",
            "{0} folder wasn't added: you can add up to {1} panels at a time.",
            "{0} folders weren't added: you can add up to {1} panels at a time."),
        new("{0} widget kaldırıldı.", "{0} widget removed.", "{0} widgets removed."),
        new("{0} öğe kısayol kutusundan çıkarıldı.", "{0} item taken out of the shortcut box.", "{0} items taken out of the shortcut box."),
    ]);
}
