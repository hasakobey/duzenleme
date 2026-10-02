namespace Duzenleme.Core;

/// <summary>Tepsi menüsünün komutları (yalnızca çalışma zamanı; hiçbir yere yazılmaz).</summary>
public enum TrayCommand
{
    Separator,
    QuickAdd,
    OpenApp,
    PeekDesktop,
    AutoMove,
    OrganizeNow,
    UndoLastMove,
    UndoRemove,
    ToggleDesktop,
    OpenSettings,
    Exit,
}

/// <summary>
/// Menü açılırken okunan durum. Kısayollar menüde öğenin sağında yazar (atanmamışsa yazmaz): QuickAddHotkey "Widget ekle…",
/// PeekHotkey göz atma, OrganizeHotkey "Dosyaları şimdi taşı".
/// </summary>
public sealed record TrayMenuState(
    bool AutoMove,
    bool DesktopHidden,
    bool Peeking,
    string? PeekHotkey,
    string? LastRemovedName,
    string? QuickAddHotkey = null,
    string? OrganizeHotkey = null);

/// <summary>Tepsi menüsünün bir öğesi. Text görünen metindir (Windows menüsüne <see cref="NativeText"/> verilir).</summary>
public sealed record TrayMenuItem(TrayCommand Command, string Text = "", string? Shortcut = null, bool IsDefault = false,
    bool IsChecked = false)
{
    public bool IsSeparator => Command == TrayCommand.Separator;

    /// <summary>Windows menüsüne verilecek metin: "&amp;" erişim tuşu sayılmasın diye ikilenir, kısayol sekmeden sonra sağa yaslanır.</summary>
    public string NativeText => Shortcut is null ? TrayText.MenuText(Text) : TrayText.MenuText(Text) + "\t" + TrayText.MenuText(Shortcut);
}

/// <summary>
/// Tepsi menüsünün içeriği (saf; her açılışta yeniden kurulur). Kolay NestDesk: alt menü yok. Sıra: varsayılan (kalın)
/// "Widget ekle…", "NestDesk'i aç", göz atma ("NestDesk'e dön"); işaretli "Otomatik taşıma", "Dosyaları şimdi taşı", "Son
/// taşımayı geri al"; yalnızca gerektiğinde "Geri getir: …" (kaldırılmış widget varken) ve "Masaüstünü göster" (masaüstü
/// gizliyken: geri dönüş yolu), "Ayarlar", "Çıkış". Windows masaüstü simgeleri Ayarlar'da; widget'ları öne getirmek kısayolda.
/// </summary>
public static class TrayMenuModel
{
    public static IReadOnlyList<TrayMenuItem> Build(TrayMenuState state)
    {
        var separator = new TrayMenuItem(TrayCommand.Separator);
        var items = new List<TrayMenuItem>
        {
            // Her ekleme yolu aynı "Widget ekle" penceresini açar (bölme, araç ve "Masaüstümü bölmelere ayır" orada).
            new(TrayCommand.QuickAdd, L.T("Widget ekle…"), Shortcut: Key(state.QuickAddHotkey), IsDefault: true),
            new(TrayCommand.OpenApp, L.F("{0}'i aç", AppInfo.Name)),
            new(TrayCommand.PeekDesktop, state.Peeking ? L.F("{0}'e dön", AppInfo.Name) : L.T("Windows masaüstüne göz at"),
                Shortcut: Key(state.PeekHotkey)),
            separator,
            // İşaretli = otomatik taşıma açık (Paused'ın tersi).
            new(TrayCommand.AutoMove, L.T("Otomatik taşıma"), IsChecked: state.AutoMove),
            new(TrayCommand.OrganizeNow, L.T("Dosyaları şimdi taşı"), Shortcut: Key(state.OrganizeHotkey)),
            new(TrayCommand.UndoLastMove, L.T("Son taşımayı geri al")),
            separator,
        };
        if (state.LastRemovedName is { } removed)
            items.Add(new(TrayCommand.UndoRemove, L.F("Geri getir: {0}", removed)));
        // Masaüstü gizliyken (Ctrl+Alt+H, çift tıklama) geri dönüş yolu; gizlemek kısayolda ve Ayarlar'da.
        if (state.DesktopHidden)
            items.Add(new(TrayCommand.ToggleDesktop, L.T("Masaüstünü göster")));
        items.Add(new(TrayCommand.OpenSettings, L.T("Ayarlar")));
        items.Add(new(TrayCommand.Exit, L.T("Çıkış")));
        return items;
    }

    /// <summary>Kısayol sütunu: atanmamışsa (boş) yok.</summary>
    private static string? Key(string? hotkey) => string.IsNullOrWhiteSpace(hotkey) ? null : hotkey;
}

/// <summary>Tepsi metinlerinin Windows sınırlarına uydurulması (saf).</summary>
public static class TrayText
{
    /// <summary>İpucu (NOTIFYICONDATA.szTip) en çok 127 karakter.</summary>
    public const int TipMax = 127;
    /// <summary>Balon başlığı (szInfoTitle) en çok 63 karakter.</summary>
    public const int TitleMax = 63;
    /// <summary>Balon metni (szInfo) en çok 255 karakter.</summary>
    public const int BodyMax = 255;

    /// <summary>Metni en çok <paramref name="max"/> karaktere kısaltır (kısaltınca sonuna "…"; vekil çifti bölünmez).</summary>
    public static string Fit(string? text, int max)
    {
        text ??= "";
        if (text.Length <= max) return text;
        var cut = max - 1;
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1])) cut--;
        return text[..cut] + "…";
    }

    /// <summary>
    /// Windows menü metni: "&amp;" erişim tuşu işareti sayılmasın diye ikilenir (widget adı "A &amp; B" olduğu gibi görünsün);
    /// sekme ve satır sonu boşluk olur (sekme kısayol sütununu başlatır).
    /// </summary>
    public static string MenuText(string text) =>
        text.Replace("&", "&&").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
