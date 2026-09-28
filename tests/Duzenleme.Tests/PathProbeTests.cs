using System.Diagnostics;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public class PathProbeTests
{
    private int _stats;
    private int _rootChecks;

    // Süre ölçen testler: 2 çekirdekli CI makinesinde paralel testler iş parçacığı havuzunu ve xUnit'in iki iş parçacıklı
    // eşitleme bağlamını doldurunca await'in devamı saniyelerce kuyrukta bekliyordu. Havuzun alt sınırı yükseltilir ve süre
    // ölçen await'ler ConfigureAwait(false) ile xUnit bağlamına dönmez.
    static PathProbeTests()
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        if (workers < 32) ThreadPool.SetMinThreads(32, io);
    }

    private PathProbe Probe(Func<string, bool>? rootExists = null, Func<string, PathState>? stat = null, Func<string, bool>? networkDrive = null) =>
        new(p =>
            {
                Interlocked.Increment(ref _stats);
                return stat?.Invoke(p) ?? new PathState(true, false, FileAttributes.Normal);
            },
            root =>
            {
                Interlocked.Increment(ref _rootChecks);
                return rootExists?.Invoke(root) ?? true;
            },
            networkDrive ?? (_ => false))
        { NetworkTimeout = TimeSpan.FromMilliseconds(200) };

    [Theory]
    [InlineData(@"\\nas\paylasim\araclar\x.exe", @"\\nas\paylasim")]
    [InlineData(@"\\nas\paylasim", @"\\nas\paylasim")]
    [InlineData(@"\\nas", null)]
    [InlineData(@"\\?\C:\uzun\yol.txt", null)]
    [InlineData(@"C:\Users\a\b.exe", null)]
    public void Network_root_is_found_from_text(string path, string? root) =>
        Assert.Equal(root, Probe().NetworkRoot(path));

    [Fact]
    public void Mapped_network_drive_counts_as_network()
    {
        var probe = Probe(networkDrive: r => r == @"Z:\");
        Assert.Equal(@"Z:\", probe.NetworkRoot(@"Z:\belgeler\a.docx"));
        Assert.Null(probe.NetworkRoot(@"C:\belgeler\a.docx"));
    }

    [Fact]
    public async Task Local_result_is_cached()
    {
        var probe = Probe();
        var first = await probe.CheckAsync(@"C:\a.exe");
        var second = await probe.CheckAsync(@"C:\a.exe");

        Assert.True(first?.Exists);
        Assert.True(second?.Exists);
        Assert.Equal(1, _stats);
        Assert.True(probe.TryGetCached(@"C:\a.exe", out _));
    }

    [Fact]
    public async Task Offline_server_times_out_quickly_and_is_asked_only_once()
    {
        // Kapalı NAS: Windows'un sorusu saniyelerce sürer.
        var probe = Probe(rootExists: _ => { Thread.Sleep(3000); return false; });
        var clock = Stopwatch.StartNew();

        var first = await probe.CheckAsync(@"\\nas\paylasim\a.exe").ConfigureAwait(false);
        var second = await probe.CheckAsync(@"\\nas\paylasim\b.exe").ConfigureAwait(false);

        Assert.Null(first);
        Assert.Null(second);
        Assert.True(clock.ElapsedMilliseconds < 2000, $"{clock.ElapsedMilliseconds} ms");
        Assert.Equal(1, _rootChecks);
        Assert.Equal(0, _stats);
    }

    [Fact]
    public async Task Reachable_server_answers_with_the_path_state()
    {
        var probe = Probe(stat: _ => new PathState(true, true, FileAttributes.Directory));
        var state = await probe.CheckAsync(@"\\nas\paylasim\klasor");

        Assert.Equal(new PathState(true, true, FileAttributes.Directory), state);
    }

    [Fact]
    public async Task Slow_path_on_a_reachable_server_is_given_up()
    {
        var probe = Probe(stat: _ => { Thread.Sleep(3000); return PathState.Absent; });
        var clock = Stopwatch.StartNew();

        Assert.Null(await probe.CheckAsync(@"\\nas\paylasim\takilan.exe").ConfigureAwait(false));
        Assert.True(clock.ElapsedMilliseconds < 2000);
        // Sunucu bir süre "yanıt vermiyor" sayılır: sonraki öğe hiç sorulmaz.
        Assert.Null(await probe.CheckAsync(@"\\nas\paylasim\diger.exe"));
        Assert.Equal(1, _stats);
    }

    [Fact]
    public async Task Real_disk_missing_and_present_paths()
    {
        var probe = new PathProbe();
        var file = Path.GetTempFileName();
        try
        {
            Assert.Equal(true, (await probe.CheckAsync(file))?.Exists);
            Assert.Equal(false, (await probe.CheckAsync(file + ".yok"))?.Exists);
            Assert.Equal(true, (await probe.CheckAsync(Path.GetTempPath()))?.IsDirectory);
        }
        finally { File.Delete(file); }
    }
}
