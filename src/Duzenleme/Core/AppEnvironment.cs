namespace Duzenleme.Core;

/// <summary>
/// Tanılama ve test ortam değişkenleri: önce NESTDESK_&lt;ad&gt;, yoksa eski DUZENLEME_&lt;ad&gt; (test betikleri ve eski
/// belgeler eski adı kullanır). Ör. Get("DEBUGLOG"), Get("WINDOW_AT"), Get("QUICKADD_AT"), Get("GPU"), Get("LANG").
/// </summary>
public static class AppEnvironment
{
    public const string Prefix = "NESTDESK_";
    public const string LegacyPrefix = "DUZENLEME_";

    public static string? Get(string name) => Pick(name, Environment.GetEnvironmentVariable);

    /// <summary>Yeni adlı değer boş değilse o, değilse eski adlı; ikisi de yoksa null.</summary>
    internal static string? Pick(string name, Func<string, string?> read) =>
        NonEmpty(read(Prefix + name)) ?? NonEmpty(read(LegacyPrefix + name));

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
