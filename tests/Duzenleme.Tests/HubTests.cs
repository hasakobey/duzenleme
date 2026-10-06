using System.Text.Json;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// NestDesk halkası (<see cref="AppSettings.Hub"/>): masaüstünde halka widget'ı yokken açılır halkanın hedefi, ilk gerekince
/// halka tohumundan kurulur, ayarda boşken yazılmaz ve kutu tesisatı (<see cref="Launchers"/>) onu da görür: yeniden adlandırma,
/// kurallardan korunan masaüstü dosyaları, kutu kayıtları. Açılışa göre çalışan öğeler (<see cref="RingAvailability"/>).
/// </summary>
public class HubTests
{
    private static WidgetConfig Ring(string id, params string[] items) => new()
    {
        Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Id = id, Tabs = [new LauncherTab { Items = [.. items] }],
    };

    [Fact]
    public void Pick_prefers_the_chosen_ring_then_the_first_ring_widget_then_the_hub()
    {
        var hub = Ring("merkez0000000000000000000000000000");
        var first = Ring("birinci");
        var second = Ring("ikinci");
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, Id = "kutu" };

        // Halka widget'ı yok: NestDesk halkası; henüz kurulmadıysa null (Target kurar).
        Assert.Null(RingTargets.Pick(new AppSettings { Widgets = [box] }));
        Assert.Same(hub, RingTargets.Pick(new AppSettings { Widgets = [box], Hub = hub }));
        Assert.Same(hub, RingTargets.Pick(new AppSettings { Widgets = [box], Hub = hub, RingWidgetId = "silinmiş" }));

        // Seçilmemiş ya da bulunamayan kimlik: ilk halka widget'ı (bugünkü davranış).
        var settings = new AppSettings { Widgets = [box, first, second], Hub = hub };
        Assert.Same(first, RingTargets.Pick(settings));
        settings.RingWidgetId = "silinmiş";
        Assert.Same(first, RingTargets.Pick(settings));
        // Var olan halka kimliği kazanır.
        settings.RingWidgetId = second.Id;
        Assert.Same(second, RingTargets.Pick(settings));
        // "hub" takma adı ve NestDesk halkasının kendi kimliği, halka widget'ları varken de NestDesk halkasını açar.
        settings.RingWidgetId = RingTargets.HubId;
        Assert.Same(hub, RingTargets.Pick(settings));
        settings.RingWidgetId = hub.Id;
        Assert.Same(hub, RingTargets.Pick(settings));
        Assert.True(RingTargets.Opens(settings, hub));
        Assert.False(RingTargets.Opens(settings, first));

        // "hub" seçili ama NestDesk halkası henüz yok: Target kurar.
        var fresh = new AppSettings { Widgets = [first], RingWidgetId = RingTargets.HubId };
        Assert.Null(RingTargets.Pick(fresh));
        var made = RingTargets.Target(fresh);
        Assert.Same(fresh.Hub, made);
        Assert.DoesNotContain(made, fresh.Widgets);
    }

    [Fact]
    public void Hub_is_seeded_once_from_the_ring_seed_with_its_own_name()
    {
        var settings = new AppSettings();
        var hub = RingTargets.EnsureHub(settings);
        Assert.Same(hub, settings.Hub);
        Assert.Same(hub, RingTargets.EnsureHub(settings));
        Assert.True(WidgetVariants.Is(hub, WidgetVariants.Ring));
        Assert.Equal(WidgetKind.Launcher, hub.Kind);
        Assert.Equal("NestDesk halkası", hub.Title);
        Assert.Equal(32, hub.Id.Length);
        // Tohumun NestDesk eylemleri; yeni RingAppIds yok.
        Assert.Equal(RingAppIds.Seed, hub.Tabs[0].Actions!.Select(a => a.App));
        Assert.Empty(settings.Widgets);
        using (L.Use(Lang.En))
            Assert.Equal("NestDesk ring", RingTargets.EnsureHub(new AppSettings()).Title);
        // Halka olmayan bir şey yazılmışsa (daha yeni sürüm) ezilmez: bu oturumda yerine ayara yazılmayan bir halka kullanılır.
        var broken = new AppSettings { Hub = new WidgetConfig { Kind = WidgetKind.Note } };
        Assert.True(WidgetVariants.Is(RingTargets.EnsureHub(broken), WidgetVariants.Ring));
        Assert.Equal(WidgetKind.Note, broken.Hub!.Kind);
    }

    [Fact]
    public void Hub_is_not_written_while_null_and_round_trips_when_set()
    {
        var json = JsonSerializer.Serialize(new AppSettings(), JsonFile.Options);
        Assert.DoesNotContain("\"Hub\"", json);

        var settings = new AppSettings();
        RingTargets.EnsureHub(settings).Tabs[0].Items.Add(@"C:\Masaüstü\a.lnk");
        var again = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonFile.Options), JsonFile.Options)!;
        Assert.NotNull(again.Hub);
        Assert.Equal(settings.Hub!.Id, again.Hub!.Id);
        Assert.Equal([@"C:\Masaüstü\a.lnk"], again.Hub.Tabs[0].Items);
        Assert.Equal(RingAppIds.Seed.Count, again.Hub.Tabs[0].Actions!.Count);
        // Widget listesine sızmaz.
        Assert.Empty(again.Widgets);
    }

    [Fact]
    public void Launchers_include_the_hub_for_boxes_pins_and_renames()
    {
        var desktop = @"C:\Users\x\Desktop";
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, Id = "kutu", Tabs = [new LauncherTab { Items = [desktop + @"\kutu.txt"] }] };
        var hub = Ring("merkez0000000000000000000000000000", desktop + @"\eski.pdf");
        var settings = new AppSettings { Widgets = [new WidgetConfig { Kind = WidgetKind.Clock }, box], Hub = hub };

        Assert.Equal([box, hub], Launchers.All(settings));
        Assert.Equal([hub], Launchers.Rings(settings));
        Assert.Same(hub, Launchers.Find(settings, hub.Id));
        Assert.Contains(desktop + @"\eski.pdf", Launchers.Referenced(settings));
        // Kurallar NestDesk halkasındaki masaüstü dosyasını da taşımaz.
        Assert.Contains(desktop + @"\eski.pdf", Launchers.PinnedDesktopPaths(settings, [desktop]));

        // Uygulamanın yeniden adlandırması NestDesk halkasının yolunu da izler.
        Assert.True(PathRenames.Apply(Launchers.Configs(settings), desktop + @"\eski.pdf", desktop + @"\yeni.pdf", false, desktop));
        Assert.Equal([desktop + @"\yeni.pdf"], hub.Tabs[0].Items);
        // Kutuya taşınma (ReplacePaths) da.
        Assert.True(Launchers.ReplacePaths(settings, [(desktop + @"\yeni.pdf", @"C:\N\Kutu\yeni.pdf", false)]));
        Assert.Equal([@"C:\N\Kutu\yeni.pdf"], hub.Tabs[0].Items);

        // NestDesk halkası yokken liste yalnızca widget'lar.
        Assert.Equal([box], Launchers.All(new AppSettings { Widgets = [box] }));
    }

    [Fact]
    public void Keys_and_text_run_only_from_the_shortcut_ring()
    {
        foreach (var origin in Enum.GetValues<RingOrigin>())
        {
            var hotkey = origin == RingOrigin.Hotkey;
            Assert.Equal(hotkey, RingAvailability.Available(RingActionTypes.Keys, origin));
            Assert.Equal(hotkey, RingAvailability.Available(RingActionTypes.Text, origin));
            foreach (var type in new[] { RingActionTypes.Url, RingActionTypes.Command, RingActionTypes.App, null })
                Assert.True(RingAvailability.Available(type, origin));
        }
    }

    /// <summary>Çalışma zamanı türleri (açılış, çift tıklamanın etkisi, tepsi komutu) hiçbir ayar sınıfında yazılmaz.</summary>
    [Fact]
    public void Runtime_enums_are_never_persisted()
    {
        var runtime = new[] { typeof(RingOrigin), typeof(DoubleClickEffect), typeof(TrayCommand) };
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([typeof(AppSettings)]);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type)) continue;
            foreach (var property in type.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.DoesNotContain(t, runtime);
                foreach (var arg in t.IsGenericType ? t.GetGenericArguments() : [t])
                    if (arg.Namespace == typeof(AppSettings).Namespace && arg.IsClass) queue.Enqueue(arg);
            }
        }
        Assert.Contains(typeof(WidgetConfig), seen);
    }
}
