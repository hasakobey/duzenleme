using System.IO;
using Duzenleme.Core;

namespace Duzenleme;

/// <summary>Tanılama günlüğü: yalnızca NESTDESK_DEBUGLOG (ya da eski adı DUZENLEME_DEBUGLOG) ortam değişkeni bir dosya yolu gösteriyorsa yazar.</summary>
public static class DebugLog
{
    private static readonly string? Path = AppEnvironment.Get("DEBUGLOG");
    private static readonly object Lock = new();

    public static bool Enabled => Path is not null;

    public static void Write(string message)
    {
        if (Path is null) return;
        lock (Lock)
        {
            try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}"); }
            catch (IOException) { }
        }
    }
}
