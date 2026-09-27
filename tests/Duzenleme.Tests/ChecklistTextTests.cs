using Duzenleme.Core;

namespace Duzenleme.Tests;

public class ChecklistTextTests
{
    [Fact]
    public void Parse_reads_marks()
    {
        Assert.Equal([new("a", false), new("b", true), new ChecklistItem("c", true)], ChecklistText.Parse("☐ a\r\n☑ b\r\n☒ c"));
    }

    [Fact]
    public void Parse_accepts_markdown_boxes()
    {
        Assert.Equal([new("a", false), new("b", true), new ChecklistItem("c", true)], ChecklistText.Parse("- [ ] a\n* [x] b\n[X] c"));
    }

    [Fact]
    public void Parse_plain_lines_become_open_items_and_bullets_are_stripped()
    {
        Assert.Equal([new("Süt, ekmek", false), new ChecklistItem("Fatura", false)], ChecklistText.Parse("• Süt, ekmek\r\n\r\n- Fatura"));
    }

    [Fact]
    public void Parse_strips_only_the_first_mark()
    {
        Assert.Equal("☐ kutu", Assert.Single(ChecklistText.Parse("☐ ☐ kutu")).Text);
    }

    [Fact]
    public void Format_skips_empty_items_and_flattens_newlines()
    {
        Assert.Equal("☐ a b", ChecklistText.Format([new("a\nb", false), new(" ", true)]));
    }

    [Theory]
    [InlineData("☐ Süt al\r\n☑ Faturayı öde")]
    [InlineData("• Süt, ekmek\r\n\r\n• Faturayı öde")]
    [InlineData("- [x] bitti\n  ☐ ☐ iç içe\n[ ]\n– tire")]
    public void Format_parse_round_trip(string text)
    {
        var once = ChecklistText.Format(ChecklistText.Parse(text));

        Assert.Equal(once, ChecklistText.Format(ChecklistText.Parse(once)));
    }

    [Fact]
    public void Progress_counts_done_items()
    {
        Assert.Equal((2, 3), ChecklistText.Progress(ChecklistText.Parse("☑ a\r\n☐ b\r\n[x] c")));
        Assert.Equal((0, 0), ChecklistText.Progress(ChecklistText.Parse(null)));
    }
}
