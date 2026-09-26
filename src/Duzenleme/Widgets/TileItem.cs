using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Bölme ve kısayol kutusundaki bir öğe; simge boyutu ve görünüm moduna göre yerleşimini taşır.</summary>
public sealed class TileItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public ImageSource? Icon { get; init; }
    public bool Missing { get; init; }

    public double IconPx { get; init; }
    public double TileWidth { get; init; }
    public Orientation Orientation { get; init; }
    public HorizontalAlignment IconAlign { get; init; }
    public TextAlignment TextAlign { get; init; }
    public TextWrapping Wrap { get; init; }
    public Thickness TextMargin { get; init; }
    public double TextMaxHeight { get; init; }
    public double Dim => Missing ? 0.45 : 1;
    public string Tooltip => Missing ? $"{Path}\n(bulunamadı)" : Path;

    public static TileItem Create(string path, IconSize size, ItemView view, string? name = null)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        var px = size switch { IconSize.Small => 24.0, IconSize.Large => 48.0, _ => 36.0 };
        var list = view == ItemView.List;
        return new TileItem
        {
            Name = name ?? DisplayName(path),
            Path = path,
            Icon = exists ? ShellIcons.For(path) : null,
            Missing = !exists,
            IconPx = list ? Math.Min(px, 28) : px,
            TileWidth = list ? double.NaN : px + 44,
            Orientation = list ? Orientation.Horizontal : Orientation.Vertical,
            IconAlign = list ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            TextAlign = list ? TextAlignment.Left : TextAlignment.Center,
            Wrap = list ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextMargin = list ? new Thickness(10, 0, 0, 0) : new Thickness(0, 5, 0, 0),
            TextMaxHeight = list ? 20 : 32,
        };
    }

    public static string DisplayName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(name)) return path;
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext is ".lnk" or ".url" or ".exe" or ".appref-ms" ? System.IO.Path.GetFileNameWithoutExtension(name) : name;
    }

    public static void Launch(string path, bool asAdmin = false)
    {
        try
        {
            var info = new ProcessStartInfo(path) { UseShellExecute = true };
            if (File.Exists(path)) info.WorkingDirectory = System.IO.Path.GetDirectoryName(path);
            if (asAdmin) info.Verb = "runas";
            Process.Start(info);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Kullanıcı yönetici onayını iptal etti.
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Düzenleme");
        }
    }

    public static void Reveal(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
}
