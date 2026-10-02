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
        // Otomatik taşıma izin hatası (FolderCreation.ExplainMove): dosya ve kuralın hedef klasörü
        new("\"{0}\" taşınamadı.", "Couldn't move \"{0}\"."),
        new("Windows Güvenliği \"{0}\" dosyasının taşınmasını engelledi (Denetimli klasör erişimi). {1}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
            "Windows Security blocked moving the file \"{0}\" (Controlled folder access). To allow {1}: Windows Security → Virus & threat protection → Ransomware protection → Allow an app through Controlled folder access."),
        new("Windows Güvenliği \"{0}\" dosyasının \"{1}\" klasörüne taşınmasını engelledi (Denetimli klasör erişimi). {2}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
            "Windows Security blocked moving the file \"{0}\" into the \"{1}\" folder (Controlled folder access). To allow {2}: Windows Security → Virus & threat protection → Ransomware protection → Allow an app through Controlled folder access."),
        new("\"{0}\" taşınamadı: Windows izin vermedi. Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.",
            "Couldn't move \"{0}\": Windows didn't give permission. Controlled folder access in Windows Security or an antivirus program might be blocking it."),
        new("\"{0}\" dosyası \"{1}\" klasörüne taşınamadı: Windows izin vermedi ({2}). Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.",
            "Couldn't move the file \"{0}\" into the \"{1}\" folder: Windows didn't give permission ({2}). Controlled folder access in Windows Security or an antivirus program might be blocking it."),

        // Windows masaüstü simgeleri
        new("Windows masaüstü simgelerine şu an ulaşılamıyor, bu yüzden gizlenemediler. Gezgin yeniden başlıyorsa birkaç saniye sonra yeniden dene.",
            "Can't reach the Windows desktop icons right now, so they weren't hidden. If File Explorer is restarting, try again in a few seconds."),
        new("Windows'ta masaüstü simgeleri zaten kapalı. Açmak için masaüstüne sağ tıkla → Görünüm → Masaüstü simgelerini göster.",
            "Desktop icons are already turned off in Windows. To turn them on, right-click the desktop → View → Show desktop icons."),

        // Kutulara eklediklerim masaüstünden kalksın: ortak masaüstü (Widgets/BoxMover)
        new("\"{0}\" bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir.",
            "\"{0}\" is on the Public Desktop shared by every account on this PC, so it stayed on the desktop. Removing it needs administrator permission."),
        new("{0} öğe bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir.",
            "{0} item is on the Public Desktop shared by every account on this PC, so it stayed on the desktop. Removing it needs administrator permission.",
            "{0} items are on the Public Desktop shared by every account on this PC, so they stayed on the desktop. Removing them needs administrator permission."),
        new("Masaüstünden kaldır", "Remove from desktop"),
        new("izin verilmedi", "permission not given"),

        // Sürükle-bırak
        new("Bırak, masaüstüne taşınsın", "Drop to move to the desktop"),
        new("Bırak, kutudan çıkarılsın", "Drop to take it out of the box"),
        new("Bırak, bu kutuya taşınsın", "Drop to move it to this box"),
    ]);
}
