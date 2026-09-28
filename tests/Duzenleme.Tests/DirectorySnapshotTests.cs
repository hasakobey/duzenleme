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
