using System.IO;
using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>Kabuktan bırakılan öğe: kutuya eklenecek yol ve (uygulamaysa) görünen adı.</summary>
public sealed record ShellDropItem(string Path, string? Name, bool IsApp);

/// <summary>
/// "Shell IDList Array" ile bırakılan öğeleri (Başlat menüsü uygulamaları; dosya sistemi öğeleri) kabuk öğesine çevirir.
/// Kimlik listeleri <see cref="ShellIdList.Parse"/>'tan gelir. Uygulamalar klasöründeki öğe uygulamadır: yolu
/// <see cref="StartApps.PathFor"/>, adı kabuğun gösterdiği ad. Dosya sistemi öğesi tam yoluyla gelir (adı dosyanınki). Başka
/// sanal öğeler (Denetim Masası öğesi, telefon…) atlanır. Kabuk STA ister: <see cref="ShellFileOperations.RunSta"/> içinden çağrılır.
/// </summary>
public static class ShellDropItems
{
    private const uint SIGDN_NORMALDISPLAY = 0;
    private const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;
    private const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHCreateItemFromIDList(IntPtr pidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem? item);

    /// <summary>Tam kimlik listelerinden eklenebilir öğeler (sırası korunur, yinelenenler bir kez).</summary>
    public static IReadOnlyList<ShellDropItem> Resolve(IReadOnlyList<byte[]> pidls)
    {
        var result = new List<ShellDropItem>();
        foreach (var bytes in pidls)
        {
            var pidl = Marshal.AllocCoTaskMem(bytes.Length);
            IShellItem? item = null;
            try
            {
                Marshal.Copy(bytes, 0, pidl, bytes.Length);
                if (SHCreateItemFromIDList(pidl, typeof(IShellItem).GUID, out item) != 0 || item is null) continue;
                if (Describe(item) is { } found && !result.Any(r => string.Equals(r.Path, found.Path, StringComparison.OrdinalIgnoreCase)))
                    result.Add(found);
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
            {
                DebugLog.Write($"kabuk öğesi okunamadı: {ex.Message}");
            }
            finally
            {
                if (item is not null) Marshal.ReleaseComObject(item);
                Marshal.FreeCoTaskMem(pidl);
            }
        }
        return result;
    }

    private static ShellDropItem? Describe(IShellItem item)
    {
        if (InAppsFolder(item) && StartApps.PathFor(Name(item, SIGDN_PARENTRELATIVEPARSING)) is { } app)
            return new ShellDropItem(app, ItemLooks.CleanName(Name(item, SIGDN_NORMALDISPLAY)), IsApp: true);
        return Name(item, SIGDN_FILESYSPATH) is { } file && Path.IsPathFullyQualified(file) ? new ShellDropItem(file, null, IsApp: false) : null;
    }

    /// <summary>Üst öğesi Uygulamalar klasörü (shell:AppsFolder) mü?</summary>
    private static bool InAppsFolder(IShellItem item)
    {
        IShellItem? parent = null;
        try
        {
            item.GetParent(out parent);
            return string.Equals(Name(parent, SIGDN_DESKTOPABSOLUTEPARSING), StartApps.FolderParsingName, StringComparison.OrdinalIgnoreCase);
        }
        catch (COMException) { return false; }
        finally
        {
            if (parent is not null) Marshal.ReleaseComObject(parent);
        }
    }

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
}
