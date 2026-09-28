using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Yeni widget türleri (alt tür + tohum) ve ileri uyumluluk: tohumlar yalnızca 2.0'ın bildiği enum adlarıyla yazılır,
/// bilinmeyen alanlar ve enum değerleri ayarları silmez, kopyalama/düzen alt türü ve verisini korur.
/// </summary>
public class WidgetVariantsTests
{
    /// <summary>2.0.0'daki kalıcı enum'ların adları (src/Duzenleme/Core/AppSettings.cs @ 2.0.0). Bu listelere ekleme yapılmaz.</summary>
    private static readonly Dictionary<string, string[]> Enums20 = new()
    {
        ["Kind"] = ["Clock", "Date", "Fence", "Note", "Launcher"],
        ["Style"] = ["Dark", "Light", "Glass"],
        ["Accent"] = ["Violet", "Blue", "Green", "Orange", "Pink"],
        ["NoteColor"] = ["Yellow", "Pink", "Green", "Blue", "Purple", "Graphite"],
        ["IconSize"] = ["Small", "Medium", "Large", "ExtraLarge"],
        ["View"] = ["Icons", "List"],
        ["Sort"] = ["Newest", "Name", "Type"],
        ["Align"] = ["Left", "Center", "Right"],
        ["Spacing"] = ["Compact", "Normal", "Wide"],
        ["LabelSize"] = ["Small", "Normal", "Large"],
        ["Corners"] = ["Round", "Soft", "Square"],
        ["Filter"] = ["None", "All", "Folders", "Shortcuts", "Files"],
    };

    private static IEnumerable<WidgetConfig> AllSeeds() =>
        WidgetSeeds.Keys.Select(k => WidgetSeeds.Create(k)!)
            .Append(WidgetSeeds.Portal(@"C:\Users\x\Downloads", FolderPortal.Downloads, "İndirilenler"));

    [Fact]
    public void Every_seed_uses_only_enum_names_known_to_20()
    {
        foreach (var seed in AllSeeds())
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(seed, JsonFile.Options));
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!Enums20.TryGetValue(property.Name, out var names)) continue;
                Assert.True(property.Value.ValueKind == JsonValueKind.String && names.Contains(property.Value.GetString()),
                    $"{seed.Variant ?? seed.Kind.ToString()}: {property.Name} = {property.Value} 2.0'da yok");
            }
        }
    }

    [Fact]
    public void Every_catalog_key_has_a_seed_with_a_known_variant()
    {
        foreach (var key in WidgetSeeds.Keys)
        {
            var seed = WidgetSeeds.Create(key);
            Assert.NotNull(seed);
            Assert.True(seed!.Variant is null || WidgetVariants.Of(seed) == seed.Variant, key);
        }
        Assert.Null(WidgetSeeds.Create("Bilinmeyen"));
    }

    [Theory]
    [InlineData(WidgetKind.Date, WidgetVariants.Month, WidgetVariants.Month)]
    [InlineData(WidgetKind.Date, WidgetVariants.Countdown, WidgetVariants.Countdown)]
    [InlineData(WidgetKind.Clock, WidgetVariants.Timer, WidgetVariants.Timer)]
    [InlineData(WidgetKind.Clock, WidgetVariants.World, WidgetVariants.World)]
    [InlineData(WidgetKind.Clock, WidgetVariants.System, WidgetVariants.System)]
    [InlineData(WidgetKind.Launcher, WidgetVariants.Recycle, WidgetVariants.Recycle)]
    [InlineData(WidgetKind.Clock, WidgetVariants.Month, null)]      // temel türü uymayan (elle düzenlenmiş) alt tür yok sayılır
    [InlineData(WidgetKind.Date, "weather", null)]                  // daha yeni sürümün alt türü: temel tür gösterilir
    [InlineData(WidgetKind.Note, null, null)]
    public void Variant_is_recognised_only_on_its_base_kind(WidgetKind kind, string? variant, string? expected)
    {
        Assert.Equal(expected, WidgetVariants.Of(new WidgetConfig { Kind = kind, Variant = variant }));
    }

    [Fact]
    public void Unknown_future_variant_is_kept_on_save()
    {
        var config = new WidgetConfig { Kind = WidgetKind.Date, Variant = "weather" };
        var copy = config.Clone();
        Assert.Equal("weather", copy.Variant);
        Assert.Null(WidgetVariants.Of(copy));
    }

    [Fact]
    public void Portal_is_a_fence_with_a_full_path()
    {
        Assert.True(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = @"D:\Fotoğraflar" }));
        Assert.True(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = @"\\nas\paylaşım\Belgeler" }));
        Assert.False(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" }));
        Assert.False(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = @"\göreli" }));
        Assert.False(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.All, FolderName = @"C:\x" }));
        Assert.False(WidgetVariants.IsPortal(new WidgetConfig { Kind = WidgetKind.Launcher, FolderName = @"C:\x" }));
    }

    [Fact]
    public void Corner_preference()
    {
        Assert.Equal(WidgetCorner.TopRight, WidgetVariants.Corner(WidgetSeeds.Create(WidgetSeeds.Calendar)!));
        Assert.Equal(WidgetCorner.TopRight, WidgetVariants.Corner(WidgetSeeds.Create(WidgetSeeds.Timer)!));
        Assert.Equal(WidgetCorner.BottomRight, WidgetVariants.Corner(WidgetSeeds.Create(WidgetSeeds.RecycleBin)!));
        Assert.Equal(WidgetCorner.TopCenter, WidgetVariants.Corner(WidgetSeeds.Create(WidgetSeeds.Launcher)!));
        Assert.Equal(WidgetCorner.TopCenter, WidgetVariants.Corner(new WidgetConfig { Kind = WidgetKind.Fence }));
    }

    [Fact]
    public void Seeds_survive_clone_and_layout_snapshot()
    {
        var seeds = AllSeeds().ToList();
        seeds.Single(s => s.Variant == WidgetVariants.Timer && s.Timer!.Mode == TimerModes.Pomodoro).Timer!.EndsUtc = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        var restored = LayoutSnapshot.Capture("Yeni", seeds).Restore();
        Assert.Equal(seeds.Count, restored.Count);
        for (var i = 0; i < seeds.Count; i++)
            Assert.Equal(JsonSerializer.Serialize(seeds[i], JsonFile.Options), JsonSerializer.Serialize(restored[i], JsonFile.Options));
        var world = restored.Single(s => s.Variant == WidgetVariants.World);
        Assert.NotEmpty(world.Zones!);
        Assert.Equal(new[] { "weeknum" }, restored.Single(s => s.Variant == WidgetVariants.Month).HiddenParts);
        Assert.Equal(new[] { WidgetSeeds.RecycleBinItem }, restored.Single(s => s.Variant == WidgetVariants.Recycle).Tabs.Single().Items);
    }

    [Fact]
    public void Duplicate_resets_a_running_timer()
    {
        var config = WidgetSeeds.Create(WidgetSeeds.Timer)!;
        TimerLogic.Start(config.Timer!, DateTime.UtcNow);
        var copy = config.Clone();
        WidgetSeeds.PrepareDuplicate(copy);
        Assert.False(TimerLogic.IsRunning(copy.Timer!));
        Assert.True(TimerLogic.IsRunning(config.Timer!));
    }

    [Fact]
    public void Countdown_date_is_stored_without_time_zone()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(WidgetSeeds.Create(WidgetSeeds.Countdown), JsonFile.Options));
        var text = doc.RootElement.GetProperty("TargetDate").GetString()!;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T00:00:00$", text);
    }

    [Fact]
    public void Default_world_zones_do_not_repeat_the_local_zone()
    {
        Assert.Equal(new[] { "Turkey Standard Time", "GMT Standard Time", "Eastern Standard Time" },
            WidgetSeeds.DefaultZones("Turkey Standard Time").Select(z => z.Id));
        Assert.Equal(new[] { "GMT Standard Time", "Eastern Standard Time" }, WidgetSeeds.DefaultZones("GMT Standard Time").Select(z => z.Id));
    }
}
