using System.Text;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    /// <summary>Elle ilerletilen sahte zamanlayıcı ve saat: patlama davranışı zamana bağlı kalmadan sınanır.</summary>
    private sealed class FakeTimer(Action tick) : IOwnerTimer
    {
        public TimeSpan? Due { get; private set; }
        public int Schedules { get; private set; }

        public void Schedule(TimeSpan due)
        {
            Due = due;
            Schedules++;
        }

        public void Cancel() => Due = null;

        public void Fire()
        {
            Due = null;
            tick();
        }
    }

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "duzenleme-store-" + Guid.NewGuid().ToString("N"))).FullName;
    private long _now = 1_000;
    private int _state;
    private int _snapshots;
    private FakeTimer _timer = null!;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _store = new SettingsStore(new DurableFile(Path.Combine(_dir, "settings.json")), () =>
        {
            _snapshots++;
            return Encoding.UTF8.GetBytes($"durum {_state}");
        }, tick => _timer = new FakeTimer(tick), () => _now);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string OnDisk()
    {
        Assert.True(_store.File.Flush(TimeSpan.FromSeconds(5)));
        return File.ReadAllText(Path.Combine(_dir, "settings.json"));
    }

    [Fact]
    public void A_burst_of_changes_is_written_once_with_the_latest_state()
    {
        for (var i = 1; i <= 20; i++)
        {
            _state = i;
            _store.MarkDirty();
            _now += 10;
        }

        Assert.Equal(1, _timer.Schedules);
        Assert.Equal(SettingsStore.BurstDelay, _timer.Due);
        Assert.Equal(0, _snapshots);

        _timer.Fire();

        Assert.Equal(1, _snapshots);
        Assert.Equal("durum 20", OnDisk());
        Assert.False(_store.IsDirty);
    }

    [Fact]
    public void Later_changes_do_not_postpone_the_write_beyond_the_first_deadline()
    {
        _store.MarkDirty();
        _now += 400;
        _store.MarkDirty();

        // Süre uzatılmaz: çökmede en fazla ilk değişiklikten bu yana ~0,5 saniye kaybolur.
        Assert.Equal(1, _timer.Schedules);
    }

    [Fact]
    public void Lazy_changes_wait_for_the_next_write_and_do_not_schedule_a_quick_one()
    {
        _state = 1;
        _store.MarkDirtyLazy();
        Assert.Equal(SettingsStore.LazyDelay, _timer.Due);

        // Normal bir değişiklik gelince ikisi birlikte, kısa sürede yazılır.
        _state = 2;
        _store.MarkDirty();
        Assert.Equal(SettingsStore.BurstDelay, _timer.Due);
        _timer.Fire();
        Assert.Equal("durum 2", OnDisk());
    }

    [Fact]
    public void Repeated_lazy_changes_schedule_only_once()
    {
        for (var i = 0; i < 100; i++)
        {
            _store.MarkDirtyLazy();
            _now += 50;
        }
        Assert.Equal(1, _timer.Schedules);
        Assert.Equal(0, _snapshots);
    }

    [Fact]
    public void FlushNow_writes_synchronously_and_cancels_the_pending_write()
    {
        _state = 7;
        _store.MarkDirty();

        Assert.True(_store.FlushNow(TimeSpan.FromSeconds(2)));
        Assert.Null(_timer.Due);
        Assert.Equal("durum 7", File.ReadAllText(Path.Combine(_dir, "settings.json")));

        // Zamanlayıcı yine de çalışırsa yazacak bir şey yoktur.
        _timer.Fire();
        Assert.Equal(1, _snapshots);
    }

    [Fact]
    public void FlushNow_without_changes_does_not_take_a_snapshot()
    {
        Assert.True(_store.FlushNow(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, _snapshots);
    }

    [Fact]
    public void A_failing_snapshot_keeps_the_change_pending()
    {
        var fail = true;
        using var store = new SettingsStore(new DurableFile(Path.Combine(_dir, "b.json")), () =>
        {
            if (fail) throw new InvalidOperationException("liste o an değişiyordu");
            return Encoding.UTF8.GetBytes("tamam");
        }, tick => new FakeTimer(tick), () => _now);

        store.MarkDirty();
        Assert.False(store.FlushNow(TimeSpan.FromMilliseconds(200)));
        Assert.True(store.IsDirty);

        fail = false;
        Assert.True(store.FlushNow(TimeSpan.FromSeconds(2)));
        Assert.Equal("tamam", File.ReadAllText(Path.Combine(_dir, "b.json")));
    }
}
