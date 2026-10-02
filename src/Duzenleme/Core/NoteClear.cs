namespace Duzenleme.Core;

/// <summary>
/// "Notu temizle"nin geri alınması (saf; testlenir). Temizlenen yazı geri gelir; o arada notta yeni bir şey yazıldıysa o da
/// kaybolmaz, geri gelen yazının altına eklenir.
/// </summary>
public static class NoteClear
{
    public static string Restore(string cleared, string? current) =>
        string.IsNullOrWhiteSpace(current) ? cleared : cleared.TrimEnd('\r', '\n') + "\r\n" + current;
}
