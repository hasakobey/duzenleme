using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, 1. dalga (2. bölüm): tek "Widget ekle" penceresi (görünen açıklamalı kutucuklar, bölme sayfası),
// "Uygulama seç" (Başlat menüsü uygulamaları), boş kutu ve halkadaki "+" yeri, alt menüsüz tepsi menüsü.
// Terim: "bölme" = "panel", Kısayol kutusu = shortcut box.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddKolay() => Add(
    [
        // "Widget ekle": ilk sayfa (kutucukların altında görünen açıklamalar en çok dört sözcük)
        new("Ne eklemek istersin?", "What would you like to add?"),
        new("Gün ve haftalık şerit", "Day and week strip"),
        new("Yazdıkça kaydedilir", "Saves as you type"),
        new("Onay kutulu liste", "List with checkboxes"),
        new("Aylık takvim", "Monthly calendar"),
        new("Uygulamalar tek tıkta", "Apps in one click"),
        new("Pomodoro ve kronometre de", "Pomodoro and stopwatch too"),
        new("Bölme ›", "Panel ›"),
        new("Dosyalarını topla", "Gather your files"),
        new("Masaüstündeki öğeleri ya da bir klasörün içini gösterir", "Shows desktop items or the contents of a folder"),
        new("Hepsini göster ({0})", "Show all ({0})"),
        new("Uygulamalar bir halkada", "Apps in a ring"),
        new("Önemli güne kaç gün", "Days until a date"),
        new("Odak ve mola", "Focus and breaks"),
        new("Başka şehirlerde saat", "Time in other cities"),
        new("İşlemci, bellek, disk", "CPU, memory, disk"),
        new("Tek tıkla açılır", "Opens in one click"),
        new("{0} eklendi.", "{0} added."),

        // "Widget ekle": bölme sayfası
        new("‹ Geri", "‹ Back"),
        new("Bölme ekle", "Add a panel"),
        new("Bölme, masaüstündeki öğeleri ya da bir klasörün içini gösterir.", "A panel shows desktop items or the contents of a folder."),
        new("Masaüstünden", "From the desktop"),
        new("Bir klasörü göster", "Show a folder"),
        new("{0} (oluşturulur)", "{0} (will be created)"),
        new("{0} (masaüstü)", "{0} (desktop)"),

        // "Uygulama seç"
        new("Uygulama seç", "Choose an app"),
        new("Dosya ya da klasör seç…", "Choose a file or folder…"),
        new("Dosya seç…", "Choose files…"),
        new("Uygulamalar yükleniyor…", "Loading apps…"),
        new("Başlat menüsünde uygulama bulunamadı.", "No apps were found in the Start menu."),
        new("Aramaya uyan uygulama yok.", "No apps match your search."),
        new("Zaten ekli", "Already added"),
        new("{0} (zaten ekli)", "{0} (already added)"),

        // Kısayol kutusu ve halka: uygulama ekleme, boş kutu
        new("Uygulama ekle…", "Add app…"),
        new("Uygulama ekle", "Add app"),
        new("Kutu boş.", "This box is empty."),
        new("Bu sekme boş.", "This tab is empty."),
        new("ya da buraya sürükle", "or drag it here"),
    ]);
}
