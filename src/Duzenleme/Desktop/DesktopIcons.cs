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
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

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
            var found = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (found == IntPtr.Zero) return true;
            defView = found;
            return false;
        }, IntPtr.Zero);
        return defView;
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

    public static bool SetVisible(bool visible)
    {
        var lv = FindListView();
        if (lv == IntPtr.Zero) return false;
        ShowWindow(lv, visible ? SW_SHOW : SW_HIDE);
        return true;
    }

    /// <summary>Seçili masaüstü simgesi sayısı (boşluğa tıklanınca 0 olur).</summary>
    public static int SelectedCount()
    {
        var lv = FindListView();
        return lv == IntPtr.Zero ? 0 : (int)SendMessage(lv, LVM_GETSELECTEDCOUNT, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Verilen pencere masaüstünün kendisi mi (simge listesi, kabı ya da duvar kağıdı)?</summary>
    public static bool IsDesktopSurface(IntPtr hwnd)
    {
        for (var depth = 0; hwnd != IntPtr.Zero && depth < 4; depth++)
        {
            var cls = ClassOf(hwnd);
            if (cls is "SysListView32" or "SHELLDLL_DefView" or "Progman" or "WorkerW")
            {
                // WorkerW yalnızca masaüstü görünümünü taşıyorsa masaüstüdür.
                return cls != "WorkerW" || FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero || hwnd == GetParent(FindDefView());
            }
            hwnd = GetParent(hwnd);
        }
        return false;
    }

    private static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(64);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
