using System.IO;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Views;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Ekleme kataloğu: her araç kutucuğunun tohumu, tanıması ve simgesi tutarlı; karşılama yalnızca ilk beşi gösterir; "Widget ekle"nin
/// ilk sayfası sekiz kutucuk (görünen açıklamalı), "Hepsini göster" öbür altı.
/// </summary>
public class WidgetCatalogTests
{
    [Fact]
    public void Welcome_shows_the_six_basic_tools_in_order()
    {
        Assert.Equal(new[] { "Clock", "Date", "Note", "Checklist", "Calendar", "Launcher" }, WidgetCatalog.WelcomeTools.Select(c => c.Key));
    }

    [Fact]
    public void Every_tool_has_a_seed_that_only_it_recognises_with_the_same_icon()
    {
        var tools = WidgetCatalog.Tools;
        Assert.Equal(tools.Count, tools.Select(t => t.Key).Distinct().Count());
        foreach (var choice in tools)
        {
            var seed = WidgetSeeds.Create(choice.Key);
            Assert.NotNull(seed);
            Assert.Single(tools, c => c.Matches!(seed!));
            Assert.True(choice.Matches!(seed!), choice.Key);
            Assert.Equal(choice.Icon, WidgetIcons.DefaultFor(seed!));
            Assert.Equal(choice.Icon, WidgetIcons.For(seed!));
            Assert.NotEqual(WidgetGroup.Fence, choice.Group);
        }
    }

    [Fact]
    public void Ring_menu_is_a_tool_of_its_own()
    {
        var ring = Assert.Single(WidgetCatalog.Tools, c => c.Key == WidgetSeeds.Ring);
        Assert.Equal("Halka menü", ring.Label);
        Assert.Equal(Wpf.Ui.Controls.SymbolRegular.DataPie24, ring.Icon);
        Assert.True(WidgetIcons.IsDrawable(ring.Icon));
        Assert.False(ring.ShowInWelcome);
        Assert.False(ring.AsksName);
        Assert.Equal(WidgetGroup.Tool, ring.Group);
        // Kısayol kutusu kutucuğu halkayı (aynı temel tür) kendisi saymaz; halka da kutuyu.
        var box = WidgetSeeds.Create(WidgetSeeds.Launcher)!;
        Assert.False(ring.Matches!(box));
        Assert.False(WidgetCatalog.Tools.Single(c => c.Key == WidgetSeeds.Launcher).Matches!(WidgetSeeds.Create(WidgetSeeds.Ring)!));
    }

    // ---------------------------------------------------------------- Kolay NestDesk: tek "Widget ekle" penceresi

    [Fact]
    public void Featured_is_eight_tiles_in_order_with_visible_subtitles()
    {
        var featured = WidgetCatalog.Featured;
        Assert.Equal(["Clock", "Date", "Note", "Checklist", "Calendar", "Launcher", "Timer", WidgetCatalog.FenceKey],
            featured.Select(c => c.Key));
        Assert.Equal(["Saat", "Tarih", "Not", "Yapılacaklar", "Takvim", "Kısayol kutusu", "Zamanlayıcı", "Bölme ›"],
            featured.Select(c => c.Label));
        Assert.Equal(
        [
            "Büyük dijital saat", "Gün ve haftalık şerit", "Yazdıkça kaydedilir", "Onay kutulu liste", "Aylık takvim",
            "Uygulamalar tek tıkta", "Pomodoro ve kronometre de", "Dosyalarını topla",
        ], featured.Select(c => c.Subtitle));
        // Kutucuk simgeleri WPF-UI'ın çizebildiği (BMP içindeki) simgeler.
        Assert.All(featured.Concat(WidgetCatalog.More), c => Assert.True(WidgetIcons.IsDrawable(c.Icon), c.Key));
        // "Bölme ›" eklemez, bölme sayfasını açar (pencere anahtarına bakar).
        var fence = featured[^1];
        Assert.Equal(WidgetGroup.Fence, fence.Group);
        Assert.Null(fence.Add());
        Assert.Null(fence.Matches);
    }

    [Fact]
    public void More_is_the_other_six_tools_and_together_they_cover_every_tool()
    {
        var more = WidgetCatalog.More;
        Assert.Equal(["WorldClock", "Countdown", "Pomodoro", "SystemStatus", "RecycleBin", "Ring"], more.Select(c => c.Key));
        Assert.Equal(6, WidgetCatalog.MoreKeys.Count);
        var shown = WidgetCatalog.Featured.Where(c => c.Key != WidgetCatalog.FenceKey).Concat(more).Select(c => c.Key).ToList();
        Assert.Equal(shown.Count, shown.Distinct().Count());
        Assert.Equal(WidgetCatalog.Tools.Select(c => c.Key).Order(), shown.Order());
    }

    [Fact]
    public void Every_tile_subtitle_is_short_in_both_languages()
    {
        foreach (var lang in new[] { Lang.Tr, Lang.En })
        {
            using var _ = L.Use(lang);
            foreach (var choice in WidgetCatalog.Featured.Concat(WidgetCatalog.More))
            {
                Assert.False(string.IsNullOrWhiteSpace(choice.Subtitle), choice.Key);
                // Kutucukta iki satıra sığsın: en çok dört sözcük; açıklama (ipucu değil) ekran okuyucuda Tip olarak kalır.
                Assert.True(choice.Subtitle!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4, $"{lang} {choice.Key}: {choice.Subtitle}");
                Assert.False(string.IsNullOrWhiteSpace(choice.Tip), choice.Key);
                Assert.NotEqual(choice.Label, choice.Subtitle);
            }
        }
    }

    /// <summary>
    /// Yeni widget adlandırmaya açılmaz ve her ekleme yolu aynı pencereyi açar: ana pencere kendi kutucuklarını kurmaz, köşeye
    /// zorlamaz; Widget'lar sayfasında simge kipi ve yer seçimi yok (Ayarlar'da).
    /// </summary>
    [Fact]
    public void Adding_never_renames_and_every_surface_opens_the_one_add_window()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([src, .. parts]));

        foreach (var file in new[] { Read("Views", "WidgetCatalog.cs"), Read("Widgets", "WidgetManager.cs"), Read("Views", "QuickAddWindow.cs") })
        {
            Assert.DoesNotContain("BeginRename", file);
            Assert.DoesNotContain("renameAfterAdd", file);
            Assert.DoesNotContain("BeginNaming", file);
        }
        Assert.DoesNotContain("BeginNaming", Read("Widgets", "NoteView.xaml.cs"));
        Assert.DoesNotContain("CornerNear", Read("Views", "QuickAddWindow.cs"));
        Assert.DoesNotContain("Badge", Read("Views", "WidgetCatalog.cs"));

        var page = Read("Views", "WidgetsPage.xaml") + Read("Views", "WidgetsPage.xaml.cs");
        foreach (var gone in new[] { "IconModeBox", "PlacementBox", "WidgetCatalog", "FenceTiles", "CornerNear" })
            Assert.DoesNotContain(gone, page);
        Assert.Contains("ShowQuickAdd()", Read("Views", "WidgetsPage.xaml.cs"));
        Assert.Contains("ShowQuickAdd()", Read("Views", "HomePage.xaml.cs"));
    }

    /// <summary>
    /// "{0} eklendi."deki "Geri al" widget'ı geri getirilebilir biçimde kaldırır: şerit uzun süre açık kalabilir, o arada
    /// doldurulan not ya da kutu geri dönüşsüz gitmesin. Yeni widget açık ana pencerenin altına düşmez (yer bulunurken dolu
    /// sayılır).
    /// </summary>
    [Fact]
    public void Undoing_an_add_keeps_the_widget_restorable_and_new_widgets_avoid_the_main_window()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var catalog = File.ReadAllText(Path.Combine(src, "Views", "WidgetCatalog.cs"));
        Assert.Contains("RemoveWithUndo(id, notify: false)", catalog);
        Assert.Contains("UndoRemove(id)", catalog);
        Assert.DoesNotContain("Widgets.Remove(", catalog);
        var manager = File.ReadAllText(Path.Combine(src, "Widgets", "WidgetManager.cs"));
        Assert.Contains("if (MainWindowBox() is { } main) taken.Add(main);", manager);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
