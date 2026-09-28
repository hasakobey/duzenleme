using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kısayol kutusunda duran masaüstü dosyası kurallarla taşınmaz (yoksa kutu "bulunamadı" gösterirdi). Önizleme (MovePlan)
/// ile taşıyıcının kararı aynı kalır.
/// </summary>
public class PinnedFilesTests
{
    private static IReadOnlySet<string> Set(params string[] paths) => new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Pinned_file_is_not_moved_by_rules()
    {
        using var t = new TempDesktop();
        var pdf = t.Folder("PDF");
        var boxed = t.File("kutuda.pdf");
        var loose = t.File("serbest.pdf");
        t.Organizer.Pinned = Set(boxed.ToUpperInvariant());

        Assert.Null(t.Organizer.Organize(boxed));
        var moved = t.Organizer.OrganizeAll();

        Assert.Single(moved);
        Assert.True(File.Exists(boxed));
        Assert.False(File.Exists(loose));
        Assert.True(File.Exists(Path.Combine(pdf, "serbest.pdf")));
    }

    [Fact]
    public void Snapshot_marks_pinned_files_and_the_preview_skips_them()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var boxed = t.File("kutuda.pdf");
        t.File("serbest.pdf");
        t.Organizer.Pinned = Set(boxed);

        var files = t.Organizer.SnapshotFiles();
        var plan = MovePlan.Build(files, t.Organizer.ExistingFolders(), t.Settings.Rules);

        Assert.True(files.Single(f => f.Name == "kutuda.pdf").Pinned);
        Assert.Equal("serbest.pdf", Assert.Single(plan).FileName);
    }

    [Fact]
    public void Unpinning_lets_the_rules_work_again()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var boxed = t.File("kutuda.pdf");
        t.Organizer.Pinned = Set(boxed);
        Assert.Empty(t.Organizer.OrganizeAll());

        t.Organizer.Pinned = Set();
        Assert.Single(t.Organizer.OrganizeAll());
        Assert.False(File.Exists(boxed));
    }

    /// <summary>Rastgele masaüstleri: önizlemenin söylediği dosyalar, taşıyıcının taşıdıklarıyla birebir aynıdır.</summary>
    [Fact]
    public void Preview_and_mover_agree_with_pinned_files()
    {
        string[] names = ["a.pdf", "b.PDF", "c.jpg", "d.zip", "e.lnk", "f.txt", "g.docx", "h.mp3", "i.pdf.crdownload", "j.png"];
        string[] folders = ["PDF", "Resimler", "Arşivler", "Belgeler", "Müzik"];
        for (var seed = 0; seed < 40; seed++)
        {
            var random = new Random(seed);
            using var t = new TempDesktop();
            foreach (var folder in folders.Where(_ => random.Next(2) == 0)) t.Folder(folder);
            var created = names.Where(_ => random.Next(3) > 0).Select(n => t.File(n)).ToList();
            t.Organizer.Pinned = Set(created.Where(_ => random.Next(3) == 0).ToArray());

            var preview = MovePlan.Build(t.Organizer.SnapshotFiles(), t.Organizer.ExistingFolders(), t.Settings.Rules)
                .Select(m => m.FileName).Order(StringComparer.Ordinal).ToList();
            var moved = t.Organizer.OrganizeAll().Select(e => Path.GetFileName(e.Source)).Order(StringComparer.Ordinal).ToList();

            Assert.True(preview.SequenceEqual(moved), $"tohum {seed}: önizleme [{string.Join(", ", preview)}], taşınan [{string.Join(", ", moved)}]");
            Assert.All(t.Organizer.Pinned, p => Assert.True(File.Exists(p), p));
        }
    }
}
