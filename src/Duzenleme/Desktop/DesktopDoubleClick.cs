using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Duzenleme.Desktop;

/// <summary>
/// Masaüstünün boş bir yerine çift tıklamayı algılar (Fences/iTop'taki "çift tıkla gizle").
/// <para>Fare kancası (WH_MOUSE_LL) kullanılmaz: kanca varken sistemdeki her fare olayı uygulamanın yanıtını bekler; uygulama
/// o an meşgulse imleç herkes için takılır ve Windows yavaş kancayı sessizce kaldırır. Bunun yerine "ham giriş" (Raw Input,
/// RIDEV_INPUTSINK) kendi iş parçacığındaki yalnızca-ileti penceresine gelir: Windows olayı kuyruğa bırakır, hiçbir şeyi
/// beklemez. Yalnızca birincil (solak ayarında sağ) tuşa basışların zamanı ve yeri bellekte tutulur.</para>
/// Çift tıklama olunca karar (masaüstüne mi tıklandı, bir simge mi seçili) arayüz iş parçacığının dışında verilir;
/// Gezgin meşgul olsa da widget'lar beklemez. Sonuç geri çağrı olarak arayüz iş parçacığına iletilir.
/// </summary>
public sealed class DesktopDoubleClick : IDisposable
{
    private const int SM_CXDOUBLECLK = 36;
    private const int SM_CYDOUBLECLK = 37;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;
    private const uint RIDEV_REMOVE = 0x00000001;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG msg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern uint GetMessagePos();

    [DllImport("user32.dll")]
    private static extern int GetMessageTime();

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    /// <summary>Bir açık kalma dönemi: kendi iş parçacığı, penceresi ve pencere yordamı (GC toplamasın diye alanda).</summary>
    private sealed class Session
    {
        public IntPtr Hwnd;
        public int Stop;
        public WndProc? Proc;
        public IntPtr Buffer;
        public uint LastTime;
        public POINT LastPoint;
        public bool HasLast;
    }

    /// <summary>Bir fare olayının ham verisi (x64'te 48 bayt) için yeterli, oturum başına bir kez ayrılan tampon.</summary>
    private const int BufferSize = 128;

    private static int _classCounter;
    private readonly Dispatcher _dispatcher;
    private readonly Action _onDoubleClick;
    private Session? _session;

    public DesktopDoubleClick(Dispatcher dispatcher, Action onDoubleClick)
    {
        _dispatcher = dispatcher;
        _onDoubleClick = onDoubleClick;
    }

    public bool Enabled => _session is not null;

    /// <summary>Ham fare girişini dinlemeye başlar. Beklemez: pencere kendi iş parçacığında kurulur.</summary>
    public void Enable()
    {
        if (_session is not null) return;
        var session = new Session();
        _session = session;
        var thread = new Thread(() => Run(session))
        {
            IsBackground = true,
            Name = $"{Core.AppInfo.Name} çift tık (ham giriş)", // l10n: çevrilmez (iş parçacığı adı)
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    /// <summary>Dinlemeyi bırakır. Beklemez: iş parçacığı penceresini kapatıp kendiliğinden biter.</summary>
    public void Disable()
    {
        var session = _session;
        if (session is null) return;
        _session = null;
        Interlocked.Exchange(ref session.Stop, 1);
        var hwnd = Interlocked.CompareExchange(ref session.Hwnd, IntPtr.Zero, IntPtr.Zero);
        if (hwnd != IntPtr.Zero) PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void Run(Session session)
    {
        var instance = GetModuleHandle(null);
        var className = $"{Core.AppInfo.Name}.RawMouse.{Interlocked.Increment(ref _classCounter)}";
        session.Proc = (hwnd, msg, wParam, lParam) => WindowProc(session, hwnd, msg, wParam, lParam);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(session.Proc),
            hInstance = instance,
            lpszClassName = className,
        };
        if (RegisterClassEx(ref wc) == 0)
        {
            DebugLog.Write($"çift tık: pencere sınıfı kaydedilemedi ({Marshal.GetLastWin32Error()})");
            return;
        }
        session.Buffer = Marshal.AllocHGlobal(BufferSize);
        try
        {
            var hwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, instance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
            {
                DebugLog.Write($"çift tık: pencere kurulamadı ({Marshal.GetLastWin32Error()})");
                return;
            }
            Interlocked.Exchange(ref session.Hwnd, hwnd);
            // Disable bu arada çağrıldıysa (pencere henüz yokken) hemen kapan.
            if (Volatile.Read(ref session.Stop) != 0)
            {
                DestroyWindow(hwnd);
                return;
            }

            // Genel masaüstü fare aygıtı (usage page 1, usage 2); arka plandayken de (INPUTSINK) bu pencereye gelsin.
            var devices = new[] { new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_INPUTSINK, hwndTarget = hwnd } };
            if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                DebugLog.Write($"çift tık: ham giriş kaydedilemedi ({Marshal.GetLastWin32Error()})");
                DestroyWindow(hwnd);
                return;
            }
            DebugLog.Write("çift tık: ham fare girişi dinleniyor");

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) DispatchMessage(ref msg);
            DebugLog.Write("çift tık: dinleme bitti");

            devices[0] = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };
            RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }
        finally
        {
            UnregisterClass(className, instance);
            Marshal.FreeHGlobal(session.Buffer);
            session.Buffer = IntPtr.Zero;
            GC.KeepAlive(session.Proc);
        }
    }

    private IntPtr WindowProc(Session session, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INPUT:
                try { OnInput(session, lParam); }
                catch (Exception ex) { DebugLog.Write("çift tık: " + ex.Message); }
                break; // DefWindowProc ham girişin temizliğini yapar
            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    // RAWINPUT: başlık, ardından RAWMOUSE { USHORT usFlags; (2 bayt hizalama) USHORT usButtonFlags; ... }.
    private static readonly int HeaderSize = Marshal.SizeOf<RAWINPUTHEADER>();
    private static readonly int ButtonFlagsOffset = HeaderSize + 4;

    private void OnInput(Session session, IntPtr rawInput)
    {
        var size = 0u;
        GetRawInputData(rawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)HeaderSize);
        if (size == 0 || size > BufferSize) return;
        if (GetRawInputData(rawInput, RID_INPUT, session.Buffer, ref size, (uint)HeaderSize) != size) return;
        if ((uint)Marshal.ReadInt32(session.Buffer) != RIM_TYPEMOUSE || size < ButtonFlagsOffset + 2) return;
        var buttons = (ushort)Marshal.ReadInt16(session.Buffer, ButtonFlagsOffset);
        // Ham giriş fiziksel düğmeyi bildirir: solak ayarında birincil düğme sağdadır (2.0'ın kancası WM_LBUTTONDOWN'a,
        // yani değişimi uygulanmış düğmeye bakıyordu). Ayar her basışta okunur (ucuz); değiştirilince hemen geçerli.
        var primary = Core.PrimaryMouseButton.RawInputDownFlag(GetSystemMetrics(Core.PrimaryMouseButton.SM_SWAPBUTTON) != 0);
        if ((buttons & primary) == 0) return;

        // Olayın ekrandaki yeri ve zamanı (fiziksel piksel; sol monitörde eksi olabilir).
        var pos = GetMessagePos();
        var pt = new POINT { X = (short)(pos & 0xFFFF), Y = (short)((pos >> 16) & 0xFFFF) };
        var time = (uint)GetMessageTime();
        var isDouble = session.HasLast
                       && time - session.LastTime <= GetDoubleClickTime()
                       && Math.Abs(pt.X - session.LastPoint.X) <= GetSystemMetrics(SM_CXDOUBLECLK)
                       && Math.Abs(pt.Y - session.LastPoint.Y) <= GetSystemMetrics(SM_CYDOUBLECLK);
        session.LastTime = time;
        session.LastPoint = pt;
        session.HasLast = !isDouble; // üçlü tıklama ikinci kez tetiklemesin
        if (isDouble) _ = EvaluateAsync(pt);
    }

    /// <summary>Karar arayüz iş parçacığının dışında: Gezgin'e yapılan (süre sınırlı) çağrılar widget'ları bekletmez.</summary>
    private async Task EvaluateAsync(POINT pt)
    {
        try
        {
            if (!DesktopIcons.IsDesktopSurface(WindowFromPoint(pt))) return;
            // Gezgin tıklamayı işlesin; simgeye çift tıklandıysa seçili simge olur ve dosya açılır — o zaman gizleme.
            await Task.Delay(90).ConfigureAwait(false);
            if (DesktopIcons.AreVisible && DesktopIcons.SelectedCount() > 0) return;
            await _dispatcher.BeginInvoke(_onDoubleClick);
        }
        catch (Exception ex)
        {
            DebugLog.Write("çift tık değerlendirilemedi: " + ex.Message);
        }
    }

    public void Dispose() => Disable();
}
