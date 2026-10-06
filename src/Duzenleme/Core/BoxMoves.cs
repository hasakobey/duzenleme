using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duzenleme.Core;

/// <summary>
/// Masaüstünden bir kısayol kutusuna (halkaya) taşınan öğe ("Kutuya koyduğum simgeler masaüstünden kalksın"). Öğe görünür, sıradan
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

    [JsonIgnore]
    public bool Active => ReturnedTo is null;

    // 2.2 kutu
    // (Metin: 2.1 bu alanları tanımaz ve kaydederken atar; kayıt yine güvenli yönde çalışır. Boşken yazılmaz.)

    /// <summary>
    /// Bu kısayol kutudaki başka bir öğe (ör. Başlat menüsünden eklenen uygulama) yüzünden masaüstünden kalktı: o öğe bir kutuda
    /// durdukça kayıt "kutuda" sayılır (<see cref="BoxPlan.Reconcile"/>). 2.1 alanı bilmez; öğeyi masaüstüne geri koyar.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ForItem { get; set; }

    /// <summary>
    /// Masaüstüne geri konuyor: taşımadan hemen önce hedef yazılır, bitince silinir. Arada kesilirse açılışta
    /// <see cref="BoxPlan.Reconcile"/> öğeyi burada bulur ve kaydı kapatır (çökmeye dayanıklı geri koyma).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReturningTo { get; set; }

    /// <summary>Daha yeni bir sürümün yazdığı, bu sürümün bilmediği alanlar: aynen geri yazılır.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// Kutuya taşınanların kaydı (veri klasöründe box-moves.json; ayar dosyasından ayrı: eski sürüm ayarları yeniden
/// kaydederken bu kayıtları silmesin). Önce kayıt yazılır, sonra dosya taşınır (geri koyarken de önce hedef yazılır:
/// <see cref="BoxMove.ReturningTo"/>): yarıda kesilirse açılışta <see cref="BoxPlan.Reconcile"/> düzeltir. İş parçacıkları
/// arasında güvenlidir.
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
        _entries.RemoveAll(e => e is null || string.IsNullOrWhiteSpace(e.Original) || string.IsNullOrWhiteSpace(e.Current));
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

    /// <summary>Kaydı ekler ve yazar. Yazılamazsa kayıt eklenmemiş sayılır ve hata fırlar (dosya o zaman taşınmaz).</summary>
    public void Add(BoxMove entry) => AddMany([entry]);

    /// <summary>Kayıtları tek yazmayla ekler (yönetici onaylı toplu taşıma, kayıt dosyası kaybolunca yeniden kurulanlar).</summary>
    public void AddMany(IReadOnlyCollection<BoxMove> entries)
    {
        if (entries.Count == 0) return;
        lock (_lock)
        {
            var copies = entries.Select(Copy).ToList();
            _entries.AddRange(copies);
            try { Save(); }
            catch
            {
                foreach (var copy in copies) _entries.Remove(copy);
                throw;
            }
        }
    }

    /// <summary>Taşıma başarısız oldu: önceden yazılan kaydı siler (yazılamazsa bellekte silinir; açılıştaki uzlaştırma
    /// diskte kalan kaydı zaten kapatır: öğe masaüstünde).</summary>
    public void Remove(string id)
    {
        lock (_lock)
        {
            if (_entries.RemoveAll(e => e.Id == id) > 0) TrySave();
        }
    }

    /// <summary>
    /// Masaüstüne geri koyma başlıyor: hedef taşımadan önce yazılır (yazılamazsa hata fırlar, dosya taşınmaz). Taşıma
    /// olmadıysa <see cref="ClearReturning"/>, olduysa <see cref="MarkReturned"/>.
    /// </summary>
    public void MarkReturning(string id, string target)
    {
        lock (_lock)
        {
            if (_entries.FirstOrDefault(e => e.Id == id) is not { } entry) return;
            var before = entry.ReturningTo;
            entry.ReturningTo = target;
            try { Save(); }
            catch
            {
                entry.ReturningTo = before;
                throw;
            }
        }
    }

    /// <summary>Geri koyma olmadı: hedef silinir (yazılamazsa bellekte silinir, sonraki yazmayla gider).</summary>
    public void ClearReturning(string id)
    {
        lock (_lock)
        {
            if (_entries.FirstOrDefault(e => e.Id == id) is not { ReturningTo: not null } entry) return;
            entry.ReturningTo = null;
            TrySave();
        }
    }

    /// <summary>
    /// Öğe masaüstüne geri kondu (ya da artık yok: <paramref name="returnedTo"/> eski masaüstü yolu). Dosya zaten yerinde
    /// olduğundan yazılamasa da kayıt bellekte kapanır (sonraki yazmayla diske gider): hata fırlatmaz.
    /// </summary>
    public void MarkReturned(string id, string returnedTo, bool reclaim)
    {
        lock (_lock)
        {
            if (_entries.FirstOrDefault(e => e.Id == id) is not { } entry) return;
            entry.ReturnedTo = returnedTo;
            entry.ReturningTo = null;
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
            TrySave();
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
                if (entry.ForItem is { } item && PathRenames.Map(item, oldPath, newPath, isDirectory) is { } forItem)
                {
                    entry.ForItem = forItem;
                    changed = true;
                }
            }
            if (changed) Save();
            return changed;
        }
    }

    private void Save() => JsonFile.Save(_path, _entries);

    private void TrySave()
    {
        try { Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("box-moves", "write-failed", ex);
        }
    }

    private static BoxMove Copy(BoxMove e) => new()
    {
        Id = e.Id, Time = e.Time, WidgetId = e.WidgetId, Original = e.Original, Current = e.Current,
        ReturnedTo = e.ReturnedTo, Reclaim = e.Reclaim, ForItem = e.ForItem, ReturningTo = e.ReturningTo,
        Extra = e.Extra is { Count: > 0 } extra ? new Dictionary<string, JsonElement>(extra) : null,
    };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kutuya eklenen öğe taşınacak mı, yoksa (eskisi gibi) yalnızca bağlantı mı olacak? (Yalnızca çalışma zamanı.)</summary>
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

    /// <summary>2.1: bir kuralın ya da klasör bölmesinin kullandığı masaüstü klasörü. 2.2'de klasör hiç taşınmaz (<see cref="Folder"/>).</summary>
    UsedFolder,

    /// <summary>Klasör: taşınmaz, kutuda bağlantı olarak durur (içindeki yolları gösteren her şey bozulurdu).</summary>
    Folder,
}

/// <summary>
/// Bir kutunun taşınmak istenen öğeleri: kutunun kimliği, NestDesk altındaki klasörü ve yollar. <see cref="ForItem"/>: bu
/// yollar kutuda değil, kutudaki o öğe (Başlat menüsünden eklenen uygulama) yüzünden kalkar (masaüstündeki kısayolu).
/// </summary>
public sealed record ClaimInput(string BoxId, string Folder, IReadOnlyList<string> Paths, string? ForItem = null);

/// <summary>
/// Taşıma planındaki bir adım: öğe hangi kutu için nereye taşınır (<see cref="Target"/>), ya da neden taşınmaz
/// (<see cref="Reason"/>). <see cref="OnPublic"/>: ortak masaüstünde (yalnızca yönetici yazar).
/// </summary>
public sealed record ClaimStep(string BoxId, string Original, string? Target, BoxLinkReason Reason, bool OnPublic, string? ForItem = null);

/// <summary>Taşıma kararının ortamı: kullanıcının masaüstü, ortak masaüstleri ve ortak masaüstündekiler de taşınsın mı.</summary>
public sealed record BoxContext(string UserDesktop, IReadOnlyList<string> PublicDesktops, bool IncludePublic);

/// <summary>Uzlaştırmada kapatılan kayıt neden kapandı (yalnızca çalışma zamanı).</summary>
public enum BoxCloseReason
{
    /// <summary>Yarıda kalan geri koyma: öğe hedefinde (ReturningTo) duruyor.</summary>
    Finished,

    /// <summary>Kullanıcı öğeyi Gezgin'de masaüstüne geri taşımış (NestDesk'te yok, eski yerinde var).</summary>
    BackOnDesktop,

    /// <summary>Öğe ne NestDesk'te ne masaüstünde: silinmiş ya da başka yere taşınmış.</summary>
    Gone,

    /// <summary>Yarıda kalan birim arası kopyalama: öğe hem masaüstünde hem NestDesk'te; kutu masaüstündekini gösterir.</summary>
    CopyLeft,
}

/// <summary>Kutulardaki masaüstü öğelerinden şimdi taşınabilenler ve yalnızca ayrı izinle (herkesin masaüstü) taşınabilenler.</summary>
public sealed record ClaimCount(List<string> Movable, List<string> PublicOnly);

/// <summary>Taşınmış öğe kutudan dışarı sürüklendikten sonra ne oldu (yalnızca çalışma zamanı).</summary>
public enum DragOutResult
{
    /// <summary>NestDesk'teki dosya duruyor: kopyalandı ya da kısayolu yapıldı; bir şey yapılmaz.</summary>
    Copied,

    /// <summary>Masaüstüne geri döndü: kayıt kapanır, öğe kutudan çıkar ("Geri al" yeniden kutuya koyar).</summary>
    Returned,

    /// <summary>Başka bir yere taşındı (ya da Geri Dönüşüm Kutusu'na): kayıt kapanır, öğe kutudan çıkar.</summary>
    LeftBox,
}

/// <summary>Kaldırırken geri konacaklar: eski yerine ve (sessiz kaldırmada herkesin masaüstündekiler) kullanıcının masaüstüne.</summary>
public sealed record UninstallPlan(List<BoxMove> InPlace, List<BoxMove> ToUserDesktop);

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

    /// <summary>
    /// Kayıttaki NestDesk yolunun kökü (kutu klasörünün üstündeki "NestDesk" klasörü); bulunamazsa null. Masaüstü sonradan
    /// başka yere taşındıysa (OneDrive) eski kayıtların kökü bugünkü <see cref="RootFor"/> değildir: boşalan eski klasörler
    /// de kendi köklerine göre temizlenir.
    /// </summary>
    public static string? RootOf(string current)
    {
        var dir = ParentOf(current);
        for (var level = 0; dir is not null && level < 2; level++, dir = ParentOf(dir))
            if (string.Equals(Path.GetFileName(dir), RootFolderName, StringComparison.OrdinalIgnoreCase)) return dir;
        return null;
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

    /// <summary>Başlığı boş kutunun (halkanın) klasör adının kaynağı: başlığı, yoksa türünün adı.</summary>
    public static string? FolderTitleOf(WidgetConfig box) =>
        !string.IsNullOrWhiteSpace(box.Title) ? box.Title
        : WidgetVariants.Is(box, WidgetVariants.Ring) ? L.T("Halka menü")
        : null;

    /// <summary>
    /// Başka bir kutunun kullanmadığı klasör adı: <paramref name="name"/>, alınmışsa "ad (2)", "ad (3)"… (harf duyarsız,
    /// en çok 60 karakter). Çoğaltılan ya da aynı adlı kutular öğelerini aynı klasöre karıştırmasın.
    /// </summary>
    public static string UniqueFolderName(string name, IEnumerable<string?> taken)
    {
        var used = new HashSet<string>(taken.OfType<string>().Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name)) return name;
        for (var i = 2; ; i++)
        {
            var suffix = $" ({i})";
            var stem = name.Length + suffix.Length > 60 ? name[..(60 - suffix.Length)].TrimEnd('.', ' ') : name;
            var candidate = stem + suffix;
            if (!used.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Yolun bulunduğu klasör (sondaki ayraç yok sayılır).</summary>
    public static string? ParentOf(string path) =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } parent
            ? Path.TrimEndingDirectorySeparator(parent)
            : null;

    /// <summary>Yolun son parçası (klasörde sondaki ayraç yok sayılır).</summary>
    public static string NameOf(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

    private static bool SameDir(string? a, string b) =>
        a is not null && string.Equals(a, Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Yol verilen klasörlerden birinin doğrudan içinde mi?</summary>
    public static bool IsDirectlyIn(string path, IEnumerable<string> directories) =>
        ParentOf(path) is { } parent && directories.Any(d => SameDir(parent, d));

    /// <summary>
    /// Kutuya eklenen öğe NestDesk klasörüne taşınır mı? Yalnızca kullanıcının masaüstündeki (ve izin verildiyse ortak
    /// masaüstündeki) dosyalar taşınır; masaüstünde olmayan hiçbir şeye dokunulmaz. Klasörler hiç taşınmaz (içindeki yolları
    /// gösteren kutu öğeleri, portallar, kurallar ve başka programlar bozulurdu): kutuda bağlantı olarak durur.
    /// </summary>
    /// <param name="attributes">Öğenin öznitelikleri; yoksa null.</param>
    /// <param name="userDesktop">İzlenen masaüstü (test örneğinde --desktop klasörü).</param>
    /// <param name="publicDesktops">Ortak masaüstü (test örneğinde boş).</param>
    public static BoxLinkReason Decide(string path, FileAttributes? attributes, string userDesktop, IEnumerable<string> publicDesktops,
        bool includePublic)
    {
        if (TileItemPath.IsShellObject(path)) return BoxLinkReason.NotOnDesktop;
        var parent = ParentOf(path);
        var onUser = SameDir(parent, userDesktop);
        var onPublic = !onUser && publicDesktops.Any(p => SameDir(parent, p));
        if (!onUser && !onPublic) return BoxLinkReason.NotOnDesktop;
        if (attributes is not { } attrs) return BoxLinkReason.Missing;
        if (onPublic && !includePublic) return BoxLinkReason.PublicDesktop;

        var name = NameOf(path);
        if ((attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0 ||
            name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.StartsWith("~$", StringComparison.Ordinal))
            return BoxLinkReason.Protected;
        if ((attrs & FileAttributes.Directory) != 0) return BoxLinkReason.Folder;
        return BoxLinkReason.None;
    }

    /// <summary>
    /// Bir taşıma işinin planı (saf): her öğe yalnızca bir kez taşınır (aynı öğe birden çok kutuda ya da halkada olsa da ilk
    /// istekte; kutu yolları sonra hepsinde yeni yola bağlanır) ve hedef adlar iş boyunca ayrılır: yönetici onayını bekleyen
    /// ortak masaüstü öğesinin hedefi, aynı işteki başka bir öğeye verilmez (hiçbir şeyin üzerine yazılmaz). Masaüstünde olmayan
    /// yolun özniteliğine bakılmaz (çevrimdışı ağ yolu işi bekletmesin).
    /// </summary>
    /// <param name="attributes">Öğenin öznitelikleri (yoksa null); yalnızca masaüstündeki yollar için çağrılır.</param>
    /// <param name="exists">Hedef klasörde bu ad dolu mu (diske bakar).</param>
    public static List<ClaimStep> PlanClaims(IReadOnlyList<ClaimInput> inputs, Func<string, FileAttributes?> attributes, BoxContext context,
        Func<string, bool> exists)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var desktops = new List<string>(context.PublicDesktops.Count + 1) { context.UserDesktop };
        desktops.AddRange(context.PublicDesktops);
        var steps = new List<ClaimStep>();
        foreach (var input in inputs)
            foreach (var path in input.Paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) continue;
                var onDesktop = !TileItemPath.IsShellObject(path) && IsDirectlyIn(path, desktops);
                var reason = Decide(path, onDesktop ? attributes(path) : null, context.UserDesktop, context.PublicDesktops,
                    context.IncludePublic);
                var onPublic = onDesktop && !IsDirectlyIn(path, [context.UserDesktop]);
                if (reason != BoxLinkReason.None)
                {
                    steps.Add(new ClaimStep(input.BoxId, path, null, reason, onPublic, input.ForItem));
                    continue;
                }
                var target = FileMover.UniquePath(input.Folder, NameOf(path), reserved, exists);
                reserved.Add(target);
                steps.Add(new ClaimStep(input.BoxId, path, target, BoxLinkReason.None, onPublic, input.ForItem));
            }
        return steps;
    }

    /// <summary>
    /// Geri konan öğenin klasörü: eski klasörü bugünkü masaüstü klasörlerinden biriyse orası, değilse (masaüstü sonradan
    /// OneDrive'a ya da başka bir yere taşındı, eski klasör artık gösterilmiyor) kullanıcının bugünkü masaüstü.
    /// </summary>
    public static string ReturnDirectory(string original, IReadOnlyList<string> desktopDirs, string userDesktop) =>
        ParentOf(original) is { } parent && desktopDirs.FirstOrDefault(d => SameDir(parent, d)) is { } desktop
            ? Path.TrimEndingDirectorySeparator(desktop)
            : Path.TrimEndingDirectorySeparator(userDesktop);

    /// <summary>Kutulardaki bütün öğe yolları.</summary>
    public static HashSet<string> Referenced(IEnumerable<WidgetConfig> widgets) =>
        new(widgets.Where(w => w.Kind == WidgetKind.Launcher).SelectMany(w => w.Tabs).Where(t => t?.Items is not null)
                .SelectMany(t => t.Items).Where(p => p is not null),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kutularda duran masaüstü öğeleri: kurallar bunları otomatik taşımaz (yoksa kutu "bulunamadı" gösterirdi).
    /// Yalnızca masaüstü klasörlerindeki yollar sayılır.
    /// </summary>
    public static HashSet<string> PinnedDesktopPaths(IEnumerable<WidgetConfig> widgets, IReadOnlyList<string> desktopDirs) =>
        new(Referenced(widgets).Where(p => desktopDirs.Any(d => SameDir(ParentOf(p), d))), StringComparer.OrdinalIgnoreCase);

    /// <summary>Kayıtlarla kutuların uzlaştırılması: masaüstüne geri konacaklar, kutuda yolu düzeltilecek öğeler ve kapanacak kayıtlar.</summary>
    /// <param name="Return">Hiçbir kutunun göstermediği, NestDesk klasöründeki öğeler: masaüstüne geri konur.</param>
    /// <param name="Remap">Kutunun gösterdiği ama yerinde olmayan öğenin yeni yolu. Reclaim: kutu geri geldi, öğe yeniden taşınır.</param>
    /// <param name="Close">Dışarıda değişmiş kayıtlar (Gezgin'de geri taşınmış, silinmiş, yarıda kalmış): dosyaya dokunulmadan
    /// kapanır. RemapTo: kutu artık bu yolu göstermeli (yoksa null).</param>
    public sealed record Reconciliation(List<BoxMove> Return, List<(string From, string To, bool Reclaim)> Remap,
        List<(BoxMove Record, string? RemapTo, BoxCloseReason Reason)> Close);

    /// <summary>
    /// Kutu ya da öğe kaldırıldıysa (Geri al'dan, düzen uygulamaktan, çökmeden sonra da) kayıtları kutularla uzlaştırır:
    /// hiçbir kutunun göstermediği taşınmış öğe masaüstüne döner. Kutunun gösterdiği yol yoksa: öğe taşınmış ama kutu henüz
    /// güncellenmemişse (yarıda kalan taşıma) NestDesk'teki yola, kutu kaldırılıp geri döndüyse masaüstündeki yola bağlanır.
    /// Dışarıda değişenler de bulunur: NestDesk'teki öğe yoksa yarıda kalan geri koymanın hedefine ya da (Gezgin'de geri
    /// taşınmışsa) masaüstündeki eski yerine bağlanır; ikisi de yoksa kayıt kapanır ve kutu "bulunamadı" gösterir. Yarıda kalan
    /// birim arası kopyalamada (öğe iki yerde de) kayıt dosyaya dokunmadan kapanır: geri koymak "ad (1)" kopyası üretirdi.
    /// <see cref="BoxMove.ForItem"/> kaydı, o öğe bir kutuda durdukça da kutuda sayılır.
    /// </summary>
    /// <param name="exists">Diske bakar; yalnızca bir kayıtla ilgili yollar için çağrılır (kutudaki çevrimdışı bir ağ yolu
    /// arka plan işini bile saniyelerce bekletebilir).</param>
    /// <param name="movedByRule">
    /// Masaüstüne geri konan öğe o yolda artık yoksa (ör. geri konduktan sonra bir kural onu klasörüne taşıdı) taşıma geçmişinden
    /// nereye gittiği: yol ve geri konma zamanı verilir, o zamandan sonraki son (geri alınmamış) taşımanın hedefi döner
    /// (bkz. <see cref="MovedByRule"/>). Verilmezse bakılmaz.
    /// </param>
    public static Reconciliation Reconcile(IReadOnlyList<BoxMove> records, IReadOnlySet<string> referenced, Func<string, bool> exists,
        Func<string, DateTime, string?>? movedByRule = null)
    {
        var remap = new List<(string From, string To, bool Reclaim)>();
        var close = new List<(BoxMove Record, string? RemapTo, BoxCloseReason Reason)>();
        var closed = new HashSet<string>(StringComparer.Ordinal);

        void Close(BoxMove record, string? remapTo, BoxCloseReason reason)
        {
            if (closed.Add(record.Id)) close.Add((record, remapTo, reason));
        }

        foreach (var path in referenced)
        {
            if (TileItemPath.IsShellObject(path)) continue;
            var here = records.Where(r => r.Active && Same(r.Current, path)).ToList();
            var moved = records.Where(r => r.Active && Same(r.Original, path)).ToList();
            var returned = records.Where(r => !r.Active && Same(r.Current, path)).ToList();
            if (here.Count == 0 && moved.Count == 0 && returned.Count == 0) continue;

            if (here.Count > 0)
            {
                // Kutu NestDesk'teki yolu gösteriyor: öğe yerindeyse sorun yok.
                if (exists(path)) continue;
                var record = here[^1];
                if (record.ReturningTo is { } returning && exists(returning))
                {
                    // Geri koyma taşımadan sonra, kayıt kapanmadan kesildi: öğe masaüstünde.
                    remap.Add((path, returning, false));
                    Close(record, returning, BoxCloseReason.Finished);
                }
                else if (exists(record.Original))
                {
                    // Kullanıcı öğeyi Gezgin'de masaüstüne geri taşımış: kutu oradakini gösterir.
                    remap.Add((path, record.Original, false));
                    Close(record, record.Original, BoxCloseReason.BackOnDesktop);
                }
                else Close(record, null, BoxCloseReason.Gone);
                // Aynı yolu gösteren başka (eski, bozuk) kayıtlar da kapanır.
                foreach (var other in here.Take(here.Count - 1)) Close(other, null, BoxCloseReason.Gone);
                continue;
            }

            if (exists(path))
            {
                // Kutu masaüstündeki yolu gösteriyor ve öğe orada. Taşınmış görünen kaydın kopyası NestDesk'te de duruyorsa
                // birim arası kopyalama yarıda kalmıştır: kayıt dosyaya dokunmadan kapanır, kutu masaüstündekini gösterir.
                foreach (var m in moved)
                    if (!referenced.Contains(m.Current) && exists(m.Current)) Close(m, null, BoxCloseReason.CopyLeft);
                continue;
            }
            if (moved.LastOrDefault(r => exists(r.Current)) is { } interrupted)
                remap.Add((path, interrupted.Current, false));
            else if (returned.LastOrDefault(r => exists(r.ReturnedTo!)) is { } back)
                remap.Add((path, back.ReturnedTo!, back.Reclaim));
            else if (movedByRule is not null &&
                     returned.Select(r => (Record: r, To: movedByRule(r.ReturnedTo!, r.Time))).LastOrDefault(x => x.To is not null && exists(x.To))
                         is { To: { } ruleTarget } found)
                remap.Add((path, ruleTarget, found.Record.Reclaim));
        }
        // Yeniden bağlanan öğe kutuda gösterilmiş sayılır: masaüstüne geri konmaz.
        var linked = new HashSet<string>(remap.Select(r => r.To), StringComparer.OrdinalIgnoreCase);
        var toReturn = records.Where(r => r.Active && !closed.Contains(r.Id) && !referenced.Contains(r.Current) && !linked.Contains(r.Current) &&
                                          !(r.ForItem is { } item && referenced.Contains(item)))
            .ToList();
        return new Reconciliation(toReturn, remap, close);
    }

    /// <summary>
    /// Kayıt dosyası kaybolduysa (silinmiş, bozuk ve yedeği de okunamıyor) kutulardaki NestDesk öğeleri için kayıtları yeniden
    /// kurar: kutunun gösterdiği, kökün bir kutu klasöründe duran ve etkin kaydı olmayan her öğe için bir kayıt (aynı öğe
    /// birden çok kutudaysa bir kez). Öğe kutudan çıkınca kullanıcının masaüstüne aynı adla (alınmışsa "ad (1)") döner.
    /// </summary>
    /// <param name="boxItems">Kutu kimliği ve gösterdiği yol (bütün kutular).</param>
    /// <param name="exists">Diske bakar; yalnızca kökün altındaki yollar için çağrılır.</param>
    public static List<BoxMove> Adopt(IReadOnlyList<BoxMove> records, IEnumerable<(string BoxId, string Path)> boxItems, string root,
        string userDesktop, Func<string, bool> exists)
    {
        var top = Path.TrimEndingDirectorySeparator(root);
        var known = new HashSet<string>(records.Where(r => r.Active).Select(r => r.Current), StringComparer.OrdinalIgnoreCase);
        var adopted = new List<BoxMove>();
        foreach (var (boxId, path) in boxItems)
        {
            if (string.IsNullOrWhiteSpace(path) || TileItemPath.IsShellObject(path) || known.Contains(path)) continue;
            // Yalnızca kökün bir kutu klasörünün doğrudan içindekiler (NestDesk\<kutu>\öğe).
            if (ParentOf(path) is not { } folder || !SameDir(ParentOf(folder), top) || !exists(path)) continue;
            known.Add(path);
            adopted.Add(new BoxMove
            {
                WidgetId = boxId,
                Original = Path.Combine(Path.TrimEndingDirectorySeparator(userDesktop), NameOf(path)),
                Current = path,
            });
        }
        return adopted;
    }

    /// <summary>
    /// Yeniden denenecekler listesi (<see cref="AppSettings.BoxClaimRetry"/>): açık olduğu için ya da bir G/Ç hatasıyla
    /// taşınamayan öğe girer; taşınan, artık taşınmayacak olan (klasör, yok, korunan, ortak masaüstü, izin verilmedi) çıkar.
    /// Sıra korunur. Değişmediyse aynı liste nesnesi, boşaldıysa null döner.
    /// </summary>
    public static List<string>? UpdateRetry(List<string>? list, IEnumerable<ClaimOutcome> outcomes)
    {
        var current = list ?? [];
        var set = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var outcome in outcomes)
        {
            if (outcome.Failure is BoxFailure.Locked or BoxFailure.Other) changed |= set.Add(outcome.Step.Original);
            else changed |= set.Remove(outcome.Step.Original);
        }
        if (!changed) return list;
        if (set.Count == 0) return null;
        var kept = current.Where(set.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        kept.AddRange(set.Except(kept, StringComparer.OrdinalIgnoreCase));
        return kept;
    }

    // ------------------------------------------------------------------------------------------------- 2.2 kolay (UX)

    /// <summary>
    /// Kutulardaki (verilen) yollardan kaç tanesi masaüstünden kalkabilir: <c>Movable</c> şimdi taşınır (Decide None),
    /// <c>PublicOnly</c> herkesin masaüstünde ve yalnızca ayrı izinle taşınır. Aynı yol bir kez sayılır; klasörler, korunanlar
    /// ve masaüstünde olmayanlar sayılmaz. Masaüstünde olmayan yolun özniteliğine bakılmaz.
    /// </summary>
    /// <param name="attributes">Masaüstündeki öğenin öznitelikleri (arayüzde masaüstünün anlık görüntüsünden; yoksa null).</param>
    public static ClaimCount ClaimCandidates(IEnumerable<string> paths, Func<string, FileAttributes?> attributes, BoxContext context)
    {
        var movable = new List<string>();
        var publicOnly = new List<string>();
        var desktops = new List<string>(context.PublicDesktops.Count + 1) { context.UserDesktop };
        desktops.AddRange(context.PublicDesktops);
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TileItemPath.IsShellObject(path) || !IsDirectlyIn(path, desktops)) continue;
            var attrs = attributes(path);
            var reason = Decide(path, attrs, context.UserDesktop, context.PublicDesktops, context.IncludePublic);
            if (reason == BoxLinkReason.None) movable.Add(path);
            else if (reason == BoxLinkReason.PublicDesktop &&
                     Decide(path, attrs, context.UserDesktop, context.PublicDesktops, includePublic: true) == BoxLinkReason.None)
                publicOnly.Add(path);
        }
        return new ClaimCount(movable, publicOnly);
    }

    /// <summary>
    /// Yol birden çok yerde mi duruyor (başka bir kutuda, halkada ya da aynı kutunun başka sekmesinde)? Öyleyse bir yerden
    /// kaldırılınca masaüstüne dönmez, bölmede de görünmez.
    /// </summary>
    public static bool HeldElsewhere(IEnumerable<WidgetConfig> launchers, string path) =>
        launchers.Where(w => w.Kind == WidgetKind.Launcher).SelectMany(w => w.Tabs).Where(t => t?.Items is not null)
            .Count(t => t.Items.Any(p => p is not null && Same(p, path))) > 1;

    /// <summary>
    /// Kaldırma sonrasında masaüstüne dönecek taşınmış öğeler: <paramref name="boxId"/> kutusundan (ya da yalnızca
    /// <paramref name="tab"/> sekmesinden; <paramref name="path"/> verilirse yalnızca o öğe) çıkınca hiçbir kutuda ya da
    /// halkada kalmayan ve taşınmış olanlar. Bildirim cümleleri ("Masaüstünden kalkan {0} simge geri dönüyor.") bunu sayar.
    /// </summary>
    public static List<string> ReturningIf(IEnumerable<WidgetConfig> launchers, string boxId, LauncherTab? tab, string? path,
        Func<string, bool> isMoved)
    {
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var box in launchers.Where(w => w.Kind == WidgetKind.Launcher))
        {
            var thisBox = string.Equals(box.Id, boxId, StringComparison.Ordinal);
            foreach (var t in box.Tabs.Where(t => t?.Items is not null))
            {
                var inScope = thisBox && (tab is null || ReferenceEquals(t, tab));
                foreach (var p in t.Items.Where(p => p is not null))
                {
                    if (inScope && (path is null || Same(p, path))) removed.Add(p);
                    else remaining.Add(p);
                }
            }
        }
        return removed.Where(p => !remaining.Contains(p) && isMoved(p)).ToList();
    }

    /// <summary>
    /// Bölmeler masaüstünü yönetirken (ve "Kutuya koyduğum simgeler masaüstünden kalksın" açıkken) masaüstü bölmelerinin
    /// göstermediği yollar: kutularda ve halkalarda duran masaüstü öğeleri (ortak masaüstündekiler ve klasörler de) ve
    /// kutudaki bir uygulama yüzünden kalkan kısayollar (<paramref name="appShortcuts"/>: kısayol → uygulamanın kutu yolu;
    /// uygulama hâlâ bir kutudaysa). Dosyaya dokunulmaz; klasör bölmeleri ve portallar süzülmez.
    /// </summary>
    public static HashSet<string> FenceExclusions(IEnumerable<WidgetConfig> launchers, IReadOnlyList<string> desktopDirs,
        IReadOnlyDictionary<string, string>? appShortcuts = null)
    {
        var list = launchers.ToList();
        var set = PinnedDesktopPaths(list, desktopDirs);
        if (appShortcuts is { Count: > 0 })
        {
            var referenced = Referenced(list);
            foreach (var (shortcut, app) in appShortcuts)
                if (referenced.Contains(app) && IsDirectlyIn(shortcut, desktopDirs)) set.Add(shortcut);
        }
        return set;
    }

    /// <summary>
    /// Taşınmış öğe kutudan dışarı (Gezgin'e, Windows masaüstüne) sürüklendi: ne oldu? NestDesk'teki dosya duruyorsa
    /// kopyalandı ya da kısayolu yapıldı (Copied: bir şey yapılmaz); yoksa bir masaüstü klasöründe aynı adla duruyorsa geri
    /// döndü (Returned: kayıt kapanır, öğe kutudan çıkar); hiçbirinde yoksa başka bir yere taşındı (LeftBox). Önce eski
    /// klasörüne (bugün de masaüstüyse) bakılır. Yalnızca kayıtla ilgili yollar için diske bakar.
    /// </summary>
    public static (DragOutResult Result, string? Path) AfterDragOut(BoxMove record, Func<string, bool> exists, IReadOnlyList<string> desktopDirs)
    {
        if (exists(record.Current)) return (DragOutResult.Copied, null);
        var name = NameOf(record.Current);
        var first = ParentOf(record.Original) is { } parent && desktopDirs.Any(d => SameDir(parent, d)) ? parent : null;
        var order = (first is null ? desktopDirs : desktopDirs.Where(d => !SameDir(first, d)).Prepend(first))
            .Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in order)
        {
            var candidate = Path.Combine(dir, name);
            if (exists(candidate)) return (DragOutResult.Returned, candidate);
        }
        return (DragOutResult.LeftBox, null);
    }

    /// <summary>
    /// Kaldırırken (<c>--restore-desktop</c>) masaüstüne geri konacak kayıtlar: etkin olanların hepsi. Eski yerine
    /// (<c>InPlace</c>; herkesin masaüstüne dönecekse yönetici onayıyla, verilmezse kullanıcının masaüstüne) ya da sessiz
    /// kaldırmada (<paramref name="quiet"/>: onay penceresi açılamaz) herkesin masaüstündekiler kullanıcının masaüstüne
    /// (<c>ToUserDesktop</c>). <paramref name="include"/>: test masaüstünde yalnızca o masaüstünün kayıtları.
    /// </summary>
    public static UninstallPlan PlanUninstallReturns(IEnumerable<BoxMove> records, IReadOnlyList<string> desktopDirs, string userDesktop,
        bool quiet, Func<BoxMove, bool>? include = null)
    {
        var inPlace = new List<BoxMove>();
        var toUser = new List<BoxMove>();
        foreach (var record in records.Where(r => r.Active && (include?.Invoke(r) ?? true)))
        {
            var dir = ReturnDirectory(record.Original, desktopDirs, userDesktop);
            if (quiet && !SameDir(dir, userDesktop)) toUser.Add(record);
            else inPlace.Add(record);
        }
        return new UninstallPlan(inPlace, toUser);
    }

    /// <summary>
    /// Test masaüstünde kaldırma: yalnızca o masaüstünün NestDesk kökündeki ve eski yeri o masaüstlerinden birinde olan
    /// kayıtlar (gerçek masaüstünün kayıtlarına asla dokunulmaz).
    /// </summary>
    public static Func<BoxMove, bool> UninstallScope(string root, IReadOnlyList<string> desktopDirs)
    {
        var top = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return r => r.Current.StartsWith(top, StringComparison.OrdinalIgnoreCase) && IsDirectlyIn(r.Original, desktopDirs);
    }

    /// <summary>
    /// Masaüstü OneDrive'da mı (yol parçalarından biri "OneDrive" ya da "OneDrive - …")? Öyleyse kalkan simgeler OneDrive'ın
    /// eşitlediği öbür bilgisayarlarda da masaüstünden kalkar (Ayarlar bunu söyler). Diske bakmaz.
    /// </summary>
    public static bool IsOneDrivePath(string path) =>
        Path.TrimEndingDirectorySeparator(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                         part.StartsWith("OneDrive - ", StringComparison.OrdinalIgnoreCase));

    // --- "Bu klasör nedir.txt": NestDesk kökündeki açıklama (kaldırma adımı olmayan Store ve taşınabilir sürüm için de) ---

    /// <summary>Kökteki açıklama dosyasının adı (yazıldığı anın dilinde).</summary>
    public static string BreadcrumbName => L.T("Bu klasör nedir.txt");

    /// <summary>Ad, açıklama dosyasının (iki dildeki) adlarından biri mi? Boş klasör temizliği onu yok sayar.</summary>
    public static bool IsBreadcrumb(string name) =>
        L.Variants("Bu klasör nedir.txt").Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Açıklama dosyasının metni: ne olduğu, nasıl geri konacağı ve köke taşınmış her öğe için "kutud ← eski klasör"
    /// (yalnızca bu kökteki etkin kayıtlar; yoksa null: dosya silinir).
    /// </summary>
    public static string? BreadcrumbText(IEnumerable<BoxMove> records, string root)
    {
        var top = Path.TrimEndingDirectorySeparator(root);
        var lines = records.Where(r => r.Active && RootOf(r.Current) is { } own && SameDir(own, top))
            .Select(r => (Item: Path.GetRelativePath(top, r.Current), From: ParentOf(r.Original) ?? r.Original))
            .OrderBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item + "  ←  " + x.From)
            .ToList();
        if (lines.Count == 0) return null;
        var header = L.F("Bu klasördeki dosyalar, {0}'te kutulara ve halkalara koyduğun masaüstü simgeleridir. {0} açıkken bir simgeyi kutudan çıkarınca ya da Ayarlar → Masaüstü → \"Hepsini masaüstüne geri koy\" ile masaüstüne geri döner. {0}'i kaldırdıysan dosyaları aşağıdaki eski klasörlerine kendin taşıyabilirsin; {0} onları silmez.",
            AppInfo.Name);
        return header + Environment.NewLine + Environment.NewLine + L.T("Simge ← eski klasörü:") + Environment.NewLine +
               string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>
    /// Taşıma geçmişinden arama: masaüstündeki yoldan, verilen zamandan sonra (ya da aynı anda) kuralla ya da elle taşınan
    /// dosyanın son hedefi; yoksa null. Geri alınmış kayıtlar sayılmaz (dosya masaüstüne dönmüştür).
    /// </summary>
    public static Func<string, DateTime, string?> MovedByRule(IEnumerable<MoveEntry> journal)
    {
        var moves = journal.Where(e => !e.Undone).OrderBy(e => e.Time).ToList();
        return (path, since) => moves.LastOrDefault(e => e.Time >= since && Same(e.Source, path))?.Destination;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kutu öğelerini NestDesk klasörüne taşır ve masaüstüne geri koyar (disk işi; arka planda çağrılır).</summary>
public static class BoxFiles
{
    /// <summary>Hedef klasördeki boş ad (aynı adda varsa "ad (1)").</summary>
    public static string TargetIn(string folder, string path) =>
        FileMover.UniquePath(folder, BoxPlan.NameOf(path));

    /// <summary>Öğeyi (dosya ya da klasör) verilen yola taşır; klasörü yoksa açar. Hedef doluysa hata (üzerine yazılmaz).</summary>
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
        var target = FileMover.UniquePath(directory, BoxPlan.NameOf(original));
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
                if (!Directory.Exists(dir)) return;
                var entries = Directory.EnumerateFileSystemEntries(dir).ToList();
                // Kökte yalnızca açıklama dosyası ("Bu klasör nedir.txt") kaldıysa o da gider: anlatacak öğe yok.
                if (string.Equals(dir, top, StringComparison.OrdinalIgnoreCase) && entries.Count > 0 &&
                    entries.All(e => File.Exists(e) && BoxPlan.IsBreadcrumb(Path.GetFileName(e))))
                {
                    foreach (var file in entries) File.Delete(file);
                    entries.Clear();
                }
                if (entries.Count > 0) return;
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

    /// <summary>Karşılaştırılan en büyük dosya: daha büyükler hiçbir zaman "aynı" sayılmaz.</summary>
    public const long SameContentLimit = 4 * 1024 * 1024;

    /// <summary>
    /// İki dosya bayt bayt aynı mı (aynı kısayolun masaüstüne yeniden konmuş kopyası)? Klasörler, okunamayanlar ve
    /// <see cref="SameContentLimit"/>'ten büyükler aynı sayılmaz.
    /// </summary>
    public static bool SameContent(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (!fa.Exists || !fb.Exists || fa.Length != fb.Length || fa.Length > SameContentLimit) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>Kutu öğesi yolları: "::{CLSID}" bir kabuk nesnesidir (Bu Bilgisayar…), dosya değil.</summary>
public static class TileItemPath
{
    public static bool IsShellObject(string path) => path.StartsWith("::", StringComparison.Ordinal);
}
