// tools/perf/perf-run.ps1 yardımcısı (PowerShell Add-Type ile derlenir; uygulamaya girmez). Windows PowerShell 5.1'in
// derleyicisi C# 5'tir: $"", ?., => üyeler, nameof ve out var kullanma.
// Yalnızca test örneğinin kendi pencerelerine ileti gönderir; gerçek fare/klavye kullanılmaz. Bkz. docs/perf.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Bir klasörde belli bir dosyanın kaç kez (yeniden adlandırmayla ya da oluşturularak) yerine konduğunu sayar.</summary>
public sealed class WriteCounter : IDisposable
{
    private readonly System.IO.FileSystemWatcher _watcher;
    private readonly string _name;
    private int _count;

    public WriteCounter(string directory, string name)
    {
        _name = name;
        _watcher = new System.IO.FileSystemWatcher(directory) { NotifyFilter = System.IO.NotifyFilters.FileName, InternalBufferSize = 65536 };
        _watcher.Created += (s, e) => Hit(e.Name);
        _watcher.Renamed += (s, e) => Hit(e.Name);
        _watcher.EnableRaisingEvents = true;
    }

    private void Hit(string name)
    {
        if (string.Equals(name, _name, StringComparison.OrdinalIgnoreCase)) System.Threading.Interlocked.Increment(ref _count);
    }

    public int Count { get { return System.Threading.Volatile.Read(ref _count); } }

    public void Dispose() { _watcher.Dispose(); }
}

public static class PerfNative
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
    const int WM_HOTKEY = 0x0312;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>GetGuiResources bayrakları: GDI nesneleri, USER nesneleri ve süreç boyunca en yüksek değerleri.</summary>
    public const int GdiObjects = 0, UserObjects = 1, GdiObjectsPeak = 2, UserObjectsPeak = 4;

    /// <summary>Sürecin görünür widget pencereleri (başlığı "... widget").</summary>
    public static List<IntPtr> WidgetWindows(int pid)
    {
        var list = new List<IntPtr>();
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            var sb = new StringBuilder(128); GetWindowText(h, sb, 128);
            if (sb.ToString().EndsWith(" widget")) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static int UiThreadOf(IntPtr h) { uint p; return (int)GetWindowThreadProcessId(h, out p); }

    public static int Left(IntPtr h) { RECT r; GetWindowRect(h, out r); return r.L; }

    /// <summary>WM_NULL'un pencerenin arayüz iş parçacığından dönüş süresi, ms (zaman aşımında -1).</summary>
    public static double Ping(IntPtr h, uint timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        IntPtr r;
        var ok = SendMessageTimeout(h, 0 /*WM_NULL*/, IntPtr.Zero, IntPtr.Zero, 0 /*SMTO_NORMAL*/, timeoutMs, out r);
        return ok == IntPtr.Zero ? -1 : sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>Tıklamanın gönderdiği WM_MOUSEACTIVATE (widget öne gelir; uygulama sırayı kaydeder).</summary>
    public static void MouseActivate(IntPtr h)
    {
        IntPtr r;
        SendMessageTimeout(h, 0x0021, h, new IntPtr((0x0201 << 16) | 1 /*HTCLIENT*/), 0, 5000, out r);
    }

    /// <summary>
    /// Sürecin kısayol penceresi: yalnızca ileti alan (HWND_MESSAGE altında), başlığı "DuzenlemeHotkeys" olan pencere
    /// (Desktop/HotkeyManager; ad bilerek eski). Kurulu NestDesk'in de aynı adlı penceresi var: süreç kimliğiyle ayırt edilir.
    /// Yoksa IntPtr.Zero.
    /// </summary>
    public static IntPtr HotkeyWindow(int pid)
    {
        var child = IntPtr.Zero;
        while (true)
        {
            child = FindWindowEx(HWND_MESSAGE, child, null, "DuzenlemeHotkeys");
            if (child == IntPtr.Zero) return IntPtr.Zero;
            uint p; GetWindowThreadProcessId(child, out p);
            if (p == (uint)pid) return child;
        }
    }

    /// <summary>
    /// Test örneğine WM_HOTKEY gönderir (id = HotkeyAction + 1; 1 = masaüstünü gizle/göster). Kısayolun kayıtlı olması
    /// gerekmez: uygulama iletiyi kimliğinden tanır, test örneğinin ayarlarında bütün kısayollar boştur (gerçek tuş kapılmaz).
    /// Pencere bulunamazsa false.
    /// </summary>
    public static bool PostHotkey(int pid, int id)
    {
        var h = HotkeyWindow(pid);
        return h != IntPtr.Zero && PostMessage(h, WM_HOTKEY, new IntPtr(id), IntPtr.Zero);
    }

    /// <summary>Sürecin GDI ya da USER nesne sayısı (GetGuiResources; bayraklar yukarıda); süreç açılamazsa -1.</summary>
    public static int GuiResources(int pid, int flags)
    {
        var h = OpenProcess(PROCESS_QUERY_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return -1;
        try { return (int)GetGuiResources(h, (uint)flags); }
        finally { CloseHandle(h); }
    }

    static string ClassOf(IntPtr h) { var sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }

    /// <summary>Gerçek masaüstü simge listesi (SHELLDLL_DefView altındaki SysListView32) görünür mü? Yalnızca okunur.</summary>
    public static string DesktopIconsState()
    {
        var progman = FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (ClassOf(h) != "WorkerW") return true;
                var f = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (f == IntPtr.Zero) return true;
                defView = f; return false;
            }, IntPtr.Zero);
        }
        if (defView == IntPtr.Zero) return "DefView yok";
        var lv = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        if (lv == IntPtr.Zero) return "SysListView32 yok";
        return IsWindowVisible(lv) ? "VISIBLE" : "HIDDEN";
    }
}
