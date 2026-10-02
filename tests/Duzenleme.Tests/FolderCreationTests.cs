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

    /// <summary>
    /// Otomatik taşımanın izin hatası dosyayı klasör sanmaz ("klasörü açılamadı" değil) ve izin vermeyen yer olarak masaüstünü
    /// değil kuralın hedef klasörünü gösterir; Denetimli klasör erişimi açıkken de kapalıyken de.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Move_failure_names_the_file_and_the_target_folder(bool controlledFolderAccess)
    {
        var desktop = @"C:\Users\x\Desktop";
        var result = FolderCreation.ExplainMove(new UnauthorizedAccessException("Access to the path is denied."), desktop + @"\fatura.pdf",
            desktop + @"\Belgeler PDF\", controlledFolderAccess);
        Assert.True(result.BlockedBySecurity);
        Assert.Contains("\"fatura.pdf\" dosya", result.Error);
        Assert.Contains("\"Belgeler PDF\" klasörüne", result.Error);
        Assert.Contains("Denetimli klasör erişimi", result.Error);
        Assert.DoesNotContain("klasörü açılamadı", result.Error);
        Assert.DoesNotContain("\"fatura.pdf\" klasör", result.Error);
        Assert.DoesNotContain("Access to the path", result.Error);
        if (!controlledFolderAccess)
        {
            Assert.Contains(@"(C:\Users\x\Desktop\Belgeler PDF)", result.Error);
            Assert.DoesNotContain("(" + desktop + ")", result.Error);
        }
    }

    [Fact]
    public void Move_failure_without_a_target_or_permission_problem()
    {
        var unknown = FolderCreation.ExplainMove(new UnauthorizedAccessException(), @"C:\d\a.pdf", null, controlledFolderAccess: false);
        Assert.True(unknown.BlockedBySecurity);
        Assert.StartsWith("\"a.pdf\" taşınamadı: Windows izin vermedi.", unknown.Error);
        Assert.Contains("Denetimli klasör erişimi", FolderCreation.ExplainMove(new UnauthorizedAccessException(), @"C:\d\a.pdf", null, true).Error);
        var io = FolderCreation.ExplainMove(new IOException("disk"), @"C:\d\a.pdf", @"C:\d\PDF", controlledFolderAccess: false);
        Assert.False(io.BlockedBySecurity);
        Assert.Equal("\"a.pdf\" taşınamadı.", io.Error);

        using var _ = L.Use(Lang.En);
        Assert.Equal(@"Couldn't move the file ""a.pdf"" into the ""PDF"" folder: Windows didn't give permission (C:\d\PDF). Controlled folder access in Windows Security or an antivirus program might be blocking it.",
            FolderCreation.ExplainMove(new UnauthorizedAccessException(), @"C:\d\a.pdf", @"C:\d\PDF", controlledFolderAccess: false).Error);
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
