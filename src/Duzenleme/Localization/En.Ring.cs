using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Halka menü: widget, kısayolla açılan halka, öğe türleri ve ayarları. Terim: "halka menü" = "ring menu",
// "alt halka" = "sub-ring", "ana halka" = "main ring".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddRing() => Add(
    [
        // Tohum başlığı (2.0/2.1'de "Kısayol kutusu · Halka menü"), katalog ve ad
        new("Halka menü", "Ring menu"),
        new("Uygulamaların bir halkada; tıkla ya da 1–9'a bas. İstersen kısayolla imlecin yanında açılır.",
            "Your apps in a ring; click or press 1–9. If you like, it opens next to the pointer with a shortcut."),
        new("Halka menü · {0}", "Ring menu · {0}"),

        // Halka
        new("Geri: {0}", "Back: {0}"),
        new("Uygulama, dosya ya da klasörü buraya sürükle", "Drag an app, file or folder here"),
        new("Bu öğe bu sürümde çalışmıyor ya da kapalı.", "This item doesn't work in this version or is turned off."),
        new("Alt halka", "Sub-ring"),
        new("Ana halka", "Main ring"),
        new("Bilinmeyen öğe", "Unknown item"),
        new("{0} eylemi", "{0} action"),
        new("Web adresi", "Web address"),
        new("Komut", "Command"),
        new("Tuş kısayolu", "Keyboard shortcut"),
        new("Metin", "Text"),

        // Widget ve öğe menüleri
        new("Ana halkaya dön", "Back to main ring"),
        new("Tuş numaraları", "Key numbers"),
        new("Kısayolla imleçte açılsın", "Open at the pointer with a shortcut"),
        new("Web adresi…", "Web address…"),
        new("Komut…", "Command…"),
        new("Tuş kısayolu gönder…", "Send keyboard shortcut…"),
        new("Metin yapıştır…", "Paste text…"),
        new("Alt halka…", "Sub-ring…"),
        new("Saat yönünde taşı", "Move clockwise"),
        new("Ters yönde taşı", "Move counterclockwise"),
        new("Alt halkaya taşı", "Move to sub-ring"),
        new("Başka halkaya taşı", "Move to another ring"),
        new("Düzenle…", "Edit…"),
        new("Öğe masaüstüne döner ve halkada kalır.", "The item goes back to the desktop and stays in the ring."),
        new("Halkadan kaldır", "Remove from ring"),
        new("Halkaya ekle", "Add to ring"),
        new("Geri al: \"{0}\" halkaya dönsün", "Undo: put \"{0}\" back in the ring"),

        // Bildirimler
        new("\"{0}\" o halkada zaten var.", "\"{0}\" is already in that ring."),
        new("\"{0}\" zaten halkada.", "\"{0}\" is already in the ring."),
        new("Bu öğeler zaten halkada.", "These items are already in the ring."),
        new("\"{0}\" halkadan kaldırıldı ve masaüstüne geri konuyor.", "\"{0}\" was removed from the ring and is going back to the desktop."),
        new("\"{0}\" halkadan kaldırıldı.", "\"{0}\" was removed from the ring."),
        new("\"{0}\" alt halkası kaldırılsın mı?", "Remove the \"{0}\" sub-ring?"),
        new("İçindeki {0} öğe de halkadan kalkar. Dosyalara dokunulmaz; bildirimden geri alabilirsin.",
            "The {0} item in it is removed from the ring too. Your files aren't touched; you can undo from the notification.",
            "The {0} items in it are removed from the ring too. Your files aren't touched; you can undo from the notification."),
        new("\"{0}\" alt halkası kaldırıldı.", "The \"{0}\" sub-ring was removed."),
        new("Bu öğe kısayolla açılan halkada çalışır.", "This item works in the ring that opens with the shortcut."),
        new("Bu öğe kapalı: başka bir yerden aktarıldı ve henüz onaylanmadı.", "This item is off: it was imported and hasn't been confirmed yet."),
        new("Bu öğe eksik ya da geçersiz. Düzenle…'yi kullanarak düzeltebilirsin.", "This item is incomplete or invalid. You can fix it with Edit…"),
        new("Bu öğe {0}'in daha yeni bir sürümünde eklenmiş; bu sürümde çalışmaz.",
            "This item was added in a newer version of {0}; it doesn't work in this version."),
        new("Komut çalıştırılamadı. Programın adını ve çalışma klasörünü denetle.",
            "Couldn't run the command. Check the program name and the working folder."),

        // Sürükle-bırak
        new("Bırak, \"{0}\" alt halkasına eklensin", "Drop to add to the \"{0}\" sub-ring"),
        new("Bırak, bu halkaya taşınsın", "Drop to move to this ring"),
        new("Bırak, halkaya eklensin", "Drop to add to the ring"),

        // Öğe penceresi
        new("Düzenle", "Edit"),
        new("Web adresi ekle", "Add web address"),
        new("Komut ekle", "Add command"),
        new("Tuş kısayolu ekle", "Add keyboard shortcut"),
        new("Metin ekle", "Add text"),
        new("Alt halka ekle", "Add sub-ring"),
        new("Alt halkanın adı", "Sub-ring name"),
        new("ör. Oyunlar, İş", "e.g. Games, Work"),
        new("Alt halka kök halkanın sonunda durur; içine öğe eklemek için ona sağ tıkla.",
            "The sub-ring sits at the end of the main ring; right-click it to add items to it."),
        new("Alt halkaya bir ad ver.", "Give the sub-ring a name."),
        new("Yalnızca halkada görünen ad değişir; dosyanın adı değişmez.", "Only the name shown in the ring changes; the file keeps its name."),
        new("ör. https://example.com", "e.g. https://example.com"),
        new("Bu bir web adresi değil. Örnek: https://example.com", "This isn't a web address. Example: https://example.com"),
        new("Bu adres bir uygulama açar", "This address opens an app"),
        new("\"{0}:\" ile başlayan adres web tarayıcısında değil, bu bilgisayardaki bir uygulamada açılır. Yalnızca tanıdığın adresleri ekle.",
            "An address that starts with \"{0}:\" opens in an app on this PC, not in your web browser. Only add addresses you know."),
        new("Program ya da komut", "Program or command"),
        new("ör. notepad.exe", "e.g. notepad.exe"),
        new("Program seç", "Choose a program"),
        new("Programlar|*.exe;*.bat;*.cmd;*.msc;*.lnk|Tüm dosyalar|*.*", "Programs|*.exe;*.bat;*.cmd;*.msc;*.lnk|All files|*.*"),
        new("Bağımsız değişkenler (isteğe bağlı)", "Arguments (optional)"),
        new("Çalışma klasörü (isteğe bağlı)", "Working folder (optional)"),
        new("Konsol penceresini gizle", "Hide the console window"),
        new("Yönetici olarak çalıştır (Windows her seferinde sorar)", "Run as administrator (Windows asks every time)"),
        new("Komut yalnızca sen bu öğeyi seçince çalışır. Tanımadığın bir komutu ekleme.",
            "The command runs only when you pick this item. Don't add a command you don't recognize."),
        new("Programı ya da komutu yaz.", "Type the program or command."),
        new("Gönderilecek tuşlar", "Keys to send"),
        new("Tuşlara bas…", "Press keys…"),
        new("Sonuncuyu sil", "Remove last"),
        new("En çok 8 tuş birleşimi.", "Up to 8 key combinations."),
        new("Bu tuş gönderilemez.", "This key can't be sent."),
        new("En az bir tuş birleşimine bas.", "Press at least one key combination."),
        new("Yapıştırılacak metin", "Text to paste"),
        new("Parolaları buraya kaydetme.", "Don't save passwords here."),
        new("Yapıştırılan metin pano geçmişine ve bulut panosuna eklenmez; önceki pano içeriği geri konur.",
            "Pasted text isn't added to clipboard history or the cloud clipboard; the previous clipboard content is put back."),
        new("Yapıştırılacak metni yaz.", "Type the text to paste."),
        new("Ne yapılsın?", "What should it do?"),
        new("Bir eylem seç.", "Choose an action."),
    ]);
}
