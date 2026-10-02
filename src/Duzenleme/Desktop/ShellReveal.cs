using System.IO;
using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// Dosyayı Gezgin'de, klasörü açıp seçili olarak gösterir ("Dosya konumunu aç"). <c>explorer.exe /select</c> yerine kabuğun
/// kendi çağrısı (SHOpenFolderAndSelectItems): yol tırnak/virgül yorumuna takılmaz, zaten açık klasör penceresi yeniden
/// kullanılır. Kabuk çağrısı arayüzü bekletmesin diye kendi STA iş parçacığında (<see cref="ShellFileOperations.RunSta"/>).
/// </summary>
internal static class ShellReveal
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, IntPtr[]? items, uint flags);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    /// <summary>
    /// <paramref name="path"/>'i (Gezgin'in gördüğü yol; Store sürümünde <c>AppHost.PathOnDisk</c> ile çözülmüş) içeren
    /// klasörü açıp öğeyi seçer. Öğe artık yoksa klasörü açar. Hata fırlatmaz.
    /// </summary>
    public static void Select(string path) => _ = ShellFileOperations.RunSta(() =>
    {
        var pidl = IntPtr.Zero;
        try
        {
            var hr = SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _);
            if (hr == 0 && pidl != IntPtr.Zero)
            {
                hr = SHOpenFolderAndSelectItems(pidl, 0, null, 0);
                if (hr == 0) return;
            }
            AppLog.Info("shell", "reveal-failed", Marshal.GetExceptionForHR(hr));
            // Öğe bulunamadı (silindi/taşındı): yalnızca klasörü aç.
            if (Path.GetDirectoryName(path) is { } folder && Directory.Exists(folder))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or COMException or IOException)
        {
            AppLog.Info("shell", "reveal-failed", ex);
        }
        finally
        {
            if (pidl != IntPtr.Zero) ILFree(pidl);
        }
    });
}
