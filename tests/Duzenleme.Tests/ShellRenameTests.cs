using Duzenleme.Desktop;

namespace Duzenleme.Tests;

/// <summary>
/// Gezgin'in dosya işlemi (IFileOperation) ile yeniden adlandırma: Ortak Masaüstü'nde izin gerektiğinde kullanılan yol.
/// Geçici klasörde, izin gerektirmeyen bir dosyayla (pencere açılmaz); COM tanımının doğru olduğunu sınar.
/// </summary>
public sealed class ShellRenameTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nestdesk-shell-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Shell_rename_renames_files_and_folders()
    {
        var file = Path.Combine(_dir, "eski ad.txt");
        File.WriteAllText(file, "x");
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "Klasör")).FullName;

        await ShellFileOperations.RunSta(() =>
        {
            ShellFileOperations.RenameWithShell(file, "Yeni Ad.txt", IntPtr.Zero);
            ShellFileOperations.RenameWithShell(folder, "Klasör 2026", IntPtr.Zero);
        });

        Assert.True(File.Exists(Path.Combine(_dir, "Yeni Ad.txt")));
        Assert.False(File.Exists(file));
        Assert.True(Directory.Exists(Path.Combine(_dir, "Klasör 2026")));
    }

    [Fact]
    public async Task Missing_item_is_an_io_error()
    {
        await Assert.ThrowsAnyAsync<IOException>(() =>
            ShellFileOperations.RunSta(() => ShellFileOperations.RenameWithShell(Path.Combine(_dir, "yok.txt"), "x.txt", IntPtr.Zero)));
    }

    [Fact]
    public void Recycle_confirmation_setting_is_readable() =>
        // Windows'un "silme onayı" ayarı okunabilmeli (varsayılan: sormaz); hata fırlatmaz.
        _ = ShellFileOperations.AsksBeforeRecycling();
}
