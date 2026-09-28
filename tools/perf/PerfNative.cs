// tools/perf/perf-run.ps1 yardımcısı (PowerShell Add-Type ile derlenir; uygulamaya girmez).
// Yalnızca test örneğinin kendi pencerelerine ileti gönderir; gerçek fare/klavye kullanılmaz.
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
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

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
