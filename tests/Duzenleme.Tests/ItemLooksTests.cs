using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Kısayol kutusu öğelerinin adı ve simgesi (ItemLooks), yol değişimleri ve ayar uyumluluğu.</summary>
public class ItemLooksTests
{
    private const string Chrome = @"C:\Users\ali\Desktop\Google Chrome.lnk";

    private static WidgetConfig Box(params string[] items) =>
        new() { Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Name = "Uygulamalar", Items = [.. items] }] };

    [Fact]
    public void Name_equal_to_the_file_name_means_default()
    {
        var box = Box(Chrome);
        Assert.True(ItemLooks.SetName(box, Chrome, "  Tarayıcı ", "Google Chrome"));
        Assert.Equal("Tarayıcı", ItemLooks.Get(box, Chrome)!.Name);
        Assert.False(ItemLooks.SetName(box, Chrome, "Tarayıcı", "Google Chrome"));

        Assert.True(ItemLooks.SetName(box, Chrome, "Google Chrome", "Google Chrome"));
        Assert.Null(ItemLooks.Get(box, Chrome));
        Assert.Null(box.ItemLooks); // boşalan sözlük ayar dosyasında kalmaz
        Assert.False(ItemLooks.SetName(box, Chrome, "", "Google Chrome"));
    }

    [Fact]
    public void Long_names_are_cut()
    {
        var box = Box(Chrome);
        ItemLooks.SetName(box, Chrome, new string('x', 200), "Google Chrome");
        Assert.Equal(ItemLooks.MaxName, ItemLooks.Get(box, Chrome)!.Name!.Length);
    }

    [Fact]
    public void Keys_are_case_insensitive()
    {
        var box = Box(Chrome);
        ItemLooks.SetIcon(box, Chrome.ToUpperInvariant(), "sym:Globe24");
        ItemLooks.SetName(box, Chrome, "Web", "Google Chrome");
        Assert.Single(box.ItemLooks!);
        Assert.Equal("sym:Globe24", ItemLooks.Get(box, Chrome)!.Icon);
        Assert.Equal("Web", ItemLooks.Lookup(box)[Chrome.ToLowerInvariant()].Name);
    }

    [Fact]
    public void Icon_and_name_are_independent()
    {
        var box = Box(Chrome);
        ItemLooks.SetIcon(box, Chrome, "sym:Globe24");
        ItemLooks.SetName(box, Chrome, "Web", "Google Chrome");
        ItemLooks.SetIcon(box, Chrome, null);
        Assert.Equal("Web", ItemLooks.Get(box, Chrome)!.Name);
        Assert.Null(ItemLooks.Get(box, Chrome)!.Icon);
        Assert.True(ItemLooks.Reset(box, Chrome));
        Assert.Null(box.ItemLooks);
    }

    [Fact]
    public void Look_follows_a_moved_or_renamed_item()
    {
        var box = Box(Chrome, @"C:\Users\ali\Desktop\Proje\plan.txt");
        ItemLooks.SetName(box, Chrome, "Web", "Google Chrome");
        ItemLooks.SetIcon(box, @"C:\Users\ali\Desktop\Proje\plan.txt", "sym:Rocket24");

        Assert.True(ItemLooks.Move(box, Chrome, @"C:\Users\ali\NestDesk\Kutu\Google Chrome.lnk"));
        Assert.Equal("Web", ItemLooks.Get(box, @"C:\Users\ali\NestDesk\Kutu\Google Chrome.lnk")!.Name);
        Assert.Null(ItemLooks.Get(box, Chrome));

        // Klasör yeniden adlandırılınca altındaki öğeler de taşınır.
        Assert.True(ItemLooks.Move(box, @"C:\Users\ali\Desktop\Proje", @"C:\Users\ali\Desktop\Proje 2026", isDirectory: true));
        Assert.Equal("sym:Rocket24", ItemLooks.Get(box, @"C:\Users\ali\Desktop\Proje 2026\plan.txt")!.Icon);
        Assert.False(ItemLooks.Move(box, @"C:\başka", @"C:\yer"));
    }

    [Fact]
    public void Prune_keeps_items_still_in_a_tab()
    {
        var box = Box(Chrome);
        ItemLooks.SetName(box, Chrome, "Web", "Google Chrome");
        ItemLooks.SetName(box, @"C:\silindi.exe", "Eski", "silindi");
        ItemLooks.SetName(box, @"C:\geri-al.exe", "Geri", "geri-al");

        Assert.True(ItemLooks.Prune(box, keep: [@"C:\geri-al.exe"]));
        Assert.NotNull(ItemLooks.Get(box, Chrome));
        Assert.NotNull(ItemLooks.Get(box, @"C:\geri-al.exe"));
        Assert.Null(ItemLooks.Get(box, @"C:\silindi.exe"));
    }

    [Fact]
    public void Take_and_restore_for_undo()
    {
        var box = Box(Chrome);
        ItemLooks.SetName(box, Chrome, "Web", "Google Chrome");
        var look = ItemLooks.Take(box, Chrome);
        Assert.Null(box.ItemLooks);
        ItemLooks.Restore(box, Chrome, look);
        Assert.Equal("Web", ItemLooks.Get(box, Chrome)!.Name);
    }

    [Fact]
    public void Name_and_icon_round_trip_in_settings()
    {
        var box = Box(Chrome);
        box.Icon = "sym:Games24";
        ItemLooks.SetName(box, Chrome, "Oyun", "Google Chrome");
        ItemLooks.SetIcon(box, Chrome, @"res:%SystemRoot%\System32\imageres.dll,12");
        var settings = new AppSettings { Widgets = [box] };

        var json = JsonSerializer.Serialize(settings, JsonFile.Options);
        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonFile.Options)!.Widgets[0];

        Assert.Equal("sym:Games24", back.Icon);
        Assert.Equal("Oyun", ItemLooks.Get(back, Chrome)!.Name);
        Assert.Equal(@"res:%SystemRoot%\System32\imageres.dll,12", ItemLooks.Get(back, Chrome)!.Icon);
        // Kopya (Çoğalt, kayıtlı düzen, Geri al) yeni alanları da taşır.
        Assert.Equal("Oyun", ItemLooks.Get(box.Clone(), Chrome)!.Name);
    }

    /// <summary>2.0'ın WidgetConfig'i: yeni alanları tanımaz.</summary>
    private sealed class OldWidget
    {
        public string Id { get; set; } = "";
        public WidgetKind Kind { get; set; }
        public string? Title { get; set; }
        public List<LauncherTab> Tabs { get; set; } = [];
    }

    private sealed class OldSettings
    {
        public List<OldWidget> Widgets { get; set; } = [];
    }

    [Fact]
    public void Older_versions_read_new_settings_and_new_version_reads_old_ones()
    {
        var box = Box(Chrome);
        box.Icon = "sym:Games24";
        ItemLooks.SetName(box, Chrome, "Oyun", "Google Chrome");
        var json = JsonSerializer.Serialize(new AppSettings { Widgets = [box] }, JsonFile.Options);

        // 2.0 bilmediği alanları yok sayar (ayarlar .bozuk-* olmaz).
        var old = JsonSerializer.Deserialize<OldSettings>(json, JsonFile.Options)!;
        Assert.Equal(Chrome, old.Widgets[0].Tabs[0].Items[0]);

        // 2.0'ın yazdığı ayar: ad/simge yok.
        var fromOld = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(old, JsonFile.Options), JsonFile.Options)!;
        Assert.Null(fromOld.Widgets[0].Icon);
        Assert.Null(fromOld.Widgets[0].ItemLooks);
    }
}
