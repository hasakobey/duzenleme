using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>Geri Dönüşüm Kutusu'ndaki öğe sayısı ve toplam boyut (bütün sürücüler).</summary>
public readonly record struct RecycleBinInfo(long Items, long Bytes)
{
    public bool IsEmpty => Items <= 0;
}

/// <summary>
/// Geri Dönüşüm Kutusu: sayı/boyut (SHQueryRecycleBin; öğe ve sürücü sayısıyla uzar, bu yüzden hep arka planda), onaylı
/// boşaltma (SHEmptyRecycleBin, Windows'un ilerleme penceresi ve sesiyle) ve değişiklik bildirimi (SHChangeNotifyRegister;
/// yoklama yok). Bildirim yalnızca abone varken dinlenir. Uygulamadaki tek geri alınamaz iş boşaltmadır: yalnızca açık
/// onaydan sonra çağrılır.
/// </summary>
public static class RecycleBin
{
    /// <summary>Kabuk adı (bölme ve kutu öğesi, simge, açma).</summary>
    public const string ShellName = WidgetSeeds.RecycleBinItem;

    // SHQUERYRBINFO: 64 bit Windows'ta doğal hizalı (24 bayt), 32 bitte 1 bayt paketli (20 bayt).
    [StructLayout(LayoutKind.Sequential)]
    private struct QueryInfo64 { public uint Size; public long Bytes; public long Items; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct QueryInfo32 { public uint Size; public long Bytes; public long Items; }

    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int Query64(string? root, ref QueryInfo64 info);

    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int Query32(string? root, ref QueryInfo32 info);

    [DllImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr owner, string? root, uint flags);

    /// <summary>Sayı ve boyut; okunamazsa null. Kabuk çağrısı (on milisaniyeler sürebilir): arayüz iş parçacığında çağırma.</summary>
    public static RecycleBinInfo? Query()
    {
        try
        {
            if (IntPtr.Size == 8)
            {
                var info = new QueryInfo64 { Size = (uint)Marshal.SizeOf<QueryInfo64>() };
                return Query64(null, ref info) == 0 ? new RecycleBinInfo(info.Items, info.Bytes) : null;
            }
            var small = new QueryInfo32 { Size = (uint)Marshal.SizeOf<QueryInfo32>() };
            return Query32(null, ref small) == 0 ? new RecycleBinInfo(small.Items, small.Bytes) : null;
        }
        catch (Exception ex) when (ex is COMException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    public static Task<RecycleBinInfo?> QueryAsync() => Task.Run(Query);

    /// <summary>
    /// Kutuyu kalıcı olarak boşaltır (onay çağıranın işidir: kendi sorusunu sorar). Windows'un ilerleme penceresi ve sesi
    /// kalır; ayrı bir STA iş parçacığında çalışır. Sahip pencere verilmez: widget masaüstü katmanında ve en alttadır, ilerleme
    /// penceresi onun arkasında kalırdı. Kutu zaten boşsa (E_UNEXPECTED) hata sayılmaz.
    /// </summary>
    public static Task EmptyAsync() => ShellFileOperations.RunSta(() =>
    {
        const uint SHERB_NOCONFIRMATION = 0x1;
        const int E_UNEXPECTED = unchecked((int)0x8000FFFF);
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION);
        if (hr != 0 && hr != E_UNEXPECTED) Marshal.ThrowExceptionForHR(hr);
    });

    // ---- Değişiklik bildirimi ----

    private static Action? _changed;
    private static HwndSource? _sink;
    private static uint _registration;
    private static DispatcherTimer? _debounce;

    /// <summary>
    /// Kutu değişti (öğe atıldı, geri yüklendi, boşaltıldı): arayüz iş parçacığında, art arda gelenler birleştirilmiş olarak.
    /// İlk abone bildirimi başlatır, son abone bırakınca durur. Yalnızca arayüz iş parçacığından abone olunur.
    /// </summary>
    public static event Action? Changed
    {
        add
        {
            _changed += value;
            if (_changed is not null) Start();
        }
        remove
        {
            _changed -= value;
            if (_changed is null) Stop();
        }
    }

    private const int WM_BIN_CHANGED = 0x0400 + 0x3B1;   // WM_USER + …

    [StructLayout(LayoutKind.Sequential)]
    private struct SHChangeNotifyEntry
    {
        public IntPtr Pidl;
        [MarshalAs(UnmanagedType.Bool)] public bool Recursive;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetSpecialFolderLocation(IntPtr owner, int folder, out IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern uint SHChangeNotifyRegister(IntPtr hwnd, int sources, int events, uint message, int count, ref SHChangeNotifyEntry entry);

    [DllImport("shell32.dll")]
    private static extern bool SHChangeNotifyDeregister(uint registration);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHChangeNotification_Lock(IntPtr memory, uint processId, out IntPtr pidls, out int eventId);

    [DllImport("shell32.dll")]
    private static extern bool SHChangeNotification_Unlock(IntPtr handle);

    private static void Start()
    {
        if (_sink is not null) return;
        const int CSIDL_BITBUCKET = 0x000A;
        const int SHCNRF_InterruptLevel = 0x0001, SHCNRF_ShellLevel = 0x0002, SHCNRF_NewDelivery = 0x8000;
        const int SHCNE_ALLEVENTS = 0x7FFFFFFF;
        try
        {
            // Yalnızca ileti alan görünmez pencere (HWND_MESSAGE).
            _sink = new HwndSource(new HwndSourceParameters($"{AppInfo.Name} RecycleBin") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
            _sink.AddHook(WndProc);
            if (SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL_BITBUCKET, out var pidl) != 0 || pidl == IntPtr.Zero) return;
            try
            {
                var entry = new SHChangeNotifyEntry { Pidl = pidl, Recursive = true };
                _registration = SHChangeNotifyRegister(_sink.Handle, SHCNRF_InterruptLevel | SHCNRF_ShellLevel | SHCNRF_NewDelivery,
                    SHCNE_ALLEVENTS, WM_BIN_CHANGED, 1, ref entry);
            }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        catch (Exception ex) when (ex is COMException or EntryPointNotFoundException or System.ComponentModel.Win32Exception)
        {
            DebugLog.Write($"Geri Dönüşüm Kutusu izlenemiyor: {ex.Message}");
        }
    }

    private static void Stop()
    {
        if (_registration != 0) SHChangeNotifyDeregister(_registration);
        _registration = 0;
        _debounce?.Stop();
        _sink?.Dispose();
        _sink = null;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_BIN_CHANGED) return IntPtr.Zero;
        handled = true;
        // Yeni teslim biçiminde bildirimin belleği kilitlenip bırakılmalı (içeriğe bakılmaz: "bir şey değişti" yeter).
        var lockHandle = SHChangeNotification_Lock(wParam, unchecked((uint)lParam.ToInt64()), out _, out _);
        if (lockHandle != IntPtr.Zero) SHChangeNotification_Unlock(lockHandle);
        if (_debounce is null)
        {
            _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(400) };
            _debounce.Tick += (_, _) =>
            {
                _debounce!.Stop();
                _changed?.Invoke();
            };
        }
        _debounce.Stop();
        _debounce.Start();
        return IntPtr.Zero;
    }
}
