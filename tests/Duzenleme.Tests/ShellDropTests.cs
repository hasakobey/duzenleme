using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Kısayol kutusuna Başlat menüsünden bırakılan uygulamalar (2.2): "Shell IDList Array" (CIDA) ayrıştırması sınırlı ve
/// bozuk veriye dayanıklı; uygulama yolu "::{4234D49B-…}\&lt;AUMID&gt;" (2.1 de kabuk kutucuğu sayar), simge ve açma
/// "shell:AppsFolder\&lt;AUMID&gt;" ile. Gerçek kabukla uçtan uca çözümleme de denenir (hiçbir şey açılmaz).
/// </summary>
public sealed class ShellDropTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-shelldrop-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>Masaüstünün altındaki kök öğe (cb 0x14, tür 0x1F, sıra 0x80, sınıf kimliği) — kabuğun Uygulamalar klasörü için verdiği.</summary>
    private static byte[] RootItem(Guid clsid)
    {
        var item = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(item, 20);
        item[2] = 0x1F;
        item[3] = 0x80;
        clsid.TryWriteBytes(item.AsSpan(4));
        return item;
    }

    /// <summary>Tek öğeli kimlik listesi gövdesi (cb + veri), sonlandırıcısız.</summary>
    private static byte[] Item(params byte[] data)
    {
        var item = new byte[2 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(item, (ushort)item.Length);
        data.CopyTo(item, 2);
        return item;
    }

    /// <summary>CIDA: sayı, (sayı + 1) uzaklık, ardından sonlandırıcılı listeler (önce üst klasör).</summary>
    private static byte[] Cida(byte[] parent, params byte[][] children)
    {
        var lists = new[] { parent }.Concat(children).Select(l => l.Concat(new byte[2]).ToArray()).ToList();
        var header = 4 + 4 * lists.Count;
        var data = new byte[header + lists.Sum(l => l.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)children.Length);
        var at = header;
        for (var i = 0; i < lists.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4 + 4 * i), (uint)at);
            lists[i].CopyTo(data, at);
            at += lists[i].Length;
        }
        return data;
    }

    [Fact]
    public void Cida_gives_full_id_lists_of_the_items()
    {
        var parent = RootItem(StartApps.FolderId);
        var a = Item(1, 2, 3);
        var b = Item(9, 8, 7, 6);
        var cida = Cida(parent, a, b);

        var items = ShellIdList.Parse(cida);
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal(parent.Concat(a).Concat(new byte[2]), items[0]);
        Assert.Equal(parent.Concat(b).Concat(new byte[2]), items[1]);
        Assert.Equal(parent, ShellIdList.Parent(cida));
        Assert.True(ShellIdList.IsAppsFolder(ShellIdList.Parent(cida)));
    }

    [Fact]
    public void Apps_folder_is_recognized_from_the_parent_id_list()
    {
        // Bu makinede kabuğun verdiği: 14-00-1F-80-9B-D4-34-42-45-02-F3-4D-B7-80-38-93-94-34-56-E1(-00-00).
        byte[] seen = [0x14, 0x00, 0x1F, 0x80, 0x9B, 0xD4, 0x34, 0x42, 0x45, 0x02, 0xF3, 0x4D, 0xB7, 0x80, 0x38, 0x93, 0x94, 0x34, 0x56, 0xE1];
        Assert.True(ShellIdList.IsAppsFolder(seen));
        Assert.True(ShellIdList.IsAppsFolder([.. seen, 0, 0]));
        Assert.False(ShellIdList.IsAppsFolder(RootItem(new Guid("645FF040-5081-101B-9F08-00AA002F954E")))); // Geri Dönüşüm Kutusu
        Assert.False(ShellIdList.IsAppsFolder([.. seen, .. Item(1, 2), 0, 0]));                             // klasörün altındaki bir şey
        Assert.False(ShellIdList.IsAppsFolder([]));                                                           // masaüstünün kendisi
        var wrongType = seen.ToArray();
        wrongType[2] = 0x31;
        Assert.False(ShellIdList.IsAppsFolder(wrongType));
    }

    [Fact]
    public void Desktop_parent_gives_the_child_list_itself()
    {
        var child = Item(5, 5, 5);
        var items = ShellIdList.Parse(Cida([], child));
        Assert.Equal(child.Concat(new byte[2]), Assert.Single(items!));
        Assert.False(ShellIdList.IsAppsFolder(ShellIdList.Parent(Cida([], child))));
    }

    [Fact]
    public void Broken_or_oversized_cida_is_rejected()
    {
        var good = Cida(RootItem(StartApps.FolderId), Item(1, 2, 3));
        Assert.NotNull(ShellIdList.Parse(good));

        Assert.Null(ShellIdList.Parse([]));
        Assert.Null(ShellIdList.Parse(good.AsSpan(0, 10)));

        var none = good.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(none, 0);
        Assert.Null(ShellIdList.Parse(none));

        var many = good.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(many, ShellIdList.MaxItems + 1);
        Assert.Null(ShellIdList.Parse(many));

        var outside = good.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outside.AsSpan(8), (uint)good.Length + 10);
        Assert.Null(ShellIdList.Parse(outside));

        var intoTable = good.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(intoTable.AsSpan(8), 2);
        Assert.Null(ShellIdList.Parse(intoTable));

        var overrun = good.ToArray();
        var childAt = (int)BinaryPrimitives.ReadUInt32LittleEndian(overrun.AsSpan(8));
        BinaryPrimitives.WriteUInt16LittleEndian(overrun.AsSpan(childAt), 0x7FFF);
        Assert.Null(ShellIdList.Parse(overrun));

        var zeroLength = good.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(zeroLength.AsSpan(childAt), 1);
        Assert.Null(ShellIdList.Parse(zeroLength));

        Assert.Null(ShellIdList.Parse(good.AsSpan(0, good.Length - 2))); // son sonlandırıcı yok
        Assert.Null(ShellIdList.Parse(new byte[ShellIdList.MaxBytes + 1]));
    }

    [Fact]
    public void Start_app_paths_are_shell_objects_with_a_parsable_shell_name()
    {
        const string id = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var path = StartApps.PathFor(id)!;
        Assert.Equal(@"::{4234D49B-0245-4DF3-B780-3893943456E1}\" + id, path);
        Assert.True(StartApps.IsApp(path));
        Assert.True(StartApps.IsApp(path.ToLowerInvariant()));
        Assert.Equal(id, StartApps.Id(path));
        Assert.Equal(@"shell:AppsFolder\" + id, StartApps.ShellPath(path));
        // 2.1 ve kutunun geri kalanı onu kabuk nesnesi sayar: "bulunamadı" değil, taşınmaz, diskte aranmaz.
        Assert.True(TileItem.IsShellObject(path));
        Assert.True(TileItemPath.IsShellObject(path));
        // Ad silinirse görünen yedek ad; yol biçimli kimlikte dosya adı.
        Assert.Equal(id, TileItem.ShellName(path));
        Assert.Equal("RiotClientServices", TileItem.ShellName(StartApps.PathFor(@"C:\Riot Games\Riot Client\RiotClientServices.exe")!));
        // Bırakılırken yazılan ad (ItemLooks) kullanıcının adı sayılır (simge seçerken korunur); ipucunda yol gösterilmez.
        var tile = new TileItem { Name = "Hesap Makinesi", Path = path };
        Assert.True(tile.Renamed);
        Assert.Equal("Hesap Makinesi", tile.Tooltip);
        Assert.False(new TileItem { Name = id, Path = path }.Renamed);

        Assert.False(StartApps.IsApp(StartApps.Prefix));
        Assert.False(StartApps.IsApp("::{645FF040-5081-101B-9F08-00AA002F954E}"));
        Assert.False(StartApps.IsApp(@"C:\x.exe"));
        Assert.Equal("::{645FF040-5081-101B-9F08-00AA002F954E}", StartApps.ShellPath("::{645FF040-5081-101B-9F08-00AA002F954E}"));
        Assert.Null(StartApps.PathFor(""));
        Assert.Null(StartApps.PathFor("a\r\nb"));
        Assert.Null(StartApps.PathFor(new string('a', StartApps.MaxId + 1)));
    }

    [Fact]
    public void Overlapping_or_huge_id_lists_are_rejected()
    {
        // Bütün uzaklıklar aynı (büyük ama sınırın altındaki) listeyi gösterir: 64 kopya 1 MB'ı aşar, ayrılmadan reddedilir.
        var big = new byte[ShellIdList.MaxListBytes - 4];
        BinaryPrimitives.WriteUInt16LittleEndian(big, (ushort)Math.Min(big.Length, 0xFFFF));
        var child = big.AsSpan(0, BinaryPrimitives.ReadUInt16LittleEndian(big)).ToArray();
        var children = Enumerable.Repeat(child, ShellIdList.MaxItems).ToArray();
        Assert.NotNull(ShellIdList.Parse(Cida([], child)));
        var parentAt = 4 + 4 * (ShellIdList.MaxItems + 1);
        var childAt = parentAt + 2; // boş üst klasör (masaüstü): yalnızca sonlandırıcı
        var shared = new byte[childAt + child.Length + 2];
        BinaryPrimitives.WriteUInt32LittleEndian(shared, ShellIdList.MaxItems);
        child.CopyTo(shared, childAt);
        BinaryPrimitives.WriteUInt32LittleEndian(shared.AsSpan(4), (uint)parentAt);
        for (var i = 1; i <= ShellIdList.MaxItems; i++) BinaryPrimitives.WriteUInt32LittleEndian(shared.AsSpan(4 + 4 * i), (uint)childAt);
        Assert.True(shared.Length < ShellIdList.MaxBytes);
        Assert.Null(ShellIdList.Parse(shared));
        Assert.NotNull(ShellIdList.Parse(Cida([], children.Take(4).ToArray())));

        // Tek liste sınırı aşarsa (öğeleri geçerli olsa da) reddedilir.
        var item = Item(new byte[60_000]);
        Assert.NotNull(ShellIdList.Parse(Cida([], item)));
        Assert.Null(ShellIdList.Parse(Cida([], [.. item, .. item])));
    }

    [Fact]
    public void Start_app_icons_are_cached_by_app_not_by_extension()
    {
        // "shell:AppsFolder\MSEdge" ile uzantısız README, "…WINWORD.EXE.15" ile "…EXCEL.EXE.15" aynı simgeyi paylaşmamalı.
        string[] paths =
        [
            StartApps.PathFor("MSEdge")!, StartApps.PathFor("Chrome")!, StartApps.PathFor("308046B0AF4A39CB")!,
            StartApps.PathFor("Microsoft.Office.WINWORD.EXE.15")!, StartApps.PathFor("Microsoft.Office.EXCEL.EXE.15")!,
            @"shell:AppsFolder\MSEdge", @"shell:AppsFolder\Chrome", Path.Combine(_dir, "README"), Path.Combine(_dir, "LICENSE"),
        ];
        var keys = paths.Select(p => ShellIcons.Key(p, 32, preview: false, isDir: false)).ToList();
        Assert.Equal(keys.Count - 1, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()); // yalnızca README ile LICENSE (".|32") ortak
        Assert.All(paths.Take(7), p => Assert.True(ShellIcons.IsShellName(p)));
        Assert.False(ShellIcons.IsShellName(Path.Combine(_dir, "README")));
    }

    [Fact]
    public void Start_app_icon_loads_from_the_shell()
    {
        // Gerçek kabukla: Ayarlar uygulamasının kutu yolu (kabuğun ayrıştıramadığı "::{…}\AUMID") simgeye çevrilir.
        const string settings = "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";
        if (IdList(@"shell:AppsFolder\" + settings) is null) return; // bu Windows'ta yok
        var icon = ShellIcons.For(StartApps.PathFor(settings)!, 32);
        Assert.NotNull(icon);
        Assert.True(ShellIcons.TryCached(StartApps.PathFor(settings)!, 32, preview: false, isDirectory: false, out var cached));
        Assert.Same(icon, cached);
    }

    [Fact]
    public void Dropped_text_is_read_only_when_small() => OnSta(() =>
    {
        var small = new System.Windows.DataObject(System.Windows.DataFormats.UnicodeText, "https://example.com/");
        Assert.Equal("https://example.com/", LauncherDrop.SmallUnicodeText(small));
        Assert.Equal(LinkKind.Allowed, LauncherDrop.ReadLink(small, withTitle: false).Kind);

        var huge = new System.Windows.DataObject(System.Windows.DataFormats.UnicodeText, "https://example.com/" + new string('a', 100_000));
        Assert.Null(LauncherDrop.SmallUnicodeText(huge));
        Assert.Equal(LinkKind.None, LauncherDrop.ReadLink(huge, withTitle: false).Kind);
        Assert.Null(LauncherDrop.SmallUnicodeText(new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { @"C:\x.txt" })));
    });

    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA testi zaman aşımına uğradı");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr pidl, uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll")]
    private static extern uint ILGetSize(IntPtr pidl);

    /// <summary>Kabuğun verdiği tam kimlik listesi (bayt olarak); ayrıştıramazsa null.</summary>
    private static byte[]? IdList(string name)
    {
        if (SHParseDisplayName(name, IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero) return null;
        try
        {
            var bytes = new byte[ILGetSize(pidl)];
            Marshal.Copy(pidl, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
    }

    [Fact]
    public async Task Real_shell_items_resolve_to_apps_and_files()
    {
        // Gerçek kabukla: Ayarlar uygulaması (Windows 10/11'de hep var) ve geçici bir dosya; CIDA masaüstü üst klasörüyle kurulur
        // (öğe listesi tam kimlik listesidir). Hiçbir şey açılmaz.
        const string settings = "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";
        var file = Path.Combine(_dir, "not.txt");
        File.WriteAllText(file, "x");
        var app = IdList(@"shell:AppsFolder\" + settings);
        var fileList = IdList(file);
        var bin = IdList("::{645FF040-5081-101B-9F08-00AA002F954E}");
        Assert.NotNull(fileList);
        var lists = new List<byte[]>();
        if (app is not null) lists.Add(app[..^2]);
        lists.Add(fileList![..^2]);
        if (bin is not null) lists.Add(bin[..^2]); // ne uygulama ne dosya (Geri Dönüşüm Kutusu): atlanır

        var items = await LauncherDrop.ResolveAsync(Cida([], [.. lists]));

        var resolvedFile = Assert.Single(items, i => !i.IsApp);
        Assert.Equal(file, resolvedFile.Path, ignoreCase: true);
        Assert.Null(resolvedFile.Name);
        if (app is not null)
        {
            var resolvedApp = Assert.Single(items, i => i.IsApp);
            Assert.Equal(StartApps.PathFor(settings), resolvedApp.Path, ignoreCase: true);
            Assert.False(string.IsNullOrWhiteSpace(resolvedApp.Name));
        }
        Assert.Empty(await LauncherDrop.ResolveAsync(null));
        Assert.Empty(await LauncherDrop.ResolveAsync([1, 2, 3]));
    }
}
