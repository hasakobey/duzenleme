using System.IO;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>
/// Kutu taşımalarının gerçek diski (<see cref="IBoxDisk"/>; arka plandaki iş parçacığından çağrılır). Yönetici onaylı toplu
/// taşıma Gezgin'in yoluyla, kendi STA iş parçacığında (<see cref="ShellFileOperations.MoveManyWithShell"/>).
/// Test örneğinde (--desktop) <c>NESTDESK_TEST_PUBLIC_DESKTOP</c> klasörü ortak masaüstü yerine geçer (bkz.
/// <see cref="AppHost.TestPublicDesktop"/>); <c>NESTDESK_TEST_ELEVATION=allow|deny</c> verilirse o klasöre ya da oradan düz
/// taşıma izin hatası verir ve toplu taşıma gerçek yönetici onayı olmadan izin verilmiş (allow: taşır) ya da vazgeçilmiş
/// (deny) gibi davranır. Gerçek örnekte ikisi de yok sayılır.
/// </summary>
internal sealed class BoxDisk : IBoxDisk
{
    public static BoxDisk Instance { get; } = new();

    /// <summary>Test örneğinde yönetici onayı benzetiliyorsa "allow" ya da "deny"; değilse null.</summary>
    private static string? SimulatedElevation =>
        AppHost.TestPublicDesktop is not null && AppEnvironment.Get("TEST_ELEVATION")?.Trim().ToLowerInvariant() is ("allow" or "deny") and var mode
            ? mode
            : null;

    private static bool InSimulatedPublic(string path) =>
        AppHost.TestPublicDesktop is { } folder && BoxPlan.IsDirectlyIn(path, [folder]);

    public FileAttributes? Attributes(string path) => BoxFiles.Attributes(path);

    public bool Exists(string path) => BoxFiles.Exists(path);

    public void Move(string from, string to)
    {
        if (SimulatedElevation is not null && (InSimulatedPublic(from) || InSimulatedPublic(to)))
            throw new UnauthorizedAccessException("test: public desktop needs elevation"); // l10n: çevrilmez (test)
        BoxFiles.Move(from, to);
    }

    public void MoveElevated(IReadOnlyList<(string From, string To)> moves)
    {
        if (moves.Count == 0) return;
        foreach (var folder in moves.Select(m => Path.GetDirectoryName(m.To)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            Directory.CreateDirectory(folder);
        switch (SimulatedElevation)
        {
            case "deny":
                throw new OperationCanceledException();
            case "allow":
                foreach (var (from, to) in moves) BoxFiles.Move(from, to);
                return;
        }
        ShellFileOperations.RunSta(() => ShellFileOperations.MoveManyWithShell(moves, IntPtr.Zero)).GetAwaiter().GetResult();
    }

    public void RemoveIfEmpty(string folder, string root) => BoxFiles.RemoveIfEmpty(folder, root);

    public bool SameContent(string a, string b) => BoxFiles.SameContent(a, b);

    public void Recycle(string path) =>
        ShellFileOperations.RunSta(() => ShellFileOperations.Recycle(path)).GetAwaiter().GetResult();
}
