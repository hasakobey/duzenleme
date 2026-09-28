using System.IO;
using System.Runtime.InteropServices;

namespace Duzenleme.Desktop;

/// <summary>
/// Uzun sürebilecek dosya işlemleri (başka sürücüye taşıma, Geri Dönüşüm Kutusu'na gönderme) Windows'un kendi kabuk
/// işlemiyle ve arayüzden ayrı bir STA iş parçacığında yapılır: büyük dosyada Windows kendi ilerleme penceresini gösterir,
/// widget'lar o sırada donmaz.
/// </summary>
public static class ShellFileOperations
{
    private const uint FO_MOVE = 0x0001;
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_MULTIDESTFILES = 0x0001;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMMKDIR = 0x0200;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    /// <summary>
    /// Dosyayı tam hedef yoluna taşır (hedef adı çağıran seçer, çakışma olmamalı). Uzun sürerse Windows ilerleme gösterir.
    /// Kullanıcı iptal ederse <see cref="OperationCanceledException"/>, başarısızsa <see cref="IOException"/>.
    /// Arka plandaki (tercihen STA) iş parçacığından çağrılır.
    /// </summary>
    public static void Move(string source, string destination)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_MOVE,
            pFrom = source + "\0\0",
            pTo = destination + "\0\0",
            // Hedef bir dosya adıdır (klasör değil); hatayı biz gösteririz.
            fFlags = FOF_MULTIDESTFILES | FOF_NOCONFIRMMKDIR | FOF_NOCONFIRMATION | FOF_NOERRORUI,
        };
        Check(SHFileOperation(ref op), op.fAnyOperationsAborted, source);
    }

    /// <summary>
    /// Dosya ya da klasörü Geri Dönüşüm Kutusu'na gönderir (geri alınabilir). Geri dönüşüme sığmıyorsa Windows kalıcı
    /// silme için ayrıca sorar. Arka plandaki (tercihen STA) iş parçacığından çağrılır.
    /// </summary>
    public static void Recycle(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING,
        };
        Check(SHFileOperation(ref op), op.fAnyOperationsAborted, path);
    }

    private static void Check(int result, bool aborted, string path)
    {
        if (aborted) throw new OperationCanceledException();
        // 0x4C7 (ERROR_CANCELLED) ve 0x75 (DE_OPCANCELLED): kullanıcı vazgeçti.
        if (result is 0x4C7 or 0x75) throw new OperationCanceledException();
        if (result != 0) throw new IOException($"\"{Path.GetFileName(path)}\" için dosya işlemi başarısız oldu (kod 0x{result:X}).");
    }

    /// <summary>İşi kendi STA iş parçacığında çalıştırır (kabuk işlemleri ve eklentileri STA ister).</summary>
    public static Task RunSta(Action work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                work();
                done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        })
        { IsBackground = true, Name = $"{Core.AppInfo.Name} dosya işlemi" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }
}
