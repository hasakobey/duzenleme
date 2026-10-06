namespace Duzenleme.Core;

/// <summary>
/// Halkanın çizimini belirleyen ayarların parmak izi (saf; testlenir): kimlik, görünüş (Style, Accent, tema, özel vurgu, simge
/// boyutu, ölçek, biçim), ad, simge, parçalar, sekmeler (ad, yollar, yol olmayan öğelerin bütün alanları), öğe adları/simgeleri
/// ve verilen Windows durumu. Açılır halka, düzenleyicinin önizlemesi ve halka widget'ı ilgisiz bir kayıtta (konum, başka
/// widget) yeniden kurulmasın diye karşılaştırılır. Serileştirme yok: alanlar 64 bit FNV-1a ile tek geçişte karılır; yalnızca
/// bellekte, aynı oturumda karşılaştırılır.
/// </summary>
public static class RingFingerprint
{
    private const ulong Offset = 14695981039346656037UL, Prime = 1099511628211UL;

    public static ulong Of(WidgetConfig config, bool highContrast, bool systemDark)
    {
        var h = new Hasher();
        h.Add(config.Id);
        h.Add((int)config.Style);
        h.Add((int)config.Accent);
        h.Add(config.LookTheme);
        h.Add(config.LookAccentColor);
        h.Add((int)config.IconSize);
        h.Add(BitConverter.DoubleToInt64Bits(config.Scale));
        h.Add(config.Title);
        h.Add(config.Icon);
        h.Add(config.RingStyle);
        h.Add(config.Variant);
        h.Add(config.HiddenParts.Count);
        foreach (var part in config.HiddenParts) h.Add(part);
        h.Add(config.Tabs.Count);
        foreach (var tab in config.Tabs)
        {
            h.Add(tab.Name);
            h.Add(tab.Items.Count);
            foreach (var item in tab.Items) h.Add(item);
            h.Add(tab.Actions?.Count ?? -1);
            if (tab.Actions is { } actions)
                foreach (var a in actions)
                {
                    h.Add(a.Id);
                    h.Add(a.Type);
                    h.Add(a.PathsBefore);
                    h.Add(a.Name);
                    h.Add(a.Icon);
                    h.Add(a.Url);
                    h.Add(a.Command);
                    h.Add(a.Args);
                    h.Add(a.Dir);
                    h.Add(a.HideWindow ? 1 : 0);
                    h.Add(a.Admin ? 1 : 0);
                    h.Add(a.Disabled ? 1 : 0);
                    h.Add(a.Keys?.Count ?? -1);
                    if (a.Keys is { } keys)
                        foreach (var key in keys) h.Add(key);
                    h.Add(a.Text);
                    h.Add(a.App);
                }
        }
        h.Add(config.ItemLooks?.Count ?? -1);
        if (config.ItemLooks is { } looks)
            // Sözlüğün sırası ekleme sırasıdır; içerik aynıysa sıra fark etmesin.
            foreach (var (path, look) in looks.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                h.Add(path);
                h.Add(look?.Name);
                h.Add(look?.Icon);
            }
        h.Add(highContrast ? 1 : 0);
        h.Add(systemDark ? 1 : 0);
        return h.Value;
    }

    private struct Hasher()
    {
        public ulong Value = Offset;

        public void Add(string? text)
        {
            if (text is null)
            {
                Add(-1);
                return;
            }
            Add(text.Length);
            foreach (var ch in text) Mix(ch);
        }

        public void Add(long value)
        {
            for (var i = 0; i < 8; i++)
            {
                Mix((byte)value);
                value >>= 8;
            }
        }

        private void Mix(uint b)
        {
            Value ^= b;
            Value *= Prime;
        }
    }
}
