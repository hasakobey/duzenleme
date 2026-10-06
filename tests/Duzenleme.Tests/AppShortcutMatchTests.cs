using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// "Uygulama eklenince masaüstündeki kısayolu da kalksın": masaüstündeki kısayol yalnızca kesin eşleşmeyle (Uygulamalar
/// klasöründeki öğe, aynı uygulama kimliği, aynı hedef ve bağımsız değişkenler) uygulamanın sayılır; adla asla. Saf
/// (<see cref="AppShortcutMatch"/>); kabuktan okuma <c>Desktop/ShortcutFacts</c>'tadır.
/// </summary>
public class AppShortcutMatchTests
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    private static AppInfoFacts App(string aumid, string? target = null, string? args = null, string? known = null) =>
        new(aumid, target, args, known);

    [Fact]
    public void Same_aumid_matches()
    {
        Assert.True(AppShortcutMatch.Matches(App("Chrome"), new ShortcutInfo(Chrome, "", "Chrome", null)));
        Assert.True(AppShortcutMatch.Matches(App("MSEdge"), new ShortcutInfo(null, "--profile-directory=Default", "msedge", null)));
        Assert.False(AppShortcutMatch.Matches(App("Chrome"), new ShortcutInfo(null, null, "Chrome.Beta", null)));
    }

    [Fact]
    public void AppsFolder_idlist_matches()
    {
        const string store = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        Assert.True(AppShortcutMatch.Matches(App(store), new ShortcutInfo(null, null, null, StartApps.Prefix + store)));
        Assert.False(AppShortcutMatch.Matches(App(store), new ShortcutInfo(null, null, null, StartApps.Prefix + "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")));
    }

    [Fact]
    public void Same_target_and_args_match()
    {
        Assert.True(AppShortcutMatch.Matches(App("Notepad++", @"C:\Program Files\Notepad++\notepad++.exe"),
            new ShortcutInfo(@"C:\PROGRAM FILES\Notepad++\notepad++.exe", "", null, null)));
        Assert.True(AppShortcutMatch.Matches(App("Tool", @"C:\Tools\tool.exe", "--fast"), new ShortcutInfo(@"C:\Tools\tool.exe", " --fast ", null, null)));
    }

    [Fact]
    public void Known_folder_aumid_expands()
    {
        const string aumid = @"{6D809377-6AF0-444B-8957-A3773F02200E}\Google\Chrome\Application\chrome.exe";
        var expanded = AppShortcutMatch.ExpandKnownFolder(aumid, id => id == new Guid("6D809377-6AF0-444B-8957-A3773F02200E") ? @"C:\Program Files" : null);
        Assert.Equal(Chrome, expanded);
        Assert.True(AppShortcutMatch.Matches(App(aumid, known: expanded), new ShortcutInfo(Chrome, null, null, null)));

        Assert.Null(AppShortcutMatch.ExpandKnownFolder("Chrome", _ => @"C:\Program Files"));
        Assert.Null(AppShortcutMatch.ExpandKnownFolder(aumid, _ => null));
        Assert.Null(AppShortcutMatch.ExpandKnownFolder(@"{6D809377-6AF0-444B-8957-A3773F02200E}\..\Windows\cmd.exe", _ => @"C:\Program Files"));
    }

    [Fact]
    public void Different_args_do_not_match()
    {
        // Chrome'un profil kısayolu başka bir kısayoldur.
        Assert.False(AppShortcutMatch.Matches(App("x", Chrome), new ShortcutInfo(Chrome, "--profile-directory=\"Profile 2\"", null, null)));
        Assert.False(AppShortcutMatch.Matches(App("x", Chrome, "--incognito"), new ShortcutInfo(Chrome, "", null, null)));
    }

    [Fact]
    public void Name_only_never_matches()
    {
        // Adı aynı ama hedefi başka (ya da hiç bilgi yok): eşleşmez.
        Assert.False(AppShortcutMatch.Matches(App("Chrome", Chrome), new ShortcutInfo(@"C:\Users\ali\Downloads\Chrome.exe", null, null, null)));
        Assert.False(AppShortcutMatch.Matches(App("Chrome"), new ShortcutInfo(null, null, null, null)));
        Assert.False(AppShortcutMatch.Matches(App(""), new ShortcutInfo(null, null, "", "")));
        // Göreli ya da bozuk hedef karşılaştırılmaz.
        Assert.False(AppShortcutMatch.Matches(App("x", "chrome.exe"), new ShortcutInfo("chrome.exe", null, null, null)));
    }

    [Fact]
    public void Only_direct_desktop_children_are_candidates()
    {
        DirEntry Entry(string path, FileAttributes attributes = FileAttributes.Normal) =>
            new(path, Path.GetFileName(path), (attributes & FileAttributes.Directory) != 0, attributes, DateTime.UtcNow, 1);
        DirEntry[] entries =
        [
            Entry($@"{Desktop}\Chrome.lnk"),
            Entry($@"{Public}\Edge.lnk"),
            Entry($@"{Desktop}\rapor.pdf"),
            Entry($@"{Desktop}\Gizli.lnk", FileAttributes.Hidden),
            Entry($@"{Desktop}\Kutuda.lnk"),
            Entry($@"{Desktop}\Klasör.lnk", FileAttributes.Directory),
            Entry($@"{Desktop}\Alt\İç.lnk"),
        ];
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $@"{Desktop}\kutuda.lnk" };
        Assert.Equal([$@"{Desktop}\Chrome.lnk", $@"{Public}\Edge.lnk"], AppShortcutMatch.Candidates(entries, [Desktop, Public], excluded));
    }

    [Fact]
    public void Public_needs_opt_in_only_for_moving()
    {
        // Eşleşme ortak masaüstünde de bulunur; taşıma için izin BoxPlan.Decide'dadır (bölme kipinde yalnızca gizlenir).
        Assert.Equal(BoxLinkReason.PublicDesktop,
            BoxPlan.Decide($@"{Public}\Edge.lnk", FileAttributes.Normal, Desktop, [Public], includePublic: false));
        Assert.Equal(BoxLinkReason.None, BoxPlan.Decide($@"{Public}\Edge.lnk", FileAttributes.Normal, Desktop, [Public], includePublic: true));
    }
}
