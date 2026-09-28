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
        // Klasör portalı: yolun son parçası.
        Assert.Equal("Bölme · Downloads", Name(new() { Kind = WidgetKind.Fence, FolderName = @"C:\Users\x\Downloads" }));
    }

    [Fact]
    public void New_widget_names_are_static()
    {
        Assert.Equal("Takvim", Name(WidgetSeeds.Create(WidgetSeeds.Calendar)!));
        var countdown = WidgetSeeds.Create(WidgetSeeds.Countdown)!;
        Assert.Equal("Geri sayım", Name(countdown));
        countdown.Title = "Tatil";
        Assert.Equal("Geri sayım · Tatil", Name(countdown));
        var timer = WidgetSeeds.Create(WidgetSeeds.Timer)!;
        TimerLogic.Start(timer.Timer!, DateTime.UtcNow);          // çalışırken de ad değişmez
        Assert.Equal("Zamanlayıcı · 10 dk", Name(timer));
        Assert.Equal("Pomodoro", Name(WidgetSeeds.Create(WidgetSeeds.Pomodoro)!));
        Assert.Equal("Kronometre", Name(WidgetSeeds.Create(WidgetSeeds.Stopwatch)!));
        Assert.Equal("Dünya saati · İstanbul, Londra, New York",
            Name(new() { Kind = WidgetKind.Clock, Variant = WidgetVariants.World, Zones = WidgetSeeds.DefaultZones("Turkey Standard Time") }));
        Assert.Equal("Sistem durumu", Name(WidgetSeeds.Create(WidgetSeeds.SystemStatus)!));
        Assert.Equal("Geri Dönüşüm Kutusu", Name(WidgetSeeds.Create(WidgetSeeds.RecycleBin)!));
        // Tanınmayan (daha yeni sürümün) alt tür: temel türün adı.
        Assert.Equal("Saat", Name(new() { Kind = WidgetKind.Clock, Variant = "weather" }));

        using var _ = Localization.L.Use(Localization.Lang.En);
        Assert.Equal("Calendar", Name(WidgetSeeds.Create(WidgetSeeds.Calendar)!));
        Assert.Equal("Timer · 10 min", Name(WidgetSeeds.Create(WidgetSeeds.Timer)!));
        Assert.Equal("World clock · Istanbul, London, New York",
            Name(new() { Kind = WidgetKind.Clock, Variant = WidgetVariants.World, Zones = WidgetSeeds.DefaultZones("Turkey Standard Time") }));
        Assert.Equal("Panel · Downloads", Name(new() { Kind = WidgetKind.Fence, FolderName = @"C:\Users\x\Downloads" }));
        // Türkçe oluşturulmuş Geri Dönüşüm Kutusu'nun (2.0 için yazılan) varsayılan başlığı İngilizcede İngilizce; özel başlık aynen.
        var bin = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Recycle, Title = "Geri Dönüşüm Kutusu" };
        Assert.Equal("Recycle Bin", Name(bin));
        bin.Title = "Çöp";
        Assert.Equal("Çöp", Name(bin));
    }

    [Fact]
    public void Small_widgets_can_be_told_apart_by_their_name()
    {
        // İki zamanlayıcı (çay, çamaşır) listede, tepside ve "Süre doldu" bildiriminde ayırt edilsin.
        var tea = WidgetSeeds.Create(WidgetSeeds.Timer)!;
        tea.Title = " Çay ";
        Assert.Equal("Zamanlayıcı · 10 dk · Çay", Name(tea));
        var focus = WidgetSeeds.Create(WidgetSeeds.Pomodoro)!;
        focus.Title = "Çalışma";
        Assert.Equal("Pomodoro · Çalışma", Name(focus));
        var calendar = WidgetSeeds.Create(WidgetSeeds.Calendar)!;
        calendar.Title = "İş";
        Assert.Equal("Takvim · İş", Name(calendar));
        var status = WidgetSeeds.Create(WidgetSeeds.SystemStatus)!;
        status.Title = "Ev bilgisayarı";
        Assert.Equal("Sistem durumu · Ev bilgisayarı", Name(status));
        // Dünya saatinde ad şehir listesinin yerini alır.
        var world = new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.World, Zones = WidgetSeeds.DefaultZones("Turkey Standard Time"), Title = "Ofisler" };
        Assert.Equal("Dünya saati · Ofisler", Name(world));
        // Boş ad yok sayılır.
        tea.Title = "  ";
        Assert.Equal("Zamanlayıcı · 10 dk", Name(tea));

        using var _ = Localization.L.Use(Localization.Lang.En);
        Assert.Equal("Pomodoro · Çalışma", Name(focus));
        Assert.Equal("Calendar · İş", Name(calendar));
    }
}
