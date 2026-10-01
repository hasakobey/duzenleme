using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Duzenleme.Widgets;

namespace Duzenleme.Desktop;

/// <summary>Tuş ya da metin gönderilecek hedefin durumu (kalıcı değil).</summary>
public enum InputTarget
{
    /// <summary>Hedef önde ve girdiyi alabilir.</summary>
    Ok,

    /// <summary>Hedef yok, masaüstünün kendisi ya da bu uygulamanın penceresi: gönderilmez.</summary>
    NoTarget,

    /// <summary>Öndeki pencere hedef değil (öne getirilemedi ya da kullanıcı başka pencereye geçti): gönderilmez.</summary>
    NotInFront,

    /// <summary>
    /// Hedef daha yüksek bütünlük düzeyinde (yönetici olarak ya da uiAccess ile) çalışıyor ya da bu bilinemiyor: Windows
    /// (UIPI) girdiyi hata vermeden yutar, gönderilmez.
    /// </summary>
    Elevated,
}

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
    private const int TokenIntegrityLevel = 25;

    // Bu sürecin bütünlük düzeyi (değişmez): bir kez okunur.
    private static readonly Lazy<int?> OwnIntegrity = new(() => IntegrityRid(GetCurrentProcess()));

    /// <summary>Bu sürecin bütünlük düzeyi (RID: 0x2000 orta, 0x3000 yüksek); okunamazsa null.</summary>
    public static int? OwnIntegrityLevel => OwnIntegrity.Value;

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
    /// Pencerenin süreci yönetici olarak (yükseltilmiş) mi çalışıyor? Bilinemiyorsa null. Bildirimin metni için; göndermeden
    /// önceki karar <see cref="CanReceiveInput"/> ile (UIPI yükseltmeye değil bütünlük düzeyine bakar).
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

    /// <summary>
    /// Pencere bu süreçten girdi (SendInput) alabilir mi? UIPI daha yüksek bütünlük düzeyindeki pencereye giden girdiyi hata
    /// vermeden yutar: yönetici olarak çalışan ve yükseltilmemiş ama orta düzeyin üstündeki (uiAccess, ör. ekran klavyesi)
    /// süreçler. Hedefin düzeyi bizimkinden yüksekse false, ikisinden biri okunamazsa null (çağıran göndermez).
    /// </summary>
    public static bool? CanReceiveInput(IntPtr hwnd)
    {
        var pid = ProcessId(hwnd);
        if (pid == 0) return null;
        if (pid == (uint)Environment.ProcessId) return true;
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try { return Receives(IntegrityRid(process), OwnIntegrity.Value); }
        finally { CloseHandle(process); }
    }

    /// <summary>Hedefin bütünlük düzeyi (RID) bizimkinden yüksek değilse true; biri bilinmiyorsa null (saf).</summary>
    public static bool? Receives(int? targetRid, int? ownRid) =>
        targetRid is { } target && ownRid is { } own ? target <= own : null;

    /// <summary>Hedef şu an tuş/metin alabilir mi (gönderimden hemen önce ve her birleşimden önce bakılır).</summary>
    public static InputTarget Check(IntPtr target) =>
        target == IntPtr.Zero ? InputTarget.NoTarget
            : Decide(target, Window(), IsOwnProcess(target), IsDesktopSurface(target), CanReceiveInput(target));

    /// <summary>
    /// Karar (saf; testlenir): hedef yoksa, masaüstüyse ya da bu uygulamanınsa NoTarget; öndeki pencere hedef değilse
    /// NotInFront; hedef girdiyi alamıyorsa ya da bu bilinmiyorsa Elevated.
    /// </summary>
    public static InputTarget Decide(IntPtr target, IntPtr foreground, bool own, bool desktop, bool? canReceive)
    {
        if (target == IntPtr.Zero || own || desktop) return InputTarget.NoTarget;
        if (foreground != target) return InputTarget.NotInFront;
        return canReceive == true ? InputTarget.Ok : InputTarget.Elevated;
    }

    private static bool? TokenElevated(IntPtr process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return null;
        try
        {
            return GetTokenInformation(token, TokenElevation, out var elevated, sizeof(uint), out _) ? elevated != 0 : null;
        }
        finally { CloseHandle(token); }
    }

    /// <summary>Sürecin zorunlu bütünlük etiketinin RID'i (0x2000 orta, 0x3000 yüksek…); okunamazsa null.</summary>
    private static int? IntegrityRid(IntPtr process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return null;
        try
        {
            GetTokenInformationBuffer(token, TokenIntegrityLevel, IntPtr.Zero, 0, out var needed);
            if (needed <= 0) return null;
            var buffer = Marshal.AllocHGlobal(needed);
            try
            {
                if (!GetTokenInformationBuffer(token, TokenIntegrityLevel, buffer, needed, out _)) return null;
                var sid = Marshal.ReadIntPtr(buffer); // TOKEN_MANDATORY_LABEL.Label.Sid
                if (sid == IntPtr.Zero || !IsValidSid(sid)) return null;
                var count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return count == 0 ? null : Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            }
            finally { Marshal.FreeHGlobal(buffer); }
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

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformationBuffer(IntPtr token, int infoClass, IntPtr info, int length, out int returned);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSid(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
