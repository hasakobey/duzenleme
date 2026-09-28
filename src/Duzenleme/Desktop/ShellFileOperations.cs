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

    // --- IFileOperation: Gezgin'in kendi kullandığı dosya işlemi (yönetici izni isteyebilir, Windows'un onay ayarına uyar) ---

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem;

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr sink);
        void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? newName, IntPtr sink);
        void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
        void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? copyName, IntPtr sink);
        void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
        void DeleteItem(IShellItem item, IntPtr sink);
        void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);
        void NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.LPWStr)] string? template, IntPtr sink);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)] bool GetAnyOperationsAborted();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [DllImport("shell32.dll")]
    private static extern void SHGetSettings(out uint state, uint mask);

    private const uint FOFX_SHOWELEVATIONPROMPT = 0x00040000;
    private const uint FOFX_RECYCLEONDELETE = 0x00080000;

    /// <summary>
    /// Öğeyi aynı klasörde yeniden adlandırır (Gezgin'in yolu): Ortak Masaüstü gibi izin isteyen yerde Windows'un kendi
    /// "Klasör erişimi reddedildi → Devam" (yönetici onayı) penceresi çıkar. Kullanıcı vazgeçerse
    /// <see cref="OperationCanceledException"/>. Arka plandaki STA iş parçacığından (<see cref="RunSta"/>) çağrılır.
    /// </summary>
    public static void RenameWithShell(string path, string newName, IntPtr owner) =>
        // FOF_ALLOWUNDO yok: Gezgin'in Ctrl+Z'si bu adlandırmayı taşıma geçmişinden habersiz geri almasın.
        Perform(path, owner, (uint)FOF_NOCONFIRMMKDIR | FOFX_SHOWELEVATIONPROMPT, (op, item) => op.RenameItem(item, newName, IntPtr.Zero));

    /// <summary>
    /// Geri Dönüşüm Kutusu'na gönderir; Gezgin gibi Windows'un "Silme onayı iletişim kutusunu göster" ayarına uyar ve
    /// gerekirse yönetici onayı ister (Ortak Masaüstü). Vazgeçilirse <see cref="OperationCanceledException"/>.
    /// Arka plandaki STA iş parçacığından çağrılır.
    /// </summary>
    public static void RecycleWithShell(string path, IntPtr owner)
    {
        var flags = (uint)FOF_ALLOWUNDO | FOF_WANTNUKEWARNING | FOFX_RECYCLEONDELETE | FOFX_SHOWELEVATIONPROMPT;
        if (!AsksBeforeRecycling()) flags |= FOF_NOCONFIRMATION;
        Perform(path, owner, flags, (op, item) => op.DeleteItem(item, IntPtr.Zero));
    }

    /// <summary>Windows Geri Dönüşüm Kutusu'na göndermeden önce soruyor mu? (Geri Dönüşüm Kutusu özellikleri; varsayılan hayır.)</summary>
    public static bool AsksBeforeRecycling()
    {
        const uint SSF_NOCONFIRMRECYCLE = 0x00008000;
        const uint fNoConfirmRecycle = 1u << 2; // SHELLFLAGSTATE'in üçüncü biti
        try
        {
            SHGetSettings(out var state, SSF_NOCONFIRMRECYCLE);
            return (state & fNoConfirmRecycle) == 0;
        }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static void Perform(string path, IntPtr owner, uint flags, Action<IFileOperation, IShellItem> queue)
    {
        var type = Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"), throwOnError: true)!;
        var op = (IFileOperation)Activator.CreateInstance(type)!;
        IShellItem? item = null;
        try
        {
            SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem).GUID, out item);
            op.SetOperationFlags(flags);
            if (owner != IntPtr.Zero) op.SetOwnerWindow(owner);
            queue(op, item);
            try { op.PerformOperations(); }
            catch (COMException ex) when (ex.HResult is unchecked((int)0x800704C7) or unchecked((int)0x80270000))
            {
                throw new OperationCanceledException();
            }
            if (op.GetAnyOperationsAborted()) throw new OperationCanceledException();
        }
        catch (COMException ex)
        {
            throw new IOException(ex.Message, ex);
        }
        finally
        {
            if (item is not null) Marshal.ReleaseComObject(item);
            Marshal.ReleaseComObject(op);
        }
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
