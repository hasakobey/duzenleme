using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>Kısayol kutusuna bırakılan verinin türü (sürükleme başına bir kez belirlenir; DragOver her fare hareketinde gelir).</summary>
internal enum DropContent
{
    None,
    /// <summary>Dosya ve klasörler (Gezgin, masaüstü, başka widget).</summary>
    Files,
    /// <summary>Başlat menüsü uygulamaları (Shell IDList, üst klasör Uygulamalar).</summary>
    Apps,
    /// <summary>Başka kabuk öğeleri (Shell IDList; dosya sistemindekiler eklenir, gerisi atlanır).</summary>
    ShellItems,
    /// <summary>http, https ya da mailto bağlantısı.</summary>
    Link,
    /// <summary>Eklenemeyen türde bağlantı (file:, javascript:…): bırakılamaz, nedeni gösterilir.</summary>
    BlockedLink,
}

/// <summary>
/// Kısayol kutusunun akıllı bırakması: sürükleme verisini okur ve sınıflandırır. Sıra: FileDrop (bugünkü yol) → "Shell IDList
/// Array" (Başlat menüsü uygulaması) → UniformResourceLocatorW / text/x-moz-url → adres gibi duran UnicodeText. Veri bırakma
/// anında (Drop içinde) kopyalanır, sonrası geçersizdir; kabuk çözümlemesi STA'da, dosya yazma arka planda yapılır.
/// </summary>
internal static class LauncherDrop
{
    private const string UrlW = "UniformResourceLocatorW";
    private const string UrlA = "UniformResourceLocator";
    private const string MozUrl = "text/x-moz-url";
    private const string GroupW = "FileGroupDescriptorW";
    private const int MaxTextBytes = 64 * 1024;

    public static DropContent Classify(IDataObject data)
    {
        if (Has(data, DataFormats.FileDrop)) return DropContent.Files;
        if (Has(data, ShellIdList.Format))
            return Bytes(data, ShellIdList.Format, ShellIdList.MaxBytes) is { } cida && ShellIdList.Parent(cida) is { } parent && ShellIdList.IsAppsFolder(parent)
                ? DropContent.Apps
                : DropContent.ShellItems;
        return ReadLink(data, withTitle: false).Kind switch
        {
            LinkKind.Allowed => DropContent.Link,
            LinkKind.Blocked => DropContent.BlockedLink,
            _ => DropContent.None,
        };
    }

    /// <summary>Bırakılan bağlantı: adres (denetlenmiş) ve varsa sayfa başlığı (Firefox'un satırı ya da tarayıcının .url adı).</summary>
    public static (LinkKind Kind, string? Url, string? Title) ReadLink(IDataObject data, bool withTitle)
    {
        var moz = Has(data, MozUrl) ? LinkFiles.ParseMozUrl(Text(data, MozUrl, Encoding.Unicode)) : (null, null);
        var raw = (Has(data, UrlW) ? Text(data, UrlW, Encoding.Unicode) : null)
                  ?? moz.Url
                  ?? (Has(data, UrlA) ? Text(data, UrlA, Encoding.ASCII) : null);
        if (raw is not null)
        {
            var kind = LinkFiles.ClassifyUrl(raw, out var url);
            if (kind != LinkKind.None)
            {
                var title = !withTitle || kind != LinkKind.Allowed ? null
                    : ItemLooks.CleanName(moz.Title) ?? (Bytes(data, GroupW, MaxTextBytes) is { } group ? LinkFiles.TitleFromFileGroupDescriptor(group) : null);
                return (kind, url, title);
            }
        }
        if (!Has(data, DataFormats.UnicodeText)) return (LinkKind.None, null, null);
        var text = Read(() => data.GetData(DataFormats.UnicodeText)) as string;
        var textKind = LinkFiles.ClassifyText(text, out var textUrl);
        return (textKind, textUrl, null);
    }

    /// <summary>Bırakılan "Shell IDList Array" verisinin kopyası (bırakma anında alınmalı); yoksa null.</summary>
    public static byte[]? ReadShellIdList(IDataObject data) => Bytes(data, ShellIdList.Format, ShellIdList.MaxBytes);

    /// <summary>Kabuk öğelerini kendi STA iş parçacığında çözer (arayüz beklemez).</summary>
    public static async Task<IReadOnlyList<ShellDropItem>> ResolveAsync(byte[]? cida)
    {
        if (cida is null || ShellIdList.Parse(cida) is not { Count: > 0 } pidls) return [];
        IReadOnlyList<ShellDropItem> found = [];
        try { await ShellFileOperations.RunSta(() => found = ShellDropItems.Resolve(pidls)); }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or OutOfMemoryException)
        {
            DebugLog.Write($"kabuk öğeleri çözülemedi: {ex.Message}");
        }
        return found;
    }

    private static bool Has(IDataObject data, string format) => Read(() => data.GetDataPresent(format)) is true;

    private static string? Text(IDataObject data, string format, Encoding encoding)
    {
        if (Bytes(data, format, MaxTextBytes) is not { } bytes) return null;
        // ANSI biçimi yalnızca ASCII ise okunur (kod sayfası tahmin edilmez).
        if (ReferenceEquals(encoding, Encoding.ASCII) && bytes.Any(b => b >= 0x80)) return null;
        var text = encoding.GetString(bytes);
        var end = text.IndexOf('\0');
        return end >= 0 ? text[..end] : text;
    }

    private static byte[]? Bytes(IDataObject data, string format, int max) => Read(() => data.GetData(format, autoConvert: false)) switch
    {
        MemoryStream { Length: > 0 } stream when stream.Length <= max => stream.ToArray(),
        byte[] { Length: > 0 } bytes when bytes.Length <= max => bytes,
        _ => null,
    };

    /// <summary>Başka uygulamanın verisi okunurken hata (biçim yok, kaynak reddetti) bırakmayı düşürmesin.</summary>
    private static object? Read(Func<object?> read)
    {
        try { return read(); }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException
                                       or NotSupportedException or IOException or OutOfMemoryException)
        {
            return null;
        }
    }
}
