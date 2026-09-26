using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace Duzenleme.Icons;

/// <summary>
/// İsteğe bağlı: kullanıcının Claude API anahtarıyla, yazdığı açıklamaya göre klasör simgesi (SVG) üretir.
/// Anahtar yoksa uygulama hazır kütüphaneyle çalışmaya devam eder.
/// </summary>
public static partial class AiIconGenerator
{
    public const string Model = "claude-opus-5";

    private const string SystemPrompt = """
        You design Windows desktop folder icons. Reply with exactly one SVG document and nothing else.

        Requirements:
        - Root element: <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"> with a transparent background.
        - Draw a modern, flat Windows 11 (Fluent) style folder that fills most of the canvas: a darker back panel with a tab at the top-left and a lighter front panel covering the lower two thirds.
        - On the front panel, draw one clear emblem that represents the user's description. It must stay readable at 32×32 pixels: bold, simple shapes, strong contrast, no thin details.
        - Choose a harmonious 2–4 color palette that fits the theme; subtle linear gradients are welcome.
        - Allowed elements only: g, path, rect, circle, ellipse, line, polyline, polygon, defs, linearGradient, radialGradient, stop.
        - No text, no <image>, no filters, no masks, no clipPath, no scripts, no external references, no CSS <style> blocks.
        """;

    [GeneratedRegex(@"<svg[\s\S]*?</svg>", RegexOptions.IgnoreCase)]
    private static partial Regex SvgBlock();

    [GeneratedRegex(@"<(script|foreignObject|image|style|iframe|audio|video)\b[\s\S]*?(</\1\s*>|/>)", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousElements();

    [GeneratedRegex(@"\s(on\w+|href|xlink:href)\s*=\s*(""[^""]*""|'[^']*')", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousAttributes();

    /// <summary>Anahtarın geçerli olduğunu ücretsiz bir çağrıyla (model bilgisi) doğrular.</summary>
    public static async Task<string?> ValidateKeyAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            AnthropicClient client = new() { ApiKey = apiKey };
            await client.Models.Retrieve(Model, cancellationToken: ct);
            return null;
        }
        catch (AnthropicUnauthorizedException) { return "Anahtar geçersiz."; }
        catch (AnthropicForbiddenException) { return "Bu anahtarın modele erişim izni yok."; }
        catch (AnthropicApiException ex) { return $"API hatası: {ex.Message}"; }
        catch (Exception ex) when (ex is AnthropicIOException or System.Net.Http.HttpRequestException) { return "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et."; }
    }

    /// <summary>Açıklamaya göre SVG üretir ve güvenli hale getirilmiş SVG metnini döner.</summary>
    public static async Task<string> GenerateSvgAsync(string apiKey, string folderName, string description, CancellationToken ct = default)
    {
        AnthropicClient client = new() { ApiKey = apiKey };
        var prompt = $"""
            Folder name: {folderName}
            What the icon should show: {(string.IsNullOrWhiteSpace(description) ? folderName : description)}
            """;

        BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                System = SystemPrompt,
                // Basit bir yaratıcı görev: orta düzey çaba hız/maliyet dengesini iyi tutar.
                OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
                // Güvenlik sınıflandırıcısı reddederse istek aynı çağrıda yedek modelle tamamlanır.
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                Messages = [new() { Role = Role.User, Content = prompt }],
            }, ct);
        }
        catch (AnthropicUnauthorizedException) { throw new AiIconException("API anahtarı geçersiz. Ayarlar'dan kontrol et."); }
        catch (AnthropicRateLimitException) { throw new AiIconException("Çok fazla istek gönderildi; biraz bekleyip tekrar dene."); }
        catch (AnthropicApiException ex) { throw new AiIconException($"API hatası: {ex.Message}"); }
        catch (Exception ex) when (ex is AnthropicIOException or System.Net.Http.HttpRequestException) { throw new AiIconException("Sunucuya ulaşılamadı. İnternet bağlantını kontrol et."); }

        if (response.StopReason == "refusal")
            throw new AiIconException("Bu açıklama için simge üretilemedi. Farklı bir açıklama dene.");

        var text = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        var match = SvgBlock().Match(text);
        if (!match.Success) throw new AiIconException("Yanıtta SVG bulunamadı. Tekrar dene.");
        return Sanitize(match.Value);
    }

    /// <summary>Model çıktısı güvenilmez veridir: çalıştırılabilir ya da dışarı bağlanan her şey atılır.</summary>
    public static string Sanitize(string svg)
    {
        svg = DangerousElements().Replace(svg, "");
        svg = DangerousAttributes().Replace(svg, "");
        return svg;
    }

    /// <summary>SVG'yi WPF çizimine dönüştürür.</summary>
    public static Drawing ToDrawing(string svg)
    {
        var settings = new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true };
        using var reader = new FileSvgReader(settings);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
        var drawing = reader.Read(stream) ?? throw new AiIconException("Üretilen SVG çizilemedi.");
        drawing.Freeze();
        return drawing;
    }
}

public sealed class AiIconException(string message) : Exception(message);
