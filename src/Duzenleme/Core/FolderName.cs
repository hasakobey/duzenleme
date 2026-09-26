using System.Text;

namespace Duzenleme.Core;

/// <summary>
/// Klasör adlarını büyük/küçük harf ve Türkçe karakter farkı gözetmeden karşılaştırır:
/// "Arşivler", "ARSIVLER" ve "arsivler" aynı kabul edilir.
/// </summary>
public static class FolderName
{
    public static string Fold(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            sb.Append(ch switch
            {
                'ı' or 'I' or 'İ' or 'i' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ü' or 'Ü' => 'u',
                'ö' or 'Ö' => 'o',
                'ç' or 'Ç' => 'c',
                _ => char.ToLowerInvariant(ch),
            });
        }
        return sb.ToString();
    }

    public static bool Equal(string a, string b) => Fold(a) == Fold(b);
}
