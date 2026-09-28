using System.Windows;
using System.Windows.Controls.Primitives;

namespace Duzenleme.Views;

/// <summary>
/// Animasyonsuz katlanır bölüm. Görünüm tek kalıptır: Border Style=Surface içinde Grid [simge | başlık + Muted açıklama |
/// sağda ui:Button "Göster"]; altında içerik StackPanel'i (Visibility=Collapsed).
/// </summary>
internal static class Fold
{
    /// <summary>Düğmeye basınca content anında açılır/kapanır (Visibility). Düğme metni "Göster" / "Gizle".
    /// firstOpen yalnızca ilk açılışta çalışır (ör. simgeleri yükle). Başlangıçta kapalı.</summary>
    public static void Attach(ButtonBase toggle, UIElement content, Action? firstOpen = null)
    {
        content.Visibility = Visibility.Collapsed;
        toggle.Content = L.T("Göster");
        var opened = false;
        toggle.Click += (_, _) =>
        {
            var open = content.Visibility != Visibility.Visible;
            if (open && !opened)
            {
                opened = true;
                firstOpen?.Invoke();
            }
            content.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = open ? L.T("Gizle") : L.T("Göster");
        };
    }
}
