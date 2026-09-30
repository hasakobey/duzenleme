using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Klasör açılamayınca kullanıcıya .NET'in İngilizce iletisi değil, nedenini söyleyen bir cümle gösterilir.</summary>
public class FolderCreationTests
{
    [Fact]
    public void Creates_the_folder_and_reports_success()
    {
        var root = Path.Combine(Path.GetTempPath(), "nestdesk-folder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "PDF");
            var result = FolderCreation.TryCreate(path);
            Assert.True(result.Ok);
            Assert.True(Directory.Exists(path));
            Assert.True(FolderCreation.TryCreate(path).Ok); // zaten varsa da başarılı
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Access_denied_is_explained_as_a_security_block()
    {
        var result = FolderCreation.Explain(new UnauthorizedAccessException("Access to the path is denied."), @"C:\Users\x\Desktop\PDF");
        Assert.False(result.Ok);
        Assert.True(result.BlockedBySecurity);
        Assert.Contains("PDF", result.Error);
        Assert.Contains("Denetimli klasör erişimi", result.Error);
        Assert.DoesNotContain("Access to the path", result.Error);
    }

    [Fact]
    public void Unreachable_desktop_names_the_location()
    {
        var result = FolderCreation.Explain(new DirectoryNotFoundException("Could not find a part of the path."), @"D:\OneDrive\Desktop\PDF");
        Assert.False(result.BlockedBySecurity);
        Assert.Contains(@"D:\OneDrive\Desktop", result.Error);
        Assert.Contains("OneDrive", result.Error);
    }

    [Fact]
    public void Too_long_and_invalid_names_reuse_the_rename_hints()
    {
        Assert.Equal(L.T("Ad çok uzun: Windows bu kadar uzun bir yolu açamaz."),
            FolderCreation.Explain(new PathTooLongException(), @"C:\a\b").Error);
        Assert.Equal(FileNames.InvalidCharactersHint, FolderCreation.Explain(new ArgumentException(), @"C:\a\b").Error);
    }

    [Fact]
    public void Messages_are_translated_to_english()
    {
        using var _ = L.Use(Lang.En);
        var result = FolderCreation.Explain(new UnauthorizedAccessException(), @"C:\Users\x\Desktop\PDF");
        Assert.Contains("PDF", result.Error);
        Assert.Contains("Controlled folder access", result.Error);
    }
}
