using System.Text;
using Duzenleme.Core;
using Duzenleme.Icons;

namespace Duzenleme.Tests;

/// <summary>2.1 ad geçişi: veri klasörü, "Windows ile başlat" değeri, eski program dosyaları, ortam değişkenleri, Store kimliği.</summary>
public class MigrationTests
{
    // ---------- Ortam değişkenleri ----------

    [Theory]
    [InlineData("yeni", "eski", "yeni")]
    [InlineData(null, "eski", "eski")]
    [InlineData("", "eski", "eski")]
    [InlineData("yeni", null, "yeni")]
    [InlineData(null, null, null)]
    public void Environment_prefers_new_name_and_falls_back_to_old(string? current, string? legacy, string? expected)
    {
        var values = new Dictionary<string, string?> { ["NESTDESK_DEBUGLOG"] = current, ["DUZENLEME_DEBUGLOG"] = legacy };
        Assert.Equal(expected, AppEnvironment.Pick("DEBUGLOG", name => values.GetValueOrDefault(name)));
    }

    // ---------- Veri klasörü (saf plan) ----------

    private const string Roaming = @"C:\Users\ali\AppData\Roaming";
    private const string Current = Roaming + @"\NestDesk";
    private const string Legacy = Roaming + @"\Duzenleme";

    /// <summary>Sahte dosya sistemi: var olan dosya/klasörler, bağlantı klasörleri ve dolu klasörler.</summary>
    private static DataFolderPlan Plan(bool packaged = false, string? dataOverride = null, string? portable = null,
        string[]? files = null, string[]? dirs = null, string[]? links = null, string[]? nonEmpty = null)
    {
        var fileSet = new HashSet<string>(files ?? [], StringComparer.OrdinalIgnoreCase);
        var dirSet = new HashSet<string>(dirs ?? [], StringComparer.OrdinalIgnoreCase);
        var linkSet = new HashSet<string>(links ?? [], StringComparer.OrdinalIgnoreCase);
        var fullSet = new HashSet<string>(nonEmpty ?? [], StringComparer.OrdinalIgnoreCase);
        return DataFolderLocator.Plan(dataOverride, portable, packaged, Roaming,
            fileSet.Contains, dirSet.Contains, linkSet.Contains, d => !fullSet.Contains(d));
    }

    [Fact]
    public void Override_and_portable_folders_are_used_as_is()
    {
        Assert.Equal(new DataFolderPlan(@"D:\test", DataFolderSource.Override), Plan(dataOverride: @"D:\test", dirs: [Legacy]));
        Assert.Equal(new DataFolderPlan(@"E:\NestDesk\data", DataFolderSource.Portable), Plan(portable: @"E:\NestDesk\data", dirs: [Legacy]));
    }

    [Fact]
    public void New_user_gets_new_folder()
    {
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current), Plan());
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current), Plan(packaged: true));
    }

    [Fact]
    public void Legacy_folder_is_moved_to_new_name()
    {
        var plan = Plan(files: [Legacy + @"\settings.json"], dirs: [Legacy]);
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Moved, Legacy), plan);
        // Ayar dosyası olmasa da (ör. yalnızca günlük ve yapay zekâ simgeleri) klasör bizimdir: taşınır.
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Moved, Legacy), Plan(dirs: [Legacy]));
        // Boş yeni klasör engel değil (Apply önce onu siler).
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Moved, Legacy), Plan(dirs: [Legacy, Current]));
    }

    [Fact]
    public void Already_migrated_user_keeps_new_folder()
    {
        // Eski klasör (ör. bir kez taşınamayıp sonra yeniden oluşan) kendi hâlinde kalır; ayarları olan yeni klasör kazanır.
        var plan = Plan(files: [Current + @"\settings.json", Legacy + @"\settings.json"], dirs: [Current, Legacy], nonEmpty: [Current, Legacy]);
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current), plan);
    }

    [Fact]
    public void Two_non_empty_folders_are_never_merged()
    {
        // Yeni klasörde ayar yok ama başka şeyler var: eski ayarlar yerinde kullanılır, hiçbir şey taşınmaz ya da silinmez.
        var plan = Plan(files: [Legacy + @"\settings.json"], dirs: [Current, Legacy], nonEmpty: [Current]);
        Assert.Equal(new DataFolderPlan(Legacy, DataFolderSource.Legacy), plan);
        // Eski klasörde ayar da yoksa yeni klasör.
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current), Plan(dirs: [Current, Legacy], nonEmpty: [Current]));
    }

    [Fact]
    public void Linked_legacy_folder_is_used_in_place()
    {
        var plan = Plan(files: [Legacy + @"\settings.json"], dirs: [Legacy], links: [Legacy]);
        Assert.Equal(new DataFolderPlan(Legacy, DataFolderSource.Legacy), plan);
    }

    [Fact]
    public void Store_edition_never_moves_and_reads_installer_settings_in_place()
    {
        Assert.Equal(new DataFolderPlan(Legacy, DataFolderSource.Legacy),
            Plan(packaged: true, files: [Legacy + @"\settings.json"], dirs: [Legacy]));
        // Eski klasörde ayar yoksa (ör. yalnızca yedekler) yeni klasör.
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current), Plan(packaged: true, dirs: [Legacy]));
        Assert.Equal(new DataFolderPlan(Current, DataFolderSource.Current),
            Plan(packaged: true, files: [Current + @"\settings.json", Legacy + @"\settings.json"], dirs: [Current, Legacy]));
    }

    // ---------- Veri klasörü (gerçek diskte) ----------

    [Fact]
    public void Legacy_folder_is_renamed_on_disk_with_its_contents()
    {
        using var temp = new TempFolder();
        var (current, legacy) = DataFolderLocator.Paths(temp.Path);
        Directory.CreateDirectory(Path.Combine(legacy, "ai-icons"));
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(legacy, "ai-icons", "a.svg"), "<svg/>");
        Directory.CreateDirectory(current);   // boş yeni klasör (ör. başka bir şeyin oluşturduğu) engel olmamalı

        var logs = new List<string>();
        var result = DataFolderLocator.Apply(DataFolderLocator.Plan(null, null, false, temp.Path), log: logs.Add);

        Assert.Equal(new DataFolderPlan(current, DataFolderSource.Moved), result);
        Assert.False(Directory.Exists(legacy));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(current, "settings.json")));
        Assert.True(File.Exists(Path.Combine(current, "ai-icons", "a.svg")));
        Assert.Single(logs);

        // Sonraki açılış: taşınmış klasör olduğu gibi kullanılır.
        Assert.Equal(new DataFolderPlan(current, DataFolderSource.Current), DataFolderLocator.Plan(null, null, false, temp.Path));
    }

    [Fact]
    public void Locked_legacy_folder_is_used_for_this_session()
    {
        using var temp = new TempFolder();
        var (current, legacy) = DataFolderLocator.Paths(temp.Path);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{}");

        var result = DataFolderLocator.Apply(DataFolderLocator.Plan(null, null, false, temp.Path),
            move: (_, _) => throw new IOException("Gezgin penceresi klasörün içinde açık"));

        Assert.Equal(new DataFolderPlan(legacy, DataFolderSource.MoveFailed), result);
        Assert.True(File.Exists(Path.Combine(legacy, "settings.json")));
        // Sonraki açılışta yeniden denenir.
        Assert.Equal(DataFolderSource.Moved, DataFolderLocator.Plan(null, null, false, temp.Path).Source);
        Assert.False(File.Exists(Path.Combine(current, "settings.json")));
    }

    [Fact]
    public void Folder_with_open_file_is_not_half_moved()
    {
        // Gerçek kilit: içindeki dosya paylaşımsız açıkken klasör yeniden adlandırılamaz ya da (Windows izin verirse) bütünüyle
        // taşınır; iki durumda da ayarlar tek bir yerde, eksiksiz kalır.
        using var temp = new TempFolder();
        var (current, legacy) = DataFolderLocator.Paths(temp.Path);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{\"Paused\":true}");
        DataFolderPlan result;
        using (new FileStream(Path.Combine(legacy, "journal.json"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            result = DataFolderLocator.Apply(DataFolderLocator.Plan(null, null, false, temp.Path));

        Assert.Equal("{\"Paused\":true}", File.ReadAllText(Path.Combine(result.Directory, "settings.json")));
        Assert.True(result.Source is DataFolderSource.Moved or DataFolderSource.MoveFailed);
        Assert.NotEqual(Directory.Exists(current), Directory.Exists(legacy));
    }

    [Fact]
    public void Restore_mode_reads_settings_in_priority_order_without_touching_disk()
    {
        Assert.Equal([@"D:\data\settings.json"], DataFolderLocator.SettingsCandidates(@"D:\data", null, Roaming));
        Assert.Equal([@"E:\p\data\settings.json", Current + @"\settings.json", Legacy + @"\settings.json"],
            DataFolderLocator.SettingsCandidates(null, @"E:\p\data", Roaming));
        Assert.Equal([Current + @"\settings.json", Legacy + @"\settings.json"], DataFolderLocator.SettingsCandidates(null, null, Roaming));
    }

    [Theory]
    [InlineData("{ \"IconsHiddenByApp\": true, \"Paused\": false }", true)]
    [InlineData("{\"IconsHiddenByApp\":false}", false)]
    [InlineData("{\"Paused\":true}", false)]
    [InlineData("{ bozuk", false)]
    [InlineData("[]", false)]
    [InlineData("", false)]
    public void Uninstall_restores_icons_only_when_settings_say_app_hid_them(string json, bool expected) =>
        Assert.Equal(expected, LegacyFiles.IconsLeftHidden(json));

    // ---------- "Windows ile başlat" değeri ----------

    private const string Dir = @"C:\Users\ali\AppData\Local\Programs\NestDesk";
    private const string Exe = Dir + @"\NestDesk.exe";
    private const string OldCommand = "\"" + Dir + "\\Duzenleme.exe\" --minimized";
    private const string NewCommand = "\"" + Exe + "\" --minimized";
    private const string Portable = "\"E:\\Araclar\\NestDesk\\NestDesk.exe\" --minimized";

    [Theory]
    [InlineData("\"C:\\A B\\NestDesk.exe\" --minimized", "C:\\A B\\NestDesk.exe")]
    [InlineData("C:\\A B\\NestDesk.exe --minimized", "C:\\A B\\NestDesk.exe")]
    [InlineData("C:\\AB\\Duzenleme.EXE", "C:\\AB\\Duzenleme.EXE")]
    [InlineData("  \"C:\\x.exe\"", "C:\\x.exe")]
    [InlineData("\"C:\\yarım", "C:\\yarım")]
    [InlineData("C:\\tool\\run -x", "C:\\tool\\run")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("\"\"", null)]
    public void Exe_path_is_read_from_run_command(string? command, string? expected) =>
        Assert.Equal(expected, RunValueMigration.ExePath(command));

    [Theory]
    [InlineData(OldCommand, true)]
    [InlineData(NewCommand, true)]
    [InlineData("\"c:\\users\\ALI\\appdata\\local\\programs\\nestdesk\\NestDesk.exe\"", true)]
    [InlineData(Portable, false)]
    [InlineData("\"" + Dir + "\\sub\\NestDesk.exe\"", false)]
    [InlineData("\"" + Dir + "X\\NestDesk.exe\"", false)]
    [InlineData(null, false)]
    public void Value_is_ours_when_it_points_into_our_folder(string? command, bool expected)
    {
        Assert.Equal(expected, RunValueMigration.PointsInto(command, Dir));
        Assert.Equal(expected, RunValueMigration.PointsInto(command, Dir + "\\"));
    }

    [Fact]
    public void Command_matches_installer_format() => Assert.Equal(NewCommand, RunValueMigration.Command(Exe));

    [Theory]
    [InlineData(null, false)]                       // kayıt yok: açık
    [InlineData(new byte[0], false)]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, false)]  // Görev Yöneticisi: etkin
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x03, 0, 0, 0 }, true)]   // devre dışı
    [InlineData(new byte[] { 0x07, 0, 0, 0 }, true)]
    public void Startup_approval_disabled_state(byte[]? approval, bool disabled) =>
        Assert.Equal(disabled, RunValueMigration.ApprovalDisabled(approval));

    [Fact]
    public void Nothing_to_do_without_our_values()
    {
        Assert.True(RunValueMigration.Plan(null, null, false, false, Exe).IsEmpty);
        Assert.True(RunValueMigration.Plan(NewCommand, null, true, false, Exe).IsEmpty);
        // Başka klasördeki (taşınabilir 2.0) eski değere dokunulmaz.
        Assert.True(RunValueMigration.Plan(null, Portable.Replace("NestDesk.exe", "Duzenleme.exe"), false, true, Exe).IsEmpty);
    }

    [Fact]
    public void Legacy_value_moves_to_new_name_with_task_manager_choice()
    {
        var plan = RunValueMigration.Plan(null, OldCommand, false, true, Exe);
        Assert.Equal(new RunValuePlan(WriteCurrent: true, DeleteLegacy: true, CopyApprovalToCurrent: true, DeleteLegacyApproval: true), plan);
        // Görev Yöneticisi'nde hiç değiştirilmemişse taşınacak seçim yok.
        Assert.Equal(new RunValuePlan(WriteCurrent: true, DeleteLegacy: true), RunValueMigration.Plan(null, OldCommand, false, false, Exe));
    }

    [Fact]
    public void Existing_new_value_keeps_its_own_choice()
    {
        var plan = RunValueMigration.Plan(NewCommand, OldCommand, true, true, Exe);
        Assert.Equal(new RunValuePlan(DeleteLegacy: true, DeleteLegacyApproval: true), plan);
        plan = RunValueMigration.Plan(NewCommand, OldCommand, false, true, Exe);
        Assert.Equal(new RunValuePlan(DeleteLegacy: true, CopyApprovalToCurrent: true, DeleteLegacyApproval: true), plan);
    }

    [Fact]
    public void New_name_owned_by_another_copy_is_not_overwritten()
    {
        // Başka klasördeki taşınabilir 2.1 "NestDesk" adını almış: eski adlı değer kalır ama yeni exe'yi gösterir.
        Assert.Equal(new RunValuePlan(RewriteLegacy: true), RunValueMigration.Plan(Portable, OldCommand, false, true, Exe));
        Assert.True(RunValueMigration.Plan(Portable, NewCommand, false, false, Exe).IsEmpty);
    }

    [Fact]
    public void Our_new_value_pointing_to_old_exe_is_fixed()
    {
        var stale = "\"" + Dir + "\\Duzenleme.exe\"";
        Assert.Equal(new RunValuePlan(WriteCurrent: true), RunValueMigration.Plan(stale, null, false, false, Exe));
    }

    // ---------- Eski program dosyaları (taşınabilir klasör) ----------

    [Fact]
    public void Old_portable_program_files_are_deleted_only_when_older()
    {
        var baseDir = @"E:\NestDesk";
        var existing = new HashSet<string>(LegacyFiles.ProgramFiles.Select(f => Path.Combine(baseDir, f)).SkipLast(1), StringComparer.OrdinalIgnoreCase);
        var current = new Version(2, 1, 0, 0);

        var files = LegacyFiles.ProgramFilesToDelete(baseDir, current, existing.Contains, _ => new Version(2, 0, 0, 0));
        Assert.Equal(existing.Order(), files.Order());
        Assert.Contains(Path.Combine(baseDir, "Duzenleme.exe"), files);

        Assert.Empty(LegacyFiles.ProgramFilesToDelete(baseDir, current, existing.Contains, _ => new Version(2, 1, 0, 0)));
        Assert.Empty(LegacyFiles.ProgramFilesToDelete(baseDir, current, existing.Contains, _ => null));
        Assert.Empty(LegacyFiles.ProgramFilesToDelete(baseDir, current, _ => false, _ => new Version(1, 0)));
    }

    [Fact]
    public void Legacy_program_files_match_installer_list() =>
        Assert.Equal(["Duzenleme.exe", "Duzenleme.dll", "Duzenleme.deps.json", "Duzenleme.runtimeconfig.json"], LegacyFiles.ProgramFiles);

    // ---------- Store: "Add a widget" girişi ----------

    [Theory]
    [InlineData("Hasako.NestDesk_abc123!AddWidget", "Hasako.NestDesk_abc123!NestDesk")]
    [InlineData("Hasako.NestDesk_abc123!addwidget", "Hasako.NestDesk_abc123!NestDesk")]
    [InlineData("Hasako.NestDesk_abc123!NestDesk", null)]
    [InlineData("AddWidget", null)]
    [InlineData("!AddWidget", null)]
    [InlineData(null, null)]
    public void Add_widget_entry_hands_over_to_main_app(string? current, string? expected) =>
        Assert.Equal(expected, PackagedApp.MainAppUserModelIdFor(current));

    // ---------- Yapay zekâ içeriğini bildirme ----------

    [Fact]
    public void Ai_report_link_opens_a_prefilled_issue_without_user_data()
    {
        var url = SupportLinks.ReportAiContent("2.1.0");
        Assert.StartsWith(AppInfo.IssuesUrl + "/new?title=", url);
        var uri = new Uri(url);
        Assert.Equal("https", uri.Scheme);
        var body = Uri.UnescapeDataString(url[(url.IndexOf("&body=", StringComparison.Ordinal) + 6)..]);
        Assert.Contains("NestDesk 2.1.0", body);
        Assert.DoesNotContain(" ", url);
    }

    // ---------- Klasör simgesi öneki ----------

    [Theory]
    [InlineData(".nestdesk-20260928101500.ico", true)]
    [InlineData(".duzenleme-20250101000000.ico", true)]
    [InlineData(".DUZENLEME-1.ICO", true)]
    [InlineData(@"C:\x\.nestdesk-1.ico", true)]
    [InlineData(".nestdesk-1.png", false)]
    [InlineData("folder.ico", false)]
    [InlineData("nestdesk-1.ico", false)]
    public void Own_icon_files_are_recognised_with_both_prefixes(string name, bool expected) =>
        Assert.Equal(expected, FolderIconService.IsOwnIconFile(name));

    [Fact]
    public void Remove_all_removes_only_our_icons()
    {
        using var temp = new TempFolder();
        var ours = Directory.CreateDirectory(Path.Combine(temp.Path, "PDF")).FullName;
        var legacy = Directory.CreateDirectory(Path.Combine(temp.Path, "Oyunlar")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(temp.Path, "Okul", "Ödevler")).FullName;
        var foreign = Directory.CreateDirectory(Path.Combine(temp.Path, "Başka")).FullName;
        var plain = Directory.CreateDirectory(Path.Combine(temp.Path, "Düz")).FullName;
        WriteIcon(ours, ".nestdesk-20260928120000.ico");
        WriteIcon(legacy, ".duzenleme-20250101120000.ico");
        WriteIcon(nested, ".nestdesk-20260928120001.ico");
        // Başka programın simgesi + bizden kalmış artık bir dosya: desktop.ini'ye dokunulmaz, yalnızca artık silinir.
        File.WriteAllText(Path.Combine(foreign, "desktop.ini"), "[.ShellClassInfo]\r\nIconResource=C:\\Windows\\System32\\shell32.dll,4\r\n", Encoding.Unicode);
        File.WriteAllBytes(Path.Combine(foreign, ".duzenleme-1.ico"), [0]);

        var (removed, failed) = FolderIconService.RemoveAllOwnIcons([temp.Path]);

        Assert.Equal(0, failed);
        Assert.Equal(new[] { foreign, legacy, nested, ours }.Order(), removed.Order());
        foreach (var folder in new[] { ours, legacy, nested })
        {
            Assert.False(File.Exists(Path.Combine(folder, "desktop.ini")), folder);
            Assert.Empty(Directory.GetFiles(folder));
        }
        Assert.Contains("shell32.dll", File.ReadAllText(Path.Combine(foreign, "desktop.ini"), Encoding.Unicode));
        Assert.False(File.Exists(Path.Combine(foreign, ".duzenleme-1.ico")));
        Assert.Empty(Directory.GetFiles(plain));
        // İkinci kez: yapılacak bir şey yok.
        Assert.Empty(FolderIconService.RemoveAllOwnIcons([temp.Path]).Removed);
    }

    [Fact]
    public void Remove_all_keeps_other_desktop_ini_settings()
    {
        using var temp = new TempFolder();
        var folder = Directory.CreateDirectory(Path.Combine(temp.Path, "Belgeler")).FullName;
        WriteIcon(folder, ".nestdesk-1.ico", extra: "LocalizedResourceName=@shell32.dll,-21770\r\n");
        FolderIconService.RemoveAllOwnIcons([temp.Path]);
        var ini = File.ReadAllText(Path.Combine(folder, "desktop.ini"), Encoding.Unicode);
        Assert.Contains("LocalizedResourceName", ini);
        Assert.DoesNotContain("IconResource", ini);
    }

    private static void WriteIcon(string folder, string icon, string extra = "")
    {
        File.WriteAllBytes(Path.Combine(folder, icon), [0, 0, 1, 0]);
        File.SetAttributes(Path.Combine(folder, icon), FileAttributes.Hidden | FileAttributes.System);
        File.WriteAllText(Path.Combine(folder, "desktop.ini"), $"[.ShellClassInfo]\r\nIconResource={icon},0\r\n{extra}", Encoding.Unicode);
        File.SetAttributes(Path.Combine(folder, "desktop.ini"), FileAttributes.Hidden | FileAttributes.System);
    }

    /// <summary>Test başına geçici klasör; sonunda silinir (gizli/sistem öznitelikleri önce kaldırılır).</summary>
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("nestdesk-gecis-").FullName;

        public void Dispose()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Test_instance_without_data_folder_never_touches_the_real_one()
    {
        // --desktop verilip --data unutulursa gerçek %AppData%\Duzenleme taşınmamalı (kurulu sürüm onu kullanıyor olabilir).
        var temp = @"C:\Users\ali\AppData\Local\Temp";
        var folder = DataFolderLocator.TestDataFallback(@"C:\t\masaüstü", null, appDataRootGiven: false, temp);
        Assert.NotNull(folder);
        Assert.StartsWith(Path.Combine(temp, "NestDesk-test-data") + @"\", folder);
        // Aynı test masaüstü hep aynı klasörü alır (büyük/küçük harf ve sondaki ayraç fark etmez), başkası başkasını.
        Assert.Equal(folder, DataFolderLocator.TestDataFallback(@"C:\T\MASAÜSTÜ\", null, false, temp));
        Assert.NotEqual(folder, DataFolderLocator.TestDataFallback(@"C:\t\başka", null, false, temp));

        Assert.Null(DataFolderLocator.TestDataFallback(null, null, false, temp));                      // gerçek örnek
        Assert.Null(DataFolderLocator.TestDataFallback(@"C:\t\masaüstü", @"C:\t\veri", false, temp));  // --data verildi
        Assert.Null(DataFolderLocator.TestDataFallback(@"C:\t\masaüstü", null, true, temp));           // geçiş denemesi
    }
}
