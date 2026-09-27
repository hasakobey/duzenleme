using System.Runtime.InteropServices;
using System.Text;

namespace Duzenleme.Desktop;

/// <summary>
/// Windows masaüstü simgelerini (Explorer'ın SysListView32 penceresi) gizler/gösterir.
/// Dosyalara dokunmaz; yalnızca simge görünümünü saklar.
/// </summary>
public static class DesktopIcons
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETSELECTEDCOUNT = LVM_FIRST + 50;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    /// <summary>Masaüstündeki SHELLDLL_DefView (simge listesinin kabı).</summary>
    public static IntPtr FindDefView()
    {
        var progman = FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero) return defView;

        // Duvar kağıdı slayt gösterisinde görünüm bir WorkerW altına taşınır.
        EnumWindows((hwnd, _) =>
        {
            // Yalnızca masaüstünün WorkerW'leri: başka bir programın içindeki kabuk görünümü (ör. dosya iletişim kutusu) değil.
            if (ClassOf(hwnd) != "WorkerW") return true;
            var found = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (found == IntPtr.Zero) return true;
            defView = found;
            return false;
        }, IntPtr.Zero);
        return defView;
    }

    /// <summary>
    /// Widget'ların sahibi olacak pencere: simge görünümünü (SHELLDLL_DefView) taşıyan üst pencere.
    /// Windows 11 24H2+'da ve bölünmemiş düzende Progman; Windows 10 / 11 23H2'de slayt gösterisi vb. sonrası WorkerW.
    /// </summary>
    public static IntPtr DesktopOwner()
    {
        var defView = FindDefView();
        var host = defView == IntPtr.Zero ? IntPtr.Zero : GetParent(defView);
        return host != IntPtr.Zero ? host : FindWindow("Progman", null);
    }

    public static IntPtr FindListView()
    {
        var defView = FindDefView();
        return defView == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    public static bool AreVisible
    {
        get
        {
            var lv = FindListView();
            return lv == IntPtr.Zero || IsWindowVisible(lv);
        }
    }

    /// <summary>
    /// Simge listesini gösterir/gizler. Liste Explorer'ın penceresi olduğundan istek kuyruğuna bırakılır: Explorer
    /// o an meşgulse (büyük bir klasör açılırken) widget'lar onu beklerken donmasın.
    /// </summary>
    public static bool SetVisible(bool visible)
    {
        var lv = FindListView();
        if (lv == IntPtr.Zero) return false;
        _lastRequest = Environment.TickCount64;
        ShowWindowAsync(lv, visible ? SW_SHOW : SW_HIDE);
        return true;
    }

    private static long _lastRequest;

    /// <summary>Az önce gösterme/gizleme istendi mi? (Explorer isteği henüz işlememiş olabilir; durum yanlış okunmasın.)</summary>
    public static bool ChangePending => Environment.TickCount64 - _lastRequest < 5000;

    /// <summary>Seçili masaüstü simgesi sayısı (boşluğa tıklanınca 0 olur); Explorer yanıt vermiyorsa 0.</summary>
    public static int SelectedCount()
    {
        var lv = FindListView();
        if (lv == IntPtr.Zero) return 0;
        const uint SMTO_ABORTIFHUNG = 0x0002;
        return SendMessageTimeout(lv, LVM_GETSELECTEDCOUNT, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 300, out var count) == IntPtr.Zero
            ? 0 : (int)count;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    /// <summary>
    /// Verilen pencere masaüstünün kendisi mi (simge listesi, kabı ya da duvar kağıdı)? En üstteki pencereye bakılır:
    /// Gezgin pencereleri ve dosya açma/kaydetme kutuları da SHELLDLL_DefView içerir; onların içindeki çift tıklama
    /// masaüstü sayılırsa klasörde gezinirken her çift tıklama widget'ları gizleyip açar.
    /// </summary>
    public static bool IsDesktopSurface(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var root = GetAncestor(hwnd, GA_ROOT);
        var cls = ClassOf(root);
        if (cls == "Progman") return true;
        // Windows 10 / 11 23H2'de (duvar kağıdı slayt gösterisi vb.) simgeler bir WorkerW'nin altındadır.
        return cls == "WorkerW" && FindWindowEx(root, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
    }

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    private static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(64);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
