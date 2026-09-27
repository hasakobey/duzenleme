using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Duzenleme.Desktop;

/// <summary>
/// Masaüstünün boş bir yerine çift tıklamayı algılar (Fences/iTop'taki "çift tıkla gizle").
/// Düşük seviyeli fare kancası yalnızca tıklamanın zamanını ve yerini kaydeder; karar UI iş parçacığında verilir.
/// </summary>
public sealed class DesktopDoubleClick : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int SM_CXDOUBLECLK = 36;
    private const int SM_CYDOUBLECLK = 37;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    private readonly HookProc _proc; // GC toplamasın diye alanda tutulur.
    private readonly Dispatcher _dispatcher;
    private readonly Action _onDoubleClick;
    private IntPtr _hook;
    private uint _lastTime;
    private POINT _lastPoint;

    public DesktopDoubleClick(Dispatcher dispatcher, Action onDoubleClick)
    {
        _dispatcher = dispatcher;
        _onDoubleClick = onDoubleClick;
        _proc = HookCallback;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const uint WM_QUIT = 0x0012;
    private Thread? _thread;
    private uint _threadId;

    public bool Enabled => _thread is not null;

    /// <summary>
    /// Kanca kendi mesaj döngüsü olan ayrı bir iş parçacığında kurulur: arayüz meşgulken (büyük klasör listelenirken vb.)
    /// sistem genelinde fare takılmaz ve Windows yavaş kancayı sessizce kaldırmaz.
    /// </summary>
    public void Enable()
    {
        if (_thread is not null) return;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            using (var module = Process.GetCurrentProcess().MainModule)
                _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module?.ModuleName), 0);
            ready.Set();
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        })
        { IsBackground = true, Name = $"{Core.AppInfo.Name} çift tık kancası" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void Disable()
    {
        if (_thread is null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == WM_LBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var isDouble = info.time - _lastTime <= GetDoubleClickTime()
                           && Math.Abs(info.pt.X - _lastPoint.X) <= GetSystemMetrics(SM_CXDOUBLECLK)
                           && Math.Abs(info.pt.Y - _lastPoint.Y) <= GetSystemMetrics(SM_CYDOUBLECLK);
            _lastTime = info.time;
            _lastPoint = info.pt;

            if (isDouble)
            {
                _lastTime = 0; // Üçlü tıklama ikinci kez tetiklemesin.
                var pt = info.pt;
                _dispatcher.BeginInvoke(() => Evaluate(pt));
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Evaluate(POINT pt)
    {
        if (!DesktopIcons.IsDesktopSurface(WindowFromPoint(pt))) return;

        // Explorer tıklamayı işlesin; simgeye çift tıklandıysa seçili simge olur ve dosya açılır — o zaman gizleme.
        var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = TimeSpan.FromMilliseconds(90) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!DesktopIcons.AreVisible || DesktopIcons.SelectedCount() == 0) _onDoubleClick();
        };
        timer.Start();
    }

    public void Dispose() => Disable();
}
