using System.Runtime.InteropServices;
using Duzenleme.Widgets;

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
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    // Simge görünümünü (SHELLDLL_DefView) son bulunduğu WorkerW (Windows 10 / 11 23H2'de slayt gösterisi vb. sonrası).
    // Masaüstü nöbetçisi 3 sn'de bir sorar: o düzende pencereleri her seferinde taramamak için önce buraya bakılır. Progman'ın
    // altındaki görünüm (Windows 11 24H2+ ve bölünmemiş düzen) zaten iki çağrıyla bulunur, önbelleğe alınmaz. Herhangi bir iş
    // parçacığından okunur.
    private static IntPtr _defViewWorkerW;

    /// <summary>
    /// Masaüstündeki SHELLDLL_DefView (simge listesinin kabı). Önce Progman'ın altına, sonra son bulunduğu WorkerW'ye (hâlâ
    /// duruyor ve sınıfı WorkerW ise) bakılır; ikisinde de yoksa (Gezgin yeniden başladı, slayt gösterisi görünümü taşıdı)
    /// masaüstünün WorkerW'leri taranır. Tek simge görünümü olduğundan sonuç baştan aramayla aynıdır.
    /// </summary>
    public static IntPtr FindDefView()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero) return defView;

        var cached = Volatile.Read(ref _defViewWorkerW);
        if (cached != IntPtr.Zero && NativeMethods.IsWindow(cached) && NativeMethods.ClassOf(cached) == "WorkerW")
        {
            defView = FindWindowEx(cached, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero) return defView;
        }

        // Duvar kağıdı slayt gösterisinde görünüm bir WorkerW altına taşınır.
        var host = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            // Yalnızca masaüstünün WorkerW'leri: başka bir programın içindeki kabuk görünümü (ör. dosya iletişim kutusu) değil.
            if (NativeMethods.ClassOf(hwnd) != "WorkerW") return true;
            var found = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (found == IntPtr.Zero) return true;
            (defView, host) = (found, hwnd);
            return false;
        }, IntPtr.Zero);
        Volatile.Write(ref _defViewWorkerW, host);
        return defView;
    }

    /// <summary>
    /// Widget'ların sahibi olacak pencere: simge görünümünü (SHELLDLL_DefView) taşıyan üst pencere.
    /// Windows 11 24H2+'da ve bölünmemiş düzende Progman; Windows 10 / 11 23H2'de slayt gösterisi vb. sonrası WorkerW.
    /// Ucuzdur: pencereler yalnızca görünüm yer değiştirince taranır (bkz. <see cref="FindDefView"/>).
    /// </summary>
    public static IntPtr DesktopOwner()
    {
        var defView = FindDefView();
        var host = defView == IntPtr.Zero ? IntPtr.Zero : GetParent(defView);
        return host != IntPtr.Zero ? host : NativeMethods.FindWindow("Progman", null);
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

    /// <summary>
    /// Windows'un kendi "Masaüstü simgelerini göster" seçeneği kapalı mı (masaüstüne sağ tık → Görünüm)? Kapalıyken simgeler
    /// Gezgin tarafından gizlenir; uygulamanın gösterme/gizleme isteği görünürde bir şey değiştirmez.
    /// </summary>
    public static bool HiddenByWindows
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                return key?.GetValue("HideIcons") is int v && v != 0;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                return false;
            }
        }
    }

    /// <summary>Tanı için masaüstü pencerelerinin durumu (günlüğe yazılır; başka bilgisayardaki hatayı anlamak için).</summary>
    public static string Describe()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        var defView = FindDefView();
        var host = defView == IntPtr.Zero ? IntPtr.Zero : GetParent(defView);
        var lv = defView == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        return $"Progman={progman:X} DefView={defView:X} parent={host:X}({(host == IntPtr.Zero ? "-" : NativeMethods.ClassOf(host))}) " +
               $"liste={lv:X} visible={(lv != IntPtr.Zero && IsWindowVisible(lv))} hiddenByWindows={HiddenByWindows} " +
               $"Windows={Environment.OSVersion.Version}"; // l10n: çevrilmez
    }

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
        var cls = NativeMethods.ClassOf(root);
        if (cls == "Progman") return true;
        // Windows 10 / 11 23H2'de (duvar kağıdı slayt gösterisi vb.) simgeler bir WorkerW'nin altındadır.
        return cls == "WorkerW" && FindWindowEx(root, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
    }

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
