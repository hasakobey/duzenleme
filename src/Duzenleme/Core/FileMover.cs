using System.IO;

namespace Duzenleme.Core;

public static class FileMover
{
    /// <summary>Hedefte aynı adda dosya varsa "ad (1).uzantı", "ad (2).uzantı" … üretir.</summary>
    public static string UniquePath(string directory, string fileName)
    {
        var candidate = Path.Combine(directory, fileName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 1; ; i++)
        {
            candidate = Path.Combine(directory, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    /// <summary>Dosyayı klasöre taşır ve dosyanın son yolunu döner.</summary>
    /// <param name="move">Asıl taşıma (kaynak, hedef tam yol); verilmezse <see cref="File.Move(string, string)"/>.</param>
    public static string MoveInto(string sourcePath, string targetDirectory, Action<string, string>? move = null)
    {
        Directory.CreateDirectory(targetDirectory);
        var destination = UniquePath(targetDirectory, Path.GetFileName(sourcePath));
        (move ?? File.Move)(sourcePath, destination);
        return destination;
    }

    /// <summary>İki yol aynı birimde mi (taşıma yalnızca ad değişikliği mi)? Diske dokunmaz; emin değilse false.</summary>
    public static bool SameVolume(string a, string b)
    {
        try
        {
            var ra = Path.GetPathRoot(Path.GetFullPath(a));
            var rb = Path.GetPathRoot(Path.GetFullPath(b));
            return !string.IsNullOrEmpty(ra) && string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>Taşınan dosyayı eski yerine döndürür. Eski yer doluysa yeni bir ad seçer.</summary>
    public static string MoveBack(MoveEntry entry)
    {
        if (!File.Exists(entry.Destination))
            throw new FileNotFoundException("Dosya taşındığı klasörde artık yok.", entry.Destination);

        var directory = Path.GetDirectoryName(entry.Source)!;
        Directory.CreateDirectory(directory);
        var target = UniquePath(directory, Path.GetFileName(entry.Source));
        File.Move(entry.Destination, target);
        return target;
    }

    /// <summary>Dosya başka bir süreç tarafından yazılıyorsa (indirme, kopyalama) false döner.</summary>
    public static bool IsReady(string path)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
