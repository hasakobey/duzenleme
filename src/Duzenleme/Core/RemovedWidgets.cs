namespace Duzenleme.Core;

/// <summary>
/// Kaldırılan bir widget'ı geri getirmek için gerekenler: ayarlarının kopyası ve listedeki yeri. ModeTurnedOff: kaldırma
/// yüzünden "simgeler yalnızca bölmelerde" modu kapandıysa geri getirince yeniden açılır.
/// </summary>
public sealed record RemovedWidget(WidgetConfig Copy, int Index, bool ModeTurnedOff);

/// <summary>
/// Son kaldırılan widget'lar (en yenisi sonda, en çok <see cref="Capacity"/> tane). Her "Geri al" kendi widget'ını kimliğiyle
/// geri getirir: bildirim açıkken başka bir widget kaldırılsa da yanlış widget gelmez, önce kaldırılan da kaybolmaz.
/// Yalnızca arayüz iş parçacığında kullanılır.
/// </summary>
public sealed class RemovedWidgets
{
    public const int Capacity = 10;

    private readonly List<RemovedWidget> _items = [];

    /// <summary>En son kaldırılan (tepsi menüsündeki "Geri getir"); yoksa null.</summary>
    public RemovedWidget? Latest => _items.Count > 0 ? _items[^1] : null;

    public void Add(RemovedWidget removed)
    {
        _items.RemoveAll(r => r.Copy.Id == removed.Copy.Id);
        _items.Add(removed);
        if (_items.Count > Capacity) _items.RemoveAt(0);
    }

    /// <summary>Bu kimlikle kaldırılanı listeden çıkarıp döner; yoksa (zaten geri getirildi ya da çok eski) null.</summary>
    public RemovedWidget? Take(string id)
    {
        var index = _items.FindLastIndex(r => r.Copy.Id == id);
        if (index < 0) return null;
        var removed = _items[index];
        _items.RemoveAt(index);
        return removed;
    }
}
