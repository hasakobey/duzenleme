using System.Globalization;
using System.IO;
using Duzenleme.Core;

namespace Duzenleme.Views;

public static class UiText
{
    /// <summary>Eski ad: arayüz diline göre biçim için <see cref="L.Culture"/>, sıralama için <see cref="L.Sorter"/>.</summary>
    [Obsolete("L.Culture (biçim) ya da L.Sorter (sıralama) kullan: arayüz diline uyar.")]
    public static CultureInfo Tr => L.Culture;

    /// <summary>Taşıma listesindeki zaman: "az önce", "5 dk önce", "bugün 14:05", "dün 09:30", "3 Eyl 18:20".</summary>
    public static string When(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff < TimeSpan.FromMinutes(1)) return L.T("az önce");
        if (diff < TimeSpan.FromHours(1)) return L.F("{0} dk önce", (int)diff.TotalMinutes);
        if (time.Date == DateTime.Today) return L.F("bugün {0}", Clock(time));
        if (time.Date == DateTime.Today.AddDays(-1)) return L.F("dün {0}", Clock(time));
        return DayAndClock(time);
    }

    /// <summary>Saat: Windows'un bölge ayarı 24 saatlikse "14:05", değilse "2:05 PM" (ay/ÖÖ-ÖS adları arayüz dilinde).</summary>
    private static string Clock(DateTime time) => time.ToString(L.Uses24Hour ? "HH:mm" : "h:mm tt", L.Culture);

    /// <summary>Gün ve saat: Türkçede "3 Eyl 18:20"; İngilizcede kültürün ay-gün sırasıyla "Sep 3, 6:20 PM" / "3 Sep, 18:20".</summary>
    private static string DayAndClock(DateTime time)
    {
        var culture = L.Culture;
        if (L.Current == Lang.Tr) return $"{time.ToString("d MMM", culture)} {Clock(time)}";
        var day = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        return $"{time.ToString(day, culture)}, {Clock(time)}";
    }
}

/// <summary>Taşıma kaydının liste satırı.</summary>
public sealed class MoveRow(MoveEntry entry)
{
    public MoveEntry Entry { get; } = entry;
    public string FileName => Entry.Undone ? Path.GetFileName(Entry.Source) : Entry.FileName;
    public string Route => Entry.Undone ? L.F("{0}  →  Masaüstü (geri alındı)", Entry.FolderName) : $"{SourceFolder}  →  {Entry.FolderName}";

    /// <summary>Dosyanın geldiği yer: masaüstü ya da (bölmeler arası sürüklemede) başka bir klasör.</summary>
    private string SourceFolder
    {
        get
        {
            var dir = Path.GetDirectoryName(Entry.Source) ?? "";
            return string.Equals(dir.TrimEnd('\\'), AppHost.DesktopDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                ? L.T("Masaüstü") : Path.GetFileName(dir);
        }
    }
    public string When => UiText.When(Entry.Time);

    /// <summary>Simgesi gösterilen dosya (ShellIconImage.Path): arka planda, ekranın piksel boyutunda yüklenir.</summary>
    public string IconPath => Entry.Undone ? Entry.Source : Entry.Destination;
    public bool CanUndo => !Entry.Undone;
    public string Status => Entry.Undone ? L.T("Geri alındı") : "";

    /// <summary>Satırdaki "Geri al" düğmesi yalnızca geri alınmamış kayıtta; geri alınmışsa yerinde "Geri alındı" yazar.</summary>
    public System.Windows.Visibility UndoVisibility => CanUndo ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility UndoneVisibility => CanUndo ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    /// <summary>Ekran okuyucu için düğme adı: hangi dosyanın geri alınacağı.</summary>
    public string UndoName => L.F("Geri al: {0}", FileName);
}
