using System.Text.RegularExpressions;
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
        // XML'in dış kaynak çözücüsü (DTD/şema indirir) ve vekil sunucu.
        "XmlUrlResolver", "XmlSecureResolver", "WebProxy",
    ];

    private static readonly string[] NetworkNamespaces =
    [
        "System.Net.Http", "System.Net.Sockets", "System.Net.WebSockets", "System.Net.NetworkInformation", "System.Net.Mail",
        // WinRT ağ API'leri (projeksiyonsuz COM ile de erişilebilir; ad alanı geçerse yakalanır).
        "Windows.Web", "Windows.Networking",
    ];

    // Adresten içerik yükleyen WPF türleri: bunlara http(s) adresi verilirse uygulama ağa çıkar (bağlantıyı tarayıcıda açmak
    // ağ kullanımı sayılmaz; Browser.Open'a giden adresler serbest).
    private static readonly HashSet<string> RemoteLoaders =
    [
        "BitmapImage", "BitmapDecoder", "BitmapFrame", "ImageBrush", "MediaPlayer", "MediaElement", "XmlDocument", "XDocument",
        "XmlReader", "SvgViewbox", "SvgCanvas", "FileSvgReader",
    ];

    private static bool HasHttpLiteral(SyntaxNode node) => node.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
        .Any(l => l.IsKind(SyntaxKind.StringLiteralExpression) && l.Token.ValueText.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase));

    private static string? TypeName(SyntaxNode node) => node switch
    {
        ObjectCreationExpressionSyntax o => o.Type.ToString().Split('.').Last(),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax m } => m.Expression.ToString().Split('.').Last(),
        _ => null,
    };

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
        // Resim/ortam/XML yükleyicisine http(s) adresi: new BitmapImage(new Uri("https://…")), BitmapDecoder.Create(…),
        // x.UriSource = new Uri("http…"), x.Source = "http…".
        foreach (var node in root.DescendantNodes().Where(n => n is ObjectCreationExpressionSyntax or InvocationExpressionSyntax))
        {
            if (TypeName(node) is { } type && RemoteLoaders.Contains(type) && HasHttpLiteral(node))
                uses.Add($"{Where(node)}: {type} http");
        }
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var left = assignment.Left.ToString();
            if ((left.EndsWith("UriSource", StringComparison.Ordinal) || left.EndsWith("Source", StringComparison.Ordinal)) && HasHttpLiteral(assignment.Right))
                uses.Add($"{Where(assignment)}: {left} http");
        }
        return uses;
    }

    /// <summary>XAML'da adresten yüklenen kaynak (Image Source="https://…", ImageBrush ImageSource, MediaElement Source).</summary>
    internal static List<string> XamlRemoteSources(string rel, string xaml) =>
        Regex.Matches(xaml, @"\b(?:Source|UriSource|ImageSource)\s*=\s*""\s*https?:", RegexOptions.IgnoreCase)
            .Select(m => $"{rel}:{xaml[..m.Index].Count(c => c == '\n') + 1}: {m.Value}").ToList();

    [Fact]
    public void Xaml_never_loads_from_the_network()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var uses = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.xaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) continue;
            uses.AddRange(XamlRemoteSources(rel, File.ReadAllText(file)));
        }
        Assert.True(uses.Count == 0, "XAML ağdan kaynak yüklememeli:\n" + string.Join("\n", uses));
        Assert.Single(XamlRemoteSources("A.xaml", "<Image Source=\"https://x/y.png\" />\n<Hyperlink NavigateUri=\"https://x\" />"));
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
                    var img = new BitmapImage(new Uri("https://example.com/a.png"));
                    img.UriSource = new Uri("http://example.com/b.png");
                    var d = System.Windows.Media.Imaging.BitmapDecoder.Create(new Uri("https://x/c.png"), default, default);
                    var r = new XmlUrlResolver();
                    var local = new BitmapImage(new Uri(@"C:\a.png"));
                }
            }
            """;
        var uses = NetworkUses("A.cs", code);
        Assert.Contains(uses, u => u.StartsWith("A.cs:12:") && u.EndsWith("BitmapImage http"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:13:") && u.Contains("UriSource http"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:14:") && u.EndsWith("BitmapDecoder http"));
        Assert.Contains(uses, u => u.StartsWith("A.cs:15:") && u.EndsWith("XmlUrlResolver"));
        Assert.DoesNotContain(uses, u => u.StartsWith("A.cs:16:"));
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
