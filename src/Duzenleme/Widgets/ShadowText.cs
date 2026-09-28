using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Duzenleme.Widgets;

/// <summary>
/// Cam görünümündeki büyük yazıların (saat, tarih) gölgesi: yazının 1 DIP altında duran yarı saydam koyu kopyası,
/// açık duvar kağıdında da okunurluk için. Eskiden bütün görünüme bulanıklık efekti (DropShadowEffect) veriliyordu;
/// yazılımla çizilen pencerede o efekt her saniye bütün widget'ı yeniden bulanıklaştırıyordu. Kopya, asıl yazıyla aynı
/// stil ve metni alır (XAML'da <c>Text="{Binding Text, ElementName=…}"</c>), tıklanmaz ve ekran okuyucuya ikinci kez okunmaz.
/// Başta gizlidir; görünüm Cam paletinde <see cref="Show"/> ile açar.
/// </summary>
public sealed class ShadowText : TextBlock
{
    private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)));
    private static readonly Transform Offset = Frozen(new TranslateTransform(0, 1));

    public ShadowText()
    {
        Foreground = Ink;
        RenderTransform = Offset;
        IsHitTestVisible = false;
        Focusable = false;
        Visibility = Visibility.Collapsed;
    }

    public void Show(bool visible) => Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    protected override AutomationPeer? OnCreateAutomationPeer() => null;

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
