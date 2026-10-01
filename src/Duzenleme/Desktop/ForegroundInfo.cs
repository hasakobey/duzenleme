using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Duzenleme.Widgets;

namespace Duzenleme.Desktop;

/// <summary>
/// Ön plandaki pencere ve süreci (halka açılırken bir kez okunur). Gizlilik: süreç adı ve pencere yalnızca bellekte, halka
/// kapanana dek tutulur; hiçbir yere yazılmaz ve günlüğe girmez (RingPrivacyTests). Yalnızca sınırlı sorgu hakkıyla
/// (PROCESS_QUERY_LIMITED_INFORMATION) açılır; pencereye ileti göndermez (askıdaki uygulamada da beklemez).
/// </summary>
internal static class ForegroundInfo
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;

    /// <summary>Şu anki ön plan penceresi (yoksa IntPtr.Zero).</summary>
    public static IntPtr Window() => NativeMethods.GetForegroundWindow();

    /// <summary>Pencerenin süreç kimliği; pencere yoksa 0.</summary>
    public static uint ProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    /// <summary>Pencere bu uygulamanın mı (halka, ana pencere, widget)?</summary>
    public static bool IsOwnProcess(IntPtr hwnd) => hwnd != IntPtr.Zero && ProcessId(hwnd) == (uint)Environment.ProcessId;

    /// <summary>Masaüstünün kendisi mi (Progman / simgeli WorkerW): oraya tuş ya da metin gönderilmez.</summary>
    public static bool IsDesktopSurface(IntPtr hwnd) => DesktopIcons.IsDesktopSurface(hwnd);

    /// <summary>
    /// Pencerenin sürecinin dosya adı, uzantısız ("notepad"); okunamazsa null. Yalnızca bellekte kullanılır.
    /// </summary>
    public static string? ProcessName(IntPtr hwnd)
    {
        var pid = ProcessId(hwnd);
        if (pid == 0) return null;
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (!QueryFullProcessImageNameW(process, 0, buffer, ref size) || size <= 0) return null;
            return Path.GetFileNameWithoutExtension(buffer.ToString(0, size));
        }
        finally { CloseHandle(process); }
    }

    /// <summary>
    /// Pencerenin süreci yönetici olarak (yükseltilmiş) mi çalışıyor? Bilinemiyorsa null (ör. erişim reddedildi): çağıran
    /// bunu da "olabilir" sayar ve uyarır, çünkü UIPI engellenen girdiyi hata vermeden yutar.
    /// </summary>
    public static bool? IsElevated(IntPtr hwnd)
    {
        var pid = ProcessId(hwnd);
        if (pid == 0) return null;
        if (pid == (uint)Environment.ProcessId) return IsCurrentProcessElevated();
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try { return TokenElevated(process); }
        finally { CloseHandle(process); }
    }

    /// <summary>Bu süreç yönetici olarak mı çalışıyor (normalde hayır; bilinemiyorsa null).</summary>
    public static bool? IsCurrentProcessElevated() => TokenElevated(GetCurrentProcess());

    private static bool? TokenElevated(IntPtr process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return null;
        try
        {
            return GetTokenInformation(token, TokenElevation, out var elevated, sizeof(uint), out _) ? elevated != 0 : null;
        }
        finally { CloseHandle(token); }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref int size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, out uint info, int length, out int returned);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
