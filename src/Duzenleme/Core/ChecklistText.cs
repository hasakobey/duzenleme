namespace Duzenleme.Core;

public sealed record ChecklistItem(string Text, bool Done);

/// <summary>Yapılacaklar listesinin NoteText biçimi ("☐ madde" / "☑ madde", satır başına bir madde).</summary>
public static class ChecklistText
{
    public const string OpenMark = "☐ ", DoneMark = "☑ ";      // U+2610, U+2611 + boşluk

    // Uzun önekler önce denenir ("- [x]", "[x]"'ten önce). Yalnızca ilk eşleşen önek atılır.
    private static readonly (string Prefix, bool Done)[] Marks =
    [
        ("- [x]", true), ("- [X]", true), ("* [x]", true), ("* [X]", true),
        ("- [ ]", false), ("* [ ]", false),
        ("[x]", true), ("[X]", true), ("[ ]", false),
        ("☑", true), ("☒", true), ("☐", false),
    ];

    // Önek taşımayan satırlarda (düz nottan çevirme) atılan madde işaretleri.
    private static readonly string[] Bullets = ["• ", "- ", "* ", "– "];

    /// <summary>Boş satırlar atlanır. Önekler (baştaki boşluktan sonra, yalnızca ilki atılır):
    ///   biten: "☑", "☒", "[x]", "[X]", "- [x]", "* [x]" (büyük X de)
    ///   açık:  "☐", "[ ]", "- [ ]", "* [ ]"
    /// Öneksiz satır açık maddedir; baştaki "• ", "- ", "* ", "– " atılır. Önek ve metin kırpılır; metni boş kalan madde atlanır.</summary>
    public static List<ChecklistItem> Parse(string? text)
    {
        var items = new List<ChecklistItem>();
        if (string.IsNullOrEmpty(text)) return items;
        foreach (var raw in text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var done = false;
            var marked = false;
            foreach (var (prefix, isDone) in Marks)
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                line = line[prefix.Length..];
                done = isDone;
                marked = true;
                break;
            }
            if (!marked)
            {
                foreach (var bullet in Bullets)
                {
                    if (!line.StartsWith(bullet, StringComparison.Ordinal)) continue;
                    line = line[bullet.Length..];
                    break;
                }
            }

            line = line.Trim();
            if (line.Length > 0) items.Add(new ChecklistItem(line, done));
        }
        return items;
    }

    /// <summary>Boş metinli maddeler atlanır, madde içindeki \r\n boşluğa çevrilir, satırlar "\r\n" ile birleşir.</summary>
    public static string Format(IEnumerable<ChecklistItem> items) =>
        string.Join("\r\n", items
            .Select(i => (Text: Flatten(i.Text), i.Done))
            .Where(i => i.Text.Length > 0)
            .Select(i => (i.Done ? DoneMark : OpenMark) + i.Text));

    public static (int Done, int Total) Progress(IEnumerable<ChecklistItem> items)
    {
        int done = 0, total = 0;
        foreach (var item in items)
        {
            total++;
            if (item.Done) done++;
        }
        return (done, total);
    }

    private static string Flatten(string? text) =>
        (text ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
}
