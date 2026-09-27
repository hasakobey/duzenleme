using Duzenleme.Core;

namespace Duzenleme.Tests;

public class WidgetTextTests
{
    private static string Name(WidgetConfig c) => WidgetText.DisplayName(c);

    [Fact]
    public void Clock_and_date()
    {
        Assert.Equal("Saat", Name(new() { Kind = WidgetKind.Clock }));
        Assert.Equal("Tarih", Name(new() { Kind = WidgetKind.Date }));
    }

    [Fact]
    public void Note_uses_title_then_first_line()
    {
        Assert.Equal("Not · Alışveriş", Name(new() { Kind = WidgetKind.Note, Title = "Alışveriş", NoteText = "süt" }));
        Assert.Equal("Not · süt al", Name(new() { Kind = WidgetKind.Note, NoteText = "süt al\r\nekmek" }));
        Assert.Equal("Not · " + new string('a', 40) + "…", Name(new() { Kind = WidgetKind.Note, NoteText = new string('a', 50) }));
        Assert.Equal("Not", Name(new() { Kind = WidgetKind.Note }));
    }

    [Fact]
    public void Checklist_shows_progress()
    {
        Assert.Equal("Yapılacaklar · 1/2", Name(new() { Kind = WidgetKind.Note, NoteChecklist = true, NoteText = "☑ süt\r\n☐ ekmek" }));
        Assert.Equal("Market · 0/1", Name(new() { Kind = WidgetKind.Note, NoteChecklist = true, Title = "Market", NoteText = "☐ süt" }));
        Assert.Equal("Yapılacaklar", Name(new() { Kind = WidgetKind.Note, NoteChecklist = true }));
    }

    [Fact]
    public void Launcher_counts_items_in_all_tabs()
    {
        var launcher = new WidgetConfig
        {
            Kind = WidgetKind.Launcher,
            Tabs = [new LauncherTab { Items = [@"C:\a.exe", @"C:\b.exe"] }, new LauncherTab { Items = [@"C:\c.txt"] }],
        };
        Assert.Equal("Kısayol kutusu (3 öğe)", Name(launcher));
        launcher.Title = "Oyunlar";
        Assert.Equal("Kısayol kutusu · Oyunlar (3 öğe)", Name(launcher));
    }

    [Fact]
    public void Fence_uses_title_filter_or_folder()
    {
        Assert.Equal("Bölme · Klasörler", Name(new() { Kind = WidgetKind.Fence, Filter = DesktopFilter.Folders }));
        Assert.Equal("Bölme · PDF", Name(new() { Kind = WidgetKind.Fence, FolderName = "PDF" }));
        Assert.Equal("Bölme · İş", Name(new() { Kind = WidgetKind.Fence, FolderName = "PDF", Title = "İş" }));
    }
}
