using Duzenleme.Core;

namespace Duzenleme.Tests;

public class RemovedWidgetsTests
{
    private static RemovedWidget R(string id, int index = 0) => new(new WidgetConfig { Id = id }, index, false);

    [Fact]
    public void Each_undo_brings_back_its_own_widget()
    {
        // Bildirim açıkken başka bir widget kaldırıldı: bildirimdeki "Geri al" yine ilkini getirir.
        var removed = new RemovedWidgets();
        removed.Add(R("not"));
        removed.Add(R("saat"));

        Assert.Equal("not", removed.Take("not")?.Copy.Id);
        Assert.Equal("saat", removed.Latest?.Copy.Id);
        Assert.Null(removed.Take("not"));
        Assert.Equal("saat", removed.Take("saat")?.Copy.Id);
        Assert.Null(removed.Latest);
    }

    [Fact]
    public void Removing_again_keeps_only_the_newest_copy()
    {
        var removed = new RemovedWidgets();
        removed.Add(R("not", index: 1));
        removed.Add(R("saat"));
        removed.Add(R("not", index: 4));

        Assert.Equal(4, removed.Take("not")?.Index);
        Assert.Null(removed.Take("not"));
        Assert.Equal("saat", removed.Latest?.Copy.Id);
    }

    [Fact]
    public void Oldest_is_dropped_beyond_capacity()
    {
        var removed = new RemovedWidgets();
        for (var i = 0; i <= RemovedWidgets.Capacity; i++) removed.Add(R($"w{i}"));

        Assert.Null(removed.Take("w0"));
        Assert.NotNull(removed.Take("w1"));
        Assert.Equal($"w{RemovedWidgets.Capacity}", removed.Latest?.Copy.Id);
    }
}
