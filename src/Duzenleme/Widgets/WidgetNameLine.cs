using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Başlık satırı olmayan küçük widget'ların (zamanlayıcı, Pomodoro, kronometre, dünya saati, takvim, sistem durumu) isteğe
/// bağlı adı (<see cref="WidgetConfig.Title"/>): aynı türden birkaç widget (ör. çay ve çamaşır zamanlayıcısı) Widget'lar
/// listesinde, tepsideki "Geri getir"de ve "Süre doldu" bildiriminde ayırt edilsin (bkz. <see cref="WidgetText"/>).
/// <para>Ad verilmemişse satır yoktur ve widget'ın görünümü değişmez; verilince widget'ın üstünde bir satırda görünür. F2 ve
/// "Yeniden adlandır" satırı yerinde düzenler (<see cref="TitleEditor"/>; Enter/dışarı tıklama kaydeder, Esc vazgeçer, boş
/// bırakılırsa ad kalkar). "Görünüm ▸ Göster ▸ Ad" satırı gizler; gizliyken yeniden adlandırma küçük bir pencere açar.
/// Title 2.0'da da vardır: bu türleri tanımayan sürüm onu korur.</para>
/// </summary>
internal sealed class WidgetNameLine
{
    /// <summary>"Göster ▸" menüsündeki parça (HiddenParts anahtarı).</summary>
    public const string PartKey = "title";

    public static (string Key, string Label) Part => (PartKey, L.N("Ad"));

    private readonly FrameworkElement _view;
    private readonly WidgetConfig _config;
    private readonly Grid _row;
    private readonly TextBlock _text;
    private readonly ShadowText? _shadow;
    private readonly Action _changed;
    private readonly TitleEditor _editor;
    private bool _textShadow;

    /// <param name="view">Widget görünümü.</param>
    /// <param name="row">Ad satırı (içinde yalnızca ad yazısı ve varsa gölgesi).</param>
    /// <param name="text">Ad yazısı.</param>
    /// <param name="shadow">Saydam kartta yazının gölgesi (zamanlayıcı); yoksa null.</param>
    /// <param name="changed">Ad değişti ve kaydedildi: görünüm yeniden çizilir (erişilebilirlik adı dahil).</param>
    public WidgetNameLine(FrameworkElement view, WidgetConfig config, Grid row, TextBlock text, ShadowText? shadow, Action changed)
    {
        _view = view;
        _config = config;
        _row = row;
        _text = text;
        _shadow = shadow;
        _changed = changed;
        _editor = new TitleEditor(view, row, text, iconButton: null, () => "",
            title =>
            {
                _config.Title = title;
                AppHost.SaveSettings();
                Render();
                _changed();
            },
            pickIcon: null, refit: () => _shadow?.Show(_textShadow && !_editor!.IsEditing))
        {
            // Vazgeçilince ya da boş kaydedilince satır yeniden gizlenir.
            Ended = _ => Render(),
        };
        Render();
    }

    /// <summary>Verilmiş ad; yoksa null.</summary>
    public string? Name => string.IsNullOrWhiteSpace(_config.Title) ? null : _config.Title.Trim();

    /// <summary>Satır görünüyor mu (ad var ve gizlenmemiş, ya da düzenleniyor)?</summary>
    public bool IsShown => _row.Visibility == Visibility.Visible;

    /// <summary>
    /// Ad şu an yazılıyor mu? Görünümün kendi kısayolları (zamanlayıcıda Boşluk/R, takvimde Home/Page Up) ve tıklayınca
    /// kendine odaklanması o sırada devre dışı kalmalı: yazılan harf kutuya gitsin.
    /// </summary>
    public bool IsEditing => _editor.IsEditing;

    /// <summary>Satırı ayara göre gösterir ya da gizler.</summary>
    public void Render()
    {
        if (!_editor.IsEditing) _text.Text = Name ?? "";
        _row.Visibility = _editor.IsEditing || (Name is not null && _config.Shows(PartKey)) ? Visibility.Visible : Visibility.Collapsed;
        _shadow?.Show(_textShadow && !_editor.IsEditing);
    }

    /// <summary>
    /// F2 ve "Yeniden adlandır": ad yerinde düzenlenir (ad yoksa satır düzenleme süresince açılır). Satır gizlenmişse ad küçük
    /// bir pencerede sorulur.
    /// </summary>
    public bool Begin()
    {
        if (_editor.IsEditing) return _editor.Begin();
        if (!_config.Shows(PartKey) || !_view.IsVisible) return AskInDialog();
        _row.Visibility = Visibility.Visible;
        if (_editor.Begin()) return true;
        Render();
        return AskInDialog();
    }

    private bool AskInDialog()
    {
        if (InputDialog.Ask(L.T("Yeniden adlandır"), L.T("Ad (boş bırakırsan adı kalkar)"), Name ?? "",
                (Window.GetWindow(_view) as WidgetWindow)?.CenterPoint) is not { } text) return true;
        _config.Title = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        AppHost.SaveSettings();
        Render();
        _changed();
        return true;
    }

    /// <summary>Widget menüsünün "Yeniden adlandır" öğesi (F2 ipucuyla).</summary>
    public MenuItem MenuItem() => Menus.Item(L.T("Yeniden adlandır"), () => Begin(), KeyNames.F2);

    public void ApplyPalette(WidgetPalette palette)
    {
        _textShadow = palette.TextShadow;
        _text.Foreground = palette.Foreground;
        _editor.ApplyPalette(palette);
        ClearTypeText.Follow(_view, _text);
        Render();
    }

    public void Cancel() => _editor.Cancel();
}
