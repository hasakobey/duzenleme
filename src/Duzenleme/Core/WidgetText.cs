namespace Duzenleme.Core;

/// <summary>
/// Widget'ın kullanıcıya görünen adı (Widget'lar listesi, tepsideki "Geri getir", bildirimler). Durağandır: saniyede,
/// dakikada değişen bir şey (kalan süre, saat) içermez; Widget'lar sayfası her kayıtta adları karşılaştırır.
/// </summary>
public static class WidgetText
{
    public static string DisplayName(WidgetConfig c) => WidgetVariants.Of(c) switch
    {
        WidgetVariants.Month => L.T("Takvim"),
        WidgetVariants.Countdown => string.IsNullOrWhiteSpace(c.Title) ? L.T("Geri sayım") : L.F("Geri sayım · {0}", c.Title.Trim()),
        WidgetVariants.Timer => TimerName(c.Timer),
        WidgetVariants.World => WorldName(c),
        WidgetVariants.System => L.T("Sistem durumu"),
        // Başlık 2.0 için yazılıdır (varsayılan ad, oluşturulduğu dilde); varsayılansa arayüz dilinde, değiştirildiyse kendisi.
        WidgetVariants.Recycle => string.IsNullOrWhiteSpace(c.Title) || L.Variants("Geri Dönüşüm Kutusu").Contains(c.Title.Trim())
            ? L.T("Geri Dönüşüm Kutusu") : c.Title.Trim(),
        _ => c.Kind switch
        {
            WidgetKind.Clock => L.T("Saat"),
            WidgetKind.Date => L.T("Tarih"),
            WidgetKind.Note when c.NoteChecklist =>
                (string.IsNullOrWhiteSpace(c.Title) ? L.T("Yapılacaklar") : c.Title) + Progress(c),   // " · 2/5" (madde yoksa "")
            WidgetKind.Note => L.T("Not") + (!string.IsNullOrWhiteSpace(c.Title) ? " · " + c.Title
                                              : string.IsNullOrWhiteSpace(c.NoteText) ? "" : " · " + FirstLine(c.NoteText)),
            WidgetKind.Launcher => (c.Title is { } t ? L.F("Kısayol kutusu · {0}", t) : L.T("Kısayol kutusu")) +
                                   " " + L.P(c.Tabs.Sum(x => x.Items.Count), "({0} öğe)"),
            _ => L.F("Bölme · {0}", c.Title ?? (c.Filter != DesktopFilter.None ? DesktopItems.Label(c.Filter)
                : WidgetVariants.IsPortal(c) ? FolderPortal.DisplayName(c.FolderName!) : c.FolderName)),
        },
    };

    /// <summary>"Zamanlayıcı · 10 dk", "Pomodoro", "Kronometre" (kalan süre değil: ad durağandır).</summary>
    private static string TimerName(TimerState? timer) => TimerModes.Normalize(timer?.Mode) switch
    {
        TimerModes.Pomodoro => L.T("Pomodoro"),
        TimerModes.Stopwatch => L.T("Kronometre"),
        _ => L.F("Zamanlayıcı · {0} dk", Math.Clamp(timer?.Minutes ?? 10, TimerLogic.MinMinutes, TimerLogic.MaxMinutes)),
    };

    /// <summary>"Dünya saati · Tokyo, Londra, New York" (en çok üç şehir).</summary>
    private static string WorldName(WidgetConfig c)
    {
        var names = (c.Zones ?? []).Take(3).Select(z => WorldCities.Label(z, null)).ToList();
        return names.Count == 0 ? L.T("Dünya saati") : L.F("Dünya saati · {0}", string.Join(", ", names));
    }

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
