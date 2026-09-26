using System.Globalization;
using System.IO;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;

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
    public string Route => Entry.Undone ? $"{Entry.FolderName}  →  Masaüstü (geri alındı)" : $"Masaüstü  →  {Entry.FolderName}";
    public string When => UiText.When(Entry.Time);
    public ImageSource? Icon => ShellIcons.For(Entry.Undone ? Entry.Source : Entry.Destination);
    public bool CanUndo => !Entry.Undone;
    public string Status => Entry.Undone ? "Geri alındı" : "";
}
