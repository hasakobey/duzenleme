namespace Duzenleme.Core;

/// <summary>
/// En son kullanılanları tutan, sınırlı ve iş parçacığı güvenli önbellek. Sınır öğe sayısı ve isteğe bağlı olarak
/// toplam "maliyet" (ör. simgelerin piksel baytı) üzerinden konur; aşılınca en uzun süredir kullanılmayanlar atılır.
/// Uzun süre açık kalan uygulamada (her dosyanın önizlemesi, her kısayolun simgesi) bellek sınırsız büyümesin.
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value, long Cost)>> _map;
    private readonly LinkedList<(TKey Key, TValue Value, long Cost)> _order = new(); // baştaki en yeni
    private readonly int _capacity;
    private readonly long _maxCost;
    private readonly Func<TValue, long>? _cost;
    private long _totalCost;

    public LruCache(int capacity, long maxCost = long.MaxValue, Func<TValue, long>? cost = null, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _maxCost = maxCost;
        _cost = cost;
        _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue, long)>>(comparer);
    }

    public int Count { get { lock (_gate) return _map.Count; } }

    public long TotalCost { get { lock (_gate) return _totalCost; } }

    /// <summary>Varsa değeri verir ve onu en yeni sayar.</summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    /// <summary>Değeri ekler ya da değiştirir; sınır aşılırsa en eskileri atar (yeni eklenen hep kalır).</summary>
    public void Set(TKey key, TValue value)
    {
        var cost = Math.Max(0, _cost?.Invoke(value) ?? 0);
        lock (_gate)
        {
            if (_map.Remove(key, out var old))
            {
                _order.Remove(old);
                _totalCost -= old.Value.Cost;
            }
            _map[key] = _order.AddFirst((key, value, cost));
            _totalCost += cost;
            while (_order.Count > 1 && (_order.Count > _capacity || _totalCost > _maxCost))
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                _totalCost -= last.Value.Cost;
            }
        }
    }

    /// <summary>
    /// Yoksa üretip ekler. Üretim kilidin dışında yapılır (yavaş olabilir: kabuk simgesi); aynı anahtar için iki iş
    /// parçacığı birlikte üretirse sonraki kazanır, ikisi de geçerli bir değer döner.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (TryGetValue(key, out var value)) return value;
        value = factory(key);
        Set(key, value);
        return value;
    }

    /// <summary>Koşula uyan anahtarları atar; atılan sayısını döner.</summary>
    public int RemoveWhere(Func<TKey, bool> predicate)
    {
        lock (_gate)
        {
            var doomed = _map.Keys.Where(predicate).ToList();
            foreach (var key in doomed)
            {
                var node = _map[key];
                _map.Remove(key);
                _order.Remove(node);
                _totalCost -= node.Value.Cost;
            }
            return doomed.Count;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _order.Clear();
            _totalCost = 0;
        }
    }
}
