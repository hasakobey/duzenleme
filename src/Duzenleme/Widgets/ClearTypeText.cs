using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Duzenleme.Widgets;

/// <summary>
/// Opak kartta küçük yazının ClearType ile (renkli alt piksellerle, daha keskin) çizilmesi. WidgetWindow ipucunu görünüme
/// verir (<see cref="RenderOptions.ClearTypeHintProperty"/>; yalnızca zemin tam opak, saydamlık 1 ve "soluk dursun" kapalıyken
/// Enabled). WPF kırpılan bir alanın (kaydırılan liste, TextBox'ın içerik alanı, kısaltılan yazı) içinde ipucunu yeniden
/// kapatır: oradaki yazılara aynı değer ayrıca verilir. Yarı saydam (Opacity &lt; 1) öğeye ya da içine verilmez: saydam
/// katmanda ClearType renkli saçak bırakır; soluk yazı gri tonlamalı kalır.
/// </summary>
internal static class ClearTypeText
{
    /// <summary>Görünümün ipucu (WidgetWindow'un verdiği).</summary>
    public static ClearTypeHint Of(UIElement view) => RenderOptions.GetClearTypeHint(view);

    /// <summary>Yazılara görünümün ipucunu verir.</summary>
    public static void Follow(UIElement view, params UIElement[] texts)
    {
        var hint = Of(view);
        foreach (var text in texts) RenderOptions.SetClearTypeHint(text, hint);
    }

    /// <summary>
    /// TextBox: yazıyı çizen iç öğe (içerik alanının içi) kırpılan alandadır; ipucu hem kutuya hem ona verilir. Şablon henüz
    /// uygulanmadıysa uygulanınca verilir.
    /// </summary>
    public static void Apply(TextBox box, ClearTypeHint hint)
    {
        RenderOptions.SetClearTypeHint(box, hint);
        if (!TrySetHost(box, hint))
            box.Loaded += OnLoaded;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            box.Loaded -= OnLoaded;
            TrySetHost(box, RenderOptions.GetClearTypeHint(box));
        }
    }

    private static bool TrySetHost(TextBox box, ClearTypeHint hint)
    {
        if (box.Template?.FindName("PART_ContentHost", box) is not FrameworkElement host) return false;
        RenderOptions.SetClearTypeHint(host, hint);
        var inner = host switch
        {
            ScrollViewer scroll => scroll.Content as UIElement,
            Decorator decorator => decorator.Child,
            _ => null,
        };
        if (inner is null) return false;
        RenderOptions.SetClearTypeHint(inner, hint);
        return true;
    }
}
