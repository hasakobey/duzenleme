using System.Security.Cryptography;
using System.Text;

namespace Duzenleme.Icons;

/// <summary>
/// API anahtarını Windows DPAPI ile (yalnızca bu Windows kullanıcısının çözebileceği şekilde) şifreleyerek saklar.
/// Ayar dosyası kopyalansa bile anahtar başka bir hesapta okunamaz (dolaşım profilinde aynı hesap başka bilgisayarda çözebilir).
/// </summary>
public static class ApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Duzenleme.ApiKey.v1");

    public static bool HasKey => !string.IsNullOrEmpty(AppHost.Settings.AiKeyProtected);

    public static void Save(string? apiKey)
    {
        AppHost.Settings.AiKeyProtected = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), Entropy, DataProtectionScope.CurrentUser));
        AppHost.SaveSettings();
    }

    public static string? Load()
    {
        if (!HasKey) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(AppHost.Settings.AiKeyProtected!), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Arayüzde göstermek için: "sk-ant-…a1b2".</summary>
    public static string Masked()
    {
        var key = Load();
        if (string.IsNullOrEmpty(key)) return "";
        return key.Length <= 12 ? "••••" : $"{key[..7]}…{key[^4..]}";
    }
}
