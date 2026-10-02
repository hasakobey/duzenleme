using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// Başlat menüsündeki uygulamalar (kabuğun Uygulamalar klasörü, "shell:AppsFolder"): "Uygulama seç" penceresinin listesi.
/// Kabuk STA ister ve yavaş olabilir: liste <see cref="ShellFileOperations.RunSta"/> içinde, arayüz iş parçacığının dışında
/// okunur. Oturumda önbelleğe alınır (<see cref="Cached"/>): pencere önce onu gösterir, arka planda yenilenen liste gelince
/// günceller. Yalnızca yereldir (ağ yok); adlar ve kimlikler hiçbir günlüğe yazılmaz.
/// </summary>
public static class StartMenuApps
{
    private static IReadOnlyList<StartApp>? _cache;
    private static Task<IReadOnlyList<StartApp>>? _refreshing;
    private static readonly object Gate = new();

    /// <summary>Bu oturumda son okunan liste (sıralı, yinelenmeyen); henüz okunmadıysa null.</summary>
    public static IReadOnlyList<StartApp>? Cached => Volatile.Read(ref _cache);

    /// <summary>
    /// Listeyi arka planda yeniden okur (sürmekte olan okuma varsa ona katılır). Okunamazsa önbellekteki liste (yoksa boş)
    /// döner; hata fırlatmaz.
    /// </summary>
    public static Task<IReadOnlyList<StartApp>> RefreshAsync()
    {
        lock (Gate)
        {
            if (_refreshing is { IsCompleted: false } running) return running;
            return _refreshing = ReadAsync();
        }
    }

    private static async Task<IReadOnlyList<StartApp>> ReadAsync()
    {
        IReadOnlyList<StartApp> found = [];
        try
        {
            await ShellFileOperations.RunSta(() => found = Enumerate());
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException or ArgumentException or OutOfMemoryException)
        {
            AppLog.Warn("start-apps", "enumerate-failed", ex);
            return Cached ?? [];
        }
        var list = AppList.Prepare(found);
        // Boş sonuç (kabuk o an hazır değil) önceki listeyi silmesin.
        if (list.Count > 0 || Cached is null) Volatile.Write(ref _cache, list);
        AppLog.Info("start-apps", "enumerated", list.Count);
        return Cached ?? list;
    }

    // ---------------------------------------------------------------- kabuk (yalnızca STA iş parçacığında)

    private const uint SIGDN_NORMALDISPLAY = 0;
    private const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;
    private static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

    /// <summary>Uygulamalar klasöründeki bütün öğeler (kimlik + görünen ad); en çok <see cref="MaxApps"/>.</summary>
    private static List<StartApp> Enumerate()
    {
        var result = new List<StartApp>();
        if (Widgets.NativeMethods.SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, typeof(IShellItem).GUID, out var created) != 0 ||
            created is not IShellItem folder)
            return result;
        IEnumShellItems? items = null;
        try
        {
            var handler = BHID_EnumItems;
            var riid = typeof(IEnumShellItems).GUID;
            folder.BindToHandler(IntPtr.Zero, ref handler, ref riid, out var pointer);
            if (pointer == IntPtr.Zero) return result;
            try { items = (IEnumShellItems)Marshal.GetObjectForIUnknown(pointer); }
            finally { Marshal.Release(pointer); }
            while (result.Count < MaxApps && items.Next(1, out var item, out var fetched) == 0 && fetched == 1 && item is not null)
            {
                try
                {
                    if (Name(item, SIGDN_PARENTRELATIVEPARSING) is { Length: > 0 } id && Name(item, SIGDN_NORMALDISPLAY) is { Length: > 0 } name)
                        result.Add(new StartApp(id, ItemLooks.CleanName(name) ?? name));
                }
                finally { Marshal.ReleaseComObject(item); }
            }
        }
        finally
        {
            if (items is not null) Marshal.ReleaseComObject(items);
            Marshal.ReleaseComObject(folder);
        }
        return result;
    }

    /// <summary>Okunacak en çok öğe (kabuk bozuksa sonsuz döngü olmasın; tipik bilgisayarda birkaç yüz).</summary>
    private const int MaxApps = 5000;

    private static string? Name(IShellItem item, uint sigdn)
    {
        try
        {
            item.GetDisplayName(sigdn, out var text);
            if (text == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(text); }
            finally { Marshal.FreeCoTaskMem(text); }
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return null;
        }
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IShellItem? item, out uint fetched);
        void Skip(uint count);
        void Reset();
        void Clone(out IEnumShellItems copy);
    }
}
