using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// "Windows ile başlat" (HKCU\...\Run) değerinin 2.1 geçişi: ad Duzenleme → NestDesk, exe Duzenleme.exe → NestDesk.exe.
/// Değer bizim sayılır, yeter ki gösterdiği exe bu programın klasöründe olsun (eski değer eski exe adını gösterir; tam yol
/// karşılaştırması onu "başka program" sanırdı). Başka klasördeki kopyanın (ör. taşınabilir 2.0) değerine dokunulmaz.
/// Görev Yöneticisi'ndeki açık/kapalı seçimi Explorer\StartupApproved\Run altında değer adıyla tutulur: taşınırken
/// yorumlanmadan olduğu gibi kopyalanır. Aynı kurallar kurulumda da var (NestDesk.iss: MigrateRunValue).
/// </summary>
public static class RunValueMigration
{
    /// <summary>Run komutundaki exe yolu: tırnaklıysa tırnak içi, değilse ".exe"ye ya da ilk boşluğa kadar. Boşsa null.</summary>
    public static string? ExePath(string? command)
    {
        var text = command?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            var quoted = end < 0 ? text[1..] : text[1..end];
            return quoted.Trim() is { Length: > 0 } path ? path : null;
        }
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0 && (exe + 4 == text.Length || char.IsWhiteSpace(text[exe + 4]))) return text[..(exe + 4)];
        var space = text.IndexOfAny([' ', '\t']);
        return space < 0 ? text : text[..space];
    }

    /// <summary>Komut verilen klasördeki bir programı mı başlatıyor? (Büyük/küçük harf ve sondaki ayraç önemsiz.)</summary>
    public static bool PointsInto(string? command, string directory)
    {
        if (ExePath(command) is not { } exe || string.IsNullOrWhiteSpace(directory)) return false;
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(exe));
            return folder is not null && string.Equals(Path.TrimEndingDirectorySeparator(folder),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>Uygulamanın yazdığı komut (Ayarlar'daki anahtar ve kurulumdaki görevle aynı biçim).</summary>
    public static string Command(string exePath) => $"\"{exePath}\" --minimized";

    /// <summary>
    /// Yapılacaklar. <paramref name="current"/>/<paramref name="legacy"/>: NestDesk / Duzenleme değerlerinin içeriği (yoksa
    /// null); *Approval: StartupApproved\Run altında aynı adlı kayıt var mı.
    /// </summary>
    public static RunValuePlan Plan(string? current, string? legacy, bool currentHasApproval, bool legacyHasApproval, string exePath)
    {
        var directory = Path.GetDirectoryName(exePath) ?? "";
        var currentIsOurs = PointsInto(current, directory);
        var legacyIsOurs = PointsInto(legacy, directory);
        // Yeni adlı değer bu kopyanınsa ama başka bir exe'yi (ör. eski adı) gösteriyorsa düzeltilir.
        var fixCurrent = currentIsOurs && !string.Equals(ExePath(current), exePath, StringComparison.OrdinalIgnoreCase);
        if (!legacyIsOurs) return new(WriteCurrent: fixCurrent);

        // Yeni ad başka bir kopyaya (ör. başka klasördeki taşınabilir 2.1) ait: onu ezme; eski adlı değer yeni exe'yi göstersin.
        if (current is not null && !currentIsOurs)
            return new(RewriteLegacy: !string.Equals(ExePath(legacy), exePath, StringComparison.OrdinalIgnoreCase));

        // Görev Yöneticisi seçimi eski ad altındaysa yeni ada taşınır; yeni ad altında zaten bir seçim varsa o geçerlidir.
        var copyApproval = legacyHasApproval && !(currentIsOurs && currentHasApproval);
        return new(WriteCurrent: current is null || fixCurrent, DeleteLegacy: true,
            CopyApprovalToCurrent: copyApproval, DeleteLegacyApproval: legacyHasApproval);
    }
}

/// <summary>
/// Run değeri geçişinde yapılacaklar. WriteCurrent: NestDesk = Command(exe). RewriteLegacy: Duzenleme = Command(exe).
/// DeleteLegacy: Duzenleme silinir. CopyApprovalToCurrent: StartupApproved\Run\Duzenleme → \NestDesk (ikili değer olduğu gibi).
/// DeleteLegacyApproval: StartupApproved\Run\Duzenleme silinir.
/// </summary>
public sealed record RunValuePlan(bool WriteCurrent = false, bool RewriteLegacy = false, bool DeleteLegacy = false,
    bool CopyApprovalToCurrent = false, bool DeleteLegacyApproval = false)
{
    public bool IsEmpty => this == new RunValuePlan();
}
