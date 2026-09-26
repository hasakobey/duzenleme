using System.IO;

namespace Duzenleme;

/// <summary>Tanılama günlüğü: yalnızca DUZENLEME_DEBUGLOG ortam değişkeni bir dosya yolu gösteriyorsa yazar.</summary>
public static class DebugLog
{
    private static readonly string? Path = Environment.GetEnvironmentVariable("DUZENLEME_DEBUGLOG");
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
