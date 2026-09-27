namespace Duzenleme.Core;

/// <summary>Widget'ın kullanıcıya görünen adı (Widget'lar listesi, tepsideki "Geri getir", bildirimler).</summary>
public static class WidgetText
{
    public static string DisplayName(WidgetConfig c) => c.Kind switch
    {
        WidgetKind.Clock => "Saat",
        WidgetKind.Date => "Tarih",
        WidgetKind.Note when c.NoteChecklist =>
            (string.IsNullOrWhiteSpace(c.Title) ? "Yapılacaklar" : c.Title) + Progress(c),   // " · 2/5" (madde yoksa "")
        WidgetKind.Note => "Not" + (!string.IsNullOrWhiteSpace(c.Title) ? " · " + c.Title
                                   : string.IsNullOrWhiteSpace(c.NoteText) ? "" : " · " + FirstLine(c.NoteText)),
        WidgetKind.Launcher => (c.Title is { } t ? $"Kısayol kutusu · {t}" : "Kısayol kutusu") + $" ({c.Tabs.Sum(x => x.Items.Count)} öğe)",
        _ => $"Bölme · {c.Title ?? (c.Filter != DesktopFilter.None ? DesktopItems.Label(c.Filter) : c.FolderName)}",
    };

    /// <summary>Yapılacaklar ilerlemesi: " · 2/5"; madde yoksa boş.</summary>
    private static string Progress(WidgetConfig c)
    {
        var (done, total) = ChecklistText.Progress(ChecklistText.Parse(c.NoteText));
        return total == 0 ? "" : $" · {done}/{total}";
    }

    /// <summary>Notun ilk dolu satırı; 40 karakteri aşarsa kesilip "…" eklenir.</summary>
    private static string FirstLine(string text)
    {
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        return line.Length > 40 ? line[..40] + "…" : line;
    }
}
