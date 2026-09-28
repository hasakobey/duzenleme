using System.IO;

namespace Duzenleme.Core;

/// <summary>Kural motorunu, taşıyıcıyı ve geçmişi birleştirir.</summary>
public sealed class DesktopOrganizer(string desktopDirectory, Func<AppSettings> settings, MoveJournal journal)
{
    private readonly RuleEngine _engine = new(settings);
    private readonly object _moveLock = new();

    public string DesktopDirectory { get; } = desktopDirectory;
    public MoveJournal Journal => journal;

    public event Action<MoveEntry>? FileMoved;
    public event Action<string, Exception>? MoveFailed;

    private IReadOnlySet<string> _pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kısayol kutularında duran masaüstü dosyaları (tam yol, büyük/küçük harf duyarsız): kurallar bunları taşımaz, yoksa kutu
    /// "bulunamadı" gösterirdi. Arayüz iş parçacığında yeni bir küme atanır (yerinde değiştirilmez); izleyici arka planda okur.
    /// </summary>
    public IReadOnlySet<string> Pinned
    {
        get => Volatile.Read(ref _pinned);
        set => Volatile.Write(ref _pinned, value);
    }

    public IEnumerable<string> ExistingFolders() =>
        Directory.Exists(DesktopDirectory)
            ? Directory.EnumerateDirectories(DesktopDirectory).Select(d => Path.GetFileName(d)!)
            : [];

    /// <summary>Önizleme için masaüstündeki dosyalar (dosyaya dokunmaz, kilit denemez). IO hataları çağırana gider.</summary>
    public IReadOnlyList<DesktopFile> SnapshotFiles()
    {
        if (!Directory.Exists(DesktopDirectory)) return [];
        var pinned = Pinned;
        return new DirectoryInfo(DesktopDirectory).EnumerateFiles()
            .Select(f => new DesktopFile(f.Name, f.Attributes, journal.WasUndone(f.FullName), pinned.Contains(f.FullName))).ToList();
    }

    public RuleDecision Decide(string path) =>
        _engine.Decide(DesktopDirectory, Path.GetFileName(path), File.GetAttributes(path), ExistingFolders());

    /// <summary>Tek bir masaüstü dosyasını kurallara göre taşır. Taşındıysa kaydı döner.</summary>
    public MoveEntry? Organize(string path)
    {
        lock (_moveLock)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (!string.Equals(Path.GetDirectoryName(path), DesktopDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return null;
                if (journal.WasUndone(path)) return null;
                if (Pinned.Contains(path)) return null; // kısayol kutusunda duruyor

                var decision = Decide(path);
                if (!decision.ShouldMove) return null;

                var destination = FileMover.MoveInto(path, decision.TargetDirectory!);
                var entry = new MoveEntry { Source = path, Destination = destination };
                journal.Add(entry);
                FileMoved?.Invoke(entry);
                return entry;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MoveFailed?.Invoke(path, ex);
                return null;
            }
        }
    }

    /// <summary>Masaüstündeki tüm dosyaları bir kez tarar (kullanımda olanları atlar).</summary>
    public List<MoveEntry> OrganizeAll()
    {
        var moved = new List<MoveEntry>();
        if (!Directory.Exists(DesktopDirectory)) return moved;
        foreach (var file in Directory.EnumerateFiles(DesktopDirectory).ToList())
        {
            if (!FileMover.IsReady(file)) continue;
            if (Organize(file) is { } entry) moved.Add(entry);
        }
        return moved;
    }

    /// <summary>Kullanıcının sürükleyip bıraktığı dosyayı klasöre taşır; geçmişe yazılır, geri alınabilir.</summary>
    public MoveEntry MoveManually(string path, string targetDirectory)
    {
        lock (_moveLock)
        {
            var destination = FileMover.MoveInto(path, targetDirectory);
            var entry = new MoveEntry { Source = path, Destination = destination };
            journal.Add(entry);
            return entry;
        }
    }

    public void Undo(MoveEntry entry)
    {
        lock (_moveLock)
        {
            var restored = FileMover.MoveBack(entry);
            entry.Source = restored;
            journal.MarkUndone(entry);
        }
    }
}
