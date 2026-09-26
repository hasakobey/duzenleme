using System.IO;

namespace Duzenleme.Core;

public enum SkipReason { None, Ignored, NoRule, FolderMissing }

public readonly record struct RuleDecision(SkipReason Skip, Rule? Rule, string? TargetDirectory)
{
    public bool ShouldMove => Skip == SkipReason.None && TargetDirectory is not null;
}

/// <summary>Bir masaüstü dosyasının nereye gideceğine karar verir. Dosya sistemine dokunmaz.</summary>
public sealed class RuleEngine(Func<AppSettings> settings)
{
    /// <summary>Asla taşınmayan uzantılar: kısayollar, sistem dosyaları ve yarım kalmış indirmeler.</summary>
    public static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "lnk", "url", "ini", "appref-ms", "website",
        "crdownload", "part", "partial", "download", "opdownload", "tmp", "temp",
    };

    public static bool IsIgnored(string fileName, FileAttributes attributes)
    {
        if ((attributes & (FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System)) != 0) return true;
        if (fileName.StartsWith("~$", StringComparison.Ordinal) || fileName.StartsWith('.')) return true;
        return IgnoredExtensions.Contains(Rule.NormalizeExtension(Path.GetExtension(fileName)));
    }

    /// <param name="desktopDirectory">Masaüstü klasörü.</param>
    /// <param name="fileName">Masaüstündeki dosyanın adı.</param>
    /// <param name="attributes">Dosyanın öznitelikleri.</param>
    /// <param name="existingFolders">Masaüstündeki mevcut klasör adları.</param>
    public RuleDecision Decide(string desktopDirectory, string fileName, FileAttributes attributes, IEnumerable<string> existingFolders)
    {
        if (IsIgnored(fileName, attributes)) return new(SkipReason.Ignored, null, null);

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return new(SkipReason.NoRule, null, null);

        var current = settings();
        var rule = current.Rules.FirstOrDefault(r => r.Enabled && !string.IsNullOrWhiteSpace(r.TargetFolder) && r.Matches(ext));
        if (rule is null) return new(SkipReason.NoRule, null, null);

        var existing = existingFolders.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder));
        if (existing is not null) return new(SkipReason.None, rule, Path.Combine(desktopDirectory, existing));

        return current.CreateMissingFolders
            ? new(SkipReason.None, rule, Path.Combine(desktopDirectory, rule.TargetFolder.Trim()))
            : new(SkipReason.FolderMissing, rule, null);
    }
}
