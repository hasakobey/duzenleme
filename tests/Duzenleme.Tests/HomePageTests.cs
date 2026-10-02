using System.Xml.Linq;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Views;

namespace Duzenleme.Tests;

/// <summary>
/// Sade Ana sayfa (Kolay NestDesk 3a): iki büyük düğme, ilk ziyaretteki "Nasıl kullanılır" kartları (<see cref="HowToCards"/>),
/// tek satırlık otomatik taşıma kartı (<see cref="AutoMoveStatus.Line"/>) ve son taşınanlar.
/// </summary>
public class HomePageTests
{
    private static MoveEntry Moved(string file, string folder) =>
        new() { Source = Path.Combine(@"C:\Users\x\Desktop", file), Destination = Path.Combine(@"C:\Users\x\Desktop", folder, file) };

    [Fact]
    public void Auto_move_line_says_the_state_the_last_move_and_missing_folders()
    {
        var last = Moved("rapor.pdf", "PDF");
        Assert.Equal(("Otomatik taşıma kapalı", false), AutoMoveStatus.Line(on: false, ready: 3, last));
        Assert.Equal(("Otomatik taşıma kapalı", false), AutoMoveStatus.Line(on: false, ready: 0, null));
        Assert.Equal(("Otomatik taşıma açık · son: rapor.pdf → PDF", false), AutoMoveStatus.Line(on: true, ready: 2, last));
        Assert.Equal(("Otomatik taşıma açık", false), AutoMoveStatus.Line(on: true, ready: 2, null));
        // Açık ama masaüstünde hedef klasör yok: dosyalar gitmez; son taşıma yerine bu söylenir ve "Klasörleri ayarla" görünür.
        Assert.Equal(("Otomatik taşıma açık · masaüstünde hedef klasör yok", true), AutoMoveStatus.Line(on: true, ready: 0, last));

        using var _ = L.Use(Lang.En);
        Assert.Equal("Auto-move is on · last: rapor.pdf → PDF", AutoMoveStatus.Line(on: true, ready: 1, last).Text);
        Assert.Equal("Auto-move is off", AutoMoveStatus.Line(on: false, ready: 1, last).Text);
    }

    /// <summary>Dört durağan kart, sırasıyla; her biri tek sözcüklük başlık ve tek kısa cümle; simgeler çizilebilir (BMP).</summary>
    [Fact]
    public void How_to_cards_are_four_short_static_cards()
    {
        var cards = HowToCards.CardsFor(DoubleClickEffect.ToggleDesktop);
        Assert.Equal(["move", "resize", "edit", "ring"], cards.Select(c => c.Key));
        Assert.Equal(["Taşı", "Büyüt", "Düzenle", "Halka"], cards.Select(c => c.Title));
        Assert.Equal("Başlığından ya da boş yerinden sürükle.", cards[0].Text);
        Assert.Equal("Kenarından sürükle.", cards[1].Text);
        Assert.Equal("Üzerine sağ tıkla.", cards[2].Text);
        Assert.All(cards, c =>
        {
            Assert.DoesNotContain(' ', c.Title);
            Assert.EndsWith(".", c.Text);
            Assert.True(c.Text.Length <= 45, $"\"{c.Text}\" kısa kalmalı");
            Assert.True((int)c.Icon < 0x10000, $"{c.Icon} WPF-UI SymbolIcon'da çizilemez");
        });

        using var _ = L.Use(Lang.En);
        Assert.Equal(["Move", "Resize", "Edit", "Ring"], HowToCards.CardsFor(DoubleClickEffect.Peek).Select(c => c.Title));
    }

    /// <summary>
    /// Kartlar yalnızca bugün çalışan yolu söyler: widget'larda ⋯ düğmesi yokken "Büyüt" ve "Düzenle" onu anmaz. ⋯ düğmesi
    /// (MoreHorizontal simgesi ya da "⋯") widget'lara gelince bu test düşer: kartlara ⋯ yolunu ekle ve testi güncelle.
    /// </summary>
    [Fact]
    public void How_to_cards_do_not_name_a_more_button_widgets_do_not_have()
    {
        var widgets = Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets");
        var withMore = Directory.EnumerateFiles(widgets, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f) is var text && (text.Contains("MoreHorizontal", StringComparison.Ordinal) || text.Contains('⋯')))
            .Select(Path.GetFileName).ToList();
        Assert.True(withMore.Count == 0,
            $"Widget'larda ⋯ düğmesi var ({string.Join(", ", withMore)}): HowToCards'ta \"Büyüt\" ve \"Düzenle\" kartlarına ⋯ yolunu ekle ve bu testi güncelle.");
        Assert.All(HowToCards.CardsFor(DoubleClickEffect.ToggleDesktop), c => Assert.DoesNotContain("⋯", c.Text));
    }

    /// <summary>
    /// Halka kartı halkaya bu kullanıcının bugün ulaştığı yolu söyler: çift tıklama ayarı halkayı açıyorsa (yalnızca o seçenekte;
    /// <see cref="DoubleClickEffect"/>'e halka gelince) "Boş masaüstüne çift tıkla.", değilse "Widget ekle" yolu. Kart
    /// kullanıcının gerçek ayarına bakar (eski kullanıcı kendi çift tıklama seçimini korur); her seçenek denetlenir.
    /// </summary>
    [Fact]
    public void Ring_card_names_the_way_to_the_ring_that_works_for_this_user()
    {
        foreach (var effect in Enum.GetValues<DoubleClickEffect>())
        {
            var expected = effect.ToString() == "Ring" ? "Boş masaüstüne çift tıkla." : "Widget ekle › Hepsini göster › Halka menü.";
            Assert.Equal(expected, HowToCards.RingText(effect));
            Assert.Equal(expected, HowToCards.CardsFor(effect).Single(c => c.Key == "ring").Text);
        }
        // Kartın gösterdiği yol gerçekten var: "Widget ekle"de "Hepsini göster"in altında "Halka menü" kutucuğu. Kutucuk ilk
        // sayfaya taşınırsa kart "Widget ekle › Halka menü." olmalı.
        Assert.Contains(WidgetCatalog.Tools, c => c.Key == "Ring" && c.Label == "Halka menü");
        Assert.Contains("Ring", WidgetCatalog.MoreKeys);
        Assert.DoesNotContain("Ring", WidgetCatalog.FeaturedKeys);
    }

    [Fact]
    public void How_to_strip_shows_until_dismissed()
    {
        Assert.True(HowToCards.ShouldShow(new AppSettings()));
        Assert.False(HowToCards.ShouldShow(new AppSettings { HowToDismissed = true }));
    }

    /// <summary>
    /// Sayfanın düğmeleri ve kimlikleri: iki büyük düğme, otomatik taşıma satırı (anahtar, Geri al, Dosyaları şimdi taşı) ve
    /// "Anladım". Sayfada eski kalabalık (degrade kart, üçüncü eylem kutucuğu, açıklama satırları) yok; şerit yalnızca
    /// Views.Motion ile solar, fareyle hiçbir şey belirmez.
    /// </summary>
    [Fact]
    public void Home_page_keeps_the_plan_ids_and_stays_calm()
    {
        var xaml = Read("Views", "HomePage.xaml");
        var code = Read("Views", "HomePage.xaml.cs");
        var howTo = Read("Views", "HowToCards.cs");
        var ids = XDocument.Parse(xaml).Descendants()
            .Select(e => (string?)e.Attribute("AutomationProperties.AutomationId")).Where(id => id is not null).ToList();
        foreach (var id in new[] { "Home.AddWidget", "Home.EditRing", "Home.AutoMoveToggle", "Home.UndoLast", "Home.OrganizeNow",
                                   "Home.StatusText", "Home.Recent", "Home.SeeAllHistory" })
            Assert.Contains(id, ids);
        Assert.Contains("\"Home.HowTo.Dismiss\"", howTo);

        // Eski ana sayfanın parçaları kalktı.
        foreach (var gone in new[] { "LinearGradientBrush", "UndoTile", "OrganizeTile", "Tagline", "Describe(" })
        {
            Assert.DoesNotContain(gone, xaml);
            Assert.DoesNotContain(gone, code);
        }
        // Hareket yalnızca Motion'dan; fareyle beliren öğe yok.
        Assert.Contains("Motion.Disappear(HowToHost", code);
        foreach (var source in new[] { xaml, code, howTo })
        {
            Assert.DoesNotContain("IsMouseOver", source);
            Assert.DoesNotContain("MouseEnter", source);
            Assert.DoesNotContain("Storyboard", source);
        }
        // Ekleme her zaman tek "Widget ekle" penceresinden; halka düğmesi tek giriş noktasından.
        Assert.Contains("ShowQuickAdd()", code);
        Assert.Contains("HomeRing.Edit(", code);

        // Sayfa içeriği kadar değil pencere kadar geniş (dar düzen geri beslenip takılmasın): sütunun en çok genişliği
        // PageRoot'un MaxWidth'i + yan boşlukları (fazlası geniş pencerede sayfayı ortalardı).
        var app = XDocument.Parse(Read("App.xaml"));
        var pageRoot = app.Descendants().Single(e => e.Name.LocalName == "Style"
            && (string?)e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "PageRoot");
        var setters = pageRoot.Elements().ToDictionary(e => (string)e.Attribute("Property")!, e => (string)e.Attribute("Value")!);
        var margin = setters["Margin"].Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var max = double.Parse(setters["MaxWidth"], System.Globalization.CultureInfo.InvariantCulture) + margin[0] + margin[2];
        Assert.Contains($"<ColumnDefinition Width=\"*\" MaxWidth=\"{max.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" />", xaml);
        Assert.Contains("<StackPanel Style=\"{StaticResource PageRoot}\" HorizontalAlignment=\"Stretch\">", xaml);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), "src", "Duzenleme", .. parts]));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
