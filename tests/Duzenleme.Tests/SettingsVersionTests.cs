using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Sürüm değişikliği yedekleri (2.2): karşılaştırma, yedek adı ve kaç yedeğin kaldığı.</summary>
public class SettingsVersionTests
{
    [Theory]
    [InlineData("2.1.1", "2.2.0", SettingsVersionKind.Upgrade)]
    [InlineData("2.2.0", "2.2.0", SettingsVersionKind.Same)]
    [InlineData("2.3.0", "2.2.0", SettingsVersionKind.Downgrade)]
    [InlineData("2.9.0", "2.10.0", SettingsVersionKind.Upgrade)]      // sayıyla, metin sırasıyla değil
    [InlineData("2.10.0", "2.9.0", SettingsVersionKind.Downgrade)]
    [InlineData("2.2", "2.2.0", SettingsVersionKind.Same)]            // eksik parça 0
    [InlineData("2.2.0.0", "2.2.0", SettingsVersionKind.Same)]
    [InlineData(" v2.2.0 ", "2.2.0", SettingsVersionKind.Same)]
    [InlineData("2.3.0-beta", "2.2.0", SettingsVersionKind.Downgrade)] // son ek yok sayılır
    [InlineData("2.2.0+abc", "2.2.0", SettingsVersionKind.Same)]
    [InlineData("3", "2.2.0", SettingsVersionKind.Downgrade)]
    [InlineData("2.2.1", "2.2.0", SettingsVersionKind.Downgrade)]
    public void Versions_compare_numerically(string saved, string current, SettingsVersionKind expected)
    {
        var check = SettingsVersion.Check(saved, current);
        Assert.Equal(expected, check.Kind);
        Assert.Equal(saved.Trim(), check.From);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unstamped_file_is_an_upgrade_from_an_older_version(string? saved)
    {
        var check = SettingsVersion.Check(saved, "2.2.0");
        Assert.Equal(SettingsVersionKind.Upgrade, check.Kind);
        Assert.Null(check.From);
    }

    [Theory]
    [InlineData("gelecek")]
    [InlineData("x2.3")]
    [InlineData("99999999999.1")]     // int'e sığmaz
    public void Unparseable_stamp_is_backed_up_but_never_called_newer(string saved)
    {
        Assert.Null(SettingsVersion.Parse(saved));
        var check = SettingsVersion.Check(saved, "2.2.0");
        Assert.Equal(SettingsVersionKind.Upgrade, check.Kind);
        Assert.Equal(saved, check.From);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("bilinmiyor")]
    public void Unknown_running_version_does_nothing(string? current)
    {
        Assert.Equal(SettingsVersionKind.Same, SettingsVersion.Check("2.1.1", current).Kind);
        Assert.Equal(SettingsVersionKind.Same, SettingsVersion.Check(null, current).Kind);
    }

    [Fact]
    public void Parse_is_tolerant()
    {
        Assert.Equal(new Version(2, 2, 0, 0), SettingsVersion.Parse("2.2"));
        Assert.Equal(new Version(2, 3, 1, 0), SettingsVersion.Parse("v2.3.1-rc.2"));
        Assert.Equal(new Version(10, 0, 0, 0), SettingsVersion.Parse("10"));
        Assert.Null(SettingsVersion.Parse(null));
        Assert.Null(SettingsVersion.Parse("-1.0"));
    }

    [Fact]
    public void Backup_name_says_both_versions_and_the_time()
    {
        var at = new DateTime(2026, 10, 1, 14, 30, 5);
        Assert.Equal("before-2.2.0-from-2.1.1-20261001-143005.json", SettingsVersion.BackupName("2.1.1", "2.2.0", at));
        Assert.Equal("before-2.2.0-from-eski-20261001-143005.json", SettingsVersion.BackupName(null, "2.2.0", at));
        Assert.Equal("before-2.2.0-from-eski-20261001-143005.json", SettingsVersion.BackupName("  ", "2.2.0", at));
    }

    [Theory]
    [InlineData("2.3.0-beta")]
    [InlineData(@"..\..\settings")]
    [InlineData("2.3/0:1*?")]
    [InlineData("çok-yeni")]
    [InlineData("12345678901234567890123456789012345678901234567890")]
    public void Backup_name_is_a_safe_single_file_name(string from)
    {
        var name = SettingsVersion.BackupName(from, "2.2.0", new DateTime(2026, 10, 1));
        Assert.Equal(name, Path.GetFileName(name));
        Assert.True(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, name);
        Assert.DoesNotContain("..\\", name);
        Assert.True(SettingsVersion.IsVersionBackup(name), name);
        Assert.True(name.Length < 100, name);
    }

    [Fact]
    public void Backup_name_is_not_matched_by_the_daily_rotation()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-version-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var before = SettingsVersion.BackupName("2.1.1", "2.2.0", new DateTime(2026, 10, 1, 9, 0, 0));
            var unstamped = SettingsVersion.BackupName(null, "2.2.0", new DateTime(2026, 10, 1, 9, 0, 1));
            File.WriteAllText(Path.Combine(dir, before), "{}");
            File.WriteAllText(Path.Combine(dir, unstamped), "{}");
            File.WriteAllText(Path.Combine(dir, "settings-2026-10-01.json"), "{}");

            Assert.False(before.StartsWith("settings-", StringComparison.OrdinalIgnoreCase));
            // Günlük döndürme (AppHost.BackupSettingsDaily) bu desenle listeler ve 7'den eskisini siler.
            var daily = Directory.GetFiles(dir, SettingsVersion.DailyBackupPattern).Select(Path.GetFileName).ToList();
            Assert.Equal(["settings-2026-10-01.json"], daily);
            var versions = Directory.GetFiles(dir, SettingsVersion.VersionBackupPattern).Select(Path.GetFileName).Order().ToList();
            Assert.Equal(new[] { before, unstamped }.Order(), versions);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Prune_keeps_the_newest_three_by_time_in_the_name()
    {
        // Ad sırası zaman sırası değil (sürüm başta): en eski "before-9.9..." adına göre en sonda olurdu.
        string[] names =
        [
            "before-2.2.0-from-eski-20261001-100000.json",
            "before-9.9.0-from-2.2.0-20260101-000000.json",   // en eski
            "before-2.3.0-from-2.2.0-20261105-080000.json",
            "before-2.2.1-from-2.3.0-20261106-080000.json",
            "before-2.3.0-from-2.2.1-20261107-080000.json",   // en yeni
            "settings-2026-10-01.json",                        // günlük yedek: dokunulmaz
            "before-notlarim.json",                            // kullanıcının dosyası: dokunulmaz
            "before-2.2.0-from-eski-2026-10-01.json",          // biçim dışı: dokunulmaz
        ];
        var deleted = SettingsVersion.Prune(names);
        Assert.Equal(["before-2.2.0-from-eski-20261001-100000.json", "before-9.9.0-from-2.2.0-20260101-000000.json"], deleted);

        Assert.Empty(SettingsVersion.Prune(names.Take(3)));
        Assert.Equal(5, SettingsVersion.Prune(names, keep: 0).Count);
    }

    [Fact]
    public void Only_backup_names_are_version_backups()
    {
        Assert.True(SettingsVersion.IsVersionBackup("before-2.2.0-from-2.1.1-20261001-143005.json"));
        Assert.True(SettingsVersion.IsVersionBackup("BEFORE-2.2.0-FROM-ESKI-20261001-143005.JSON"));
        Assert.False(SettingsVersion.IsVersionBackup("before-2.2.0-20261001-143005.json"));
        Assert.False(SettingsVersion.IsVersionBackup("settings-2026-10-01.json"));
        Assert.False(SettingsVersion.IsVersionBackup("before-2.2.0-from-2.1.1-20261001-143005.json.tmp"));
        Assert.False(SettingsVersion.IsVersionBackup(null));
    }

    [Fact]
    public void Stamp_is_not_written_when_empty_and_round_trips()
    {
        var empty = System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(new AppSettings()));
        Assert.DoesNotContain("SavedWithAppVersion", empty);
        var stamped = System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(new AppSettings { SavedWithAppVersion = "2.2.0" }));
        Assert.Contains("\"SavedWithAppVersion\": \"2.2.0\"", stamped);
    }
}
