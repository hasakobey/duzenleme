using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// Kullanıcı hataları düzeltmeleri (2.1.x): klasör açılamayınca açıklama (Core/FolderCreation, Views/Notice, AppHost), Windows
// masaüstü simgelerine ulaşılamaması (AppHost), widget'lar arası sürükle-bırak (Widgets/FenceView, LauncherView).
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddFixes() => Add(
    [
        // Klasör açılamadı
        new("Windows Güvenliği \"{0}\" klasörünü açmayı engelledi (Denetimli klasör erişimi). {1}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
            "Windows Security blocked creating the \"{0}\" folder (Controlled folder access). To allow {1}: Windows Security → Virus & threat protection → Ransomware protection → Allow an app through Controlled folder access."),
        new("\"{0}\" klasörü açılamadı: Windows bu konuma yazma izni vermedi ({1}). Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.",
            "Couldn't create the \"{0}\" folder: Windows didn't allow writing to this location ({1}). Controlled folder access in Windows Security or an antivirus program might be blocking it."),
        new("\"{0}\" konumuna ulaşılamıyor. Masaüstün OneDrive'da ya da ağda duruyorsa bağlantıyı denetle.",
            "Can't reach \"{0}\". If your desktop is on OneDrive or a network, check the connection."),
        new("\"{0}\" klasörü açılamadı: {1}", "Couldn't create the \"{0}\" folder: {1}"),
        new("Windows Güvenliği'ni aç", "Open Windows Security"),
        new("\"{0}\" taşınamadı. {1}", "Couldn't move \"{0}\". {1}"),

        // Windows masaüstü simgeleri
        new("Windows masaüstü simgelerine şu an ulaşılamıyor, bu yüzden gizlenemediler. Gezgin yeniden başlıyorsa birkaç saniye sonra yeniden dene.",
            "Can't reach the Windows desktop icons right now, so they weren't hidden. If File Explorer is restarting, try again in a few seconds."),
        new("Windows'ta masaüstü simgeleri zaten kapalı. Açmak için masaüstüne sağ tıkla → Görünüm → Masaüstü simgelerini göster.",
            "Desktop icons are already turned off in Windows. To turn them on, right-click the desktop → View → Show desktop icons."),

        // Sürükle-bırak
        new("Bırak, masaüstüne taşınsın", "Drop to move to the desktop"),
        new("Bırak, kutudan çıkarılsın", "Drop to take it out of the box"),
        new("Bırak, bu kutuya taşınsın", "Drop to move it to this box"),
    ]);
}
