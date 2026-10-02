using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>"Kural ekle"nin kalıpları ve uzantı kuralları (<see cref="RuleTemplates"/>).</summary>
public class RuleTemplatesTests
{
    private static Rule R(string folder, params string[] extensions) => new() { TargetFolder = folder, Extensions = [.. extensions] };

    [Fact]
    public void Templates_are_the_six_defaults_then_design_and_ebooks()
    {
        Assert.Equal(["pdf", "pictures", "documents", "videos", "music", "archives", "design", "ebooks"], RuleTemplates.All.Select(t => t.Key));
        Assert.Equal(["PDF", "Resimler", "Belgeler", "Videolar", "Müzik", "Arşivler", "Tasarım", "E-kitaplar"],
            RuleTemplates.All.Select(t => t.Name));
        Assert.Equal(["psd", "ai", "fig", "xd"], RuleTemplates.All.Single(t => t.Key == "design").Extensions);
        Assert.Equal(["epub", "mobi"], RuleTemplates.All.Single(t => t.Key == "ebooks").Extensions);

        // Hazır kuralların kalıpları uzantılarını ve iki dildeki adlarını hazır kurallardan alır: ikisi ayrışmasın.
        foreach (var category in Rule.Categories)
        {
            var template = Assert.Single(RuleTemplates.All, t => t.NameKey == category.Tr);
            Assert.Equal(category.Extensions, template.Extensions);
            Assert.Equal(category.Tr, template.NameIn(Lang.Tr));
            Assert.Equal(category.En, template.NameIn(Lang.En));
        }

        // Kalıplar birbiriyle uzantı paylaşmaz.
        var all = RuleTemplates.All.SelectMany(t => t.Extensions).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void Folder_names_come_from_the_translation_table_in_both_languages()
    {
        // L.DynVariants: her kalıbın İngilizce adı çeviri tablosundan (PDF iki dilde aynı).
        Assert.Equal(["Tasarım", "Design"], RuleTemplates.All.Single(t => t.Key == "design").Names);
        Assert.Equal(["E-kitaplar", "E-books"], RuleTemplates.All.Single(t => t.Key == "ebooks").Names);
        Assert.Equal(["PDF"], RuleTemplates.All.Single(t => t.Key == "pdf").Names);
        Assert.All(RuleTemplates.All.Where(t => t.Key != "pdf"), t => Assert.Equal(2, t.Names.Count));

        using var _ = L.Use(Lang.En);
        Assert.Equal(["PDF", "Pictures", "Documents", "Videos", "Music", "Archives", "Design", "E-books"], RuleTemplates.All.Select(t => t.Name));
    }

    [Fact]
    public void With_default_rules_only_the_extra_templates_are_offered()
    {
        var offers = RuleTemplates.Offers(Rule.Defaults(Lang.Tr));
        Assert.Equal(["design", "ebooks"], offers.Select(o => o.Template.Key));
        Assert.Equal(["psd", "ai", "fig", "xd"], offers[0].Extensions);

        // İngilizce adlı hazır kurallar da tanınır (dil sonradan değişse de).
        Assert.Equal(["design", "ebooks"], RuleTemplates.Offers(Rule.Defaults(Lang.En)).Select(o => o.Template.Key));
    }

    [Fact]
    public void Without_rules_every_template_is_offered_in_order()
    {
        Assert.Equal(RuleTemplates.All.Select(t => t.Key), RuleTemplates.Offers([]).Select(o => o.Template.Key));
        Assert.All(RuleTemplates.Offers([]), o => Assert.Equal(o.Template.Extensions, o.Extensions));
    }

    [Fact]
    public void Missing_default_comes_back_and_any_language_name_counts_as_present()
    {
        var rules = Rule.Defaults(Lang.Tr).Where(r => r.TargetFolder != "Resimler").ToList();
        Assert.Equal(["pictures", "design", "ebooks"], RuleTemplates.Offers(rules).Select(o => o.Template.Key));

        // Aynı kural başka dildeki ya da başka yazılışlı adla ("design", "e-KİTAPLAR") varsa kalıp önerilmez; kapalı kural da sayılır.
        var named = new List<Rule> { R("design", "dwg"), R("e-KİTAPLAR", "azw"), R("Pictures", "raw") };
        named[2].Enabled = false;
        var keys = RuleTemplates.Offers(named).Select(o => o.Template.Key).ToList();
        Assert.DoesNotContain("design", keys);
        Assert.DoesNotContain("ebooks", keys);
        Assert.DoesNotContain("pictures", keys);
        Assert.Contains("pdf", keys);
    }

    [Fact]
    public void Offers_never_repeat_an_extension_that_a_rule_already_has()
    {
        // Kullanıcının kendi kuralı .psd ve .epub/.mobi'yi almış: Tasarım kalanlarla önerilir, E-kitaplar hiç önerilmez.
        var rules = new List<Rule> { R("Photoshop", "PSD"), R("Okuma", ".epub", "*.MOBI") };
        var offers = RuleTemplates.Offers(rules);
        Assert.Equal(["ai", "fig", "xd"], offers.Single(o => o.Template.Key == "design").Extensions);
        Assert.DoesNotContain(offers, o => o.Template.Key == "ebooks");
    }

    [Fact]
    public void Random_rule_sets_never_get_duplicate_extensions_from_offers()
    {
        var pool = RuleTemplates.All.SelectMany(t => t.Extensions).Concat(["dwg", "raw", "iso"]).ToArray();
        var names = new[] { "PDF", "Pictures", "Belgeler", "Design", "Okuma", "Tasarim", "Kod", "e-kitaplar" };
        var random = new Random(1234);
        for (var round = 0; round < 300; round++)
        {
            var rules = Enumerable.Range(0, random.Next(0, 6))
                .Select(_ => R(names[random.Next(names.Length)], [.. Enumerable.Range(0, random.Next(0, 5)).Select(_ => pool[random.Next(pool.Length)])]))
                .ToList();
            var used = RuleTemplates.UsedExtensions(rules);
            foreach (var offer in RuleTemplates.Offers(rules))
            {
                Assert.NotEmpty(offer.Extensions);
                Assert.DoesNotContain(offer.Extensions, used.Contains);
                Assert.DoesNotContain(rules, r => offer.Template.IsNamed(r.TargetFolder));
                // Önerilen kalıp eklenince hiçbir uzantı iki kuralda olmaz.
                var after = RuleTemplates.Added(rules, RuleTemplates.Create(offer.Extensions, offer.Template.Name));
                var newOnes = after[^1].Extensions;
                Assert.DoesNotContain(newOnes, used.Contains);
            }
        }
    }

    [Fact]
    public void Folder_follows_existing_desktop_folder_in_either_language()
    {
        var design = RuleTemplates.All.Single(t => t.Key == "design");
        Assert.Equal("Tasarım", RuleTemplates.FolderFor(design, Lang.Tr, []));
        Assert.Equal("Design", RuleTemplates.FolderFor(design, Lang.En, []));
        // Masaüstünde öteki dildeki klasör: ona yönelir (gerçek yazılışıyla).
        Assert.Equal("design", RuleTemplates.FolderFor(design, Lang.Tr, ["Oyunlar", "design"]));
        Assert.Equal("TASARIM", RuleTemplates.FolderFor(design, Lang.En, ["TASARIM"]));
        // Kendi dilindeki klasör önce gelir.
        Assert.Equal("tasarim", RuleTemplates.FolderFor(design, Lang.Tr, ["Design", "tasarim"]));
    }

    [Fact]
    public void Typed_extensions_are_normalised_and_split()
    {
        Assert.Equal(["psd", "ai", "fig", "gz"], RuleTemplates.Split("*.PSD, .ai;fig  psd\ttar.gz"));
        Assert.Equal("gz", RuleTemplates.Normalize(" .TAR.GZ "));
        Assert.Empty(RuleTemplates.Split(" , ; . "));
    }

    [Fact]
    public void Check_refuses_invalid_ignored_and_taken_extensions()
    {
        var rules = Rule.Defaults(Lang.Tr);
        Assert.Equal(new ExtensionCheck("psd", ExtensionProblem.None), RuleTemplates.Check(".PSD", rules));
        Assert.Equal(ExtensionProblem.Invalid, RuleTemplates.Check("a/b", rules).Problem);
        Assert.Equal(ExtensionProblem.Invalid, RuleTemplates.Check("", rules).Problem);
        Assert.Equal(ExtensionProblem.Invalid, RuleTemplates.Check(new string('x', RuleTemplates.MaxExtensionLength + 1), rules).Problem);
        Assert.Equal(ExtensionProblem.Ignored, RuleTemplates.Check("lnk", rules).Problem);
        Assert.Equal(ExtensionProblem.Ignored, RuleTemplates.Check(".crdownload", rules).Problem);

        var taken = RuleTemplates.Check("*.JPG", rules);
        Assert.Equal(ExtensionProblem.Taken, taken.Problem);
        Assert.Equal("Resimler", taken.Owner);
        // Düzenlenen kuralın kendi uzantısı alınmış sayılmaz.
        Assert.True(RuleTemplates.Check("jpg", rules, editing: rules[1]).Ok);
    }

    [Fact]
    public void Suggested_folder_for_typed_extensions()
    {
        Assert.Equal("Tasarım", RuleTemplates.SuggestFolder(["psd", "dwg"], Lang.Tr, []));
        Assert.Equal("Design", RuleTemplates.SuggestFolder([".PSD"], Lang.En, []));
        Assert.Equal("design", RuleTemplates.SuggestFolder(["psd"], Lang.Tr, ["design"]));
        Assert.Equal("DWG", RuleTemplates.SuggestFolder(["dwg", "psd"], Lang.Tr, []));
        Assert.Equal("Dwg", RuleTemplates.SuggestFolder(["dwg"], Lang.Tr, ["Dwg"]));
        Assert.Equal("", RuleTemplates.SuggestFolder([], Lang.Tr, []));
    }

    [Fact]
    public void Lists_are_new_and_edits_keep_state_and_unknown_fields()
    {
        var rules = Rule.Defaults(Lang.Tr);
        var added = RuleTemplates.Added(rules, RuleTemplates.Create(["PSD", "ai", "psd"], "Tasarım"));
        Assert.NotSame(rules, added);
        Assert.Equal(6, rules.Count);
        Assert.Equal(["psd", "ai"], added[^1].Extensions);
        Assert.True(added[^1].Enabled);

        var old = rules[0];
        old.Enabled = false;
        old.Extra = new() { ["Zz_future"] = System.Text.Json.JsonDocument.Parse("1").RootElement };
        var edited = RuleTemplates.Edited(old, ["pdf", ".XPS"], "Belgelerim");
        Assert.NotSame(old, edited);
        Assert.Equal(["pdf", "xps"], edited.Extensions);
        Assert.Equal("Belgelerim", edited.TargetFolder);
        Assert.False(edited.Enabled);
        Assert.True(edited.Extra!.ContainsKey("Zz_future"));
        Assert.Equal(["pdf"], old.Extensions);
        Assert.Equal("PDF", old.TargetFolder);

        var replaced = RuleTemplates.Replaced(rules, old, edited);
        Assert.NotSame(rules, replaced);
        Assert.Same(edited, replaced[0]);
        Assert.Same(old, rules[0]);
        Assert.Equal(".psd .ai", RuleTemplates.Dotted(["psd", "ai"]));
    }
}

/// <summary>"Kural ekle" penceresinin ve kural kartlarının bağlantıları (kaynak taraması; pencere testte açılmaz).</summary>
public class RuleDialogWiringTests
{
    private static string Source(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln")))
                return File.ReadAllText(Path.Combine([dir.FullName, "src", "Duzenleme", .. parts]));
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }

    private static string Body(string text, string signature)
    {
        var start = text.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        var open = text.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[open..(i + 1)];
        }
        throw new InvalidOperationException(signature);
    }

    private static void InOrder(string text, params string[] steps)
    {
        var at = -1;
        foreach (var step in steps)
        {
            var next = text.IndexOf(step, at + 1, StringComparison.Ordinal);
            Assert.True(next > at, $"sıra bozuk ya da eksik: {step}");
            at = next;
        }
    }

    [Fact]
    public void Folder_is_created_quietly_then_rules_are_replaced_then_desktop_is_scanned()
    {
        var dialog = Source("Views", "RuleDialog.cs");
        InOrder(Body(dialog, "internal static bool CreateDesktopFolder("),
            "AppHost.MarkQuietFolder(", "FolderCreation.TryCreate(", "Notice.FolderFailed(", "AppHost.NoteFolderCreated(");
        // Klasör, kural listesinden önce açılır; liste yerinde değiştirilmez, yenisi atanır; tarama en sonda.
        var apply = Body(dialog, "internal static Rule Apply(");
        InOrder(apply, "CreateDesktopFolder(", "AppHost.Settings.Rules = ", "AppHost.SaveSettings()", "AppHost.OrganizeIfActive()");
        Assert.DoesNotContain("Rules.Add(", dialog);
        Assert.DoesNotContain("Rules.Insert(", dialog);
        Assert.DoesNotContain("Rules.Remove", dialog);
    }

    [Fact]
    public void Rule_cards_have_no_text_boxes_and_open_the_dialog()
    {
        // Eski "KLASÖR / UZANTILAR" ızgarası (LostFocus'ta yazan kutular) yok: değişiklik yalnızca RuleDialog'dan.
        var xaml = Source("Views", "AutoMovePage.xaml");
        Assert.DoesNotContain("TextBox", xaml);
        Assert.DoesNotContain("LostFocus", xaml);
        var page = Source("Views", "AutoMovePage.xaml.cs");
        Assert.Contains("RuleDialog.Add(", page);
        Assert.Contains("RuleDialog.Edit(", page);
        Assert.DoesNotContain("Rules.Add(", page);
    }
}
