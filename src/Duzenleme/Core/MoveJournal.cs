using System.IO;

namespace Duzenleme.Core;

public sealed class MoveEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Time { get; set; } = DateTime.Now;
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Undone { get; set; }

    public string FileName => Path.GetFileName(Destination);
    public string FolderName => Path.GetFileName(Path.GetDirectoryName(Destination) ?? "");
}

/// <summary>
/// Taşıma geçmişi. Geri alınan dosyalar bir daha otomatik taşınmaz.
/// <para>Yeni taşıma kayıtları toplanıp arka planda tek seferde yazılır (<see cref="WriteDelay"/>; toplu düzenlemede her
/// dosya için bütün geçmiş yeniden yazılmaz). "Geri alındı" kararları ise hemen, eşzamanlı yazılır: kaybolursa dosya bir
/// sonraki taramada yeniden taşınırdı.</para>
/// </summary>
public sealed class MoveJournal : IDisposable
{
    public const int MaxEntries = 500;
    public static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(2);

    private readonly object _lock = new();
    private readonly List<MoveEntry> _entries;
    private readonly DurableFile _file;
    private readonly Timer _timer;
    private bool _dirty;
    private bool _armed;

    public event Action? Changed;

    public MoveJournal(string path)
    {
        _file = new DurableFile(path);
        _entries = JsonFile.Load(path, () => new List<MoveEntry>());
        _timer = new Timer(_ => WriteSoon(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Diske yazma denemesi bütün yeniden denemelere rağmen başarısız oldu (arka plan iş parçacığında).</summary>
    public event Action<Exception>? WriteFailed
    {
        add => _file.Failed += value;
        remove => _file.Failed -= value;
    }

    /// <summary>Kaç kez diske yazıldı (ölçüm ve testler için).</summary>
    public long WriteCount => _file.WriteCount;

    public IReadOnlyList<MoveEntry> Snapshot()
    {
        lock (_lock) return _entries.OrderByDescending(e => e.Time).ToList();
    }

    public void Add(MoveEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
            Trim();
            // Kullanıcının bilerek geri çıkardığı dosya ("bir daha taşıma") çökmede de korunmalı.
            if (entry.Undone) WriteNowLocked();
            else ScheduleLocked();
        }
        Changed?.Invoke();
    }

    public void MarkUndone(MoveEntry entry)
    {
        lock (_lock)
        {
            entry.Undone = true;
            WriteNowLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Kullanıcı bu dosyayı geri aldıysa masaüstünde kalmasını istiyordur.</summary>
    public bool WasUndone(string path)
    {
        lock (_lock)
            return _entries.Any(e => e.Undone && string.Equals(e.Source, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Geri alınmış (bir daha taşınmayacak) dosyaların yolları; toplu taramada dosya başına aramamak için.</summary>
    public HashSet<string> UndoneSources()
    {
        lock (_lock)
            return _entries.Where(e => e.Undone).Select(e => e.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public MoveEntry? LastActive()
    {
        lock (_lock) return _entries.LastOrDefault(e => !e.Undone);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => !e.Undone);
            WriteNowLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Bekleyen kayıtları hemen diske yazar (çıkış, oturum kapanışı, çökme). Yazıldıysa true.</summary>
    public bool Flush(TimeSpan? timeout = null)
    {
        lock (_lock)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _armed = false;
            if (_dirty) return WriteNowLocked(timeout ?? SyncTimeout);
        }
        return _file.Flush(timeout ?? SyncTimeout);
    }

    /// <summary>
    /// Geçmiş sınırı aşınca önce en eski normal kayıtlar silinir. Geri alınanlar ("bir daha taşıma" kararı) korunur;
    /// silinseydi masaüstünde bekleyen o dosya sonraki taramada yeniden taşınırdı. Onlar da sınırı aşarsa en eskileri gider.
    /// </summary>
    private void Trim()
    {
        var excess = _entries.Count - MaxEntries;
        for (var i = 0; i < _entries.Count && excess > 0;)
        {
            if (_entries[i].Undone) i++;
            else { _entries.RemoveAt(i); excess--; }
        }
        if (excess > 0) _entries.RemoveRange(0, excess);
    }

    private void ScheduleLocked()
    {
        _dirty = true;
        if (_armed) return;
        _armed = true;
        _timer.Change(WriteDelay, Timeout.InfiniteTimeSpan);
    }

    private void WriteSoon()
    {
        lock (_lock)
        {
            _armed = false;
            if (!_dirty) return;
            _dirty = false;
            // Kilit içinde sıraya verilir: araya giren eşzamanlı bir yazma (geri alma) daha eski içerikle ezilmesin.
            _file.Enqueue(JsonFile.Serialize(_entries));
        }
    }

    private bool WriteNowLocked(TimeSpan? timeout = null)
    {
        _dirty = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _armed = false;
        return _file.WriteNow(JsonFile.Serialize(_entries), timeout ?? SyncTimeout);
    }

    public void Dispose()
    {
        Flush();
        _timer.Dispose();
        _file.Dispose();
    }
}
