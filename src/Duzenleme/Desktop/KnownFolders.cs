using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// Windows'un bilinen klasörleri (İndirilenler, Belgeler…): kullanıcı onları taşıdıysa ya da OneDrive'a yönlendirdiyse de
/// güncel yol. Klasörün var olup olmadığına bakılmaz (KF_FLAG_DONT_VERIFY: diske ya da ağa gidilmez, kayıt defterinden okunur).
/// </summary>
public static class KnownFolders
{
    private static readonly Dictionary<string, Guid> Ids = new(StringComparer.OrdinalIgnoreCase)
    {
        [FolderPortal.Downloads] = new("374DE290-123F-4565-9164-39C4925E467B"),
        [FolderPortal.Documents] = new("FDD39AD0-238F-46AF-ADB4-6C85480369C7"),
        [FolderPortal.Pictures] = new("33E28130-4E1E-4676-835A-98395C3BC3BB"),
        [FolderPortal.Music] = new("4BD8D571-6D19-48D3-BE97-422220080E43"),
        [FolderPortal.Videos] = new("18989B1D-99B5-455B-841C-AB7C74E4DDFC"),
        [FolderPortal.Screenshots] = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F"),
    };

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

    /// <summary>Bilinen klasörün yolu (<see cref="FolderPortal.KnownIds"/>); bu Windows'ta tanımlı değilse null.</summary>
    public static string? PathOf(string id)
    {
        const uint KF_FLAG_DONT_VERIFY = 0x00004000;
        if (!Ids.TryGetValue(id, out var guid)) return null;
        var path = IntPtr.Zero;
        try
        {
            return SHGetKnownFolderPath(guid, KF_FLAG_DONT_VERIFY, IntPtr.Zero, out path) == 0 ? Marshal.PtrToStringUni(path) : null;
        }
        catch (Exception ex) when (ex is COMException or EntryPointNotFoundException) { return null; }
        finally
        {
            if (path != IntPtr.Zero) Marshal.FreeCoTaskMem(path);
        }
    }
}
