using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Tests;

/// <summary>Widget simgesinin tek kaynağı ve simge seçicinin kataloğu.</summary>
public class WidgetIconsTests
{
    [Fact]
    public void Catalog_symbols_exist_are_drawable_and_unique()
    {
        var all = IconCatalog.All;
        Assert.InRange(all.Count, 48, 80);
        Assert.Equal(all.Count, all.Select(i => i.Symbol).Distinct().Count());
        foreach (var (symbol, label) in all)
        {
            Assert.True(Enum.IsDefined(symbol), symbol.ToString());
            // U+FFFF üstündeki simgeyi SymbolIcon çizemez ("X" görünür); dolu biçimi başlıkta kullanılır.
            Assert.True(WidgetIcons.IsDrawable(symbol), $"{symbol} çizilemiyor");
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    [Fact]
    public void Catalog_labels_are_translated()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal("Folder", IconCatalog.LabelOf(SymbolRegular.Folder24));
        Assert.Equal("Games", IconCatalog.LabelOf(SymbolRegular.Games24));
    }

    [Fact]
    public void Chosen_symbol_wins_otherwise_kind_default()
    {
        var fence = new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" };
        Assert.Equal(SymbolRegular.DocumentPdf24, WidgetIcons.For(fence));
        fence.Icon = "sym:Rocket24";
        Assert.Equal(SymbolRegular.Rocket24, WidgetIcons.For(fence));

        // Tanınmayan, çizilemeyen ya da başlıkta çizilmeyen başvuru varsayılana düşer (ayar bozulmaz).
        foreach (var icon in new[] { "sym:YokBöyleBirSimge", "sym:Crown24", @"res:C:\x.dll,1", "img:" + new string('a', 32) + ".png", "rastgele" })
        {
            fence.Icon = icon;
            Assert.Equal(SymbolRegular.DocumentPdf24, WidgetIcons.For(fence));
        }
    }

    [Theory]
    [InlineData(WidgetKind.Clock, false, DesktopFilter.None, SymbolRegular.Clock24)]
    [InlineData(WidgetKind.Date, false, DesktopFilter.None, SymbolRegular.CalendarLtr24)]
    [InlineData(WidgetKind.Note, false, DesktopFilter.None, SymbolRegular.Note24)]
    [InlineData(WidgetKind.Note, true, DesktopFilter.None, SymbolRegular.TaskListLtr24)]
    [InlineData(WidgetKind.Launcher, false, DesktopFilter.None, SymbolRegular.AppsAddIn24)]
    [InlineData(WidgetKind.Fence, false, DesktopFilter.Folders, SymbolRegular.Folder24)]
    [InlineData(WidgetKind.Fence, false, DesktopFilter.Shortcuts, SymbolRegular.Apps24)]
    [InlineData(WidgetKind.Fence, false, DesktopFilter.Files, SymbolRegular.DocumentMultiple24)]
    [InlineData(WidgetKind.Fence, false, DesktopFilter.All, SymbolRegular.Desktop24)]
    [InlineData(WidgetKind.Fence, false, DesktopFilter.None, SymbolRegular.FolderOpen24)]
    public void Defaults_match_the_add_tiles(WidgetKind kind, bool checklist, DesktopFilter filter, SymbolRegular expected) =>
        Assert.Equal(expected, WidgetIcons.DefaultFor(new WidgetConfig { Kind = kind, NoteChecklist = checklist, Filter = filter, FolderName = "Projeler" }));

    [Fact]
    public void Snapshot_shows_a_renamed_item_at_once()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nestdesk-snap-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var old = Path.Combine(dir, "eski.txt");
            File.WriteAllText(old, "x");
            using var snapshot = new DirectorySnapshot(dir, action => action());
            snapshot.Start();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (snapshot.State != SnapshotState.Ready && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Assert.Equal(SnapshotState.Ready, snapshot.State);
            var version = snapshot.Version;

            var renamed = Path.Combine(dir, "Yeni Ad.txt");
            File.Move(old, renamed);
            snapshot.NoteRenamed(old, renamed);

            var entry = Assert.Single(snapshot.Entries);
            Assert.Equal(renamed, entry.Path);
            Assert.Equal("Yeni Ad.txt", entry.Name);
            Assert.True(snapshot.Version > version);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
