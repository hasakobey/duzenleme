namespace Duzenleme.Core;

/// <summary>
/// "Windows masaüstüne göz at"ın geri sayımı: kaç dakika sonra kendiliğinden NestDesk'e dönülür, kullanıcı masaüstünde
/// çalışırken (sürükleme, yeniden adlandırma) ertelenir, "+5 dk" ile uzatılır. Saf hesap; zamanı çağıran verir.
/// </summary>
public sealed class PeekClock
{
    /// <summary>Kullanıcı masaüstünde meşgulken dönüş bu kadar ertelenir.</summary>
    public static readonly TimeSpan Postponement = TimeSpan.FromSeconds(20);

    /// <summary>"+5 dk" düğmesi.</summary>
    public static readonly TimeSpan Extension = TimeSpan.FromMinutes(5);

    /// <summary>Uzatmalarla birlikte en fazla bu kadar sürebilir (unutulan göz atma saatlerce sürmesin).</summary>
    public static readonly TimeSpan MaxRemaining = TimeSpan.FromMinutes(60);

    private DateTime? _deadline;

    /// <param name="minutes">0: kendiliğinden dönülmez.</param>
    public PeekClock(DateTime now, int minutes)
    {
        if (minutes > 0) _deadline = now.AddMinutes(minutes);
    }

    /// <summary>Kendiliğinden dönüş açık mı?</summary>
    public bool AutoReturn => _deadline is not null;

    /// <summary>Kalan süre (sıfırın altına inmez); kendiliğinden dönüş yoksa null.</summary>
    public TimeSpan? Remaining(DateTime now) =>
        _deadline is { } deadline ? (deadline > now ? deadline - now : TimeSpan.Zero) : null;

    /// <summary>
    /// Süre doldu mu? Dolduysa ve kullanıcı masaüstünde meşgulse dönüş ertelenir ve false döner (simgeler sürüklenirken
    /// ya da ad yazılırken ortadan kalkmasın).
    /// </summary>
    public bool Expired(DateTime now, bool userBusy)
    {
        if (_deadline is not { } deadline || now < deadline) return false;
        if (!userBusy) return true;
        _deadline = now + Postponement;
        return false;
    }

    /// <summary>Süreyi uzatır (en fazla <see cref="MaxRemaining"/> kalır). Kendiliğinden dönüş yoksa bir şey yapmaz.</summary>
    public void Extend(DateTime now, TimeSpan by)
    {
        if (_deadline is not { } deadline) return;
        var extended = (deadline > now ? deadline : now) + by;
        _deadline = extended - now > MaxRemaining ? now + MaxRemaining : extended;
    }

    /// <summary>"1:42" biçimi (dakika:saniye, saniye yukarı yuvarlanır: son saniyede "0:01").</summary>
    public static string Format(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
