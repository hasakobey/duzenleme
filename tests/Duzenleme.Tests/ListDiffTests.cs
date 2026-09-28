using System.Collections.ObjectModel;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public class ListDiffTests
{
    private sealed class Item(string name)
    {
        public string Name { get; } = name;
        public override string ToString() => Name;
    }

    private static List<Item> Items(params string[] names) => names.Select(n => new Item(n)).ToList();

    private static void AssertSame(IReadOnlyList<Item> expected, IList<Item> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) Assert.Same(expected[i], actual[i]);
    }

    [Fact]
    public void Unchanged_list_needs_no_steps()
    {
        var list = Items("a", "b", "c");
        Assert.Empty(ListDiff.Plan(list, list.ToList()));
    }

    [Fact]
    public void New_file_is_one_insert_and_others_stay_in_place()
    {
        var current = Items("a", "c");
        var added = new Item("b");
        var desired = new List<Item> { current[0], added, current[1] };

        var steps = ListDiff.Plan(current, desired);

        var step = Assert.Single(steps);
        Assert.Equal(ListStepKind.Insert, step.Kind);
        Assert.Equal(1, step.Index);
    }

    [Fact]
    public void Deleted_file_is_one_remove()
    {
        var current = Items("a", "b", "c");
        var step = Assert.Single(ListDiff.Plan(current, [current[0], current[2]]));
        Assert.Equal(ListStepKind.Remove, step.Kind);
        Assert.Equal(1, step.Index);
    }

    [Fact]
    public void Renamed_file_that_sorts_elsewhere_is_one_move()
    {
        var current = Items("a", "b", "c", "d");
        var desired = new List<Item> { current[1], current[2], current[0], current[3] };

        var steps = ListDiff.Plan(current, desired);
        var target = new ObservableCollection<Item>(current);
        ListDiff.Apply(target, steps, target.Move);

        AssertSame(desired, target);
        Assert.InRange(steps.Count, 1, 2);
    }

    [Fact]
    public void Random_changes_always_produce_the_desired_list()
    {
        var random = new Random(2026);
        for (var trial = 0; trial < 300; trial++)
        {
            var pool = Enumerable.Range(0, random.Next(0, 40)).Select(i => new Item($"i{i}")).ToList();
            var current = pool.Where(_ => random.Next(3) > 0).OrderBy(_ => random.Next()).ToList();
            var desired = pool.Where(_ => random.Next(3) > 0).OrderBy(_ => random.Next()).ToList();
            desired.AddRange(Enumerable.Range(0, random.Next(0, 5)).Select(i => new Item($"yeni{i}")));

            var viaMove = new ObservableCollection<Item>(current);
            ListDiff.Apply(viaMove, ListDiff.Plan(current, desired), viaMove.Move);
            AssertSame(desired, viaMove);

            var viaList = new List<Item>(current);
            ListDiff.Apply(viaList, ListDiff.Plan(current, desired));
            AssertSame(desired, viaList);
        }
    }
}
