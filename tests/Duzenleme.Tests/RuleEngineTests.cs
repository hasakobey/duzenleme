using Duzenleme.Core;

namespace Duzenleme.Tests;

public class RuleEngineTests
{
    private readonly AppSettings _settings = new();
    private RuleEngine Engine => new(() => _settings);

    private RuleDecision Decide(string file, params string[] folders) =>
        Engine.Decide(@"C:\Desk", file, FileAttributes.Normal, folders);

    [Fact]
    public void Pdf_goes_to_existing_PDF_folder()
    {
        var d = Decide("rapor.pdf", "PDF", "Başka");
        Assert.True(d.ShouldMove);
        Assert.Equal(@"C:\Desk\PDF", d.TargetDirectory);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("Pdf")]
    [InlineData(" PDF ")]
    public void Folder_match_ignores_case(string folder)
    {
        Assert.True(Decide("a.PDF", folder).ShouldMove);
    }

    [Theory]
    [InlineData("ARŞİVLER")]
    [InlineData("arsivler")]
    [InlineData("Arşivler")]
    public void Folder_match_ignores_turkish_characters(string folder)
    {
        var d = Decide("yedek.zip", folder);
        Assert.True(d.ShouldMove);
        Assert.EndsWith(folder, d.TargetDirectory);
    }

    [Fact]
    public void Missing_folder_means_file_stays()
    {
        var d = Decide("rapor.pdf", "Resimler");
        Assert.False(d.ShouldMove);
        Assert.Equal(SkipReason.FolderMissing, d.Skip);
    }

    [Fact]
    public void Missing_folder_is_created_when_enabled()
    {
        _settings.CreateMissingFolders = true;
        var d = Decide("rapor.pdf");
        Assert.True(d.ShouldMove);
        Assert.Equal(@"C:\Desk\PDF", d.TargetDirectory);
    }

    [Theory]
    [InlineData("Discord.lnk")]
    [InlineData("site.url")]
    [InlineData("desktop.ini")]
    [InlineData("film.mp4.crdownload")]
    [InlineData("indirme.pdf.part")]
    [InlineData("~$rapor.docx")]
    public void Shortcuts_system_files_and_partial_downloads_are_ignored(string file)
    {
        _settings.CreateMissingFolders = true;
        Assert.Equal(SkipReason.Ignored, Decide(file, "PDF", "Belgeler", "Videolar").Skip);
    }

    [Theory]
    [InlineData(FileAttributes.Hidden)]
    [InlineData(FileAttributes.System)]
    [InlineData(FileAttributes.Directory)]
    public void Hidden_system_and_directories_are_ignored(FileAttributes attr)
    {
        Assert.Equal(SkipReason.Ignored, Engine.Decide(@"C:\Desk", "a.pdf", attr, ["PDF"]).Skip);
    }

    [Fact]
    public void Unknown_extension_has_no_rule()
    {
        Assert.Equal(SkipReason.NoRule, Decide("oyun.exe", "PDF").Skip);
        Assert.Equal(SkipReason.NoRule, Decide("uzantisiz", "PDF").Skip);
    }

    [Fact]
    public void Disabled_rule_is_skipped()
    {
        _settings.Rules.Single(r => r.TargetFolder == "PDF").Enabled = false;
        Assert.Equal(SkipReason.NoRule, Decide("a.pdf", "PDF").Skip);
    }

    [Fact]
    public void Custom_rule_with_dotted_extensions_works()
    {
        _settings.Rules.Insert(0, new Rule { TargetFolder = "Tasarım", Extensions = [".psd", "*.fig"] });
        Assert.Equal(@"C:\Desk\tasarim", Decide("logo.fig", "tasarim").TargetDirectory);
    }
}
