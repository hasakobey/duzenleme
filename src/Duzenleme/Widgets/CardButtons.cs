using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Kartın köşe düğmeleri (Kolay NestDesk): ⋯ "Düzenle" Düzenle panelini açar (sağ tık menüsünün ilk satırıyla aynı), × widget'ı
/// kaldırır ("Geri al" bildirimiyle). Konumu kilitli widget'ta ×'in yerinde kilit durur ve tek tıkla kilidi açar. Düğmeler hep
/// yerindedir (fareyle belirip kaybolmaz), ⋯ en az 28 DIP'tir. ⋯ ve kilit ikincil yazı renginde sönüktür (ClearType'lı kartta
/// saydamlıkla değil renkle; <see cref="WidgetPalette.Secondary"/>), × kendi şablonunda. "Göster" ▸ "Düzenle düğmesi (⋯)" =
/// HiddenParts "edit" (<see cref="Menus.EditPart"/>), "Kaldır düğmesi (×)" = "close" (<see cref="Menus.ClosePart"/>; kapalıyken
/// kilit de görünmez). Başlıklı widget bunu başlığın sağ ucunda, başlıksız widget kartın sağ üst köşesinde taşır; başlık
/// sığdırılırken (<see cref="HeaderFitter"/>) gizlenmez: görünüm onu sabit parça olarak verir. Görünüm
/// <see cref="Paint"/> (ApplyPalette) ve <see cref="Update"/> (parçalar) çağırır.
/// </summary>
public sealed class CardButtons : StackPanel
{
    // Segoe Fluent Icons / MDL2: More (⋯) ve Lock; ikisi de BMP içinde.
    private const string EditGlyph = "";
    private const string LockGlyph = "";

    private readonly Button _edit;
    private readonly Button _close;
    private WidgetConfig? _config;
    private bool? _locked;

    public CardButtons()
    {
        Orientation = Orientation.Horizontal;
        _edit = new Button { Content = EditGlyph, ToolTip = L.T("Düzenle") };
        _edit.SetResourceReference(StyleProperty, "WidgetGlyphButton");
        AutomationProperties.SetAutomationId(_edit, "Widget.Edit");
        AutomationProperties.SetName(_edit, L.T("Düzenle"));
        _edit.Click += (_, _) => (Window.GetWindow(this) as WidgetWindow)?.OpenEditor();

        // × ile kilit aynı yerde ve aynı boyda: kilitlenince başlık kaymaz.
        _close = new Button { Width = 26, Height = 26, Margin = new Thickness(0) };
        _close.Click += (_, _) =>
        {
            if (_config is not { } c || AppHost.Widgets is not { } widgets) return;
            if (c.Locked) widgets.Unlock(c.Id);
            else widgets.RemoveWithUndo(c.Id);
        };
        Children.Add(_edit);
        Children.Add(_close);
        ShowLock(false);
    }

    /// <summary>Kilitli widget'ta ×'in yerindeki düğme kilit mi (test ve erişilebilirlik)?</summary>
    internal bool ShowsLock => _locked == true;

    /// <summary>⋯ düğmesi (test).</summary>
    internal Button EditButton => _edit;

    /// <summary>× ya da kilit düğmesi (test).</summary>
    internal Button CloseButton => _close;

    /// <summary>
    /// Ayara göre: ⋯ ve × (ya da kilit) görünsün mü. Görünüm parçalarını uygularken (ApplyParts, Render) çağırır; kilit ve
    /// parçalar değişince WidgetWindow görünümü yeniden çizdirir.
    /// </summary>
    public void Update(WidgetConfig config)
    {
        _config = config;
        var edit = config.Shows(Menus.EditPart.Key);
        var close = config.Shows(Menus.ClosePart.Key);
        _edit.Visibility = edit ? Visibility.Visible : Visibility.Collapsed;
        _close.Visibility = close ? Visibility.Visible : Visibility.Collapsed;
        Visibility = edit || close ? Visibility.Visible : Visibility.Collapsed;
        ShowLock(config.Locked);
        Colorize();
    }

    /// <summary>Kartın paleti: × yazı renginde, ⋯ ve kilit ikincil (sönük) renkte. Görünüm ApplyPalette'te çağırır.</summary>
    public void Paint(WidgetPalette palette)
    {
        _foreground = palette.Foreground;
        _dim = palette.Secondary;
        Colorize();
    }

    private Brush? _foreground, _dim;

    private void Colorize()
    {
        if (_foreground is null || _dim is null) return;
        _edit.Foreground = _dim;
        _close.Foreground = _locked == true ? _dim : _foreground;
    }

    private void ShowLock(bool locked)
    {
        if (_locked == locked) return;
        _locked = locked;
        if (locked)
        {
            _close.SetResourceReference(StyleProperty, "WidgetGlyphButton");
            _close.Content = LockGlyph;
            _close.ToolTip = L.T("Kilidi aç");
            AutomationProperties.SetName(_close, L.T("Kilidi aç"));
            AutomationProperties.SetAutomationId(_close, "Widget.Unlock");
        }
        else
        {
            _close.SetResourceReference(StyleProperty, "WidgetCloseButton");
            _close.Content = null;
            // Adı ve ipucu ×'in kendi stilinden ("Widget'ı kaldır").
            _close.ClearValue(ToolTipProperty);
            _close.ClearValue(AutomationProperties.NameProperty);
            AutomationProperties.SetAutomationId(_close, "Widget.Remove");
        }
    }
}
