using System.Globalization;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Yeni widget'ların saf hesapları: takvim ızgarası, geri sayım, dünya saati, işlemci, boyut metinleri, bölme sırası.</summary>
public class NewWidgetLogicTests
{
    // ---- Takvim ----

    [Fact]
    public void Month_grid_is_always_six_weeks()
    {
        foreach (var month in Enumerable.Range(1, 12))
        {
            Assert.Equal(42, MonthGrid.Days(2026, month, DayOfWeek.Monday).Count);
            Assert.Equal(42, MonthGrid.Days(2026, month, DayOfWeek.Sunday).Count);
        }
    }

    [Fact]
    public void First_cell_aligns_to_first_day_of_week()
    {
        // 1 Eylül 2026 Salı.
        Assert.Equal(new DateTime(2026, 8, 31), MonthGrid.FirstCell(2026, 9, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 8, 30), MonthGrid.FirstCell(2026, 9, DayOfWeek.Sunday));
        // 1 Şubat 2027 Pazartesi: Pazartesi başlayan takvimde ilk hücre ayın 1'i.
        Assert.Equal(new DateTime(2027, 2, 1), MonthGrid.FirstCell(2027, 2, DayOfWeek.Monday));
        Assert.All(MonthGrid.Days(2026, 9, DayOfWeek.Sunday).Where((_, i) => i % 7 == 0), d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));
    }

    [Fact]
    public void Leap_february_fits()
    {
        var days = MonthGrid.Days(2028, 2, DayOfWeek.Monday);
        Assert.Contains(new DateTime(2028, 2, 29), days);
        Assert.Equal(29, days.Count(d => d.Month == 2));
    }

    [Theory]
    [InlineData(2026, 12, 31, 53)]
    [InlineData(2027, 1, 1, 53)]
    [InlineData(2027, 1, 4, 1)]
    [InlineData(2026, 9, 28, 40)]
    public void Iso_week_numbers(int y, int m, int d, int week)
    {
        var day = new DateTime(y, m, d);
        var rowStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        Assert.Equal(week, MonthGrid.WeekNumber(rowStart, DayOfWeek.Monday));
        // Pazar başlayan satırda da Pazartesi'nin haftası.
        Assert.Equal(week, MonthGrid.WeekNumber(rowStart.AddDays(-1), DayOfWeek.Sunday));
    }

    [Fact]
    public void Weekday_order_and_first_day_setting()
    {
        Assert.Equal(DayOfWeek.Sunday, MonthGrid.WeekdayOrder(DayOfWeek.Monday)[6]);
        Assert.Equal(DayOfWeek.Saturday, MonthGrid.WeekdayOrder(DayOfWeek.Sunday)[6]);
        Assert.Equal(DayOfWeek.Monday, MonthGrid.FirstDay(null, CultureInfo.GetCultureInfo("tr-TR")));
        Assert.Equal(DayOfWeek.Sunday, MonthGrid.FirstDay(null, CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal(DayOfWeek.Sunday, MonthGrid.FirstDay(0, CultureInfo.GetCultureInfo("tr-TR")));
        Assert.Equal(DayOfWeek.Monday, MonthGrid.FirstDay(9, CultureInfo.GetCultureInfo("tr-TR")));
        Assert.Equal(new DateTime(2027, 1, 1), MonthGrid.AddMonths(new DateTime(2026, 12, 15), 1));
    }

    // ---- Geri sayım ----

    [Theory]
    [InlineData("2026-10-10", "2026-09-28", false, 12)]
    [InlineData("2026-09-28", "2026-09-28", false, 0)]
    [InlineData("2026-09-25", "2026-09-28", false, -3)]
    [InlineData("1990-10-01", "2026-09-28", true, 3)]       // doğum günü: bu yılki
    [InlineData("1990-09-01", "2026-09-28", true, 338)]     // geçti: gelecek yıl (2027-09-01)
    [InlineData("2024-02-29", "2027-02-27", true, 1)]       // artık gün: artık olmayan yılda 28 Şubat
    public void Countdown_days(string target, string today, bool yearly, int expected)
    {
        var result = CountdownDays.For(DateTime.Parse(target, CultureInfo.InvariantCulture), DateTime.Parse(today, CultureInfo.InvariantCulture), yearly);
        Assert.Equal(expected, result.Days);
    }

    // ---- Dünya saati ----

    private static TimeZoneInfo Zone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    [Fact]
    public void Offsets_handle_half_and_quarter_hours()
    {
        var istanbul = Zone("Turkey Standard Time");
        var now = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(150, WorldClock.At(now, istanbul, Zone("India Standard Time")).OffsetMinutes);
        Assert.Equal(165, WorldClock.At(now, istanbul, Zone("Nepal Standard Time")).OffsetMinutes);
        Assert.Equal(360, WorldClock.At(now, istanbul, Zone("Tokyo Standard Time")).OffsetMinutes);
        Assert.Equal("+2 sa 30 dk", WorldClock.OffsetText(150));
        Assert.Equal("−45 dk", WorldClock.OffsetText(-45));
        Assert.Equal("+6 sa", WorldClock.OffsetText(360));
        Assert.Equal("aynı saat", WorldClock.OffsetText(0));
        using var _ = L.Use(Lang.En);
        Assert.Equal("+2 h 30 min", WorldClock.OffsetText(150));
    }

    [Fact]
    public void Daylight_saving_changes_the_offset()
    {
        var istanbul = Zone("Turkey Standard Time");   // yaz saati yok (UTC+3)
        var london = Zone("GMT Standard Time");
        Assert.Equal(-120, WorldClock.At(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), istanbul, london).OffsetMinutes);
        Assert.Equal(-180, WorldClock.At(new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc), istanbul, london).OffsetMinutes);
    }

    [Fact]
    public void Day_label_across_midnight()
    {
        var istanbul = Zone("Turkey Standard Time");
        // İstanbul'da 23:30 → Tokyo'da ertesi gün 05:30; Los Angeles'ta aynı gün 13:30.
        var now = new DateTime(2026, 9, 28, 20, 30, 0, DateTimeKind.Utc);
        var tokyo = WorldClock.At(now, istanbul, Zone("Tokyo Standard Time"));
        Assert.Equal(1, tokyo.DayDelta);
        Assert.Equal(new DateTime(2026, 9, 29, 5, 30, 0), tokyo.Time);
        Assert.Equal(0, WorldClock.At(now, istanbul, Zone("Pacific Standard Time")).DayDelta);
        // İstanbul'da 01:00 → Los Angeles'ta önceki gün.
        Assert.Equal(-1, WorldClock.At(new DateTime(2026, 9, 28, 22, 0, 0, DateTimeKind.Utc), Zone("Tokyo Standard Time"), Zone("Pacific Standard Time")).DayDelta);
        Assert.Equal("Yarın", WorldClock.DayText(1));
        Assert.Equal("Dün", WorldClock.DayText(-1));
        Assert.Equal("", WorldClock.DayText(0));
    }

    [Fact]
    public void Every_city_zone_exists_on_this_windows()
    {
        foreach (var city in WorldCities.All) Assert.True(WorldClock.Find(city.ZoneId) is not null, $"{city.En}: {city.ZoneId}");
        Assert.Null(WorldClock.Find("Mars Standard Time"));
    }

    [Fact]
    public void City_search_and_labels()
    {
        Assert.Contains(WorldCities.Search("istanbul"), c => c.ZoneId == "Turkey Standard Time");
        Assert.Contains(WorldCities.Search("londra"), c => c.En == "London");
        Assert.Contains(WorldCities.Search("london"), c => c.Tr == "Londra");
        Assert.Equal("Londra", WorldCities.Label(new WorldZone { Id = "GMT Standard Time" }, null));
        Assert.Equal("Ofis", WorldCities.Label(new WorldZone { Id = "GMT Standard Time", Label = " Ofis " }, null));
        using var _ = L.Use(Lang.En);
        Assert.Equal("London", WorldCities.Label(new WorldZone { Id = "GMT Standard Time" }, null));
    }

    [Fact]
    public void Twelve_hour_format()
    {
        var t = new DateTime(2026, 9, 28, 15, 7, 0);
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("15:07", WorldClock.Time(t, h24: true, en));
        Assert.Equal("3:07", WorldClock.Time(t, h24: false, en));
        Assert.Equal("PM", WorldClock.Designator(t, en));
        Assert.Equal("ÖS", WorldClock.Designator(t, CultureInfo.GetCultureInfo("tr-TR")));
        Assert.True(WorldClock.Uses24Hour(false));
        Assert.False(WorldClock.Uses24Hour(true));
    }

    // ---- Sistem durumu ----

    [Fact]
    public void Cpu_usage_from_deltas()
    {
        var a = new CpuTimes(Idle: 1000, Kernel: 3000, User: 1000);
        // 1000 birim geçti (kernel 600 + user 400), 250'si boşta: %75.
        Assert.Equal(75, CpuUsage.Percent(a, new CpuTimes(1250, 3600, 1400))!.Value, 3);
        Assert.Null(CpuUsage.Percent(a, a));                                          // süre geçmedi
        Assert.Null(CpuUsage.Percent(a, new CpuTimes(900, 2000, 900)));               // sayaçlar geri gitti
        Assert.Equal(0, CpuUsage.Percent(a, new CpuTimes(3000, 5000, 1000))!.Value);   // hepsi boşta
        // Sayaç taşması (ulong sarması): fark doğru hesaplanır.
        var high = new CpuTimes(ulong.MaxValue - 10, ulong.MaxValue - 20, 5);
        Assert.Equal(50, CpuUsage.Percent(high, new CpuTimes(9, 19, 5))!.Value, 3);
    }

    [Fact]
    public void Byte_and_uptime_texts()
    {
        Assert.Equal("812 bayt", MeasureText.Bytes(812));
        Assert.Equal("4,5 KB", MeasureText.Bytes(4608));
        Assert.Equal("44 MB", MeasureText.Bytes(44L * 1024 * 1024 + 300_000));
        Assert.Equal("1,2 GB", MeasureText.Bytes(1_288_490_189));
        Assert.Equal("3 gün 4 sa", MeasureText.Uptime(new TimeSpan(3, 4, 12, 0)));
        Assert.Equal("5 sa 12 dk", MeasureText.Uptime(new TimeSpan(5, 12, 0)));
        Assert.Equal("7 dk", MeasureText.Uptime(TimeSpan.FromMinutes(7.9)));
        using var _ = L.Use(Lang.En);
        Assert.Equal("1 byte", MeasureText.Bytes(1));
        Assert.Equal("1.2 GB", MeasureText.Bytes(1_288_490_189));
    }

    // ---- Klasör portalı ve bölme sırası ----

    [Fact]
    public void Picked_folder_is_normalized_against_the_desktop()
    {
        const string desktop = @"C:\Users\x\Desktop";
        Assert.Equal(new FenceSource(DesktopFilter.All, null), FolderPortal.Normalize(@"C:\Users\x\Desktop\", desktop));
        Assert.Equal(new FenceSource(DesktopFilter.None, "PDF"), FolderPortal.Normalize(@"c:\users\x\desktop\PDF", desktop));
        Assert.Equal(new FenceSource(DesktopFilter.None, @"C:\Users\x\Downloads"), FolderPortal.Normalize(@"C:\Users\x\Downloads\", desktop));
        Assert.Equal(new FenceSource(DesktopFilter.None, @"C:\Users\x\Desktop\PDF\Faturalar"), FolderPortal.Normalize(@"C:\Users\x\Desktop\PDF\Faturalar", desktop));
        Assert.Equal(new FenceSource(DesktopFilter.None, @"D:\"), FolderPortal.Normalize(@"D:\", desktop));
        Assert.Equal("Downloads", FolderPortal.DisplayName(@"C:\Users\x\Downloads\"));
        Assert.Equal(@"D:\", FolderPortal.DisplayName(@"D:\"));
        Assert.True(FolderPortal.SamePath(@"C:\A\b\", @"c:\a\B"));
        Assert.Equal(@"C:\Users\x\Downloads", FolderPortal.ShortPath(@"C:\Users\x\Downloads\"));
        Assert.Equal(@"C:\…\Proje\Çizimler", FolderPortal.ShortPath(@"C:\Users\surme\OneDrive\Belgeler\Çok uzun bir klasör adı\Proje\Çizimler"));
        Assert.Equal(@"\\nas\foto\…\2024\Tatil", FolderPortal.ShortPath(@"\\nas\foto\arşiv\eski yıllar\aile albümü\2024\Tatil"));
        Assert.Equal("İndirilenler", FolderPortal.KnownName(FolderPortal.Downloads));
    }

    private static DirEntry Entry(string name, bool dir = false, long length = 0, int age = 0) =>
        new($@"C:\k\{name}", name, dir, dir ? FileAttributes.Directory : FileAttributes.Normal, new DateTime(2026, 1, 1).AddDays(-age), length);

    private static List<string> Sorted(FenceSort sort, string? sortBy, IReadOnlyList<string>? order = null) =>
        FenceOrder.Sort([Entry("b.txt", length: 5, age: 1), Entry("Z", dir: true, age: 3), Entry("a.pdf", length: 50, age: 2), Entry("c.zip", length: 20, age: 0)],
            sort, sortBy, order, e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => e.Name).ToList();

    [Fact]
    public void Fence_sort_orders()
    {
        Assert.Equal(new[] { "c.zip", "b.txt", "a.pdf", "Z" }, Sorted(FenceSort.Newest, null));
        Assert.Equal(new[] { "Z", "a.pdf", "b.txt", "c.zip" }, Sorted(FenceSort.Name, null));
        Assert.Equal(new[] { "Z", "a.pdf", "b.txt", "c.zip" }, Sorted(FenceSort.Type, null));
        Assert.Equal(new[] { "Z", "a.pdf", "b.txt", "c.zip" }, Sorted(FenceSort.Newest, FenceOrder.Oldest));
        Assert.Equal(new[] { "Z", "a.pdf", "c.zip", "b.txt" }, Sorted(FenceSort.Newest, FenceOrder.Size));
        // Bilinmeyen SortBy (daha yeni sürümden) yok sayılır.
        Assert.Equal(new[] { "Z", "a.pdf", "b.txt", "c.zip" }, Sorted(FenceSort.Name, "gelecek"));
    }

    [Fact]
    public void Manual_order_keeps_saved_order_and_appends_new_items()
    {
        var order = new[] { @"C:\k\c.zip", @"C:\k\a.pdf", @"C:\k\silindi.txt" };
        // Listede olmayanlar (b.txt, Z) sona, eskiden yeniye.
        Assert.Equal(new[] { "c.zip", "a.pdf", "Z", "b.txt" }, Sorted(FenceSort.Name, FenceOrder.Manual, order));
        Assert.Equal(FenceSort.Name, FenceOrder.Legacy(FenceOrder.Manual));
        Assert.Equal(FenceSort.Newest, FenceOrder.Legacy(FenceOrder.Oldest));
    }

    [Fact]
    public void Manual_move_places_items_before_target()
    {
        var shown = new[] { "a", "b", "c", "d" };
        Assert.Equal(new[] { "a", "d", "b", "c" }, FenceOrder.Move(shown, ["d"], before: "b"));
        Assert.Equal(new[] { "b", "c", "d", "a" }, FenceOrder.Move(shown, ["a"], before: null));
        Assert.Equal(new[] { "b", "a", "c", "d" }, FenceOrder.Move(shown, ["a"], before: "c"));
        Assert.Equal(new[] { "c", "a", "b", "d" }, FenceOrder.Move(shown, ["a", "b"], before: "d"));
        Assert.Equal(new[] { "x", "a", "b", "c", "d" }, FenceOrder.Move(shown, ["x"], before: "a"));
    }

    // ---- Yapılacaklar: bitenler alta ----

    [Fact]
    public void Done_items_go_last_keeping_relative_order()
    {
        var items = new[] { new ChecklistItem("a", true), new ChecklistItem("b", false), new ChecklistItem("c", true), new ChecklistItem("d", false) };
        Assert.Equal(new[] { "b", "d", "a", "c" }, ChecklistText.DoneLast(items, i => i.Done).Select(i => i.Text));
    }

    // ---- Yerleşim: sağ alt köşe ----

    [Fact]
    public void Corner_spot_can_be_bottom_right()
    {
        var area = new Box(0, 0, 1000, 800);
        Assert.Equal((782, 18), WidgetLayout.DesiredSpot(PlaceMode.Corner, area, 200, 100, 0, 0, cornerRight: true, offset: 16, gap: 18));
        Assert.Equal((782, 682), WidgetLayout.DesiredSpot(PlaceMode.Corner, area, 200, 100, 0, 0, cornerRight: true, offset: 16, gap: 18, cornerBottom: true));
    }
}
