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

/// <summary>Taşıma geçmişi. Geri alınan dosyalar bir daha otomatik taşınmaz.</summary>
public sealed class MoveJournal
{
    public const int MaxEntries = 500;
    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<MoveEntry> _entries;

    public event Action? Changed;

    public MoveJournal(string path)
    {
        _path = path;
        _entries = JsonFile.Load(path, () => new List<MoveEntry>());
    }

    public IReadOnlyList<MoveEntry> Snapshot()
    {
        lock (_lock) return _entries.OrderByDescending(e => e.Time).ToList();
    }

    public void Add(MoveEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);
            Save();
        }
        Changed?.Invoke();
    }

    public void MarkUndone(MoveEntry entry)
    {
        lock (_lock)
        {
            entry.Undone = true;
            Save();
        }
        Changed?.Invoke();
    }

    /// <summary>Kullanıcı bu dosyayı geri aldıysa masaüstünde kalmasını istiyordur.</summary>
    public bool WasUndone(string path)
    {
        lock (_lock)
            return _entries.Any(e => e.Undone && string.Equals(e.Source, path, StringComparison.OrdinalIgnoreCase));
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
            Save();
        }
        Changed?.Invoke();
    }

    private void Save() => JsonFile.Save(_path, _entries);
}
