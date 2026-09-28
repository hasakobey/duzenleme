using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Duzenleme.Widgets;

/// <summary>
/// İşaretli seçenek satırı (Arka plan ▸ Koyu, Gölge, Göster ▸ Başlık satırı…). İşaret her zaman modelden okunur: WPF
/// tıklamada işareti kendisi çeviremez (IsChecked, <c>isOn</c>'a zorlanır), bu yüzden seçili radyo seçeneğine tıklamak onu
/// boşa düşürmez. Tıklama <c>apply</c>'ı çalıştırır, ardından <see cref="Menus.Sync"/> menünün bütün işaretlerini yerinde
/// tazeler (menü yeniden kurulmaz, açık alt menüler kapanmaz).
/// <para>Varsayılan olarak tıklanınca menü açık kalır: seçenekler art arda denenir, widget hemen değişir. Komutlar
/// (<see cref="Menus.Item"/>) ve kapsamlı değişiklikler (<c>staysOpen: false</c>) menüyü kapatır. Ekran okuyucunun
/// "Aç/Kapat"ı (UI Automation Toggle) fare tıklamasıyla aynı yoldan geçer; WPF'inki yalnızca işareti çevirirdi, ayar
/// değişmezdi.</para>
/// </summary>
public sealed class OptionMenuItem : MenuItem
{
    private readonly Func<bool> _isOn;
    private readonly Action _apply;

    static OptionMenuItem() =>
        IsCheckedProperty.OverrideMetadata(typeof(OptionMenuItem),
            new FrameworkPropertyMetadata(false, null, (d, value) => d is OptionMenuItem { _isOn: { } isOn } ? isOn() : value));

    public OptionMenuItem(object header, Func<bool> isOn, Action apply, bool staysOpen = true)
    {
        // Türetilmiş sınıf örtük MenuItem stilini (Views/QuietStyles.xaml) kendiliğinden almaz; almazsa klasik WPF menüsü çizilir.
        SetResourceReference(StyleProperty, typeof(MenuItem));
        _isOn = isOn;
        _apply = apply;
        Header = header;
        IsCheckable = true; // WPF-UI şablonu işaret kutusunu yalnızca IsCheckable iken çizer; UIA Toggle da buna bağlı
        StaysOpenOnClick = staysOpen;
        Click += (_, _) => Apply();
        Refresh();
    }

    /// <summary>İşareti modelden yeniden okur.</summary>
    public void Refresh() => CoerceValue(IsCheckedProperty);

    private void Apply()
    {
        _apply();
        Menus.Sync(this);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    /// <summary>
    /// UI Automation Toggle (Narrator'ın "Aç/Kapat"ı): fare tıklamasıyla aynı yol (OnClick → PreviewClick → Click). Menü
    /// seçeneğe göre açık kalır ya da kapanır; ayar uygulanır ve işaret modelden gelir.
    /// </summary>
    private sealed class OptionPeer(OptionMenuItem owner) : MenuItemAutomationPeer(owner), IToggleProvider
    {
        ToggleState IToggleProvider.ToggleState => owner.IsChecked ? ToggleState.On : ToggleState.Off;

        void IToggleProvider.Toggle()
        {
            if (!IsEnabled()) throw new ElementNotEnabledException();
            owner.OnClick();
        }
    }
}
