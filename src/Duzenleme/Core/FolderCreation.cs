using System.IO;
using Microsoft.Win32;

namespace Duzenleme.Core;

/// <summary>Klasör oluşturma sonucu: başarılıysa <see cref="Error"/> null; değilse kullanıcıya gösterilecek açıklama.</summary>
public readonly record struct FolderCreateResult(string? Error, bool BlockedBySecurity)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Uygulamanın kullanıcı adına açtığı klasörler (bölme klasörü, kural klasörü, "Yeni klasör"). Başka bilgisayarlarda en sık
/// hata Windows Güvenliği'nin "Denetimli klasör erişimi"dir: masaüstüne yazmak isteyen tanınmayan uygulamayı engeller ve
/// .NET yalnızca İngilizce "Access to the path is denied" der. Burada hata kullanıcının anlayacağı bir cümleye çevrilir.
/// </summary>
public static class FolderCreation
{
    /// <summary>Windows Güvenliği'nin fidye yazılımı koruması sayfası.</summary>
    public const string RansomwareProtectionUri = "windowsdefender://ransomwareprotection";

    /// <summary>Klasörü açar (varsa bir şey yapmaz). Hata fırlatmaz.</summary>
    public static FolderCreateResult TryCreate(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return new(null, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var result = Explain(ex, path);
            JsonFile.Log?.Invoke($"klasör oluşturulamadı: {path}: {ex.GetType().Name} {ex.Message} (engel={result.BlockedBySecurity})"); // l10n: çevrilmez
            AppLog.Warn("folder", "create-failed", ex);
            return result;
        }
    }

    /// <summary>Oluşturma hatasını açıklar. Denetimli klasör erişimi açıksa bunu, değilse genel izin sorununu söyler.</summary>
    public static FolderCreateResult Explain(Exception ex, string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) ?? path;
        switch (ex)
        {
            case UnauthorizedAccessException when ControlledFolderAccessOn():
                return new(L.F("Windows Güvenliği \"{0}\" klasörünü açmayı engelledi (Denetimli klasör erişimi). {1}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
                    name, AppInfo.Name), true);
            case UnauthorizedAccessException:
                return new(L.F("\"{0}\" klasörü açılamadı: Windows bu konuma yazma izni vermedi ({1}). Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.",
                    name, parent), true);
            case PathTooLongException:
                return new(L.T("Ad çok uzun: Windows bu kadar uzun bir yolu açamaz."), false);
            case DirectoryNotFoundException:
                return new(L.F("\"{0}\" konumuna ulaşılamıyor. Masaüstün OneDrive'da ya da ağda duruyorsa bağlantıyı denetle.", parent), false);
            case ArgumentException or NotSupportedException:
                return new(FileNames.InvalidCharactersHint, false);
            default:
                return new(L.F("\"{0}\" klasörü açılamadı: {1}", name, ex.Message), false);
        }
    }

    /// <summary>
    /// Otomatik taşımanın hatasını açıklar: dosya dosya olarak anılır ve izin vermeyen yer olarak kuralın hedef klasörü
    /// (<paramref name="targetDirectory"/>; karar verilemeden olduysa null) gösterilir. İzin hatası Denetimli klasör erişimi
    /// açıksa onu, değilse genel izin sorununu söyler; öbür hatalar yalnızca "taşınamadı".
    /// </summary>
    public static FolderCreateResult ExplainMove(Exception ex, string file, string? targetDirectory) =>
        ExplainMove(ex, file, targetDirectory, ex is UnauthorizedAccessException && ControlledFolderAccessOn());

    /// <param name="controlledFolderAccess">Denetimli klasör erişimi açık mı (testte verilir).</param>
    internal static FolderCreateResult ExplainMove(Exception ex, string file, string? targetDirectory, bool controlledFolderAccess)
    {
        var name = Path.GetFileName(file);
        var target = targetDirectory is null ? null : Path.TrimEndingDirectorySeparator(targetDirectory);
        var folder = target is null ? null : Path.GetFileName(target);
        if (ex is not UnauthorizedAccessException) return new(L.F("\"{0}\" taşınamadı.", name), false);
        if (controlledFolderAccess)
            return new(folder is null
                ? L.F("Windows Güvenliği \"{0}\" dosyasının taşınmasını engelledi (Denetimli klasör erişimi). {1}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
                    name, AppInfo.Name)
                : L.F("Windows Güvenliği \"{0}\" dosyasının \"{1}\" klasörüne taşınmasını engelledi (Denetimli klasör erişimi). {2}'e izin vermek için: Windows Güvenliği → Virüs ve tehdit koruması → Fidye yazılımı koruması → Denetimli klasör erişimi ile bir uygulamaya izin ver.",
                    name, folder, AppInfo.Name), true);
        return new(folder is null
            ? L.F("\"{0}\" taşınamadı: Windows izin vermedi. Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.", name)
            : L.F("\"{0}\" dosyası \"{1}\" klasörüne taşınamadı: Windows izin vermedi ({2}). Windows Güvenliği'nin Denetimli klasör erişimi ya da bir virüs koruma programı engelliyor olabilir.",
                name, folder, target), true);
    }

    /// <summary>Windows Güvenliği'nin Denetimli klasör erişimi açık mı (engelleme ya da yalnızca denetim)? Okunamazsa false.</summary>
    public static bool ControlledFolderAccessOn()
    {
        try
        {
            return ReadMode(@"SOFTWARE\Policies\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access") is 1
                || ReadMode(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access") is 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }

        static int? ReadMode(string key)
        {
            using var k = Registry.LocalMachine.OpenSubKey(key);
            return k?.GetValue("EnableControlledFolderAccess") is int v ? v : null;
        }
    }
}
