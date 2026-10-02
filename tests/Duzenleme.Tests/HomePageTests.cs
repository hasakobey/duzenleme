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
        var cards = HowToCards.Cards;
        Assert.Equal(["move", "resize", "edit", "ring"], cards.Select(c => c.Key));
        Assert.Equal(["Taşı", "Büyüt", "Düzenle", "Halka"], cards.Select(c => c.Title));
        Assert.Equal("Başlığından ya da boş yerinden sürükle.", cards[0].Text);
        Assert.Equal("Kenarından sürükle ya da ⋯ › Boyut.", cards[1].Text);
        Assert.Equal("⋯'ye bas ya da sağ tıkla.", cards[2].Text);
        Assert.All(cards, c =>
        {
            Assert.DoesNotContain(' ', c.Title);
            Assert.EndsWith(".", c.Text);
            Assert.True(c.Text.Length <= 45, $"\"{c.Text}\" kısa kalmalı");
            Assert.True((int)c.Icon < 0x10000, $"{c.Icon} WPF-UI SymbolIcon'da çizilemez");
        });

        using var _ = L.Use(Lang.En);
        Assert.Equal(["Move", "Resize", "Edit", "Ring"], HowToCards.Cards.Select(c => c.Title));
    }

    /// <summary>
    /// Halka kartı halkaya bugün ulaşılan yolu söyler. Boş masaüstüne çift tıklama halkayı açınca (<see cref="DoubleClickEffect"/>'e
    /// halka eklenince) kart "Boş masaüstüne çift tıkla." demeli: bu test o gün kartla birlikte güncellenir.
    /// </summary>
    [Fact]
    public void Ring_card_names_the_way_to_the_ring_that_works_today()
    {
        var ring = HowToCards.Cards.Single(c => c.Key == "ring");
        var doubleClickOpensRing = Enum.GetNames<DoubleClickEffect>().Contains("Ring");
        Assert.False(doubleClickOpensRing,
            "Çift tıklama halkayı açıyor: HowToCards.RingText'i \"Boş masaüstüne çift tıkla.\" yap ve bu testi güncelle.");
        Assert.Equal("Widget ekle › Halka menü.", ring.Text);
        // Kartın gösterdiği yol gerçekten var: "Widget ekle"de "Halka menü" kutucuğu.
        Assert.Contains(WidgetCatalog.Tools, c => c.Key == "Ring" && c.Label == "Halka menü");
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
