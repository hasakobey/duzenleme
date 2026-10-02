using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// "Uygulama eklenince masaüstündeki kısayolu da kalksın": masaüstündeki .lnk kısayollarının ve Başlat menüsü
/// uygulamalarının kabuk bilgilerini okur (IShellLinkW + IPropertyStore; Uygulamalar klasörü öğesinin özellikleri) ve
/// <see cref="AppShortcutMatch"/> ile kesin eşleşenleri bulur. Kabuk STA ister ve yavaş olabilir: hepsi kendi STA iş
/// parçacığında (<see cref="ShellFileOperations.RunSta"/>), arayüzün dışında. Kısayol çözülmez (Resolve yok: disk ya da ağ
/// araması yapılmaz). Adlar, yollar ve kimlikler hiçbir günlüğe yazılmaz.
/// </summary>
public static class ShortcutFacts
{
    /// <summary>
    /// Uygulamaların (kutu yolları, <see cref="StartApps"/>) masaüstündeki kısayollarını bulur: (uygulama, kısayol) çiftleri;
    /// bir kısayol en çok bir uygulamaya. Okunamayan kısayol atlanır; hata fırlatmaz.
    /// </summary>
    public static async Task<List<(string App, string Shortcut)>> MatchAsync(IReadOnlyList<string> apps, IReadOnlyList<string> shortcuts)
    {
        var result = new List<(string App, string Shortcut)>();
        if (apps.Count == 0 || shortcuts.Count == 0) return result;
        try
        {
            await ShellFileOperations.RunSta(() =>
            {
                var links = new List<(string Path, ShortcutInfo Info)>();
                foreach (var path in shortcuts.Take(MaxShortcuts))
                    if (ReadLink(path) is { } info) links.Add((path, info));
                if (links.Count == 0) return;
                foreach (var app in apps)
                {
                    if (StartApps.Id(app) is not { } aumid) continue;
                    var facts = ReadApp(aumid);
                    foreach (var (path, info) in links)
                        if (!result.Any(r => string.Equals(r.Shortcut, path, StringComparison.OrdinalIgnoreCase)) && AppShortcutMatch.Matches(facts, info))
                            result.Add((app, path));
                }
            });
        }
        catch (Exception ex)
        {
            // Kabuk her türlü hata verebilir: eşleşme bulunmamış sayılır (kısayol masaüstünde kalır).
            AppLog.Warn("box", "shortcut-match-failed", ex);
            result.Clear();
        }
        return result;
    }

    /// <summary>Bakılacak en çok kısayol (masaüstü çok kalabalıksa iş uzamasın).</summary>
    private const int MaxShortcuts = 400;

    // ---------------------------------------------------------------- kabuk (yalnızca STA iş parçacığında)

    private const int MaxText = 2048;
    private const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    private const int GPS_DEFAULT = 0;
    private const ushort VT_LPWSTR = 31;

    private static PropertyKey AppUserModelId = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
    private static PropertyKey LinkTargetParsingPath = new(new Guid("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25"), 2);
    private static PropertyKey LinkArguments = new(new Guid("436F2667-14E2-4FEB-B30A-146C53B5B674"), 100);

    /// <summary>Kısayolun bilgileri; okunamazsa null.</summary>
    private static ShortcutInfo? ReadLink(string path)
    {
        object? created = null;
        try
        {
            created = new ShellLinkObject();
            if (created is not IShellLinkW link || created is not IPersistFile file) return null;
            file.Load(path, 0); // STGM_READ
            var target = new StringBuilder(MaxText);
            var gotTarget = link.GetPath(target, target.Capacity, IntPtr.Zero, 0) == 0 && target.Length > 0;
            var arguments = new StringBuilder(MaxText);
            var gotArguments = link.GetArguments(arguments, arguments.Capacity) == 0;
            string? parsing = null;
            if (link.GetIDList(out var pidl) == 0 && pidl != IntPtr.Zero)
            {
                try
                {
                    if (SHGetNameFromIDList(pidl, SIGDN_DESKTOPABSOLUTEPARSING, out var name) == 0 && name != IntPtr.Zero)
                    {
                        try { parsing = Marshal.PtrToStringUni(name); }
                        finally { Marshal.FreeCoTaskMem(name); }
                    }
                }
                finally { Marshal.FreeCoTaskMem(pidl); }
            }
            var aumid = created is IPropertyStore store ? ReadString(store, AppUserModelId) : null;
            return new ShortcutInfo(gotTarget ? target.ToString() : null, gotArguments ? arguments.ToString() : null, aumid, parsing);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or UnauthorizedAccessException or System.IO.IOException)
        {
            return null;
        }
        finally
        {
            if (created is not null && Marshal.IsComObject(created)) Marshal.ReleaseComObject(created);
        }
    }

    /// <summary>Başlat menüsü uygulamasının bilgileri (okunamayan alan null kalır).</summary>
    private static AppInfoFacts ReadApp(string aumid)
    {
        string? target = null, arguments = null;
        object? created = null;
        try
        {
            if (Widgets.NativeMethods.SHCreateItemFromParsingName(StartApps.ShellPath(StartApps.Prefix + aumid), IntPtr.Zero,
                    typeof(IShellItem2).GUID, out created) == 0 && created is IShellItem2 item)
            {
                var riid = typeof(IPropertyStore).GUID;
                if (item.GetPropertyStore(GPS_DEFAULT, ref riid, out var pointer) == 0 && pointer != IntPtr.Zero)
                {
                    IPropertyStore? store = null;
                    try
                    {
                        store = (IPropertyStore)Marshal.GetObjectForIUnknown(pointer);
                        target = ReadString(store, LinkTargetParsingPath);
                        arguments = ReadString(store, LinkArguments);
                    }
                    finally
                    {
                        Marshal.Release(pointer);
                        if (store is not null) Marshal.ReleaseComObject(store);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            // Bilgisi okunamayan uygulamada yalnızca kimlik ve bilinen klasör kuralı kalır.
        }
        finally
        {
            if (created is not null && Marshal.IsComObject(created)) Marshal.ReleaseComObject(created);
        }
        return new AppInfoFacts(aumid, target, arguments, AppShortcutMatch.ExpandKnownFolder(aumid, KnownFolders.PathOf));
    }

    private static string? ReadString(IPropertyStore store, PropertyKey key)
    {
        var value = new PropVariant();
        try
        {
            if (store.GetValue(ref key, out value) != 0 || value.Type != VT_LPWSTR || value.Pointer == IntPtr.Zero) return null;
            return Marshal.PtrToStringUni(value.Pointer);
        }
        finally { PropVariantClear(ref value); }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdn, out IntPtr name);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        private readonly Guid _format = format;
        private readonly uint _id = id;
    }

    /// <summary>PROPVARIANT (x64/ARM64'te 24, x86'da 16 bayt); yalnızca tür ve ilk işaretçi okunur.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        private readonly ushort _reserved1, _reserved2, _reserved3;
        public IntPtr Pointer;
        private readonly IntPtr _rest;
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, IntPtr findData, uint flags);
        [PreserveSig] int GetIDList(out IntPtr pidl);
        [PreserveSig] int SetIDList(IntPtr pidl);
        [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        [PreserveSig] int GetHotkey(out short hotkey);
        [PreserveSig] int SetHotkey(short hotkey);
        [PreserveSig] int GetShowCmd(out int showCmd);
        [PreserveSig] int SetShowCmd(int showCmd);
        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int size, out int index);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        [PreserveSig] int Resolve(IntPtr hwnd, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem2
    {
        // IShellItem
        [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
        [PreserveSig] int GetParent(out IntPtr parent);
        [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IntPtr other, uint hint, out int order);
        // IShellItem2
        [PreserveSig] int GetPropertyStore(int flags, ref Guid riid, out IntPtr store);
    }
}
