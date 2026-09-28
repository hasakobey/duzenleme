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
        var undone = journal.UndoneSources();
        var pinned = Pinned;
        return new DirectoryInfo(DesktopDirectory).EnumerateFiles()
            .Select(f => new DesktopFile(f.Name, f.Attributes, undone.Contains(f.FullName), pinned.Contains(f.FullName))).ToList();
    }

    public RuleDecision Decide(string path) =>
        _engine.Decide(DesktopDirectory, Path.GetFileName(path), File.GetAttributes(path), ExistingFolders());

    /// <summary>
    /// Dosya şimdi taşınır mıydı? Dosyayı açmaz (kilit denemez): yalnızca adına, özniteliklerine, kurallara ve geçmişe
    /// bakar. İzleyici bunu dosyanın yazılması bitti mi diye bakmadan önce sorar; kuralı olmayan dosya hiç açılmaz.
    /// </summary>
    public bool WouldMove(string path)
    {
        if (!IsOnDesktop(path) || !File.Exists(path)) return false;
        if (journal.WasUndone(path)) return false;
        if (Pinned.Contains(path)) return false; // kısayol kutusunda duruyor
        return Decide(path).ShouldMove;
    }

    private bool IsOnDesktop(string path) =>
        string.Equals(Path.GetDirectoryName(path), DesktopDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Tek bir masaüstü dosyasını kurallara göre taşır. Taşındıysa kaydı döner.</summary>
    public MoveEntry? Organize(string path) => Organize(path, null);

    /// <param name="folders">Masaüstündeki klasörler (toplu taramada bir kez okunur); null ise diskten okunur.</param>
    private MoveEntry? Organize(string path, List<string>? folders)
    {
        lock (_moveLock)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (!IsOnDesktop(path)) return null;
                if (journal.WasUndone(path)) return null;
                if (Pinned.Contains(path)) return null; // kısayol kutusunda duruyor

                var decision = _engine.Decide(DesktopDirectory, Path.GetFileName(path), File.GetAttributes(path),
                    (IEnumerable<string>?)folders ?? ExistingFolders());
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

    /// <summary>
    /// Masaüstündeki tüm dosyaları bir kez tarar (kullanımda olanları atlar). Klasör listesi ve geçmiş bir kez okunur; bir
    /// dosya ancak bir kurala uyuyorsa yazılması bitti mi diye açılır (kuralı olmayan dosyalara hiç dokunulmaz).
    /// </summary>
    public List<MoveEntry> OrganizeAll()
    {
        var moved = new List<MoveEntry>();
        if (!Directory.Exists(DesktopDirectory)) return moved;
        var folders = ExistingFolders().ToList();
        var undone = journal.UndoneSources();
        var pinned = Pinned;
        foreach (var file in new DirectoryInfo(DesktopDirectory).EnumerateFiles().ToList())
        {
            if (undone.Contains(file.FullName) || pinned.Contains(file.FullName)) continue;
            FileAttributes attributes;
            try { attributes = file.Attributes; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            var decision = _engine.Decide(DesktopDirectory, file.Name, attributes, folders);
            if (!decision.ShouldMove) continue;
            if (!FileMover.IsReady(file.FullName)) continue;
            if (Organize(file.FullName, folders) is not { } entry) continue;
            moved.Add(entry);
            // "Klasör yoksa oluştur" yeni bir klasör açtıysa sonraki dosyalar onu görsün.
            var folder = Path.GetFileName(Path.GetDirectoryName(entry.Destination));
            if (folder is { Length: > 0 } && !folders.Any(f => FolderName.Equal(f, folder))) folders.Add(folder);
        }
        return moved;
    }

    /// <summary>Kullanıcının sürükleyip bıraktığı dosyayı klasöre taşır; geçmişe yazılır, geri alınabilir.</summary>
    /// <param name="move">
    /// Asıl taşıma (kaynak, hedef tam yol). Verilmezse <see cref="File.Move(string, string)"/>; arayüz başka sürücüden gelen
    /// dosyalar için Windows'un ilerleme penceresini gösteren kabuk işlemini verir.
    /// </param>
    public MoveEntry MoveManually(string path, string targetDirectory, Action<string, string>? move = null)
    {
        lock (_moveLock)
        {
            var destination = FileMover.MoveInto(path, targetDirectory, move);
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
