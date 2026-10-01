using System.IO;
using System.Xml.Linq;
using Duzenleme.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duzenleme.Tests;

/// <summary>
/// 2.2: uygulama WinForms'suz (tepsi simgesi doğrudan Shell_NotifyIcon; mimari başına ~24,6 MB daha küçük ve açılışta
/// WinForms yüklenmez). WinForms ya da System.Drawing geri gelmesin; kurulum eski sürümlerin WinForms dosyalarını silsin.
/// Klasör izleyicileri de yalnızca kendi yerlerinde kurulur.
/// </summary>
public class PlatformTests
{
    private static readonly string[] Forbidden = ["System.Windows.Forms", "System.Drawing"];

    [Fact]
    public void Project_does_not_use_windows_forms()
    {
        var csproj = XDocument.Load(Path.Combine(SourceRoot, "Duzenleme.csproj"));
        Assert.Empty(csproj.Descendants().Where(e => e.Name.LocalName == "UseWindowsForms"));
        Assert.Contains(csproj.Descendants(), e => e.Name.LocalName == "UseWPF" && e.Value.Trim() == "true");
        // Yalnızca WinForms'un uyarı kodları ve onun global using'leri de kalmasın.
        var text = File.ReadAllText(Path.Combine(SourceRoot, "Duzenleme.csproj"));
        foreach (var leftover in new[] { "WFAC010", "WFO0003", "System.Windows.Forms", "System.Drawing" })
            Assert.DoesNotContain(leftover, text);
    }

    [Fact]
    public void App_assembly_does_not_reference_windows_forms_or_system_drawing()
    {
        var references = typeof(AppInfo).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.Contains("PresentationFramework", references);
        Assert.DoesNotContain(references, name => Forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                                                  || name.StartsWith("Microsoft.VisualBasic", StringComparison.OrdinalIgnoreCase)
                                                  || name.Equals("WindowsFormsIntegration", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Kaynakta WinForms/System.Drawing kullanımı yok: using yönergeleri ve nitelikli adlar (yorumlar ve dizgeler sayılmaz;
    /// kurulumun sildiği eski dosyaların adları Core/LegacyFiles'ta veri olarak durur).
    /// </summary>
    [Fact]
    public void Source_does_not_use_windows_forms_or_system_drawing()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var rel = Path.GetRelativePath(SourceRoot, file);
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                var name = node switch
                {
                    UsingDirectiveSyntax u => u.NamespaceOrType?.ToString(),
                    QualifiedNameSyntax q when q.Parent is not QualifiedNameSyntax => q.ToString(),
                    MemberAccessExpressionSyntax m when m.Parent is not MemberAccessExpressionSyntax => m.ToString(),
                    _ => null,
                };
                if (name is null) continue;
                name = name.Replace("global::", "").Replace(" ", "");
                if (Forbidden.Any(f => name == f || name.StartsWith(f + ".", StringComparison.Ordinal)))
                    offenders.Add($"{rel}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {name}");
            }
        }
        foreach (var file in SourceFiles("*.xaml"))
        {
            var text = File.ReadAllText(file);
            foreach (var word in Forbidden.Append("WindowsFormsHost").Where(w => text.Contains(w, StringComparison.Ordinal)))
                offenders.Add($"{Path.GetRelativePath(SourceRoot, file)}: {word}");
        }
        Assert.True(offenders.Count == 0, "WinForms/System.Drawing kullanımı:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Installer_removes_the_windows_forms_files_of_earlier_versions()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "installer", "NestDesk.iss"));
        // Bölümün kendisi (başlık satır başında; yorumlarda da "[InstallDelete]" geçiyor), sonraki bölüme kadar.
        var installDelete = text[(text.IndexOf("\n[InstallDelete]", StringComparison.Ordinal) + 1)..];
        installDelete = installDelete[..installDelete.IndexOf("\n[", StringComparison.Ordinal)];
        foreach (var file in LegacyFiles.WinFormsProgramFiles)
            Assert.Contains($"Type: files; Name: \"{{app}}\\{file}\"", installDelete);
        // Listede yalnızca WinForms'un dosyaları var: uygulamanın hâlâ yüklediği (WPF'in ve .NET'in) dosyalar silinmez.
        Assert.All(LegacyFiles.WinFormsProgramFiles, f => Assert.Matches(
            @"^(tr\\)?(System\.Windows\.Forms(\.Primitives|\.Design(\.Editors)?)?|System\.(Drawing\.)?Design|Microsoft\.VisualBasic\.Forms|WindowsFormsIntegration)(\.resources)?\.dll$", f));
        // Taşınabilir klasörü temizleyen liste (eski adlı program dosyaları) değişmedi: WinForms dosyaları orada silinmez.
        Assert.Empty(LegacyFiles.ProgramFiles.Intersect(LegacyFiles.WinFormsProgramFiles, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 2.2: klasör izleyicileri tek yerden. FileSystemWatcher yalnızca ResilientWatcher'da (kendini toparlar, arka planda
    /// kurulabilir); ResilientWatcher'ı yalnızca otomatik taşıma (DesktopWatcher) ve paylaşılan anlık görüntüler
    /// (DirectorySnapshot) kurar. Klasör gösteren görünüm Snapshots.Acquire kullanır, yeni klasör önerisi anlık görüntünün
    /// DirectoriesAppeared'ını: aynı klasöre ikinci izleyici açılmaz.
    /// </summary>
    [Fact]
    public void Folder_watchers_are_only_made_in_their_own_places()
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["FileSystemWatcher"] = [@"Core\ResilientWatcher.cs"],
            ["ResilientWatcher"] = [@"Core\ResilientWatcher.cs", @"Core\DesktopWatcher.cs", @"Core\DirectorySnapshot.cs"],
        };
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var rel = Path.GetRelativePath(SourceRoot, file);
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
            foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!allowed.TryGetValue(name.Identifier.ValueText, out var places)) continue;
                if (places.Contains(rel, StringComparer.OrdinalIgnoreCase)) continue;
                offenders.Add($"{rel}:{name.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {name.Identifier.ValueText}");
            }
        }
        Assert.True(offenders.Count == 0, "İzleyici yalnızca kendi yerinde kurulur:\n" + string.Join("\n", offenders));
        // Masaüstünün ayrı "yeni klasör" izleyicisi kalktı (anlık görüntünün okuma farkı kullanılır).
        Assert.False(File.Exists(Path.Combine(SourceRoot, "Desktop", "NewFolderWatcher.cs")));
    }

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(SourceRoot, f);
                return !rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase);
            });

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
