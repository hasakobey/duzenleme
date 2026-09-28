using System.Globalization;
using System.IO;
using Duzenleme.Core;

namespace Duzenleme.Views;

public static class UiText
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string When(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff < TimeSpan.FromMinutes(1)) return "az önce";
        if (diff < TimeSpan.FromHours(1)) return $"{(int)diff.TotalMinutes} dk önce";
        if (time.Date == DateTime.Today) return "bugün " + time.ToString("HH:mm", Tr);
        if (time.Date == DateTime.Today.AddDays(-1)) return "dün " + time.ToString("HH:mm", Tr);
        return time.ToString("d MMM HH:mm", Tr);
    }
}

/// <summary>Taşıma kaydının liste satırı.</summary>
public sealed class MoveRow(MoveEntry entry)
{
    public MoveEntry Entry { get; } = entry;
    public string FileName => Entry.Undone ? Path.GetFileName(Entry.Source) : Entry.FileName;
    public string Route => Entry.Undone ? $"{Entry.FolderName}  →  Masaüstü (geri alındı)" : $"{SourceFolder}  →  {Entry.FolderName}";

    /// <summary>Dosyanın geldiği yer: masaüstü ya da (bölmeler arası sürüklemede) başka bir klasör.</summary>
    private string SourceFolder
    {
        get
        {
            var dir = Path.GetDirectoryName(Entry.Source) ?? "";
            return string.Equals(dir.TrimEnd('\\'), AppHost.DesktopDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                ? "Masaüstü" : Path.GetFileName(dir);
        }
    }
    public string When => UiText.When(Entry.Time);

    /// <summary>Simgesi gösterilen dosya (ShellIconImage.Path): arka planda, ekranın piksel boyutunda yüklenir.</summary>
    public string IconPath => Entry.Undone ? Entry.Source : Entry.Destination;
    public bool CanUndo => !Entry.Undone;
    public string Status => Entry.Undone ? "Geri alındı" : "";

    /// <summary>Satırdaki "Geri al" düğmesi yalnızca geri alınmamış kayıtta; geri alınmışsa yerinde "Geri alındı" yazar.</summary>
    public System.Windows.Visibility UndoVisibility => CanUndo ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility UndoneVisibility => CanUndo ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    /// <summary>Ekran okuyucu için düğme adı: hangi dosyanın geri alınacağı.</summary>
    public string UndoName => $"Geri al: {FileName}";
}
