using System.Net;
using System.Net.Sockets;
using System.Text;
using Duzenleme.Icons;

namespace Duzenleme.Tests;

/// <summary>
/// Gizlilik sözü: yapay zekâ simgesi yalnızca Anthropic'e ve yalnızca kullanıcının anahtarıyla gider; SVG çizimi
/// hiçbir dış adrese bağlanmaz. Denemeler yerel (127.0.0.1) dinleyicilerle yapılır, internete çıkılmaz.
/// </summary>
public class AiIconPrivacyTests
{
    [Fact]
    public void Sanitize_removes_external_references_and_keeps_internal_gradients()
    {
        const string svg = """
            <!DOCTYPE svg [<!ENTITY x SYSTEM "http://evil.example/e">]>
            <?xml-stylesheet href="http://evil.example/s.css"?>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
              <defs><linearGradient id="g"><stop offset="0" stop-color="#fff"/></linearGradient></defs>
              <rect fill="url(#g)" width="10" height="10"/>
              <rect fill="url(http://evil.example/a.svg#p)" stroke="url('https://evil.example/b')" width="10" height="10"/>
              <path style="fill:url( http://evil.example/c )" mask="url(//evil.example/m)" d="M0 0h1v1z"/>
              <use href="http://evil.example/u.svg#x"/>
            </svg>
            """;

        var clean = AiIconGenerator.Sanitize(svg);

        Assert.DoesNotContain("evil.example", clean);
        Assert.Contains("fill=\"url(#g)\"", clean);
    }

    [Fact]
    public async Task Drawing_an_svg_with_external_urls_makes_no_network_request()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var svg = $"""
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
                  <rect fill="url(http://127.0.0.1:{port}/a.svg#p)" stroke="url(http://127.0.0.1:{port}/b.svg#p)" width="100" height="100"/>
                  <circle mask="url(http://127.0.0.1:{port}/m.svg#m)" clip-path="url(http://127.0.0.1:{port}/c.svg#c)" cx="50" cy="50" r="20"/>
                </svg>
                """;
            var accept = listener.AcceptTcpClientAsync();

            // WPF çizim nesneleri STA iş parçacığında oluşturulur.
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { AiIconGenerator.ToDrawing(svg); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(20));

            Assert.Null(error);
            Assert.True(await Task.WhenAny(accept, Task.Delay(1500)) != accept, "SVG çizilirken dış adrese bağlanıldı");
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Client_ignores_anthropic_environment_variables()
    {
        var elsewhere = Listen(out var elsewherePort);  // ortam değişkeninin gösterdiği yer: gidilmemeli
        var expected = Listen(out var expectedPort);    // istemciye açıkça verilen adres
        var saved = new[] { "ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_CUSTOM_HEADERS" }
            .ToDictionary(n => n, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", $"http://127.0.0.1:{elsewherePort}");
            Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", "ortamdan-gelen-token");
            Environment.SetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS", "X-Ortam-Basligi: deger");

            var toExpected = Serve(expected);
            var client = AiIconGenerator.CreateClient("sk-test", $"http://127.0.0.1:{expectedPort}");
            try { await client.Models.Retrieve(AiIconGenerator.Model); } catch (Exception) { /* dinleyici 401 döner */ }

            var request = await toExpected;
            Assert.NotNull(request);
            Assert.False(elsewhere.Pending(), "istek ANTHROPIC_BASE_URL adresine gitti");
            Assert.Contains("X-Api-Key: sk-test", request);
            Assert.DoesNotContain("ortamdan-gelen-token", request);
            Assert.DoesNotContain("X-Ortam-Basligi", request);
        }
        finally
        {
            foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
            elsewhere.Stop();
            expected.Stop();
        }
    }

    private static TcpListener Listen(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    /// <summary>İlk isteği okuyup 401 döner; süre içinde bağlantı gelmezse null.</summary>
    private static async Task<string?> Serve(TcpListener listener)
    {
        var accept = listener.AcceptTcpClientAsync();
        if (await Task.WhenAny(accept, Task.Delay(TimeSpan.FromSeconds(8))) != accept) return null;
        using var client = accept.Result;
        var stream = client.GetStream();
        var buffer = new byte[65536];
        var read = await stream.ReadAsync(buffer);
        const string body = """{"type":"error","error":{"type":"authentication_error","message":"test"}}""";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(
            $"HTTP/1.1 401 Unauthorized\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}"));
        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
