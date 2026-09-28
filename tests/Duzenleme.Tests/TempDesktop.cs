using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Her test için geçici, gerçek masaüstünden bağımsız bir "masaüstü" klasörü.</summary>
public sealed class TempDesktop : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "duzenleme-test-" + Guid.NewGuid().ToString("N"));
    public string Desktop => Path.Combine(Root, "Desktop");
    public AppSettings Settings { get; } = new();
    public MoveJournal Journal { get; }
    public DesktopOrganizer Organizer { get; }

    public TempDesktop()
    {
        Directory.CreateDirectory(Desktop);
        Journal = new MoveJournal(Path.Combine(Root, "journal.json"));
        Organizer = new DesktopOrganizer(Desktop, () => Settings, Journal);
    }

    public string File(string name, string content = "x")
    {
        var path = Path.Combine(Desktop, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string Folder(string name) => Directory.CreateDirectory(Path.Combine(Desktop, name)).FullName;

    public void Dispose()
    {
        // Geçmişin bekleyen yazması silinen klasörü yeniden oluşturmasın.
        Journal.Dispose();
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}
