using Duzenleme.Core;

namespace Duzenleme.Tests;

public class OnboardingTests
{
    private static DesktopFile F(string name) => new(name, FileAttributes.Normal, false);

    private static StarterFence Kind(DesktopFilter filter) => new(filter, null, DesktopItems.Label(filter));

    private static StarterFence Folder(string name) => new(DesktopFilter.None, name, name);

    private static readonly StarterFence[] AllKinds = [Kind(DesktopFilter.Folders), Kind(DesktopFilter.Shortcuts), Kind(DesktopFilter.Files)];

    // FolderChoices

    [Fact]
    public void Existing_folder_is_on_by_default()
    {
        var pdf = Onboarding.FolderChoices(Rule.Defaults(), ["PDF"], []).First(c => c.Folder == "PDF");

        Assert.True(pdf.Exists);
        Assert.True(pdf.DefaultOn);
        Assert.Equal(0, pdf.FilesNow);
        Assert.Equal("pdf", pdf.Extensions);
    }

    [Fact]
    public void Missing_folder_with_waiting_files_is_on_by_default()
    {
        var pdf = Onboarding.FolderChoices(Rule.Defaults(), [], [F("a.pdf"), F("b.pdf"), F("c.lnk")]).First(c => c.Folder == "PDF");

        Assert.False(pdf.Exists);
        Assert.True(pdf.DefaultOn);
        Assert.Equal(2, pdf.FilesNow);
    }

    [Fact]
    public void Missing_folder_without_files_is_off_by_default()
    {
        var images = Onboarding.FolderChoices(Rule.Defaults(), ["PDF"], [F("a.pdf")]).First(c => c.Folder == "Resimler");

        Assert.False(images.Exists);
        Assert.False(images.DefaultOn);
        Assert.Equal(0, images.FilesNow);
    }

    [Fact]
    public void Disabled_rule_is_off_even_if_folder_exists()
    {
        var rules = Rule.Defaults();
        rules.First(r => r.TargetFolder == "PDF").Enabled = false;

        var pdf = Onboarding.FolderChoices(rules, ["PDF"], [F("a.pdf")]).First(c => c.Folder == "PDF");

        Assert.False(pdf.DefaultOn);
        Assert.Equal(0, pdf.FilesNow);
    }

    [Fact]
    public void Rules_for_same_folder_give_one_choice_with_desktop_spelling()
    {
        List<Rule> rules =
        [
            new() { TargetFolder = "Arşivler", Extensions = ["zip"] },
            new() { TargetFolder = "ARSIVLER", Extensions = ["rar", ".zip"] },
            new() { TargetFolder = "", Extensions = ["txt"] },
            new() { TargetFolder = "Boş", Extensions = [] },
        ];

        var choice = Assert.Single(Onboarding.FolderChoices(rules, ["arsivler"], [F("a.rar"), F("b.zip")]));

        Assert.Equal("arsivler", choice.Folder);
        Assert.True(choice.Exists);
        Assert.Equal(2, choice.FilesNow);
        Assert.Equal("zip, rar", choice.Extensions);
    }

    [Fact]
    public void Choices_follow_rule_order()
    {
        var folders = Onboarding.FolderChoices(Rule.Defaults(), [], []).Select(c => c.Folder);

        Assert.Equal(["PDF", "Resimler", "Belgeler", "Arşivler", "Videolar", "Müzik"], folders);
    }

    // ApplyFolderSelection

    [Fact]
    public void Selection_returns_new_list_and_leaves_rules_untouched()
    {
        var rules = Rule.Defaults();
        var images = rules.First(r => r.TargetFolder == "Resimler");
        images.Enabled = false;

        var result = Onboarding.ApplyFolderSelection(rules, ["resimler"], []);

        Assert.NotSame(rules, result);
        Assert.Equal(rules.Count, result.Count);
        Assert.All(result, r => Assert.DoesNotContain(r, rules));
        Assert.False(images.Enabled);
        Assert.True(result.First(r => r.TargetFolder == "Resimler").Enabled);
    }

    [Fact]
    public void Unselected_existing_folder_rule_is_disabled()
    {
        var result = Onboarding.ApplyFolderSelection(Rule.Defaults(), ["Resimler"], ["PDF", "Resimler"]);

        Assert.False(result.First(r => r.TargetFolder == "PDF").Enabled);
        Assert.True(result.First(r => r.TargetFolder == "Resimler").Enabled);
    }

    [Fact]
    public void Unselected_missing_folder_rule_is_kept_as_is()
    {
        var rules = Rule.Defaults();
        rules.First(r => r.TargetFolder == "Müzik").Enabled = false;

        var result = Onboarding.ApplyFolderSelection(rules, ["PDF"], ["PDF"]);

        Assert.True(result.First(r => r.TargetFolder == "Videolar").Enabled);
        Assert.False(result.First(r => r.TargetFolder == "Müzik").Enabled);
        var copy = result.First(r => r.TargetFolder == "Videolar");
        Assert.Equal(rules.First(r => r.TargetFolder == "Videolar").Extensions, copy.Extensions);
        Assert.NotSame(rules.First(r => r.TargetFolder == "Videolar").Extensions, copy.Extensions);
    }

    // FoldersToCreate

    [Fact]
    public void Folders_to_create_are_unique_trimmed_and_missing_only()
    {
        List<Rule> rules =
        [
            new() { TargetFolder = "PDF", Extensions = ["pdf"] },
            new() { TargetFolder = " Resimler ", Extensions = ["jpg"] },
            new() { TargetFolder = "resimler", Extensions = ["png"] },
            new() { TargetFolder = "Belgeler", Extensions = ["docx"] },
            new() { TargetFolder = "Videolar", Extensions = ["mp4"] },
        ];

        var create = Onboarding.FoldersToCreate(rules, ["PDF", "Resimler", "belgeler"], ["pdf"]);

        Assert.Equal(["Resimler", "Belgeler"], create);
    }

    // "Klasör yoksa oluştur" açıkken: klasörü olmayan kural da taşır.

    [Fact]
    public void Enabled_rules_are_on_by_default_when_missing_folders_are_created()
    {
        var rules = Rule.Defaults();
        rules.First(r => r.TargetFolder == "Müzik").Enabled = false;

        var choices = Onboarding.FolderChoices(rules, ["PDF"], [], createMissing: true);

        Assert.True(choices.First(c => c.Folder == "Videolar").DefaultOn);
        Assert.False(choices.First(c => c.Folder == "Müzik").DefaultOn);
    }

    [Fact]
    public void Unselected_missing_folder_rule_is_disabled_when_missing_folders_are_created()
    {
        var result = Onboarding.ApplyFolderSelection(Rule.Defaults(), ["PDF"], ["PDF"], createMissing: true);

        Assert.True(result.First(r => r.TargetFolder == "PDF").Enabled);
        Assert.All(result.Where(r => r.TargetFolder != "PDF"), r => Assert.False(r.Enabled));
    }

    [Fact]
    public void No_folder_is_created_up_front_when_missing_folders_are_created()
    {
        Assert.Empty(Onboarding.FoldersToCreate(Rule.Defaults(), ["PDF", "Resimler"], [], createMissing: true));
    }

    [Fact]
    public void Preview_does_not_hide_moves_of_unselected_rules()
    {
        // İnceleme bulgusu: yalnızca PDF seçili, "yoksa oluştur" açık. rapor.docx ne önizlemede ne de gerçekte taşınmalı.
        var files = new[] { F("a.pdf"), F("rapor.docx") };

        var moves = Onboarding.PreviewMoves(Rule.Defaults(), ["PDF"], ["PDF"], files, createMissing: true);

        var move = Assert.Single(moves);
        Assert.Equal(("a.pdf", "PDF"), (move.FileName, move.Folder));
        var applied = Onboarding.ApplyFolderSelection(Rule.Defaults(), ["PDF"], ["PDF"], createMissing: true);
        var engine = new RuleEngine(() => new AppSettings { Rules = applied, CreateMissingFolders = true });
        Assert.False(engine.Decide("", "rapor.docx", FileAttributes.Normal, ["PDF"]).ShouldMove);
    }

    [Fact]
    public void Preview_with_created_missing_folders_lists_files_for_new_folders()
    {
        var moves = Onboarding.PreviewMoves(Rule.Defaults(), ["PDF", "Belgeler"], ["PDF"], [F("rapor.docx")], createMissing: true);

        var move = Assert.Single(moves);
        Assert.Equal("Belgeler", move.Folder);
        Assert.False(move.FolderExists);
    }

    /// <summary>
    /// Karşılamanın önizlemesi, "Bitti"den sonra taşıyıcının vereceği kararla birebir aynıdır: rastgele kurallar, klasörler,
    /// dosyalar, seçimler ve "Klasör yoksa oluştur" için. Ayrıca yalnızca seçilen klasörlere dosya gider.
    /// </summary>
    [Fact]
    public void Preview_matches_what_the_mover_does_after_finish()
    {
        string[] folderNames = ["PDF", "pdf ", "Resimler", "Belgeler", "Arşivler", "ARSIVLER", "Müzik", " "];
        string[] extensions = ["pdf", "jpg", ".png", "*.docx", "zip", "RAR", "mp3", ""];
        string[] fileNames = ["a.pdf", "b.PDF", "c.jpg", "d.png", "e.docx", "f.zip", "g.rar", "h.mp3", "i.lnk", "j.txt", "k", "l.crdownload", "~$m.docx"];

        for (var seed = 0; seed < 500; seed++)
        {
            var random = new Random(seed);
            T Pick<T>(T[] items) => items[random.Next(items.Length)];

            var rules = Enumerable.Range(0, random.Next(0, 7)).Select(_ => new Rule
            {
                TargetFolder = Pick(folderNames),
                Extensions = Enumerable.Range(0, random.Next(0, 3)).Select(_ => Pick(extensions)).ToList(),
                Enabled = random.Next(4) > 0,
            }).ToList();
            var existing = folderNames.Where(n => n.Trim().Length > 0 && random.Next(3) == 0)
                .Select(n => n.Trim().ToLowerInvariant()).Distinct().ToList();
            var files = fileNames.Where(_ => random.Next(2) == 0)
                .Select(n => new DesktopFile(n, random.Next(10) == 0 ? FileAttributes.Hidden : FileAttributes.Normal, random.Next(6) == 0,
                    Pinned: random.Next(7) == 0))
                .ToList();
            var createMissing = random.Next(2) == 0;
            var selected = Onboarding.FolderChoices(rules, existing, files, createMissing)
                .Where(_ => random.Next(2) == 0).Select(c => c.Folder).ToList();

            // Taşıyıcı: "Bitti"de yazılan kurallar, açılan klasörler; geri alınmış ve kısayol kutusunda duran dosyalara dokunmaz.
            var applied = Onboarding.ApplyFolderSelection(rules, selected, existing, createMissing);
            var after = existing.Concat(Onboarding.FoldersToCreate(applied, selected, existing, createMissing)).ToList();
            var engine = new RuleEngine(() => new AppSettings { Rules = applied, CreateMissingFolders = createMissing });
            var expected = files.Where(f => !f.WasUndone && !f.Pinned)
                .Select(f => (f.Name, Decision: engine.Decide(@"C:\Masaüstü", f.Name, f.Attributes, after)))
                .Where(x => x.Decision.ShouldMove)
                .Select(x => $"{x.Name} → {FolderName.Fold(Path.GetFileName(x.Decision.TargetDirectory!))}")
                .ToList();

            var preview = Onboarding.PreviewMoves(rules, selected, existing, files, createMissing);
            var actual = preview.Select(m => $"{m.FileName} → {FolderName.Fold(m.Folder)}").ToList();

            Assert.True(expected.SequenceEqual(actual),
                $"tohum {seed}: taşıyıcı [{string.Join(", ", expected)}], önizleme [{string.Join(", ", actual)}]");
            Assert.All(preview, m => Assert.Contains(selected, s => FolderName.Equal(s, m.Folder)));
        }
    }

    // Yeni kullanıcı

    [Fact]
    public void New_user_is_paused_until_welcome_is_finished()
    {
        var settings = new AppSettings { FirstRunDone = false, Paused = false };

        Assert.True(Onboarding.PrepareNewUser(settings));
        Assert.True(settings.Paused);
        Assert.True(settings.RenameNoticeShown);
    }

    [Fact]
    public void Existing_user_is_left_alone()
    {
        var settings = new AppSettings { FirstRunDone = true, Paused = false, RenameNoticeShown = false };

        Assert.False(Onboarding.PrepareNewUser(settings));
        Assert.False(settings.Paused);
        Assert.False(settings.RenameNoticeShown);
    }

    // FencePlanText

    [Fact]
    public void Fence_plan_text_variants()
    {
        Assert.Equal("Klasörler, Kısayollar ve Dosyalar için birer bölme eklenir.",
            Onboarding.FencePlanText(AllKinds));
        Assert.Equal("Klasörler, Kısayollar ve Dosyalar için birer bölme eklenir; PDF klasörü için de bir bölme.",
            Onboarding.FencePlanText([.. AllKinds, Folder("PDF")]));
        Assert.Equal("Klasörler, Kısayollar ve Dosyalar için birer bölme eklenir; PDF ve Resimler klasörleri için de birer bölme.",
            Onboarding.FencePlanText([.. AllKinds, Folder("PDF"), Folder("Resimler")]));
        Assert.Equal("Dosyalar için bir bölme eklenir.",
            Onboarding.FencePlanText([Kind(DesktopFilter.Files)]));
        Assert.Equal("Dosyalar için bir bölme eklenir; PDF klasörü için de bir bölme.",
            Onboarding.FencePlanText([Kind(DesktopFilter.Files), Folder("PDF")]));
        Assert.Equal("Kısayollar ve Dosyalar için birer bölme eklenir.",
            Onboarding.FencePlanText([Kind(DesktopFilter.Shortcuts), Kind(DesktopFilter.Files)]));
        Assert.Equal("PDF klasörü için bir bölme eklenir.",
            Onboarding.FencePlanText([Folder("PDF")]));
        Assert.Equal("PDF ve Resimler klasörleri için birer bölme eklenir.",
            Onboarding.FencePlanText([Folder("PDF"), Folder("Resimler")]));
        Assert.Equal("Bölmelerin zaten hazır; yeni bölme eklenmez.",
            Onboarding.FencePlanText([]));
    }

    [Fact]
    public void Fence_plan_text_matches_starter_plan()
    {
        var plan = StarterFences.Plan([], Rule.Defaults(), ["PDF", "Oyunlar"]);

        Assert.Equal("Klasörler, Kısayollar ve Dosyalar için birer bölme eklenir; PDF klasörü için de bir bölme.",
            Onboarding.FencePlanText(plan));
    }

    // JoinTr

    [Fact]
    public void Join_tr()
    {
        Assert.Equal("", Onboarding.JoinTr([]));
        Assert.Equal("A", Onboarding.JoinTr(["A"]));
        Assert.Equal("A ve B", Onboarding.JoinTr(["A", "B"]));
        Assert.Equal("A, B ve C", Onboarding.JoinTr(["A", "B", "C"]));
    }

    // MovePreviewLines

    [Fact]
    public void Preview_lines_group_by_folder()
    {
        var moves = MovePlan.Build([F("a.pdf"), F("b.pdf"), F("c.jpg"), F("d.lnk")], ["PDF", "Resimler"], Rule.Defaults());

        Assert.Equal(["2 dosya → PDF", "1 dosya → Resimler"], Onboarding.MovePreviewLines(moves));
    }

    [Fact]
    public void Preview_lines_are_limited_to_four_plus_summary()
    {
        PlannedMove M(string folder) => new("x", folder, true);
        PlannedMove[] moves =
        [
            M("PDF"), M("PDF"), M("PDF"), M("PDF"),
            M("Resimler"), M("Resimler"), M("Resimler"),
            M("Belgeler"), M("Belgeler"),
            M("Arşivler"), M("Arşivler"),
            M("Videolar"),
            M("Müzik"),
        ];

        var lines = Onboarding.MovePreviewLines(moves);

        Assert.Equal(
        [
            "4 dosya → PDF",
            "3 dosya → Resimler",
            "2 dosya → Arşivler",
            "2 dosya → Belgeler",
            "+ 2 dosya daha, 2 klasöre",
        ], lines);
    }

    [Fact]
    public void Preview_lines_without_overflow_have_no_summary()
    {
        PlannedMove[] moves = [new("a", "A", true), new("b", "B", true), new("c", "C", true), new("d", "D", true)];

        Assert.Equal(4, Onboarding.MovePreviewLines(moves).Count);
        Assert.Empty(Onboarding.MovePreviewLines([]));
    }
}
