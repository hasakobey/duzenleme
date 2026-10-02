using System.Text;
using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// "Kutuya koyduğum simgeler masaüstünden kalksın" tek anahtar (bölme kipinden bağımsız, B5/B21): yeni ayarlar ileri uyumlu
/// (boşken yazılmaz, 2.1 bilmediği alanları korur), bölme kipi anahtara dokunmaz, simge seçimi iki seçenektir, öğe menüleri
/// tek modelden gelir, kaldırma programı kutu öğelerini geri koyar (sessiz kaldırmada --quiet, gizli pencere yok).
/// </summary>
public class BoxSwitchTests
{
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([RepoRoot(), "src", "Duzenleme", .. parts]));

    // ---------------------------------------------------------------- ileri uyum

    [Fact]
    public void New_box_settings_not_written_when_default()
    {
        var json = JsonSerializer.Serialize(new AppSettings(), JsonFile.Options);
        Assert.DoesNotContain("BoxLeaveAsked", json);
        Assert.DoesNotContain("BoxPublicAsked", json);
        Assert.DoesNotContain("BoxAppShortcuts", json);
    }

    [Fact]
    public void New_box_settings_round_trip_as_plain_values()
    {
        var json = JsonSerializer.Serialize(new AppSettings { BoxLeaveAsked = true, BoxPublicAsked = true, BoxAppShortcuts = "leave" }, JsonFile.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.True, doc.RootElement.GetProperty("BoxLeaveAsked").ValueKind);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("BoxAppShortcuts").ValueKind);
        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonFile.Options)!;
        Assert.True(back.BoxLeaveAsked);
        Assert.True(back.BoxPublicAsked);
        Assert.Equal(BoxAppShortcutChoices.Leave, BoxAppShortcutChoices.Normalize(back.BoxAppShortcuts));
    }

    [Fact]
    public void Unknown_app_shortcut_choice_means_ask()
    {
        Assert.Null(BoxAppShortcutChoices.Normalize(null));
        Assert.Null(BoxAppShortcutChoices.Normalize("sometimes"));
        Assert.Equal(BoxAppShortcutChoices.Keep, BoxAppShortcutChoices.Normalize(" KEEP "));
    }

    [Fact]
    public void Both_flags_true_reads_back_as_set()
    {
        // 2.2 bölme kipinde anahtarı kapatmaz: ikisi birlikte açık kalır (2.1 bunu bölme kipi görür ve hiçbir şey taşımaz).
        var back = JsonSerializer.Deserialize<AppSettings>("""{ "BoxItemsLeaveDesktop": true, "FencesReplaceIcons": true }""", JsonFile.Options)!;
        Assert.True(back.BoxItemsLeaveDesktop);
        Assert.True(back.FencesReplaceIcons);
        Assert.True(BoxChoices.Asked(back));
    }

    // ---------------------------------------------------------------- yapı

    [Fact]
    public void Fences_mode_and_its_undo_keep_the_box_switch()
    {
        // B21: bölmeleri açmak ya da "Geri al" ile kapatmak anahtarı yazmaz; yalnızca SetBoxLeaves (BoxMover.SetOn) yazar.
        foreach (var file in new[] { Source("Views", "DesktopFences.cs"), Source("AppHost.cs"), Source("Views", "WelcomeSetup.cs") })
            Assert.DoesNotContain("BoxItemsLeaveDesktop =", file);
        var modes = Source("Views", "DesktopModes.cs");
        Assert.DoesNotContain("BoxItemsLeaveDesktop =", modes);
        Assert.DoesNotContain("BoxItemsLeave,", modes);
        Assert.DoesNotContain("Klasörde kalsın", modes);
        Assert.Equal(2, Enum.GetValues<Duzenleme.Views.IconMode>().Length);
    }

    [Fact]
    public void Item_menus_of_box_and_ring_come_from_one_model()
    {
        foreach (var file in new[] { Source("Widgets", "LauncherView.xaml.cs"), Source("Widgets", "RingWidgetView.cs") })
        {
            Assert.Contains("BoxChoices.MenuRows(", file);
            Assert.DoesNotContain("\"Masaüstüne geri koy\"", file);
            Assert.DoesNotContain("Kutudan çıkar (masaüstüne döner)", file);
            Assert.Contains("BoxMover.DraggedOut(", file);
        }
        // Yeni eklenenler tek kapıdan: ilk soru, bölme kipi ve uygulama kısayolu orada.
        Assert.Contains("BoxMover.Added(", Source("Widgets", "LauncherEdits.cs"));
        Assert.DoesNotContain("BoxMover.Claim(", Source("Widgets", "LauncherEdits.cs"));
    }

    [Fact]
    public void Uninstaller_returns_box_items_with_a_visible_prompt()
    {
        var path = Path.Combine(RepoRoot(), "tools", "installer", "NestDesk.iss");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "NestDesk.iss BOM'lu UTF-8 olmalı");
        var iss = Encoding.UTF8.GetString(bytes);
        var line = iss.Split('\n').Single(l => l.Contains("--restore-desktop", StringComparison.Ordinal) && l.StartsWith("Filename:", StringComparison.Ordinal));
        Assert.Contains("{code:QuietArg}", line);
        Assert.DoesNotContain("runhidden", line);
        Assert.Contains("function QuietArg(", iss);
        Assert.Contains("english.BoxFilesKept=", iss);
        Assert.Contains("turkish.BoxFilesKept=", iss);
        Assert.Contains("args.Quiet", Source("App.xaml.cs"));
    }
}
