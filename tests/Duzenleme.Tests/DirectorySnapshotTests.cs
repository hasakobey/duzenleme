using Duzenleme.Core;

namespace Duzenleme.Tests;

public sealed class DirectorySnapshotTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "duzenleme-snap-" + Guid.NewGuid().ToString("N"))).FullName;
    private int _changes;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    // Testte "arayüz iş parçacığı" yok: bildirim olduğu yerde çalışır.
    private DirectorySnapshot Start()
    {
        var snapshot = new DirectorySnapshot(_dir, action => action());
        snapshot.Changed += () => Interlocked.Increment(ref _changes);
        snapshot.Start();
        WaitFor(() => snapshot.State == SnapshotState.Ready);
        return snapshot;
    }

    private static void WaitFor(Func<bool> condition, int seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "zaman aşımı");
            Thread.Sleep(20);
        }
    }

    [Fact]
    public void Read_lists_files_and_folders_with_attributes()
    {
        File.WriteAllText(Path.Combine(_dir, "a.pdf"), "12345");
        Directory.CreateDirectory(Path.Combine(_dir, "PDF"));
        var hidden = Path.Combine(_dir, "gizli.txt");
        File.WriteAllText(hidden, "");
        File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.Archive);

        var (entries, state) = DirectorySnapshot.Read(_dir);

        Assert.Equal(SnapshotState.Ready, state);
        Assert.Equal(3, entries.Count);
        var pdf = entries.Single(e => e.Name == "a.pdf");
        Assert.False(pdf.IsDirectory);
        Assert.Equal(5, pdf.Length);
        Assert.True(entries.Single(e => e.Name == "PDF").IsDirectory);
        var h = entries.Single(e => e.Name == "gizli.txt");
        Assert.True(h.IsHiddenOrSystem);
        // Sık değişen arşiv biti tutulmaz (boşuna yenileme olmasın).
        Assert.Equal(0, (int)(h.Attributes & FileAttributes.Archive));
    }

    [Fact]
    public void Read_reports_missing_folder()
    {
        var (entries, state) = DirectorySnapshot.Read(Path.Combine(_dir, "yok"));
        Assert.Equal(SnapshotState.Missing, state);
        Assert.Empty(entries);
    }

    [Fact]
    public void Same_entries_ignore_order_but_see_changes()
    {
        var a = new DirEntry(@"C:\d\a", "a", false, 0, DateTime.UnixEpoch, 1);
        var b = new DirEntry(@"C:\d\b", "b", true, FileAttributes.Directory, DateTime.UnixEpoch, 0);
        Assert.True(DirectorySnapshot.SameEntries([a, b], [b, a]));
        Assert.False(DirectorySnapshot.SameEntries([a, b], [a]));
        Assert.False(DirectorySnapshot.SameEntries([a, b], [a, b with { LastWriteUtc = DateTime.UnixEpoch.AddDays(1) }]));
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Created, "indirme.crdownload", null, true)]
    [InlineData(WatcherChangeTypes.Changed, "film.mkv.part", null, true)]
    [InlineData(WatcherChangeTypes.Deleted, "indirme.crdownload", null, false)]
    [InlineData(WatcherChangeTypes.Renamed, "rapor.pdf", "rapor.pdf.crdownload", false)]
    [InlineData(WatcherChangeTypes.Renamed, "rapor.pdf.crdownload", "Onaylanmamış 123.crdownload", true)]
    [InlineData(WatcherChangeTypes.Created, "rapor.pdf", null, false)]
    public void Partial_download_events_are_noise(WatcherChangeTypes type, string name, string? oldName, bool noise)
    {
        FileSystemEventArgs e = type == WatcherChangeTypes.Renamed
            ? new RenamedEventArgs(type, @"C:\d", name, oldName)
            : new FileSystemEventArgs(type, @"C:\d", name);
        Assert.Equal(noise, DirectorySnapshot.IsNoise(e));
    }

    [Fact]
    public void Picks_up_new_files_and_coalesces_a_burst_into_few_reads()
    {
        using var snapshot = Start();
        var before = snapshot.ScanCount;

        for (var i = 0; i < 40; i++) File.WriteAllText(Path.Combine(_dir, $"dosya {i}.txt"), "x");

        WaitFor(() => snapshot.Entries.Count == 40);
        Thread.Sleep(DirectorySnapshot.IdleDelay * 3);
        // 40 dosya × birkaç olay: birleştirilip bir iki okumada karşılanır.
        Assert.InRange(snapshot.ScanCount - before, 1, 6);
    }

    [Fact]
    public void Download_in_progress_does_not_trigger_reads_but_its_completion_does()
    {
        using var snapshot = Start();
        var partial = Path.Combine(_dir, "rapor.pdf.crdownload");
        var before = snapshot.ScanCount;

        File.WriteAllText(partial, "a");
        for (var i = 0; i < 10; i++)
        {
            File.AppendAllText(partial, new string('b', 4096));
            Thread.Sleep(20);
        }
        Thread.Sleep(DirectorySnapshot.IdleDelay * 3);
        Assert.Equal(before, snapshot.ScanCount);

        File.Move(partial, Path.Combine(_dir, "rapor.pdf"));
        WaitFor(() => snapshot.Entries.Any(e => e.Name == "rapor.pdf"));
        Assert.DoesNotContain(snapshot.Entries, e => e.Name.EndsWith(".crdownload"));
    }

    [Fact]
    public void Nothing_changed_means_no_notification()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "x");
        using var snapshot = Start();
        var changes = Volatile.Read(ref _changes);
        var version = snapshot.Version;

        snapshot.Request();
        WaitFor(() => snapshot.ScanCount >= 2);
        Thread.Sleep(50);

        Assert.Equal(changes, Volatile.Read(ref _changes));
        Assert.Equal(version, snapshot.Version);
    }

    [Fact]
    public void Folder_created_by_the_app_appears_before_the_watcher_reports_it()
    {
        using var snapshot = Start();
        var folder = Path.Combine(_dir, "PDF");
        Directory.CreateDirectory(folder);
        snapshot.NoteCreated(folder, isDirectory: true);

        Assert.Contains(snapshot.Entries, e => e.IsDirectory && e.Name == "PDF");
    }

    [Fact]
    public void Missing_folder_becomes_ready_when_created()
    {
        var path = Path.Combine(_dir, "sonra");
        using var snapshot = new DirectorySnapshot(path, action => action());
        snapshot.Start();
        WaitFor(() => snapshot.State == SnapshotState.Missing);
        Assert.Empty(snapshot.Entries);

        // İzleyici klasörü bekler; oluşunca kendini kurar ve klasör okunur.
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "a.txt"), "x");
        WaitFor(() => snapshot.State == SnapshotState.Ready && snapshot.Entries.Count == 1, seconds: 10);
    }

    // --- Yeni klasörler (2.2: ayrı NewFolderWatcher yerine anlık görüntünün okuma farkı) ---

    /// <summary>Anlık görüntüyü DirectoriesAppeared'a ilk okumadan önce abone olarak başlatır.</summary>
    private (DirectorySnapshot Snapshot, List<string> Appeared) StartWatchingFolders()
    {
        var appeared = new List<string>();
        var snapshot = new DirectorySnapshot(_dir, action => action());
        snapshot.DirectoriesAppeared += folders => { lock (appeared) appeared.AddRange(folders); };
        snapshot.Start();
        WaitFor(() => snapshot.State == SnapshotState.Ready);
        return (snapshot, appeared);
    }

    private static List<string> Names(List<string> appeared)
    {
        lock (appeared) return appeared.Select(Path.GetFileName).ToList()!;
    }

    [Fact]
    public void New_folder_made_outside_is_reported_but_not_files_or_what_the_first_read_found()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Eski"));
        File.WriteAllText(Path.Combine(_dir, "eski.txt"), "x");
        var (snapshot, appeared) = StartWatchingFolders();
        using var _ = snapshot;
        Assert.Empty(Names(appeared)); // ilk okumada var olanlar "yeni" değildir

        File.WriteAllText(Path.Combine(_dir, "rapor.pdf"), "x");
        Directory.CreateDirectory(Path.Combine(_dir, "Faturalar"));

        WaitFor(() => Names(appeared).Contains("Faturalar"));
        WaitFor(() => snapshot.Entries.Any(e => e.Name == "rapor.pdf"));
        Thread.Sleep(DirectorySnapshot.IdleDelay * 2);
        Assert.Equal(["Faturalar"], Names(appeared));
        Assert.Equal(Path.Combine(_dir, "Faturalar"), appeared.Single());
    }

    [Fact]
    public void Folder_created_by_the_app_is_still_reported_after_note_created()
    {
        // Uygulamanın açtığı klasör listeye beklemeden eklenir; okuma farkı yine de onu yeni sayar ("simge ver" önerisini
        // sessiz işaret susturur, izleyici değil).
        var (snapshot, appeared) = StartWatchingFolders();
        using var _ = snapshot;
        var folder = Path.Combine(_dir, "PDF");
        Directory.CreateDirectory(folder);
        snapshot.NoteCreated(folder, isDirectory: true);
        Assert.Contains(snapshot.Entries, e => e.Name == "PDF");

        WaitFor(() => Names(appeared).Contains("PDF"));
    }

    [Fact]
    public void Renamed_folder_is_reported_under_its_new_name_also_after_note_renamed()
    {
        var first = Directory.CreateDirectory(Path.Combine(_dir, "Yeni klasör")).FullName;
        var (snapshot, appeared) = StartWatchingFolders();
        using var _ = snapshot;

        // Bölmede F2: ad diskte değişir ve liste beklemeden güncellenir; okuma yeni adı yine yeni görür.
        var renamed = Path.Combine(_dir, "Faturalar");
        Directory.Move(first, renamed);
        snapshot.NoteRenamed(first, renamed);
        WaitFor(() => Names(appeared).Contains("Faturalar"));

        // Gezgin'deki yeniden adlandırma da.
        Directory.Move(renamed, Path.Combine(_dir, "Belgeler"));
        WaitFor(() => Names(appeared).Contains("Belgeler"));
        Assert.DoesNotContain("Yeni klasör", Names(appeared));
    }

    [Fact]
    public void Folders_are_tracked_only_while_someone_listens()
    {
        // Ortak masaüstü ve portallar dinlenmez: her okumada klasör kümesi kurulmaz.
        using var snapshot = Start();
        Directory.CreateDirectory(Path.Combine(_dir, "Önceden"));
        WaitFor(() => snapshot.Entries.Any(e => e.Name == "Önceden"));
        Assert.False(snapshot.TracksDirectories);

        // Sonradan abone olunca o anki liste taban olur: önceden var olan yeni sayılmaz, sonra gelen kaçmaz.
        var appeared = new List<string>();
        Action<IReadOnlyList<string>> listener = folders => { lock (appeared) appeared.AddRange(folders); };
        snapshot.DirectoriesAppeared += listener;
        Directory.CreateDirectory(Path.Combine(_dir, "Sonra"));
        WaitFor(() => Names(appeared).Contains("Sonra"));
        Assert.Equal(["Sonra"], Names(appeared));
        Assert.True(snapshot.TracksDirectories);

        // Abonelik bitince küme bırakılır; yeniden abone olununca arada gelenler yeni sayılmaz.
        snapshot.DirectoriesAppeared -= listener;
        Directory.CreateDirectory(Path.Combine(_dir, "Arada"));
        WaitFor(() => snapshot.Entries.Any(e => e.Name == "Arada"));
        WaitFor(() => !snapshot.TracksDirectories);
        snapshot.DirectoriesAppeared += listener;
        Directory.CreateDirectory(Path.Combine(_dir, "Son"));
        WaitFor(() => Names(appeared).Contains("Son"));
        Assert.Equal(["Sonra", "Son"], Names(appeared));
    }

    [Fact]
    public void Directory_diff_reports_only_folders_new_since_the_previous_read()
    {
        static DirEntry Folder(string name) => new(Path.Combine(@"C:\d", name), name, true, FileAttributes.Directory, DateTime.UnixEpoch, 0);
        static DirEntry Document(string name) => new(Path.Combine(@"C:\d", name), name, false, 0, DateTime.UnixEpoch, 1);

        var (first, none) = DirectorySnapshot.DirectoryDiff(null, [Folder("A"), Document("b.txt")]);
        Assert.Empty(none); // ilk okuma
        Assert.Equal([@"C:\d\A"], first);

        var (second, appeared) = DirectorySnapshot.DirectoryDiff(first, [Folder("A"), Folder("C"), Document("d.txt")]);
        Assert.Equal([@"C:\d\C"], appeared);

        // Silinen klasör bildirilmez; yalnızca harfi değişen klasör (eskiden olduğu gibi) yeni görünür.
        var (_, afterRename) = DirectorySnapshot.DirectoryDiff(second, [Folder("c")]);
        Assert.Equal([@"C:\d\c"], afterRename);
    }

    [Fact]
    public void Shared_snapshots_are_reference_counted()
    {
        using var all = new DirectorySnapshots(action => action());
        var first = all.Acquire(_dir);
        var second = all.Acquire(_dir + Path.DirectorySeparatorChar);

        Assert.Same(first, second);
        all.Release(first);
        Assert.Same(first, all.Find(_dir));
        all.Release(second);
        Assert.Null(all.Find(_dir));
    }
}

public sealed class ResilientWatcherTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "duzenleme-watch-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Dispose_does_not_wait_for_a_start_stuck_on_an_unreachable_folder()
    {
        // Ulaşılamayan ağ klasörü (\sunucu\paylaşım, VPN kapalı): klasöre bakmak ~40 sn sürer. Portal kaldırılınca ya da
        // uygulama kapanırken Dispose arayüzde çağrılır; kurulumun bitmesini beklememeli.
        using var probing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var watcher = ResilientWatcher.ForTests(_dir, path =>
        {
            probing.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return Directory.Exists(path);
        });
        var start = Task.Run(watcher.Start);
        Assert.True(probing.Wait(TimeSpan.FromSeconds(5)));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        watcher.Dispose();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"Dispose {sw.ElapsedMilliseconds} ms bekledi");

        // Kurulum sonradan bitince izleyici yerine konmaz, kendini atar.
        release.Set();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(watcher.IsRunning);
    }

    [Fact]
    public async Task Start_in_background_returns_at_once_even_on_a_slow_folder()
    {
        // Ağdaki ya da OneDrive'daki masaüstü: klasöre bakmak saniyelerce sürebilir; açılış ve "otomatik taşımayı aç" beklemesin.
        using var release = new ManualResetEventSlim();
        var watcher = ResilientWatcher.ForTests(_dir, path =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return Directory.Exists(path);
        });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        watcher.StartInBackground(() => started.TrySetResult());
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 50, $"StartInBackground {sw.ElapsedMilliseconds} ms bekledi");
        Assert.False(started.Task.IsCompleted);

        release.Set();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(watcher.IsRunning);
        watcher.Dispose();
    }

    [Fact]
    public void Watcher_reports_changes_and_stops_after_dispose()
    {
        var seen = 0;
        var watcher = ResilientWatcher.ForTests(_dir, Directory.Exists, _ => Interlocked.Increment(ref seen));
        watcher.Start();
        Assert.True(watcher.IsRunning);
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "a");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref seen) == 0 && DateTime.UtcNow < deadline) Thread.Sleep(20);
        Assert.True(Volatile.Read(ref seen) > 0);

        watcher.Dispose();
        Assert.False(watcher.IsRunning);
        watcher.Dispose(); // ikinci kez de sorun değil
    }
}
