using System.Diagnostics;

namespace Duzenleme.Views;

/// <summary>Bağlantıları varsayılan tarayıcıda açar; kabuk çağrısı (ShellExecute) arayüzü bekletmesin diye arka planda.</summary>
internal static class Browser
{
    public static void Open(string url) => Task.Run(() =>
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DebugLog.Write($"bağlantı açılamadı: {url} {ex.Message}");
        }
    });
}
