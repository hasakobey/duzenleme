using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duzenleme.Tests;

/// <summary>
/// Gizlilik sözü (PRIVACY.md, Store metinleri): uygulama ağa yalnızca kullanıcının istediği yapay zekâ simgesi için çıkar.
/// Ağ API'leri (HttpClient, soket, DNS, ping, WebSocket, Anthropic istemcisi) yalnızca Icons/AiIconGenerator.cs'te bulunabilir.
/// Yeni bir ağ kullanımı eklenirse önce PRIVACY.md ve tools/store/listing.md güncellenir, sonra bu liste.
/// </summary>
public class NetworkUseTests
{
    private const string Allowed = @"Icons\AiIconGenerator.cs";

    private static readonly HashSet<string> NetworkTypes =
    [
        "HttpClient", "HttpClientHandler", "SocketsHttpHandler", "HttpMessageHandler", "DelegatingHandler", "HttpRequestMessage",
        "HttpResponseMessage", "WebClient", "WebRequest", "HttpWebRequest", "FtpWebRequest", "Socket", "TcpClient", "TcpListener",
        "UdpClient", "Dns", "Ping", "ClientWebSocket", "AnthropicClient", "NetworkStream", "HttpListener",
    ];

    private static readonly string[] NetworkNamespaces =
    [
        "System.Net.Http", "System.Net.Sockets", "System.Net.WebSockets", "System.Net.NetworkInformation", "System.Net.Mail",
    ];

    /// <summary>Kaynakta geçen ağ türü ve ad alanı kullanımları (yorumlar ve metinler sayılmaz).</summary>
    internal static List<string> NetworkUses(string rel, string code)
    {
        var uses = new List<string>();
        var root = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
        string Where(SyntaxNode n) => $"{rel}:{n.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (NetworkTypes.Contains(name.Identifier.Text)) uses.Add($"{Where(name)}: {name.Identifier.Text}");
        }
        foreach (var qualified in root.DescendantNodes().OfType<QualifiedNameSyntax>().Where(q => q.Parent is not QualifiedNameSyntax))
        {
            var text = qualified.ToString().Replace(" ", "");
            if (NetworkNamespaces.Any(ns => text == ns || text.StartsWith(ns + ".", StringComparison.Ordinal)))
                uses.Add($"{Where(qualified)}: {text}");
        }
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(m => m.Parent is not MemberAccessExpressionSyntax))
        {
            var text = access.ToString().Replace(" ", "");
            if (NetworkNamespaces.Any(ns => text.StartsWith(ns + ".", StringComparison.Ordinal)))
                uses.Add($"{Where(access)}: {text}");
        }
        return uses;
    }

    [Fact]
    public void Network_apis_only_in_ai_icon_generator()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var outside = new List<string>();
        var inside = 0;
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) continue;
            var uses = NetworkUses(rel, File.ReadAllText(file));
            if (rel.Equals(Allowed, StringComparison.OrdinalIgnoreCase)) inside += uses.Count;
            else outside.AddRange(uses);
        }
        Assert.True(outside.Count == 0,
            "Ağ API'si yalnızca Icons/AiIconGenerator.cs'te kullanılabilir (yeni ağ kullanımı PRIVACY.md ve Store metinlerini değiştirir):\n"
            + string.Join("\n", outside));
        // Tarayıcı gerçekten görüyor mu: yapay zekâ istemcisi orada.
        Assert.True(inside > 0);
    }

    [Fact]
    public void Scanner_finds_types_namespaces_and_ignores_comments()
    {
        const string code = """
            using System.Net.Http;
            // HttpClient yorumda sayılmaz
            class A
            {
                string s = "Socket metinde sayılmaz";
                async Task M()
                {
                    using var c = new HttpClient();
                    var h = System.Net.Dns.GetHostName();
                    var p = new System.Net.NetworkInformation.Ping();
                    var u = new Uri("https://example.com");
                }
            }
            """;
        var uses = NetworkUses("A.cs", code);
        Assert.Contains(uses, u => u.StartsWith("A.cs:1:") && u.EndsWith("System.Net.Http"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:8:") && u.EndsWith("HttpClient"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:9:") && u.EndsWith("Dns"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:10:") && u.EndsWith("Ping"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:10:") && u.Contains("System.Net.NetworkInformation"));
        Assert.DoesNotContain(uses, u => u.StartsWith("A.cs:2:") || u.StartsWith("A.cs:5:") || u.StartsWith("A.cs:11:"));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
