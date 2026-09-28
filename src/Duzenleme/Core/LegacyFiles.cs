using System.IO;
using System.Text.Json;

namespace Duzenleme.Core;

/// <summary>2.0 ve öncesinden kalanlar: eski adlı program dosyaları ve kaldırma sırasında okunan ayar.</summary>
public static class LegacyFiles
{
    /// <summary>2.0'ın (Duzenleme.exe) program dosyaları; kurulum [InstallDelete] ile aynı liste.</summary>
    public static readonly string[] ProgramFiles =
        [AppInfo.LegacyExeName, "Duzenleme.dll", "Duzenleme.deps.json", "Duzenleme.runtimeconfig.json"];

    /// <summary>
    /// Taşınabilir 2.0 klasörünün üzerine 2.1 açılınca eski exe yanında kalır ve eski Run değeri ya da sabitleme onu (eski
    /// sürümü) başlatır. Eski exe bu sürümden eskiyse silinecek dosyalar; değilse (ya da sürümü okunamazsa) hiçbiri.
    /// </summary>
    public static IReadOnlyList<string> ProgramFilesToDelete(string baseDirectory, Version current,
        Func<string, bool> fileExists, Func<string, Version?> fileVersion)
    {
        var exe = Path.Combine(baseDirectory, AppInfo.LegacyExeName);
        if (!fileExists(exe) || fileVersion(exe) is not { } version || version >= current) return [];
        return ProgramFiles.Select(name => Path.Combine(baseDirectory, name)).Where(fileExists).ToList();
    }

    /// <summary>
    /// Ayar dosyası uygulamanın Windows masaüstü simgelerini gizli bıraktığını mı söylüyor? (Kaldırırken ve
    /// --restore-desktop ile; bozuk ya da okunamayan dosya "hayır" sayılır ve dosyaya dokunulmaz.)
    /// </summary>
    public static bool IconsLeftHidden(string settingsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(settingsJson, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(nameof(AppSettings.IconsHiddenByApp), out var hidden)
                && hidden.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }
}
