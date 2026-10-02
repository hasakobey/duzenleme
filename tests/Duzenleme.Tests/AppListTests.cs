using System.IO;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Uygulama seç"in saf listesi (<see cref="AppList"/>): geçersiz ve adsız uygulamalar atılır, aynı kimlik (AUMID) bir kez
/// kalır, ada göre arayüz dilinde sıralanır; arama arayüz dilinin kurallarıyla harf ve aksan farkı gözetmez, Türkçe harfler
/// yazılmadan da bulunur. Kutuya yazılan yol <see cref="StartApps.PathFor"/>.
/// </summary>
public class AppListTests
{
    private static StartApp App(string id, string name) => new(id, name);

    [Fact]
    public void Prepare_drops_invalid_ids_and_blank_names()
    {
        var apps = AppList.Prepare(
        [
            App("", "Boş kimlik"),
            App("   ", "Boşluk"),
            App(new string('a', StartApps.MaxId + 1), "Çok uzun"),
            App("abc\u0001def", "Denetim karakteri"),
            App("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "  "),
            App("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Hesap Makinesi"),
        ]);

        var app = Assert.Single(apps);
        Assert.Equal("Hesap Makinesi", app.Name);
        Assert.Equal(StartApps.Prefix + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", app.Path);
    }

    [Fact]
    public void Prepare_keeps_one_entry_per_aumid_ignoring_case_and_trims()
    {
        var apps = AppList.Prepare(
        [
            App(" Microsoft.Office.WINWORD.EXE.15 ", " Word "),
            App("microsoft.office.winword.exe.15", "Word (2)"),
            App("Chrome", "Google Chrome"),
        ]);

        Assert.Equal(["Google Chrome", "Word"], apps.Select(a => a.Name));
        Assert.Equal("Microsoft.Office.WINWORD.EXE.15", apps.Single(a => a.Name == "Word").Id);
        Assert.Equal(StartApps.Prefix + "Microsoft.Office.WINWORD.EXE.15", apps.Single(a => a.Name == "Word").Path);
    }

    [Fact]
    public void Prepare_sorts_by_name_in_the_ui_language()
    {
        StartApp[] apps = [App("z", "Zoom"), App("c2", "Çay Saati"), App("c1", "Cz"), App("a", "ayarlar")];

        // Türkçede Ç, C'den sonra gelen ayrı bir harftir; İngilizcede C'nin aksanlısı sayılır.
        Assert.Equal(["ayarlar", "Cz", "Çay Saati", "Zoom"], AppList.Prepare(apps).Select(a => a.Name));
        using var _ = L.Use(Lang.En);
        Assert.Equal(["ayarlar", "Çay Saati", "Cz", "Zoom"], AppList.Prepare(apps).Select(a => a.Name));
    }

    private static readonly IReadOnlyList<StartApp> Sample = AppList.Prepare(
    [
        App("1", "İşlem Merkezi"),
        App("2", "Microsoft Word"),
        App("3", "Işık Ayarları"),
        App("4", "Résumé Builder"),
        App("5", "Settings"),
        App("6", "Not Defteri"),
    ]);

    private static string[] Find(string? query) => AppList.Filter(Sample, query).Select(a => a.Name).ToArray();

    [Fact]
    public void Empty_query_shows_everything_in_order()
    {
        Assert.Same(Sample, AppList.Filter(Sample, null));
        Assert.Same(Sample, AppList.Filter(Sample, "   "));
    }

    [Fact]
    public void Turkish_search_ignores_case_accents_and_turkish_letters()
    {
        Assert.Equal(["İşlem Merkezi"], Find("işlem"));
        Assert.Equal(["İşlem Merkezi"], Find("İŞLEM"));
        Assert.Equal(["İşlem Merkezi"], Find("islem"));
        Assert.Equal(["Işık Ayarları"], Find("ışık"));
        Assert.Equal(["Işık Ayarları"], Find("isik"));
        Assert.Equal(["Résumé Builder"], Find("resume"));
        Assert.Equal(["Microsoft Word"], Find("WORD"));
        Assert.Empty(Find("excel"));
    }

    [Fact]
    public void English_search_ignores_case_and_accents()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal(["Settings"], Find("SETT"));
        Assert.Equal(["Résumé Builder"], Find("resume"));
        Assert.Equal(["Microsoft Word"], Find("word"));
        // Türkçe harfler İngilizce arayüzde de katlanarak bulunur.
        Assert.Equal(["İşlem Merkezi"], Find("islem"));
    }

    [Fact]
    public void Every_word_of_the_query_must_match()
    {
        Assert.Equal(["Microsoft Word"], Find("mic wor"));
        Assert.Equal(["Microsoft Word"], Find("  word   micro "));
        Assert.Empty(Find("word defteri"));
        Assert.Equal(["Not Defteri"], Find("not def"));
    }

    [Fact]
    public void Added_apps_are_found_by_their_box_path_ignoring_case()
    {
        var word = Sample.Single(a => a.Name == "Microsoft Word");
        Assert.True(AppList.IsAdded(word, [StartApps.Prefix + "2"]));
        Assert.True(AppList.IsAdded(word, [(StartApps.Prefix + "2").ToUpperInvariant()]));
        Assert.False(AppList.IsAdded(word, [@"C:\Program Files\Word.lnk", StartApps.Prefix + "3"]));
        Assert.False(AppList.IsAdded(App("", "Bozuk"), [StartApps.Prefix]));
    }

    /// <summary>
    /// Başlat menüsü yalnızca arka plandaki STA iş parçacığında okunur (arayüz donmasın); pencere kabuğa kendisi dokunmaz ve
    /// beklerken dönen halka göstermez (yalnızca durağan "Uygulamalar yükleniyor…").
    /// </summary>
    [Fact]
    public void Start_menu_is_read_on_a_background_sta_thread_and_the_picker_never_spins()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var apps = File.ReadAllText(Path.Combine(src, "Desktop", "StartMenuApps.cs"));
        Assert.Contains("ShellFileOperations.RunSta(() => found = Enumerate())", apps);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(apps, @"\bEnumerate\(\)").Count); // tanım + tek çağrı
        var picker = File.ReadAllText(Path.Combine(src, "Views", "AppPicker.cs"));
        Assert.Contains("StartMenuApps.RefreshAsync()", picker);
        Assert.DoesNotContain("SHCreateItem", picker);
        Assert.DoesNotContain("ProgressRing", picker);
        Assert.DoesNotContain("AppLog", picker);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }

    [Fact]
    public void Path_for_rejects_empty_long_and_control_ids()
    {
        Assert.Null(StartApps.PathFor(null));
        Assert.Null(StartApps.PathFor(""));
        Assert.Null(StartApps.PathFor("   "));
        Assert.Null(StartApps.PathFor(new string('a', StartApps.MaxId + 1)));
        Assert.Null(StartApps.PathFor("a\tb"));
        Assert.Equal(StartApps.Prefix + new string('a', StartApps.MaxId), StartApps.PathFor(new string('a', StartApps.MaxId)));
        Assert.Equal(StartApps.Prefix + "Chrome", StartApps.PathFor("  Chrome "));
        Assert.Null(App("a\u0000b", "x").Path);
        Assert.Equal("Chrome", StartApps.Id(StartApps.PathFor("Chrome")!));
    }
}
