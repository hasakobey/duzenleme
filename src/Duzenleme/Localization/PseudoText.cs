using System.Text;

namespace Duzenleme.Localization;

/// <summary>
/// Sahte dil (DUZENLEME_LANG=pseudo): çevrilen metin aksanlı harflerle, köşeli ayraç içinde ve ~%30 uzatılarak gösterilir
/// ("Add widget" → "⟦Áðð ŵíðĝéţ~~~~⟧"). Çevrilmeden kalan Türkçe metin ve uzun İngilizcede sığmayan yerler ekran
/// görüntüsünde hemen görünür. Çevirisi olmayan anahtar "⟦‼…⟧" ile işaretlenir.
/// Biçim yer tutucuları ({0}, {1:N0}) ve dosya süzgecindeki desenler ("Resimler|*.png") bozulmaz.
/// </summary>
internal static class PseudoText
{
    private const string Plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Accented = "áƀçðéƒĝĥíĵķļɱñóþǫŕšţúṽŵẋýžÅƁÇÐÉƑĜĤÍĴĶĻṀÑÓÞǪŔŠŢÚṼŴẊÝŽ";

    public static string Apply(string text, bool missing)
    {
        // Dosya süzgeci "Ad|*.ico;*.png|Tüm dosyalar|*.*": yalnızca çift sıradaki parçalar (adlar) değişir.
        if (text.Contains('|'))
        {
            var parts = text.Split('|');
            for (var i = 0; i < parts.Length; i += 2) parts[i] = Wrap(parts[i], missing);
            return string.Join("|", parts);
        }
        return Wrap(text, missing);
    }

    private static string Wrap(string text, bool missing)
    {
        var sb = new StringBuilder(text.Length * 2 + 4);
        sb.Append(missing ? "⟦‼" : "⟦");
        var letters = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '{')
            {
                // Yer tutucu olduğu gibi kopyalanır ("{{" kaçışı da).
                var end = text.IndexOf('}', i);
                if (end < 0) end = text.Length - 1;
                sb.Append(text, i, end - i + 1);
                i = end;
                continue;
            }
            var index = Plain.IndexOf(ch);
            if (index >= 0)
            {
                sb.Append(Accented[index]);
                letters++;
            }
            else
            {
                if (char.IsLetter(ch)) letters++;
                sb.Append(ch);
            }
        }
        sb.Append('~', (letters * 3 + 9) / 10);
        sb.Append('⟧');
        return sb.ToString();
    }
}
