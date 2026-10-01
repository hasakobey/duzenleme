namespace Duzenleme.Core;

/// <summary>Tepsi menüsünün komutları (yalnızca çalışma zamanı; hiçbir yere yazılmaz).</summary>
public enum TrayCommand
{
    Separator,
    QuickAdd,
    OpenApp,
    AutoMove,
    OrganizeNow,
    UndoLastMove,
    ToggleDesktop,
    PeekDesktop,
    UndoRemove,
    IconModes,
    IconMode,
    RevealWidgets,
    Exit,
}

/// <summary>Menü açılırken okunan durum. IconModes: "Windows masaüstü simgeleri" seçeneklerinin etiketleri, sırasıyla.</summary>
public sealed record TrayMenuState(
    bool AutoMove,
    bool DesktopHidden,
    bool Peeking,
    string? PeekHotkey,
    string? LastRemovedName,
    IReadOnlyList<string> IconModes,
    int CurrentIconMode);

/// <summary>
/// Tepsi menüsünün bir öğesi. Text görünen metindir (Windows menüsüne <see cref="NativeText"/> verilir). Choice: alt menüdeki
/// seçeneğin sırası (IconMode için), öbürlerinde -1.
/// </summary>
public sealed record TrayMenuItem(TrayCommand Command, string Text = "", string? Shortcut = null, bool IsDefault = false,
    bool IsChecked = false, bool IsRadio = false, int Choice = -1, IReadOnlyList<TrayMenuItem>? Children = null)
{
    public bool IsSeparator => Command == TrayCommand.Separator;

    /// <summary>Windows menüsüne verilecek metin: "&amp;" erişim tuşu sayılmasın diye ikilenir, kısayol sekmeden sonra sağa yaslanır.</summary>
    public string NativeText => Shortcut is null ? TrayText.MenuText(Text) : TrayText.MenuText(Text) + "\t" + TrayText.MenuText(Shortcut);
}

/// <summary>
/// Tepsi menüsünün içeriği (saf; her açılışta yeniden kurulur). Sıra, metinler ve işaretler 2.1.1'deki menüyle aynıdır:
/// varsayılan (kalın) "Widget ekle…", işaretli "Otomatik taşıma", radyo seçimli "Windows masaüstü simgeleri", kısayolu
/// yazılı göz atma; "Geri getir" yalnızca kaldırılmış bir widget varken görünür.
/// </summary>
public static class TrayMenuModel
{
    public static IReadOnlyList<TrayMenuItem> Build(TrayMenuState state)
    {
        var separator = new TrayMenuItem(TrayCommand.Separator);
        var items = new List<TrayMenuItem>
        {
            // Her ekleme yolu aynı "Widget ekle" penceresini açar (bölme, araç ve "Masaüstümü bölmelere ayır" orada).
            new(TrayCommand.QuickAdd, L.T("Widget ekle…"), IsDefault: true),
            new(TrayCommand.OpenApp, L.F("{0}'i aç", AppInfo.Name)),
            separator,
            // İşaretli = otomatik taşıma açık (Paused'ın tersi).
            new(TrayCommand.AutoMove, L.T("Otomatik taşıma"), IsChecked: state.AutoMove),
            new(TrayCommand.OrganizeNow, L.T("Masaüstünü şimdi düzenle")),
            new(TrayCommand.UndoLastMove, L.T("Son taşımayı geri al")),
            new(TrayCommand.ToggleDesktop, state.DesktopHidden ? L.T("Masaüstünü göster") : L.T("Masaüstünü gizle")),
            new(TrayCommand.PeekDesktop, state.Peeking ? L.F("{0}'e dön", AppInfo.Name) : L.T("Windows masaüstüne göz at"),
                Shortcut: string.IsNullOrWhiteSpace(state.PeekHotkey) ? null : state.PeekHotkey),
            separator,
        };
        if (state.LastRemovedName is { } removed)
            items.Add(new(TrayCommand.UndoRemove, L.F("Geri getir: {0}", removed)));
        // Windows masaüstü simgeleri: Ayarlar ve Widget'lar sayfasıyla aynı üç seçenek (Views.DesktopModes).
        var modes = state.IconModes.Select((label, i) =>
            new TrayMenuItem(TrayCommand.IconMode, label, IsChecked: i == state.CurrentIconMode, IsRadio: true, Choice: i)).ToList();
        items.Add(new(TrayCommand.IconModes, L.T("Windows masaüstü simgeleri"), Children: modes));
        items.Add(new(TrayCommand.RevealWidgets, L.T("Widget'ları öne getir (5 sn)")));
        items.Add(separator);
        items.Add(new(TrayCommand.Exit, L.T("Çıkış")));
        return items;
    }
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
