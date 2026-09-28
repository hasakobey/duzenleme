namespace Duzenleme.Core;

/// <summary>
/// Eklenebilir her widget'ın başlangıç ayarı (saf; arayüzden bağımsız). Ekleme yüzeyleri (<c>Views/WidgetCatalog</c>)
/// widget'ı buradan kurar; testler her tohumun yalnızca 2.0'ın bildiği enum adlarıyla yazıldığını ve kopyalanınca/düzen
/// olarak saklanınca bozulmadığını denetler. Yer, sıra (Z) ve not rengi eklenirken verilir (WidgetManager.AddSeed).
/// </summary>
public static class WidgetSeeds
{
    public const string Clock = "Clock", Date = "Date", Note = "Note", Checklist = "Checklist", Launcher = "Launcher",
        Calendar = "Calendar", Countdown = "Countdown", Timer = "Timer", Pomodoro = "Pomodoro", Stopwatch = "Stopwatch",
        WorldClock = "WorldClock", SystemStatus = "SystemStatus", RecycleBin = "RecycleBin";

    /// <summary>Geri Dönüşüm Kutusu'nun kabuk adı ("::{CLSID}"; bölme ve kutu öğelerindeki gibi).</summary>
    public const string RecycleBinItem = "::{645FF040-5081-101B-9F08-00AA002F954E}";

    /// <summary>Tohumu olan bütün anahtarlar (portallar hariç: onlar klasör yolu ister).</summary>
    public static IReadOnlyList<string> Keys =>
        [Clock, Date, Note, Checklist, Launcher, Calendar, Countdown, Timer, Pomodoro, Stopwatch, WorldClock, SystemStatus, RecycleBin];

    /// <summary>Anahtarın yeni widget ayarı; tanınmayan anahtarda null.</summary>
    public static WidgetConfig? Create(string key) => key switch
    {
        Clock => new WidgetConfig { Kind = WidgetKind.Clock },
        Date => new WidgetConfig { Kind = WidgetKind.Date },
        Note => new WidgetConfig { Kind = WidgetKind.Note },
        // Onay kutulu liste: Kind yine Note'tur (eski sürümler maddeleri düz not olarak görür).
        Checklist => new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true },
        // Sekme adları kullanıcı verisidir: oluşturulurken bir kez arayüz dilinde yazılır.
        Launcher => new WidgetConfig
        {
            Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Name = L.T("Uygulamalar") }, new LauncherTab { Name = L.T("Dosyalar") }],
        },
        // Hafta numaraları seçmeli: yeni takvimde gizli başlar.
        Calendar => new WidgetConfig { Kind = WidgetKind.Date, Variant = WidgetVariants.Month, HiddenParts = ["weeknum"] },
        // Gün saat dilimi eki olmadan saklanır (Unspecified): dilim değişince gün kaymaz.
        Countdown => new WidgetConfig
        {
            Kind = WidgetKind.Date, Variant = WidgetVariants.Countdown,
            TargetDate = DateTime.SpecifyKind(DateTime.Today.AddDays(30), DateTimeKind.Unspecified),
        },
        Timer => TimerSeed(TimerModes.Countdown),
        Pomodoro => TimerSeed(TimerModes.Pomodoro),
        Stopwatch => TimerSeed(TimerModes.Stopwatch),
        WorldClock => new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.World, Zones = DefaultZones(TimeZoneInfo.Local.Id) },
        // Açık kalma süresi seçmeli: gizli başlar.
        SystemStatus => new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.System, HiddenParts = ["uptime"] },
        // Alt türü tanımayan sürüm (2.0) içinde Geri Dönüşüm Kutusu duran bir kısayol kutusu görür.
        RecycleBin => new WidgetConfig
        {
            Kind = WidgetKind.Launcher, Variant = WidgetVariants.Recycle, Title = L.T("Geri Dönüşüm Kutusu"),
            Tabs = [new LauncherTab { Name = L.T("Geri Dönüşüm Kutusu"), Items = [RecycleBinItem] }],
        },
        _ => null,
    };

    private static WidgetConfig TimerSeed(string mode) =>
        new() { Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer, Timer = new TimerState { Mode = mode } };

    /// <summary>
    /// Klasör portalı: masaüstü dışındaki bir klasörü gösteren bölme. Başlık klasörün adıyla yazılır (2.0 da başlığı
    /// gösterir); bilinen klasörse kimliği de saklanır (klasör taşınırsa yeniden bulunur).
    /// </summary>
    public static WidgetConfig Portal(string path, string? knownId, string title) => new()
    {
        Kind = WidgetKind.Fence, Filter = DesktopFilter.None, FolderName = path, FolderKnownId = knownId, Title = title,
        Sort = FenceSort.Newest,
    };

    /// <summary>Dünya saatinin ilk satırları: buradaki saat, Londra ve New York (aynı dilimde olan tekrar edilmez).</summary>
    public static List<WorldZone> DefaultZones(string localZoneId)
    {
        var zones = new List<WorldZone> { new() { Id = localZoneId } };
        foreach (var id in new[] { "GMT Standard Time", "Eastern Standard Time" })
            if (!zones.Any(z => string.Equals(z.Id, id, StringComparison.OrdinalIgnoreCase))) zones.Add(new WorldZone { Id = id });
        return zones;
    }

    /// <summary>
    /// Çoğaltılan widget'ın kopyası için: çalışan zamanlayıcı baştan başlar (iki widget aynı anda çalmasın), elle sıra ve
    /// gizlenen öğeler korunur.
    /// </summary>
    public static void PrepareDuplicate(WidgetConfig copy)
    {
        if (WidgetVariants.Is(copy, WidgetVariants.Timer) && copy.Timer is { } timer) copy.Timer = TimerLogic.Fresh(timer);
    }
}
