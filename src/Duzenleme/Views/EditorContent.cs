using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>Düzenle panelindeki bir seçim seçeneği (bölümlü seçim ya da yonga).</summary>
/// <param name="Id">Seçimin değeri (<c>selected</c> bununla karşılaştırılır).</param>
/// <param name="Label">Görünen ad.</param>
/// <param name="AutomationId">UI Automation kimliği (ör. "Editor.Fence.Folders").</param>
/// <param name="OpensWindow">Pencere açar ("Özel…", "Başka…"): oklarla üstüne gelince seçilmez, seçiliyken basılınca yine açar.</param>
/// <param name="Menu">Basılınca seçmek yerine altında bu menüyü açar ("Bir klasör seç…").</param>
/// <param name="HelpText">Ekran okuyucunun okuyacağı açıklama (yoksa seçimin başlığı).</param>
public sealed record EditorChoice(string Id, string Label, string AutomationId, bool OpensWindow = false, Func<ContextMenu>? Menu = null,
    string? HelpText = null);

/// <summary>
/// Düzenle panelinin "widget'ın ayarları" bölümü, görünümün kendi denetimleriyle (<see cref="IWidgetView.AddEditorContent"/>:
/// bölme, kısayol kutusu, not, zamanlayıcı, geri sayım). Satırlar panelin öbür satırlarıyla aynıdır (<see cref="EditorRows"/>):
/// durumu okuyucudan alır, panel bir şey değişince hepsini modelden tazeler, her denetimin adı ve AutomationId'si vardır;
/// fareyle hiçbir şey belirmez ya da önizlenmez. Görünüm burada WPF-UI türlerine dokunmaz. Seçimden sonra panel kendini tazeler;
/// panelin yapısını değiştiren seçim (zamanlayıcının türü, kutunun sekmeleri) <c>rebuild: true</c> ile paneli yeniden kurdurur.
/// </summary>
public sealed class EditorContent
{
    private readonly Panel _panel;
    private readonly ICollection<Action> _refreshers;
    private readonly Action<bool> _changed;
    private readonly Action<string, Action> _notice;

    internal EditorContent(Panel panel, ICollection<Action> refreshers, Action<bool> changed, Action<string, Action> notice)
    {
        _panel = panel;
        _refreshers = refreshers;
        _changed = changed;
        _notice = notice;
    }

    /// <summary>Panelin penceresi (dosya ve klasör seçicilerin sahibi); panel bir sayfadaysa o sayfanın penceresi.</summary>
    public Window? Owner => Window.GetWindow(_panel);

    /// <summary>Bir şey değişti: satırlar modelden tazelenir; <paramref name="rebuild"/>: panel yeniden kurulur.</summary>
    public void Changed(bool rebuild = false) => _changed(rebuild);

    /// <summary>Panelin içinde bildirim satırı: metin ve "Geri al" (ör. bölmenin kaynağı değişti).</summary>
    public void Notice(string text, Action undo) => _notice(text, undo);

    /// <summary>
    /// Görünümün kendi denetimi (halka düzenleyicisi, <see cref="RingEditor"/>): panele olduğu gibi eklenir; panel tazelenince
    /// <paramref name="refresh"/> çağrılır.
    /// </summary>
    public void Custom(FrameworkElement element, Action? refresh = null)
    {
        if (refresh is not null) _refreshers.Add(refresh);
        _panel.Children.Add(element);
    }

    /// <summary>Gri açıklama satırı (değişebilir: tazelemede yeniden okunur).</summary>
    public void Text(Func<string> text)
    {
        var block = EditorRows.Muted(text(), top: 2);
        _refreshers.Add(() => block.Text = text());
        _panel.Children.Add(block);
    }

    /// <summary>Anahtar satırı.</summary>
    public void Switch(string label, string automationId, Func<bool> isOn, Action<bool> set, bool rebuild = false) =>
        _panel.Children.Add(EditorRows.Switch(() => label, automationId, isOn, on =>
        {
            set(on);
            _changed(rebuild);
        }, _refreshers));

    /// <summary>Bölümlü seçim (en çok dört kısa seçenek yan yana).</summary>
    public void Segments(string? label, IReadOnlyList<EditorChoice> options, Func<string?> selected, Action<string> choose, bool rebuild = false) =>
        _panel.Children.Add(EditorRows.Segments(label is null ? null : () => label, Map(options), selected, Choose(choose, rebuild), _refreshers));

    /// <summary>Yongalar (çok seçenek; satıra sığdıkça dizilir).</summary>
    public void Chips(string? label, IReadOnlyList<EditorChoice> options, Func<string?> selected, Action<string> choose, bool rebuild = false) =>
        _panel.Children.Add(EditorRows.Segments(label is null ? null : () => label, Map(options), selected, Choose(choose, rebuild), _refreshers,
            wrap: true));

    /// <summary>Açılır liste (uzun seçenekler).</summary>
    public void Dropdown(string label, string automationId, IReadOnlyList<string> items, Func<int> selected, Action<int> choose, bool rebuild = false) =>
        _panel.Children.Add(EditorRows.Dropdown(() => label, automationId, items, selected, i =>
        {
            choose(i);
            _changed(rebuild);
        }, _refreshers));

    /// <summary>Renk örnekleri (notun kâğıt rengi).</summary>
    public void Swatches(string label, IReadOnlyList<(string Id, string Label, Brush Fill, string AutomationId)> swatches, Func<string?> selected,
        Action<string> choose) =>
        _panel.Children.Add(EditorRows.Swatches(() => label, swatches, selected, id =>
        {
            choose(id);
            _changed(false);
        }, _refreshers));

    /// <summary>Gün seçici.</summary>
    public void Date(string label, string automationId, Func<DateTime> selected, Action<DateTime> choose) =>
        _panel.Children.Add(EditorRows.Date(() => label, automationId, selected, day =>
        {
            choose(day);
            _changed(false);
        }, _refreshers));

    /// <summary>Yan yana düğmeler; basılınca iş yapılır, sonra panel tazelenir (<paramref name="rebuild"/>: yeniden kurulur).</summary>
    public void Buttons(IEnumerable<(string Label, string AutomationId, Action Click)> buttons, bool rebuild = false) =>
        _panel.Children.Add(EditorRows.ButtonBar(buttons.Select(b => (b.Label, b.AutomationId, (Action)(() =>
        {
            b.Click();
            _changed(rebuild);
        })))));

    /// <summary>Tek düğme.</summary>
    public void Button(string label, string automationId, Action click, bool rebuild = false) => Buttons([(label, automationId, click)], rebuild);

    /// <summary>Öğe listesi: yalnızca seçili satırda "Kaldır" · "↑" · "↓".</summary>
    public void Items(string label, string emptyText, Func<IReadOnlyList<TileItem>> items, Action<string> remove, Action<string, int> move) =>
        _panel.Children.Add(EditorRows.ItemList(() => label, emptyText, items, path =>
        {
            remove(path);
            _changed(false);
        }, (path, delta) =>
        {
            move(path, delta);
            _changed(false);
        }, _refreshers));

    /// <summary>
    /// Bölme ve kısayol kutusunun görünüşü: "Görünüş: Izgara · Liste" ve "Simge boyutu" (Ctrl + tekerleğin adımları). Seyrek simge
    /// seçenekleri "Daha fazla"dadır (<see cref="Menus.TileOptions"/>).
    /// </summary>
    public void TileLook(WidgetConfig c, Action<Action> change)
    {
        Segments(L.T("Görünüş"),
            [new("Icons", L.T("Izgara"), "Editor.View.Icons"), new("List", L.T("Liste"), "Editor.View.List")],
            () => c.View.ToString(), id => change(() => c.View = id == "List" ? ItemView.List : ItemView.Icons));
        Segments(L.T("Simge boyutu"),
            [
                new(nameof(IconSize.Small), L.T("Küçük"), "Editor.IconSize.Small"), new(nameof(IconSize.Medium), L.T("Orta"), "Editor.IconSize.Medium"),
                new(nameof(IconSize.Large), L.T("Büyük"), "Editor.IconSize.Large"),
                new(nameof(IconSize.ExtraLarge), L.T("Çok büyük"), "Editor.IconSize.ExtraLarge"),
            ],
            () => c.IconSize.ToString(), id =>
            {
                if (Enum.TryParse<IconSize>(id, out var size)) change(() => c.IconSize = size);
            });
    }

    private Action<string> Choose(Action<string> choose, bool rebuild) => id =>
    {
        choose(id);
        _changed(rebuild);
    };

    private static List<EditorRows.Segment> Map(IReadOnlyList<EditorChoice> options) =>
        options.Select(o => new EditorRows.Segment(o.Id, o.Label, o.AutomationId, SelectsOnArrow: !o.OpensWindow, o.Menu, o.HelpText)).ToList();
}
