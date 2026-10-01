using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kutu/halka sekmesinde yolların (Items) ve yol olmayan öğelerin (Actions, PathsBefore) tek sıralı listesi: birleştirme,
/// taşıma, ekleme, silme; 2.1'in Actions'ı bilmeden yol ekleyip silmesi; tanınmayan türün korunması.
/// </summary>
public class LauncherEntriesTests
{
    private static LauncherAction Act(string id, int before, string type = RingActionTypes.Url) =>
        new() { Id = id, Type = type, PathsBefore = before, Url = "https://example.com/" + id };

    private static string Describe(LauncherTab tab) =>
        string.Join(" ", LauncherEntries.Ordered(tab).Select(e => e.Path ?? e.Action!.Id.ToUpperInvariant()));

    [Fact]
    public void Merge_is_stable_and_clamps_positions()
    {
        var tab = new LauncherTab
        {
            Items = ["a", "b", "c"],
            Actions = [Act("y", 2), Act("x", 0), Act("z", 2), Act("w", 9), Act("v", -3)],
        };
        Assert.Equal("X V a b Y Z c W", Describe(tab));
        Assert.Equal(8, LauncherEntries.Count(tab));
    }

    [Fact]
    public void Paths_only_and_actions_only()
    {
        Assert.Equal("a b", Describe(new LauncherTab { Items = ["a", "b"] }));
        Assert.Equal("X Y", Describe(new LauncherTab { Items = [], Actions = [Act("x", 3), Act("y", 0)] }));
        Assert.Equal("", Describe(new LauncherTab { Items = [] }));
    }

    [Fact]
    public void Write_rewrites_items_and_positions()
    {
        var tab = new LauncherTab { Items = ["a", "b"], Actions = [Act("x", 0)] };
        var entries = LauncherEntries.Ordered(tab);   // X a b
        entries.Reverse();                            // b a X
        LauncherEntries.Write(tab, entries);
        Assert.Equal(new[] { "b", "a" }, tab.Items);
        Assert.Equal(2, Assert.Single(tab.Actions!).PathsBefore);
        Assert.Equal("b a X", Describe(tab));
    }

    [Fact]
    public void Move_between_paths_and_actions()
    {
        var tab = new LauncherTab { Items = ["a", "b", "c"], Actions = [Act("x", 1)] };   // a X b c
        Assert.True(LauncherEntries.Move(tab, 1, 2));      // hedef sıra, öğe çıkarıldıktan sonraki listede: a b | c
        Assert.Equal("a b X c", Describe(tab));
        Assert.Equal(2, tab.Actions![0].PathsBefore);

        Assert.True(LauncherEntries.Move(tab, 3, 0));      // c başa
        Assert.Equal("c a b X", Describe(tab));
        Assert.Equal(new[] { "c", "a", "b" }, tab.Items);
        Assert.Equal(3, tab.Actions![0].PathsBefore);

        Assert.False(LauncherEntries.Move(tab, 1, 1));     // yerinde
        Assert.False(LauncherEntries.Move(tab, 7, 0));     // olmayan sıra
        Assert.True(LauncherEntries.Move(tab, 0, 99));     // kısılır: sona
        Assert.Equal("a b X c", Describe(tab));
    }

    [Fact]
    public void Insert_and_remove()
    {
        var tab = new LauncherTab { Items = ["a"] };
        Assert.Equal(0, LauncherEntries.Insert(tab, 0, LauncherEntry.Of(Act("x", 0))));
        Assert.Equal(2, LauncherEntries.Insert(tab, 99, LauncherEntry.Of("b")));
        Assert.Equal(1, LauncherEntries.Insert(tab, 1, LauncherEntry.Of(Act("y", 0))));
        Assert.Equal("X Y a b", Describe(tab));
        Assert.Equal(1, LauncherEntries.IndexOfAction(tab, "y"));
        Assert.Equal(3, LauncherEntries.IndexOfPath(tab, "B"));
        Assert.Equal(-1, LauncherEntries.IndexOfPath(tab, "yok"));

        var removed = LauncherEntries.RemoveAt(tab, 0);
        Assert.Equal("x", removed!.Value.Action!.Id);
        Assert.Null(LauncherEntries.RemoveAt(tab, 5));
        LauncherEntries.RemoveAt(tab, 0);
        Assert.Null(tab.Actions);                          // eylem kalmadı: alan yazılmaz
        Assert.Equal("a b", Describe(tab));
        Assert.Throws<ArgumentException>(() => LauncherEntries.Insert(tab, 0, default));
    }

    [Fact]
    public void Move_to_another_tab()
    {
        var root = new LauncherTab { Items = ["a", "b"], Actions = [Act("x", 1)] };  // a X b
        var sub = new LauncherTab { Items = ["c"] };
        Assert.True(LauncherEntries.MoveTo(root, 1, sub, 0));
        Assert.Equal("a b", Describe(root));
        Assert.Null(root.Actions);
        Assert.Equal("X c", Describe(sub));
        Assert.True(LauncherEntries.MoveTo(root, 1, sub, 99));
        Assert.Equal("X c b", Describe(sub));
        Assert.Equal(new[] { "a" }, root.Items);
        Assert.True(LauncherEntries.MoveTo(sub, 0, sub, 2));   // aynı sekme: Move
        Assert.Equal("c b X", Describe(sub));
    }

    [Fact]
    public void Paths_removed_by_21_shift_actions_but_lose_none()
    {
        // 2.2: a b X c d Y. 2.1 Actions'ı bilmez: "a" ve "c"yi siler, "e"yi sona ekler.
        var tab = new LauncherTab { Items = ["a", "b", "c", "d"], Actions = [Act("x", 2), Act("y", 4)] };
        tab.Items = ["b", "d", "e"];
        Assert.Equal("b d X e Y", Describe(tab));            // X en çok silinen yol sayısı kadar kaydı; Y sona kısıldı
        Assert.Equal(5, LauncherEntries.Count(tab));

        // 2.1 hepsini silse de eylemler durur.
        tab.Items = [];
        Assert.Equal("X Y", Describe(tab));
    }

    [Fact]
    public void Paths_added_by_21_go_after_existing_actions()
    {
        var tab = new LauncherTab { Items = ["a", "b"], Actions = [Act("x", 2)] };   // a b X
        tab.Items = ["a", "b", "c"];                                                  // 2.1 kutuya ekledi (sona)
        Assert.Equal("a b X c", Describe(tab));
    }

    [Fact]
    public void Unknown_action_type_and_fields_are_kept_through_edits()
    {
        var unknown = JsonSerializer.Deserialize<LauncherAction>("""{ "Id": "m", "Type": "macro", "Steps": [1, 2] }""", JsonFile.Options)!;
        var tab = new LauncherTab { Items = ["a"], Actions = [unknown] };
        LauncherEntries.Move(tab, 0, 1);
        Assert.Same(unknown, Assert.Single(tab.Actions!));
        Assert.Equal("macro", unknown.Type);
        Assert.Equal(2, unknown.Extra!["Steps"].GetArrayLength());
        Assert.False(RingActions.IsRunnable(unknown));

        var json = JsonSerializer.Serialize(tab, JsonFile.Options);
        Assert.Contains("\"macro\"", json);
        Assert.Contains("\"Steps\"", json);
    }
}
