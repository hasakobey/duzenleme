namespace Duzenleme.Core;

/// <summary>Belirli uzantılardaki dosyaların taşınacağı masaüstü klasörünü tanımlar.</summary>
public sealed class Rule
{
    public string TargetFolder { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
    public bool Enabled { get; set; } = true;

    public bool Matches(string extension)
    {
        var ext = NormalizeExtension(extension);
        return Extensions.Any(e => NormalizeExtension(e) == ext);
    }

    public static string NormalizeExtension(string extension) =>
        extension.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant();

    public static List<Rule> Defaults() =>
    [
        new() { TargetFolder = "PDF", Extensions = ["pdf"] },
        new() { TargetFolder = "Resimler", Extensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "heic", "svg"] },
        new() { TargetFolder = "Belgeler", Extensions = ["doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt", "csv"] },
        new() { TargetFolder = "Arşivler", Extensions = ["zip", "rar", "7z", "tar", "gz"] },
        new() { TargetFolder = "Videolar", Extensions = ["mp4", "mov", "avi", "mkv", "webm"] },
        new() { TargetFolder = "Müzik", Extensions = ["mp3", "wav", "flac", "m4a", "ogg"] },
    ];
}
