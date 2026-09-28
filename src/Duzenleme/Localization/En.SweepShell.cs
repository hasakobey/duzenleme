using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 yerelleştirme taraması, kabuk: App.xaml.cs, AppHost.cs, MainWindow.xaml.cs, Core/{AppInfo, PackagedApp, FolderIconCatalog},
// Desktop/HotkeyManager, Views/{Confirm, Fold}, Icons/{FolderIconWindow, AiIconGenerator}. Ortak metinler (Vazgeç, Göster,
// Gizle, Renk, renk ve klasör adları) En.Common.cs / En.P5.cs'te. Terim: "bölme" = "panel".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSweepShell() => Add(
    [
        // Açılış, çökme, yeniden adlandırma ve güncelleme bildirimleri (App)
        new("{0} başlatılamadı", "{0} couldn't start"),
        new("Ayar klasörüne erişilemiyor:\n{0}\n\n{1}\n\nTaşınabilir sürümü kullanıyorsan programı yazılabilir bir klasöre (ör. Belgeler) çıkar.",
            "Can't access the settings folder:\n{0}\n\n{1}\n\nIf you're using the portable version, extract the program to a folder you can write to (for example, Documents)."),
        new("Beklenmeyen bir hata oluştu:\n{0}", "Something went wrong:\n{0}"),
        new("{0} — beklenmeyen hata", "{0} — unexpected error"),
        new("{0} kuruluma hazır", "{0} is ready to set up"),
        new("Masaüstünü birkaç adımda düzenlemek için buraya tıkla.", "Click here to organize your desktop in a few steps."),
        new("{0} artık {1}", "{0} is now {1}"),
        new("Adı ve ana penceresi yenilendi; ayarların, widget'ların ve kuralların olduğu gibi duruyor. Açmak için tıkla.",
            "It has a new name and a new main window; your settings, widgets and rules are just as they were. Click to open it."),
        new("{0} güncellendi. Program dosyasının adı değiştiği için tepsi simgesi saatin yanındaki ^ okunun altına geçmiş olabilir; oradan görev çubuğuna sürükleyebilirsin. Görev çubuğuna sabitlediysen yeniden sabitle.",
            "{0} was updated. Because the program file was renamed, the tray icon may have moved under the ^ arrow next to the clock; you can drag it back to the taskbar from there. If you pinned it to the taskbar, pin it again."),

        // Ana pencere kapatılınca (bir kez)
        new("{0} arka planda çalışıyor", "{0} is running in the background"),
        new("Widget'lar ve otomatik taşıma çalışmaya devam ediyor. Açmak için saatin yanındaki {0} simgesine tıkla.",
            "Widgets and auto-move keep working. To open it, click the {0} icon next to the clock."),

        // AppHost: kayıt hatası, çift tıklama ve bölme kipi bildirimleri
        new("Ayarlar şu an kaydedilemedi (dosyayı başka bir program kullanıyor olabilir). Değişikliklerin duruyor; kayıt birazdan yeniden denenecek.",
            "Settings couldn't be saved right now (another program might be using the file). Your changes are kept, and saving will be retried shortly."),
        new("Windows masaüstü açıldı", "Showing the Windows desktop"),
        new("Geri dönmek için yeniden çift tıkla ya da üstteki \"{0}'e dön\"e bas. Çift tıklamanın ne yapacağını Ayarlar > Masaüstü'nden seçebilirsin.",
            "To go back, double-click again or select \"Back to {0}\" at the top. You can choose what double-clicking does in Settings > Desktop."),
        new("Widget'lar ve simgeler gizlendi", "Widgets and icons are hidden"),
        new("Masaüstü simgeleri gizlendi", "Desktop icons are hidden"),
        new("Masaüstüne yeniden çift tıkla ya da buraya tıkla, geri gelsin. Bu özellik Ayarlar'dan kapatılabilir.",
            "Double-click the desktop again or click here to bring them back. You can turn this off in Settings."),
        new("Masaüstü simgeleri yeniden gösteriliyor", "Desktop icons are showing again"),
        new("Bir bölme kaldırıldığı için bazı masaüstü öğeleri hiçbir bölmede görünmüyordu. İstersen Widget'lar sayfasından yeniden aç.",
            "A panel was removed, so some desktop items weren't showing in any panel. You can turn this back on from the Widgets page."),

        // AppInfo
        new("Masaüstün için derli toplu bir yuva.", "A tidy home for your desktop."),

        // Store sürümü: "Windows ile başlat" (PackagedApp)
        new("Windows'un Başlangıç ayarlarından ya da Görev Yöneticisi'nden kapatılmış; Windows yeniden açmaya yalnızca oradan izin verir.",
            "Turned off in Windows Startup settings or Task Manager; Windows only lets you turn it back on from there."),
        new("Yönetici ilkesiyle kapalı; buradan değiştirilemez.", "Turned off by administrator policy; you can't change it here."),
        new("Yönetici ilkesiyle açık; buradan değiştirilemez.", "Turned on by administrator policy; you can't change it here."),
        new("Durum okunamadı; ayar Windows'un Başlangıç uygulamaları sayfasında.",
            "Couldn't read the status; you'll find this setting on the Windows Startup apps page."),

        // Genel kısayollar (HotkeyManager)
        new("Geçersiz kısayol", "Invalid shortcut"),
        new("Klavyende AltGr ile \"{0}\" yazılıyor; başka bir kısayol seç (ör. Win+Shift+…)",
            "Your keyboard types \"{0}\" with this combination (AltGr); choose another shortcut (for example, Win+Shift+…)"),
        new("Başka bir uygulama kullanıyor", "Another app is using it"),

        // Klasör simgesi kütüphanesi (FolderIconCatalog, L.N): öteki adlar En.Common.cs ve En.P5.cs'te
        new("Finans", "Finance"),
        new("Kişisel", "Personal"),
        new("Önemli", "Important"),
        new("Sağlık", "Health"),
        new("Gizli", "Private", Context: "simge"),
        new("Kitaplar", "Books"),
        new("Planlar", "Plans"),
        new("Araç", "Vehicles", Context: "taşıt"),
        new("Kehribar", "Amber"),
        new("Kırmızı", "Red"),
        new("Çivit", "Indigo"),
        new("Turkuaz", "Turquoise"),
        new("Gri", "Gray"),

        // Klasör simgesi penceresi (FolderIconWindow)
        new("Klasör simgesi", "Folder icon"),
        new("Klasör simgesi — {0}", "Folder icon — {0}"),
        new("Simgeyi uygula", "Apply icon"),
        new("Dosyadan yükle…", "Load from file…"),
        new(".ico, .png, .jpg veya .svg", ".ico, .png, .jpg or .svg"),
        new("Varsayılana dön", "Restore default"),
        new("Sembol", "Symbol"),
        new("Yapay zekâ ile üret (isteğe bağlı)", "Generate with AI (optional)"),
        new("Ne görmek istediğini yaz; Claude bu klasöre özel bir simge çizsin. Ör: \"retro oyun kolu, neon mor\", \"kahve fincanı ve kitap\".",
            "Describe what you'd like to see, and Claude will draw an icon just for this folder. For example: \"retro game controller, neon purple\", \"coffee cup and a book\"."),
        new("Üret", "Generate"),
        new("Claude simgeyi çiziyor… (10–30 sn)", "Claude is drawing the icon… (10–30 s)"),
        new("API anahtarı eklenmemiş", "No API key added"),
        new("Bu özellik isteğe bağlı. Ayarlar → Gelişmiş → Yapay zekâ ile klasör simgesi bölümünden Claude API anahtarını ekleyince açılır; hazır simgeler anahtarsız da çalışır.",
            "This feature is optional. It turns on when you add your Claude API key in Settings → Advanced; the built-in icons work without a key."),
        new("Önceki üretimler", "Previously generated"),
        new("Tarayıcıda boş bir bildirim formu açılır; hiçbir şey kendiliğinden gönderilmez.",
            "Opens a blank report form in your browser; nothing is sent automatically."),
        new("Uygunsuz içeriği bildir", "Report inappropriate content"),
        new("Gizlilik politikası", "Privacy policy"),
        new("Yapay zekâ · {0}", "AI · {0}"),
        new("Dosya · {0}", "File · {0}"),
        new("Uygulandı", "Applied"),
        new("Masaüstü birkaç saniye içinde yeni simgeyi gösterir.", "The desktop will show the new icon in a few seconds."),
        new("Uygulanamadı", "Couldn't apply"),
        new("Varsayılana döndü", "Default restored"),
        new("Klasör standart Windows simgesini kullanıyor.", "The folder is using the standard Windows icon."),
        new("Geri alınamadı", "Couldn't restore the default", Context: "klasör simgesi"),
        new("Simge ve resimler|*.ico;*.png;*.jpg;*.jpeg;*.svg|Tüm dosyalar|*.*", "Icons and images|*.ico;*.png;*.jpg;*.jpeg;*.svg|All files|*.*"),
        new("Dosya okunamadı", "Couldn't read the file"),
        new("Üretilemedi", "Couldn't generate"),
        new("Beklenmeyen hata", "Unexpected error"),

        // Yapay zekâ simgesi hataları (AiIconGenerator)
        new("Anahtar geçersiz.", "The key isn't valid."),
        new("Bu anahtarın modele erişim izni yok.", "This key doesn't have access to the model."),
        new("API hatası: {0}", "API error: {0}"),
        new("Sunucuya ulaşılamadı. İnternet bağlantını kontrol et.", "Couldn't reach the server. Check your internet connection."),
        new("API anahtarı geçersiz. Ayarlar'dan kontrol et.", "The API key isn't valid. Check it in Settings."),
        new("Çok fazla istek gönderildi; biraz bekleyip tekrar dene.", "Too many requests were sent; wait a moment and try again."),
        new("Bu açıklama için simge üretilemedi. Farklı bir açıklama dene.", "Couldn't generate an icon for this description. Try a different description."),
        new("Yanıtta SVG bulunamadı. Tekrar dene.", "The response didn't contain an SVG. Try again."),
        new("Üretilen SVG çizilemedi.", "Couldn't draw the generated SVG."),
    ]);
}
