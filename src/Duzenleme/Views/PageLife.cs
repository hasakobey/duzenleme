using System.Windows;

namespace Duzenleme.Views;

/// <summary>
/// Ana pencere sayfalarının olay abonelikleri. Pencere kapatılınca yalnızca gizlenir; sayfalar yüklü kalır ve eskiden her
/// ayar kaydında (widget sürüklemek de kaydeder) görünmeden yeniden hesaplanır, masaüstünü okurdu. Abonelik yalnızca sayfa
/// hem yüklü hem görünürken açıktır; gizliyken kaçırılanlar yeniden göründüğünde bir kez güncellenir.
/// </summary>
internal static class PageLife
{
    /// <param name="page">Sayfa.</param>
    /// <param name="attach">Olaylara abone ol (sayfa görünür oldu).</param>
    /// <param name="detach">Abonelikleri bırak (sayfa gizlendi ya da kaldırıldı).</param>
    /// <param name="refresh">Gizliyken kaçırılanları güncelle (yeniden göründü). Loaded zaten yeniliyorsa ilk yüklemede çağrılmaz.</param>
    public static void WhileShown(FrameworkElement page, Action attach, Action detach, Action? refresh = null)
    {
        var attached = false;
        var missed = false;

        void Sync(bool unloading)
        {
            var want = page.IsLoaded && page.IsVisible && !unloading;
            if (want != attached)
            {
                attached = want;
                if (want)
                {
                    attach();
                    if (missed) refresh?.Invoke();
                    missed = false;
                }
                else
                {
                    detach();
                    missed = true;
                }
            }
            // Sayfa kaldırıldıysa yeniden yüklenince Loaded zaten yeniler; yalnızca gizlenme "kaçırılan" sayılır.
            if (unloading) missed = false;
        }

        page.Loaded += (_, _) => Sync(false);
        page.Unloaded += (_, _) => Sync(true);
        page.IsVisibleChanged += (_, _) => Sync(false);
    }
}
