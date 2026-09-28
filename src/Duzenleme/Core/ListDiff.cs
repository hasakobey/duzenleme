namespace Duzenleme.Core;

/// <summary>Bir listeyi yenisine çeviren tek adım: sil, taşı ya da ekle.</summary>
public readonly record struct ListStep<T>(ListStepKind Kind, int Index, int To, T? Item);

public enum ListStepKind { Remove, Move, Insert }

/// <summary>
/// Gösterilen listeyi (ör. bölmedeki simgeler) yeni hâline az adımla getirir: yalnızca kalkan öğeler silinir, yer
/// değiştirenler taşınır, yeniler eklenir. Aynı öğe (aynı nesne) yerinde kalır; arayüz onun kabını yeniden kurmaz.
/// Öğeler nesne kimliğiyle karşılaştırılır: çağıran eski öğeleri anahtarıyla yeniden kullanmalıdır.
/// </summary>
public static class ListDiff
{
    /// <summary>Adımları çıkarır (listeye dokunmaz).</summary>
    public static List<ListStep<T>> Plan<T>(IReadOnlyList<T> current, IReadOnlyList<T> desired) where T : class
    {
        var steps = new List<ListStep<T>>();
        var work = new List<T>(current);
        var wanted = new HashSet<T>(desired, ReferenceEqualityComparer.Instance);

        for (var i = work.Count - 1; i >= 0; i--)
        {
            if (wanted.Contains(work[i])) continue;
            steps.Add(new(ListStepKind.Remove, i, 0, null));
            work.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            if (i < work.Count && ReferenceEquals(work[i], item)) continue;
            var from = -1;
            for (var j = i + 1; j < work.Count; j++)
                if (ReferenceEquals(work[j], item)) { from = j; break; }
            if (from >= 0)
            {
                steps.Add(new(ListStepKind.Move, from, i, null));
                work.RemoveAt(from);
                work.Insert(i, item);
            }
            else
            {
                steps.Add(new(ListStepKind.Insert, i, 0, item));
                work.Insert(i, item);
            }
        }

        // desired'da aynı nesne iki kez varsa (olmamalı) fazlalık kalabilir.
        for (var i = work.Count - 1; i >= desired.Count; i--) steps.Add(new(ListStepKind.Remove, i, 0, null));
        return steps;
    }

    /// <summary>Adımları uygular. <paramref name="move"/> verilirse taşıma onunla yapılır (ObservableCollection.Move).</summary>
    public static void Apply<T>(IList<T> target, IEnumerable<ListStep<T>> steps, Action<int, int>? move = null) where T : class
    {
        foreach (var step in steps)
        {
            switch (step.Kind)
            {
                case ListStepKind.Remove:
                    target.RemoveAt(step.Index);
                    break;
                case ListStepKind.Move when move is not null:
                    move(step.Index, step.To);
                    break;
                case ListStepKind.Move:
                    var item = target[step.Index];
                    target.RemoveAt(step.Index);
                    target.Insert(step.To, item);
                    break;
                case ListStepKind.Insert:
                    target.Insert(step.Index, step.Item!);
                    break;
            }
        }
    }
}
