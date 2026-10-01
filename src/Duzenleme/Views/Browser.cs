using System.Diagnostics;

namespace Duzenleme.Views;

/// <summary>Bağlantıları varsayılan tarayıcıda açar; kabuk çağrısı (ShellExecute) arayüzü bekletmesin diye arka planda.</summary>
internal static class Browser
{
    public static void Open(string url) => Open(url, log: true);

    /// <summary>
    /// <paramref name="log"/> false ise adres günlüğe hiç yazılmaz, açılamazsa yalnızca hatanın türü yazılır (halka menünün
    /// kullanıcı adresleri: RingPrivacyTests Ring* dosyalarında yalnızca bu biçimi kabul eder).
    /// </summary>
    public static void Open(string url, bool log) => Task.Run(() =>
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (log) DebugLog.Write($"bağlantı açılamadı: {url} {ex.Message}");
            else DebugLog.Write($"bağlantı açılamadı: {ex.GetType().Name}");
        }
    });
}
