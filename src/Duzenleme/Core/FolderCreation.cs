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
