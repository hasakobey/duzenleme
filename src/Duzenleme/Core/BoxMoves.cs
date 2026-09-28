using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Masaüstünden bir kısayol kutusuna taşınan öğe ("Kutulara eklediklerim masaüstünden kalksın"). Öğe görünür, sıradan
/// bir klasöre (<see cref="BoxPlan.RootFor"/>) taşınır; kutu yeni yolu gösterir. Kayıt masaüstüne geri koymak için tutulur.
/// </summary>
public sealed class BoxMove
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>Taşındığında hangi kutuya eklenmişti (bilgi amaçlı; geri koyma yola göre yapılır).</summary>
    public string WidgetId { get; set; } = "";

    /// <summary>Masaüstündeki eski yol.</summary>
    public string Original { get; set; } = "";

    /// <summary>NestDesk klasöründeki yol (kutu bu yolu gösterir).</summary>
    public string Current { get; set; } = "";

    /// <summary>Masaüstüne geri konduysa oradaki yolu; null ise öğe hâlâ NestDesk klasöründe.</summary>
    public string? ReturnedTo { get; set; }

    /// <summary>
    /// Kutudan (ya da kutunun kendisi) kaldırıldığı için geri kondu: kutu "Geri al" ile ya da kayıtlı düzenle geri gelirse
    /// öğe yeniden kutuya taşınır. Kullanıcı "Masaüstüne geri koy" dediyse false.
    /// </summary>
    public bool Reclaim { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Active => ReturnedTo is null;
}

/// <summary>
/// Kutuya taşınanların kaydı (veri klasöründe box-moves.json; ayar dosyasından ayrı: eski sürüm ayarları yeniden
/// kaydederken bu kayıtları silmesin). Önce kayıt yazılır, sonra dosya taşınır: yarıda kesilirse açılışta
/// <see cref="BoxPlan.Reconcile"/> düzeltir. İş parçacıkları arasında güvenlidir.
/// </summary>
public sealed class BoxMoveLog
{
    /// <summary>Geri konanlardan en çok bu kadarı tutulur (kutu "Geri al" ile gelirse öğeleri bulunsun diye).</summary>
    public const int MaxReturned = 300;

    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<BoxMove> _entries;

    public BoxMoveLog(string path)
    {
        _path = path;
        _entries = JsonFile.Load(path, () => new List<BoxMove>());
        _entries.RemoveAll(e => string.IsNullOrWhiteSpace(e.Original) || string.IsNullOrWhiteSpace(e.Current));
    }

    /// <summary>Kayıtların kopyası (en eskisi önce).</summary>
    public List<BoxMove> Snapshot()
    {
        lock (_lock) return _entries.Select(Copy).ToList();
    }

    /// <summary>NestDesk klasöründe duran öğe sayısı.</summary>
    public int ActiveCount
    {
        get { lock (_lock) return _entries.Count(e => e.Active); }
    }

    /// <summary>Bu yolda (NestDesk klasöründe) duran taşınmış öğe var mı?</summary>
    public bool IsMoved(string current)
    {
        lock (_lock) return _entries.Any(e => e.Active && Same(e.Current, current));
    }

    public void Add(BoxMove entry)
    {
        lock (_lock)
        {
            _entries.Add(Copy(entry));
            Save();
        }
    }

    /// <summary>Taşıma başarısız oldu: önceden yazılan kaydı siler.</summary>
    public void Remove(string id)
    {
        lock (_lock)
        {
            if (_entries.RemoveAll(e => e.Id == id) > 0) Save();
        }
    }

    /// <summary>Öğe masaüstüne geri kondu (ya da artık yok: <paramref name="returnedTo"/> eski masaüstü yolu).</summary>
    public void MarkReturned(string id, string returnedTo, bool reclaim)
    {
        lock (_lock)
        {
            if (_entries.FirstOrDefault(e => e.Id == id) is not { } entry) return;
            entry.ReturnedTo = returnedTo;
            entry.Reclaim = reclaim;
            entry.Time = DateTime.Now;
            // Geri konanlar sıraya yeniden girer: en eskisi önce silinir.
            _entries.Remove(entry);
            _entries.Add(entry);
            var excess = _entries.Count(e => !e.Active) - MaxReturned;
            for (var i = 0; i < _entries.Count && excess > 0;)
            {
                if (_entries[i].Active) i++;
                else { _entries.RemoveAt(i); excess--; }
            }
            Save();
        }
    }

    /// <summary>
    /// Uygulama bir öğeyi yeniden adlandırdı (F2): kayıtlar yeni yolu izler. Masaüstündeki eski yol (Original), NestDesk
    /// klasöründeki yol (Current) ve geri konduğu yer (ReturnedTo) güncellenir; klasörde altındaki yollar da. Yoksa kutu
    /// öğesi "taşınmış" sayılmaz, masaüstüne geri konamaz ya da kutu geri gelince bulunamazdı. Değiştiyse true.
    /// </summary>
    public bool NoteRename(string oldPath, string newPath, bool isDirectory)
    {
        lock (_lock)
        {
            var changed = false;
            foreach (var entry in _entries)
            {
                if (PathRenames.Map(entry.Original, oldPath, newPath, isDirectory) is { } original)
                {
                    entry.Original = original;
                    changed = true;
                }
                if (PathRenames.Map(entry.Current, oldPath, newPath, isDirectory) is { } current)
                {
                    entry.Current = current;
                    changed = true;
                }
                if (entry.ReturnedTo is { } returned && PathRenames.Map(returned, oldPath, newPath, isDirectory) is { } back)
                {
                    entry.ReturnedTo = back;
                    changed = true;
                }
            }
            if (changed) Save();
            return changed;
        }
    }

    private void Save() => JsonFile.Save(_path, _entries);

    private static BoxMove Copy(BoxMove e) => new()
    {
        Id = e.Id, Time = e.Time, WidgetId = e.WidgetId, Original = e.Original, Current = e.Current,
        ReturnedTo = e.ReturnedTo, Reclaim = e.Reclaim,
    };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kutuya eklenen öğe taşınacak mı, yoksa (eskisi gibi) yalnızca bağlantı mı olacak?</summary>
public enum BoxLinkReason
{
    /// <summary>Taşınır.</summary>
    None,

    /// <summary>Öğe yok (silinmiş, taşınmış).</summary>
    Missing,

    /// <summary>Masaüstünde değil (ör. Program Files'taki bir uygulama): asla taşınmaz.</summary>
    NotOnDesktop,

    /// <summary>Ortak masaüstünde; taşımak bu bilgisayardaki tüm hesapları etkiler (ayrıca izin verilmediyse).</summary>
    PublicDesktop,

    /// <summary>desktop.ini, gizli ya da sistem öğesi, Office kilit dosyası: dokunulmaz.</summary>
    Protected,

    /// <summary>Bir kuralın ya da klasör bölmesinin kullandığı masaüstü klasörü: taşınırsa otomatik taşıma ve bölme bozulur.</summary>
    UsedFolder,
}

/// <summary>Kutu öğelerinin taşınma planı. Saf hesap: dosya sistemine dokunmaz (dosya bilgisini çağıran verir).</summary>
public static class BoxPlan
{
    /// <summary>Kutu klasörlerinin kök klasörünün adı (görünür bir klasör, uygulamanın görünen adı).</summary>
    public const string RootFolderName = AppInfo.Name;

    /// <summary>Kutu klasörü adı verilemezse. Klasör ilk taşımada bir kez o anki dilde adlandırılır (BoxFolder'a yazılır).</summary>
    public static string DefaultBoxFolder => L.T("Kısayol kutusu");

    /// <summary>
    /// Taşınan öğelerin kökü: masaüstü klasörünün üstündeki "NestDesk" klasörü (ör. C:\Users\ad\NestDesk). OneDrive'a
    /// yönlendirilmiş masaüstünde OneDrive'ın içinde kalır; test masaüstünün (--desktop) de yanındadır. Masaüstü bir sürücünün
    /// köküyse kullanıcı klasörüne düşer.
    /// </summary>
    public static string RootFor(string desktopDirectory, string userProfile)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(desktopDirectory));
        return Path.Combine(string.IsNullOrEmpty(parent) ? userProfile : parent, RootFolderName);
    }

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Kutu başlığından klasör adı: geçersiz karakterler atılır, sondaki nokta ve boşluklar kırpılır, aygıt adları
    /// (CON, NUL…) kullanılmaz, en çok 60 karakter. Boş kalırsa <see cref="DefaultBoxFolder"/>.
    /// </summary>
    public static string FolderNameFor(string? title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string((title ?? "").Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (name.Length > 60) name = name[..60];
        name = name.TrimEnd('.', ' ').Trim();
        if (name.Length == 0) return DefaultBoxFolder;
        var stem = name.Split('.')[0].Trim();
        return ReservedNames.Contains(stem) ? L.F("{0} kutusu", name) : name;
    }

    /// <summary>Yolun bulunduğu klasör (sondaki ayraç yok sayılır).</summary>
    public static string? ParentOf(string path) =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } parent
            ? Path.TrimEndingDirectorySeparator(parent)
            : null;

    private static bool SameDir(string? a, string b) =>
        a is not null && string.Equals(a, Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Kutuya eklenen öğe NestDesk klasörüne taşınır mı? Yalnızca kullanıcının masaüstündeki (ve izin verildiyse ortak
    /// masaüstündeki) öğeler taşınır; masaüstünde olmayan hiçbir şeye dokunulmaz.
    /// </summary>
    /// <param name="attributes">Öğenin öznitelikleri; yoksa null.</param>
    /// <param name="userDesktop">İzlenen masaüstü (test örneğinde --desktop klasörü).</param>
    /// <param name="publicDesktops">Ortak masaüstü (test örneğinde boş).</param>
    /// <param name="usedFolders">Kural ya da klasör bölmesinin kullandığı masaüstü klasörlerinin adları.</param>
    public static BoxLinkReason Decide(string path, FileAttributes? attributes, string userDesktop, IEnumerable<string> publicDesktops,
        bool includePublic, IEnumerable<string> usedFolders)
    {
        if (TileItemPath.IsShellObject(path)) return BoxLinkReason.NotOnDesktop;
        var parent = ParentOf(path);
        var onUser = SameDir(parent, userDesktop);
        var onPublic = !onUser && publicDesktops.Any(p => SameDir(parent, p));
        if (!onUser && !onPublic) return BoxLinkReason.NotOnDesktop;
        if (attributes is not { } attrs) return BoxLinkReason.Missing;
        if (onPublic && !includePublic) return BoxLinkReason.PublicDesktop;

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if ((attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0 ||
            name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.StartsWith("~$", StringComparison.Ordinal))
            return BoxLinkReason.Protected;
        if ((attrs & FileAttributes.Directory) != 0 && usedFolders.Any(f => FolderName.Equal(f, name)))
            return BoxLinkReason.UsedFolder;
        return BoxLinkReason.None;
    }

    /// <summary>Kısayol kutularındaki bütün öğe yolları.</summary>
    public static HashSet<string> Referenced(IEnumerable<WidgetConfig> widgets) =>
        new(widgets.Where(w => w.Kind == WidgetKind.Launcher).SelectMany(w => w.Tabs).SelectMany(t => t.Items),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kutularda duran masaüstü öğeleri: kurallar bunları otomatik taşımaz (yoksa kutu "bulunamadı" gösterirdi).
    /// Yalnızca masaüstü klasörlerindeki yollar sayılır.
    /// </summary>
    public static HashSet<string> PinnedDesktopPaths(IEnumerable<WidgetConfig> widgets, IReadOnlyList<string> desktopDirs) =>
        new(Referenced(widgets).Where(p => desktopDirs.Any(d => SameDir(ParentOf(p), d))), StringComparer.OrdinalIgnoreCase);

    /// <summary>Kayıtlarla kutuların uzlaştırılması: masaüstüne geri konacaklar ve kutuda yolu düzeltilecek öğeler.</summary>
    /// <param name="Return">Hiçbir kutunun göstermediği, NestDesk klasöründeki öğeler: masaüstüne geri konur.</param>
    /// <param name="Remap">Kutunun gösterdiği ama yerinde olmayan öğenin yeni yolu. Reclaim: kutu geri geldi, öğe yeniden taşınır.</param>
    public sealed record Reconciliation(List<BoxMove> Return, List<(string From, string To, bool Reclaim)> Remap);

    /// <summary>
    /// Kutu ya da öğe kaldırıldıysa (Geri al'dan, düzen uygulamaktan, çökmeden sonra da) kayıtları kutularla uzlaştırır:
    /// hiçbir kutunun göstermediği taşınmış öğe masaüstüne döner. Kutunun gösterdiği yol yoksa: öğe taşınmış ama kutu henüz
    /// güncellenmemişse (yarıda kalan taşıma) NestDesk'teki yola, kutu kaldırılıp geri döndüyse masaüstündeki yola bağlanır.
    /// </summary>
    /// <param name="exists">Diske bakar; yalnızca bir kayıtla ilgili yollar için çağrılır (kutudaki çevrimdışı bir ağ yolu
    /// arka plan işini bile saniyelerce bekletebilir).</param>
    public static Reconciliation Reconcile(IReadOnlyList<BoxMove> records, IReadOnlySet<string> referenced, Func<string, bool> exists)
    {
        var remap = new List<(string From, string To, bool Reclaim)>();
        foreach (var path in referenced)
        {
            if (TileItemPath.IsShellObject(path)) continue;
            var moved = records.Where(r => r.Active && Same(r.Original, path)).ToList();
            var returned = records.Where(r => !r.Active && Same(r.Current, path)).ToList();
            if ((moved.Count == 0 && returned.Count == 0) || exists(path)) continue;
            if (moved.LastOrDefault(r => exists(r.Current)) is { } interrupted)
                remap.Add((path, interrupted.Current, false));
            else if (returned.LastOrDefault(r => exists(r.ReturnedTo!)) is { } back)
                remap.Add((path, back.ReturnedTo!, back.Reclaim));
        }
        // Yeniden bağlanan öğe kutuda gösterilmiş sayılır: masaüstüne geri konmaz.
        var linked = new HashSet<string>(remap.Select(r => r.To), StringComparer.OrdinalIgnoreCase);
        var toReturn = records.Where(r => r.Active && !referenced.Contains(r.Current) && !linked.Contains(r.Current)).ToList();
        return new Reconciliation(toReturn, remap);
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kutu öğelerini NestDesk klasörüne taşır ve masaüstüne geri koyar (disk işi; arka planda çağrılır).</summary>
public static class BoxFiles
{
    /// <summary>Hedef klasördeki boş ad (aynı adda varsa "ad (1)").</summary>
    public static string TargetIn(string folder, string path) =>
        FileMover.UniquePath(folder, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));

    /// <summary>Öğeyi (dosya ya da klasör) verilen yola taşır; klasörü yoksa açar.</summary>
    public static void Move(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (Directory.Exists(from)) Directory.Move(from, to);
        else File.Move(from, to);
    }

    /// <summary>
    /// Öğeyi eski klasörüne geri koyar (adı alınmışsa "ad (1)"); eski klasör yoksa <paramref name="fallbackDirectory"/>'ye.
    /// Son yolu döner.
    /// </summary>
    public static string MoveBack(string current, string original, string fallbackDirectory)
    {
        var directory = BoxPlan.ParentOf(original) is { } dir && Directory.Exists(dir) ? dir : fallbackDirectory;
        var target = FileMover.UniquePath(directory, Path.GetFileName(Path.TrimEndingDirectorySeparator(original)));
        Move(current, target);
        return target;
    }

    /// <summary>
    /// Boşalan kutu klasörünü ve (o da boşaldıysa) NestDesk kök klasörünü siler. Yalnızca kökün içindeki ya da kökün kendisi
    /// olan, gerçekten boş klasörlere dokunulur; kullanıcının içine koyduğu bir şey varsa klasör kalır.
    /// </summary>
    public static void RemoveIfEmpty(string folder, string root)
    {
        var dir = Path.TrimEndingDirectorySeparator(folder);
        var top = Path.TrimEndingDirectorySeparator(root);
        while (dir.Length >= top.Length &&
               (string.Equals(dir, top, StringComparison.OrdinalIgnoreCase) ||
                dir.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (!Directory.Exists(dir) || Directory.EnumerateFileSystemEntries(dir).Any()) return;
                Directory.Delete(dir, recursive: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
            if (BoxPlan.ParentOf(dir) is not { } parent) return;
            dir = parent;
        }
    }

    /// <summary>Öğenin öznitelikleri; yoksa ya da okunamıyorsa null.</summary>
    public static FileAttributes? Attributes(string path)
    {
        try { return TileItemPath.IsShellObject(path) ? null : File.GetAttributes(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}

/// <summary>Kutu öğesi yolları: "::{CLSID}" bir kabuk nesnesidir (Bu Bilgisayar…), dosya değil.</summary>
public static class TileItemPath
{
    public static bool IsShellObject(string path) => path.StartsWith("::", StringComparison.Ordinal);
}
