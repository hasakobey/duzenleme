using System.IO.Compression;
using System.Text;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Tanılama paketi: yalnızca beyaz listedeki dosyalar girer; ayar özetinde kullanıcının hiçbir metni yoktur.</summary>
public class DiagnosticPackageTests : IDisposable
{
    private readonly string _data = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-pkg-" + Guid.NewGuid().ToString("N"))).FullName;
    private string Logs => Path.Combine(_data, LogFiles.FolderName);
    private string Crashes => Path.Combine(Logs, CrashReports.FolderName);
    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    // Kullanıcının verisi: hiçbiri pakete, ozet.json'a ya da rapora girmemeli.
    private const string Note = "GIZLI-NOT-7f3a";
    private const string Title = "GIZLI-BASLIK-91c2";
    private const string Folder = "GIZLI-KLASOR-55de";
    private const string Pattern = "gizliuzanti";
    private const string Item = @"C:\Users\Zeynep\Desktop\GIZLI-OGE-0b1e.lnk";
    private const string Key = "GIZLI-ANAHTAR-c0ffee";
    private const string Tab = "GIZLI-SEKME-aa11";
    private const string ZoneLabel = "GIZLI-SEHIR-77";
    private const string Layout = "GIZLI-DUZEN-13";
    private static readonly string[] Secrets = [Note, Title, Folder, Pattern, "GIZLI-OGE-0b1e", Key, Tab, ZoneLabel, Layout, "Zeynep", "Ctrl+Alt+Q"];

    public void Dispose()
    {
        try { Directory.Delete(_data, true); }
        catch (IOException) { }
    }

    private static AppSettings UserSettings()
    {
        var s = new AppSettings
        {
            Rules = [new Rule { TargetFolder = Folder, Extensions = [Pattern] }, new Rule { TargetFolder = "PDF", Extensions = ["pdf"], Enabled = false }],
            AiKeyProtected = Key,
            Language = "en",
            CrashReportMode = "always",
            ErrorLogOff = true,
            IconFolders = [@"C:\Users\Zeynep\Desktop\" + Folder],
            DoubleClickAction = "peek",
            NewWidgetPlacement = Folder,
        };
        s.Hotkeys.QuickAdd = "Ctrl+Alt+Q";
        s.Hotkeys.PeekWidgets = "";
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Note, Title = Title, NoteText = Note });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true, NoteText = "☐ " + Note, Locked = true });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = Folder, Title = Title, HiddenItems = [Item], Scale = 1.5 });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = @"D:\" + Folder });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Shortcuts, ItemOrder = [Item] });
        s.Widgets.Add(new WidgetConfig
        {
            Kind = WidgetKind.Launcher,
            Tabs = [new LauncherTab { Name = Tab, Items = [Item, Item + "2"] }],
            ItemLooks = new() { [Item] = new ItemLook { Name = Title } },
            BoxFolder = Folder,
        });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.World, Zones = [new WorldZone { Id = "UTC", Label = ZoneLabel }] });
        s.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Date, Variant = "future-kind" });
        s.Layouts.Add(LayoutSnapshot.Capture(Layout, s.Widgets));
        return s;
    }

    private void Touch(string relative, string content = "{}")
    {
        var path = Path.Combine(_data, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void CreateDataFolder()
    {
        var secret = string.Join(" ", Secrets);
        foreach (var file in new[]
                 {
                     "settings.json", "settings.json.bak", "settings.json.tmp", "settings.json.bozuk-20260901-101010", "yedekler/settings-20260930.json",
                     "yedekler/before-2.2.0-from-2.1.1-x.json", "journal.json", "journal.json.bak", "box-moves.json", "icons/abc.png",
                     "ai-icons/x.svg", "links/github-1.url",
                     "logs/settings.json", "logs/notlar.txt", "logs/tanilama.html", "logs/session.json", "logs/nestdesk-20260920.jsonl",
                     "logs/sub/nestdesk-20261001.jsonl", "logs/nestdesk-20261001.jsonl.bak", "logs/crashes/pending-20261001-090000-000.json",
                     "logs/crashes/other.json", "logs/crashes/crash-notes.txt",
                 })
            Touch(file, secret);
        Touch("logs/nestdesk-20261001.jsonl", "{\"t\":\"2026-10-01T09:00:00.000Z\",\"l\":\"E\",\"a\":\"a\",\"m\":\"b\"}\n");
        Touch("logs/nestdesk-20260925-1.jsonl", "{}\n");
        CrashReports.Write(Crashes, CrashReportModes.Always, false, CrashReports.KindCrash, null, [], "abc123", "2.2.0", Now.AddHours(-1));
    }

    private DiagnosticPackage Plan(AppSettings? settings = null)
    {
        var snapshot = DiagnosticSnapshot.From(settings ?? UserSettings());
        var summary = snapshot.ToJson(new DiagnosticEnvironment { Channel = "test" }, PerfFacts.Capture(1234, null), Now);
        var html = DiagnosticReport.Render(new ReportInput { Snapshot = snapshot, NowUtc = Now });
        return DiagnosticPackage.Plan(Logs, Now, html, summary);
    }

    [Fact]
    public void Only_whitelisted_files_are_planned()
    {
        CreateDataFolder();
        var names = Plan().Entries.Select(e => e.Name).ToList();
        Assert.Equal(6, names.Count);
        Assert.Equal(["rapor.html", "ozet.json", "README.txt"], names.Take(3));
        Assert.Contains("logs/nestdesk-20261001.jsonl", names);
        Assert.Contains("logs/nestdesk-20260925-1.jsonl", names);
        Assert.Single(names, n => n.StartsWith("crashes/crash-", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal));
        // 11 gün önceki günlük, bekleyen rapor, ayarlar, yedekler, geçmiş, simgeler girmez.
        Assert.DoesNotContain(names, n => n.Contains("20260920") || n.Contains("pending") || n.Contains("settings") || n.Contains("journal")
                                         || n.Contains("box-moves") || n.Contains("icons") || n.Contains("bozuk") || n.Contains("yedekler")
                                         || n.Contains("session") || n.Contains("tanilama") || n.Contains("sub/") || n.Contains(".bak"));
        Assert.All(Plan().Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Label)));
        Assert.All(Plan().Entries, e => Assert.True(e.Size > 0));
    }

    [Fact]
    public void Zip_contains_exactly_the_plan_and_no_user_text()
    {
        CreateDataFolder();
        var plan = Plan();
        using var buffer = new MemoryStream();
        plan.Write(buffer);
        buffer.Position = 0;
        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        Assert.Equal(plan.Entries.Select(e => e.Name), zip.Entries.Select(e => e.FullName));
        foreach (var entry in zip.Entries)
        {
            Assert.DoesNotContain("..", entry.FullName);
            Assert.False(Path.IsPathRooted(entry.FullName));
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var text = reader.ReadToEnd();
            foreach (var secret in Secrets)
                Assert.True(text.IndexOf(secret, StringComparison.OrdinalIgnoreCase) < 0, $"{entry.FullName}: \"{secret}\"");
        }
        var readme = new StreamReader(zip.GetEntry("README.txt")!.Open()).ReadToEnd();
        Assert.Contains(AppInfo.Name, readme);
        Assert.Contains("TR", readme);
        Assert.Contains("EN", readme);
        Assert.DoesNotContain("{0}", readme);
    }

    [Fact]
    public void Preview_lists_every_entry_with_its_size_and_merges_rotated_files_of_a_day()
    {
        CreateDataFolder();
        Touch("logs/nestdesk-20261001-1.jsonl", "{}\n{}\n");
        var plan = Plan();
        var preview = plan.Preview();
        // Aynı günün iki dosyası tek satır; başka her öğe kendi satırında, plandaki sırayla.
        Assert.Equal(plan.Entries.Count - 1, preview.Count);
        Assert.Equal(plan.Entries.Select(e => e.Label).Distinct(), preview.Select(p => p.Label));
        Assert.Equal(plan.TotalSize, preview.Sum(p => p.Size));
        Assert.All(preview, p => Assert.True(p.Size > 0));
        Assert.Equal(L.T("Tanılama sayfası"), preview[0].Label);
    }

    [Fact]
    public void Missing_file_after_plan_is_skipped()
    {
        CreateDataFolder();
        var plan = Plan();
        File.Delete(Path.Combine(Logs, "nestdesk-20260925-1.jsonl"));
        using var buffer = new MemoryStream();
        plan.Write(buffer);
        buffer.Position = 0;
        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        Assert.Equal(plan.Entries.Count - 1, zip.Entries.Count);
    }

    [Fact]
    public void Plan_without_logs_folder_has_report_summary_and_readme()
    {
        Assert.Equal(["rapor.html", "ozet.json", "README.txt"], Plan().Entries.Select(e => e.Name));
    }

    [Fact]
    public void Summary_holds_only_flags_choices_counts_and_widget_types()
    {
        var snapshot = DiagnosticSnapshot.From(UserSettings());
        var json = Encoding.UTF8.GetString(snapshot.ToJson(null, null, Now));
        foreach (var secret in Secrets)
            Assert.True(json.IndexOf(secret, StringComparison.OrdinalIgnoreCase) < 0, secret);

        Assert.True(snapshot.Flags["AiKeyPresent"]);
        Assert.True(snapshot.Flags[nameof(AppSettings.ErrorLogOff)]);
        Assert.Equal("en", snapshot.Choices[nameof(AppSettings.Language)]);
        Assert.Equal("always", snapshot.Choices[nameof(AppSettings.CrashReportMode)]);
        Assert.Equal("peek", snapshot.Choices[nameof(AppSettings.DoubleClickAction)]);
        Assert.Equal("other", snapshot.Choices[nameof(AppSettings.NewWidgetPlacement)]);
        Assert.Equal("System", snapshot.Choices[nameof(AppSettings.Theme)]);
        Assert.Equal(2, snapshot.Counts["Rules"]);
        Assert.Equal(1, snapshot.Counts["RulesEnabled"]);
        Assert.Equal(8, snapshot.Counts["Widgets"]);
        Assert.Equal(1, snapshot.Counts["Layouts"]);
        Assert.Equal(6, snapshot.Counts["HotkeysAssigned"]);
        Assert.Equal(DesktopState.DefaultPeekMinutes, snapshot.Counts[nameof(AppSettings.PeekMinutes)]);

        var rows = snapshot.Widgets.ToDictionary(r => r.Kind + "/" + r.Type);
        Assert.Equal(1, rows["Note/note"].Count);
        Assert.Equal(1, rows["Note/checklist"].Locked);
        Assert.Equal(1.5, rows["Fence/folder"].ScaleMax);
        Assert.Equal(1, rows["Fence/portal"].Count);
        Assert.Equal(1, rows["Fence/desktop-shortcuts"].Count);
        Assert.Equal(2, rows["Launcher/box"].Items);
        Assert.Equal(1, rows["Launcher/box"].Tabs);
        Assert.Equal(1, rows["Clock/world"].Count);
        Assert.Equal(1, rows["Date/other"].Count);
    }

    [Fact]
    public void Defaults_snapshot()
    {
        var snapshot = DiagnosticSnapshot.From(new AppSettings());
        Assert.False(snapshot.Flags["AiKeyPresent"]);
        Assert.False(snapshot.Flags[nameof(AppSettings.ErrorLogOff)]);
        Assert.Equal("ask", snapshot.Choices[nameof(AppSettings.CrashReportMode)]);
        Assert.Equal("system", snapshot.Choices[nameof(AppSettings.Language)]);
        Assert.Empty(snapshot.Widgets);
    }

    [Fact]
    public void Suggested_file_name_is_neutral_and_dated()
    {
        Assert.Equal($"{AppInfo.Name}-diagnostics-20261001-1305.zip", DiagnosticPackage.SuggestedFileName(new DateTime(2026, 10, 1, 13, 5, 0)));
    }
}
