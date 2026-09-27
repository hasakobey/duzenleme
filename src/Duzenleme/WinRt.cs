using System.Runtime.InteropServices;

namespace Duzenleme;

/// <summary>
/// WinRT'ye projeksiyonsuz erişim (WinRT projeksiyonu ~25 MB eklemesin diye; bkz. <see cref="PackageInfo"/>): etkinleştirme
/// fabrikası, HSTRING ve IAsyncOperation&lt;T&gt; bekleme. IAsyncOperation&lt;T&gt;'nin IID'si T'ye göre hesaplanır; sonuç
/// onun yerine arayüzün her T için aynı olan sanal tablo yuvasından alınır.
/// </summary>
internal static class WinRt
{
    // IAsyncOperation<T>: IUnknown (3) + IInspectable (3) + put_Completed, get_Completed, GetResults.
    private const int GetResultsSlot = 8;
    private const int AsyncCompleted = 1, AsyncCanceled = 2, AsyncError = 3;
    private const int E_FAIL = unchecked((int)0x80004005);

    /// <summary>Sonucu IAsyncOperation&lt;T&gt; olan bir WinRT yöntemi (HRESULT döndürür).</summary>
    public delegate int AsyncCall(out IntPtr operation);

    /// <summary>Sınıfın etkinleştirme fabrikası (statik yöntemlerinin arayüzü); alınamazsa null.</summary>
    public static T? Factory<T>(string classId) where T : class
    {
        using var name = new HString(classId);
        if (RoGetActivationFactory(name.Handle, typeof(T).GUID, out var factory) < 0 || factory == IntPtr.Zero) return null;
        try { return Marshal.GetObjectForIUnknown(factory) as T; }
        finally { Marshal.Release(factory); }
    }

    /// <summary>Çağrıyı yapar, bitmesini bekler ve nesne sonucunu verir (Marshal.Release çağırana düşer).</summary>
    public static IntPtr AwaitObject(AsyncCall call, TimeSpan timeout)
    {
        var operation = Start(call, timeout);
        try
        {
            Marshal.ThrowExceptionForHR(Slot<GetResultsObject>(operation)(operation, out var result));
            return result;
        }
        finally { Marshal.Release(operation); }
    }

    /// <summary>Çağrıyı yapar, bitmesini bekler ve 32 bitlik sonucunu (WinRT enum'ları) verir.</summary>
    public static int AwaitInt32(AsyncCall call, TimeSpan timeout)
    {
        var operation = Start(call, timeout);
        try
        {
            Marshal.ThrowExceptionForHR(Slot<GetResultsInt32>(operation)(operation, out var result));
            return result;
        }
        finally { Marshal.Release(operation); }
    }

    /// <summary>
    /// İşlemi başlatıp bitene dek yoklar (tamamlanma işleyicisi de IID'si T'ye göre hesaplanan bir temsilci arayüzü ister).
    /// Yoklama iş parçacığını bekletir: arka planda çağır. Hata, iptal ya da zaman aşımında istisna fırlatır.
    /// </summary>
    private static IntPtr Start(AsyncCall call, TimeSpan timeout)
    {
        Marshal.ThrowExceptionForHR(call(out var operation));
        try
        {
            var info = (IAsyncInfo)Marshal.GetObjectForIUnknown(operation);
            var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
            while (true)
            {
                Marshal.ThrowExceptionForHR(info.GetStatus(out var status));
                if (status == AsyncCompleted) return operation;
                if (status == AsyncError)
                {
                    info.GetErrorCode(out var error);
                    throw Marshal.GetExceptionForHR(error < 0 ? error : E_FAIL)!;
                }
                if (status == AsyncCanceled) throw new OperationCanceledException();
                if (Environment.TickCount64 > deadline)
                {
                    info.Cancel();
                    throw new TimeoutException();
                }
                Thread.Sleep(5);
            }
        }
        catch
        {
            Marshal.Release(operation);
            throw;
        }
    }

    private static T Slot<T>(IntPtr operation) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(operation), GetResultsSlot * IntPtr.Size));

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetResultsObject(IntPtr self, out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetResultsInt32(IntPtr self, out int result);

    /// <summary>Kullanım süresince yaşayan HSTRING.</summary>
    public sealed class HString : IDisposable
    {
        public IntPtr Handle { get; private set; }

        public HString(string value)
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(value, value.Length, out var handle));
            Handle = handle;
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) WindowsDeleteString(Handle);
            Handle = IntPtr.Zero;
        }
    }

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr classId, [MarshalAs(UnmanagedType.LPStruct)] Guid iid, out IntPtr factory);

    // WinRT arayüzleri IInspectable'dan türer: ilk üç yuva (GetIids, GetRuntimeClassName, GetTrustLevel) hiç çağrılmaz,
    // yalnızca sanal tablo sırası tutsun diye yer tutar.
    [ComImport, Guid("00000036-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAsyncInfo
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int GetId(out uint id);
        [PreserveSig] int GetStatus(out int status);
        [PreserveSig] int GetErrorCode(out int error);
        [PreserveSig] int Cancel();
        [PreserveSig] int Close();
    }
}
