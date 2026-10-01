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

    [Fact]
    public void Prune_never_deletes_the_copy_just_taken()
    {
        // Saat geri alınmış ya da daha eski bir yedeğin adında gelecekten bir zaman var: az önce alınan kopya adına göre en
        // eskisi olsa da kalır ve kalan üç yedekten biri sayılır.
        string[] names =
        [
            "before-2.3.0-from-2.2.0-20301231-235959.json",   // gelecekten
            "before-2.2.1-from-2.3.0-20261106-080000.json",
            "before-2.3.0-from-2.2.1-20261107-080000.json",
            "before-2.2.0-from-2.3.0-20261001-090000.json",   // az önce alınan (saat geri alınmış)
        ];
        var deleted = SettingsVersion.Prune(names, justTaken: names[3]);
        Assert.Equal(["before-2.2.1-from-2.3.0-20261106-080000.json"], deleted);
        Assert.DoesNotContain(names[3], SettingsVersion.Prune(names, keep: 1, justTaken: names[3]));
        Assert.Equal(3, SettingsVersion.Prune(names, keep: 1, justTaken: names[3]).Count);
        // Alınamayan (listede olmayan) kopya yer tutmaz.
        Assert.Single(SettingsVersion.Prune(names, justTaken: "before-9.9.9-from-2.2.0-20261001-090000.json"));
    }

    [Theory]
    [InlineData("2.3.0", "2.3.0")]
    [InlineData("2.3", "2.3.0")]
    [InlineData("v3", "3.0.0")]
    [InlineData("2.3.0-beta <b>uydurma</b>", "2.3.0")]   // elle düzenlenmiş damganın son eki gösterilmez
    [InlineData("gelecek", "?")]
    [InlineData(null, "?")]
    public void Display_shows_only_the_parsed_numbers(string? stamp, string expected) =>
        Assert.Equal(expected, SettingsVersion.Display(stamp));

    // --- Dosya işleri (geçici klasörde) ---

    private static string TempDir() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-version-" + Guid.NewGuid().ToString("N"))).FullName;

    private static readonly DateTime At = new(2026, 10, 2, 9, 30, 0);

    [Fact]
    public void Backup_before_change_copies_the_file_byte_for_byte()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var bytes = System.Text.Encoding.UTF8.GetBytes("{\n  \"Paused\": true,\n  \"Theme\": \"Sepia\"\n}\n");
            File.WriteAllBytes(settings, bytes);
            var backups = Path.Combine(dir, SettingsVersion.BackupFolder);

            var name = SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check("2.1.1", "2.2.0"), "2.2.0", At);
            Assert.Equal("before-2.2.0-from-2.1.1-20261002-093000.json", name);
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(backups, name!)));
            Assert.Equal(bytes, File.ReadAllBytes(settings));   // asıl dosyaya dokunulmaz

            var unstamped = SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check(null, "2.2.0"), "2.2.0", At.AddSeconds(1));
            Assert.Equal("before-2.2.0-from-eski-20261002-093001.json", unstamped);
            var newer = SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check("2.3.0", "2.2.0"), "2.2.0", At.AddSeconds(2));
            Assert.Equal("before-2.2.0-from-2.3.0-20261002-093002.json", newer);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Same_version_or_missing_settings_take_no_backup()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var backups = Path.Combine(dir, SettingsVersion.BackupFolder);
            var log = new List<string>();

            // Yeni kullanıcı: dosya yok, klasör de açılmaz.
            Assert.Null(SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check(null, "2.2.0"), "2.2.0", At, log.Add));
            Assert.False(Directory.Exists(backups));

            File.WriteAllText(settings, "{}");
            Assert.Null(SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check("2.2.0", "2.2.0"), "2.2.0", At, log.Add));
            Assert.False(Directory.Exists(backups));
            Assert.Empty(log);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Failed_backup_returns_null_and_startup_goes_on()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            File.WriteAllText(settings, "{}");
            // Yedek klasörünün yerinde bir dosya var: klasör oluşturulamaz.
            var backups = Path.Combine(dir, SettingsVersion.BackupFolder);
            File.WriteAllText(backups, "dosya");
            var log = new List<string>();
            Assert.Null(SettingsVersion.BackupBeforeChange(settings, backups, SettingsVersion.Check("2.1.1", "2.2.0"), "2.2.0", At, log.Add));
            Assert.Single(log);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Prune_on_disk_keeps_three_and_touches_nothing_else()
    {
        var dir = TempDir();
        try
        {
            string[] versions =
            [
                "before-2.1.1-from-eski-20260901-100000.json",
                "before-2.2.0-from-2.1.1-20261001-100000.json",
                "before-2.2.1-from-2.2.0-20261020-100000.json",
                "before-2.3.0-from-2.2.1-20261101-100000.json",
                "before-2.2.1-from-2.3.0-20261102-100000.json",
            ];
            string[] others = ["settings-2026-10-01.json", "settings-2026-10-02.json", "before-notlarim.json", "notlar.txt"];
            foreach (var name in versions.Concat(others)) File.WriteAllText(Path.Combine(dir, name), name);

            Assert.Equal(2, SettingsVersion.PruneBackups(dir, justTaken: versions[4]));
            var left = Directory.GetFiles(dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
            Assert.Equal(versions[2..].Concat(others).Order(StringComparer.Ordinal), left);
            Assert.All(others, name => Assert.Equal(name, File.ReadAllText(Path.Combine(dir, name))));

            Assert.Equal(0, SettingsVersion.PruneBackups(Path.Combine(dir, "yok"), justTaken: null));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Clearing_the_key_rewrites_only_version_backups_and_keeps_the_rest()
    {
        var dir = TempDir();
        try
        {
            var original = new AppSettings
            {
                Paused = true,
                AiKeyProtected = "QUJDREVGR0g=",
                SavedWithAppVersion = "2.2.0",
                Widgets = [new WidgetConfig { Id = Guid.NewGuid().ToString("N"), Kind = WidgetKind.Note, NoteText = "AiKeyProtected not ç" }],
            };
            var json = JsonFile.Serialize(original);
            var withKey = "before-2.2.0-from-2.1.1-20261001-100000.json";
            var withoutKey = "before-2.2.1-from-2.2.0-20261020-100000.json";
            var broken = "before-2.2.2-from-2.2.1-20261021-100000.json";
            File.WriteAllBytes(Path.Combine(dir, withKey), json);
            var keyless = JsonFile.Serialize(new AppSettings { Paused = true });
            File.WriteAllBytes(Path.Combine(dir, withoutKey), keyless);
            File.WriteAllText(Path.Combine(dir, broken), "{ \"AiKeyProtected\": \"QUJD\", yarım");
            File.WriteAllBytes(Path.Combine(dir, "settings-2026-10-01.json"), json);      // günlük yedek: kendi sırasıyla döner
            File.WriteAllBytes(Path.Combine(dir, "before-notlarim.json"), json);          // kullanıcının dosyası

            Assert.Equal(2, SettingsVersion.ClearPropertyInBackups(dir, nameof(AppSettings.AiKeyProtected)));

            var scrubbed = File.ReadAllText(Path.Combine(dir, withKey));
            Assert.DoesNotContain("QUJDREVGR0g=", scrubbed);
            var restored = JsonFile.Load(Path.Combine(dir, withKey), () => new AppSettings());
            Assert.Null(restored.AiKeyProtected);
            Assert.True(restored.Paused);
            Assert.Equal("2.2.0", restored.SavedWithAppVersion);
            Assert.Equal("AiKeyProtected not ç", Assert.Single(restored.Widgets).NoteText);   // notun içindeki aynı söz kalır
            Assert.Equal(original.Widgets[0].Id, restored.Widgets[0].Id);

            Assert.Equal(keyless, File.ReadAllBytes(Path.Combine(dir, withoutKey)));      // anahtarsız yedek değişmez
            Assert.False(File.Exists(Path.Combine(dir, broken)));                           // temizlenemeyen yedek silinir
            Assert.Equal(json, File.ReadAllBytes(Path.Combine(dir, "settings-2026-10-01.json")));
            Assert.Equal(json, File.ReadAllBytes(Path.Combine(dir, "before-notlarim.json")));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));

            // İkinci kez: yapılacak bir şey yok.
            Assert.Equal(0, SettingsVersion.ClearPropertyInBackups(dir, nameof(AppSettings.AiKeyProtected)));
            Assert.Equal(0, SettingsVersion.ClearPropertyInBackups(Path.Combine(dir, "yok"), nameof(AppSettings.AiKeyProtected)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Startup_backs_up_settings_before_anything_can_save_them()
    {
        // AppHost.Initialize'ın sırası (arayüzsüz sınanamaz): yükle → sürüm yedeği → kayıt deposu ve ilk kayıt. Yedek kayıttan
        // sonraya kayarsa daha eski sürüm, tanımadığı enum değerlerini yedek alınmadan ezerdi.
        // Satır sonları çalışma kopyasına göre CRLF ya da LF olabilir (core.autocrlf).
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "AppHost.cs")).Replace("\r\n", "\n");
        var start = text.IndexOf("public static void Initialize(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var body = text[start..text.IndexOf("\n    }\n", start, StringComparison.Ordinal)];
        var load = body.IndexOf("JsonFile.Load(SettingsPath", StringComparison.Ordinal);
        var backup = body.IndexOf("SettingsVersion.BackupBeforeChange(SettingsPath", StringComparison.Ordinal);
        var store = body.IndexOf("new SettingsStore(", StringComparison.Ordinal);
        var stamp = body.IndexOf("Settings.SavedWithAppVersion = AppInfo.Version", StringComparison.Ordinal);
        var firstSave = body.IndexOf("SaveSettings", StringComparison.Ordinal);
        Assert.True(load >= 0 && backup > load, "yedek, dosya yüklendikten sonra alınmalı");
        Assert.True(store > backup && firstSave > backup && stamp > backup, "yedek, ilk kayıttan ve damgadan önce alınmalı");
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
