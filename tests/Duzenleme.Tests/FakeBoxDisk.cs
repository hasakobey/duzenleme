using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kutu taşımaları için bellekte bir disk (<see cref="IBoxDisk"/>): "ortak masaüstü" klasörlerine düz taşıma izin hatası verir,
/// kilitli dosya paylaşım ihlali verir; yönetici onaylı toplu taşıma Windows gibi "Tümüne evet" ile çalışır (hedef doluysa
/// üzerine yazar: <see cref="BoxEngine"/>'in bekçisi bunu önlemeli). Gerçek UAC yok.
/// </summary>
internal sealed class FakeBoxDisk : IBoxDisk
{
    /// <summary>Dosyalar: yol → içerik.</summary>
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca yöneticinin yazdığı klasörler (ortak masaüstü).</summary>
    public HashSet<string> AdminOnly { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Başka bir programın açık tuttuğu dosyalar.</summary>
    public HashSet<string> Locked { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yönetici onayı: "allow", "deny" (vazgeçildi) ya da "fail" (G/Ç hatası).</summary>
    public string Elevation { get; set; } = "allow";

    public int ElevatedCalls { get; private set; }
    public List<(string From, string To)> Elevated { get; } = [];
    public List<string> Recycled { get; } = [];
    public List<(string Folder, string Root)> Cleaned { get; } = [];
    public List<string> AttributeCalls { get; } = [];

    /// <summary>Her düz taşımadan hemen önce (kaydın durumunu denetlemek için).</summary>
    public Action<string, string>? BeforeMove { get; set; }

    public FakeBoxDisk File(string path, string content = "x")
    {
        Files[path] = content;
        return this;
    }

    public FileAttributes? Attributes(string path)
    {
        AttributeCalls.Add(path);
        return Files.ContainsKey(path) ? FileAttributes.Normal : Folders.Contains(path) ? FileAttributes.Directory : null;
    }

    public bool Exists(string path) => Files.ContainsKey(path) || Folders.Contains(path);

    public void Move(string from, string to)
    {
        BeforeMove?.Invoke(from, to);
        if (Locked.Contains(from)) throw new IOException("locked", unchecked((int)0x80070020));
        if (InAdminOnly(from) || InAdminOnly(to)) throw new UnauthorizedAccessException("admin only");
        if (!Files.TryGetValue(from, out var content)) throw new FileNotFoundException("missing", from);
        if (Exists(to)) throw new IOException("exists");
        Files.Remove(from);
        Files[to] = content;
    }

    public void MoveElevated(IReadOnlyList<(string From, string To)> moves)
    {
        ElevatedCalls++;
        Elevated.AddRange(moves);
        switch (Elevation)
        {
            case "deny": throw new OperationCanceledException();
            case "fail": throw new IOException("shell failed");
        }
        foreach (var (from, to) in moves)
        {
            // Windows'un toplu taşıması "Tümüne evet": hedef doluysa üzerine yazar; kaynak yoksa o taşıma atlanır.
            if (!Files.TryGetValue(from, out var content) || Locked.Contains(from)) continue;
            Files.Remove(from);
            Files[to] = content;
        }
    }

    public void RemoveIfEmpty(string folder, string root) => Cleaned.Add((folder, root));

    public bool SameContent(string a, string b) =>
        Files.TryGetValue(a, out var x) && Files.TryGetValue(b, out var y) && string.Equals(x, y, StringComparison.Ordinal);

    public void Recycle(string path)
    {
        Recycled.Add(path);
        Files.Remove(path);
    }

    private bool InAdminOnly(string path) => BoxPlan.ParentOf(path) is { } parent && AdminOnly.Contains(parent);
}
