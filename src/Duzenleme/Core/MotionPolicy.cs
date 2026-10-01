namespace Duzenleme.Core;

// Yalnızca çalışma zamanında yaşayan türler: hiçbiri ayar dosyasına yazılmaz (kalıcı enum'a üye eklemek eski sürümü bozar).

/// <summary>Hareket düzeyi: Kapalı (hiçbir şey oynamaz), Sakin (yalnızca kısa solmalar, 2.1 davranışı), Canlı (varsayılan).</summary>
public enum MotionLevel { Off, Calm, Lively }

/// <summary>
/// Hareketin oynadığı yüzey. <see cref="Window"/>: donanımla çizilen pencere (ana pencere, iletişim kutuları, şerit).
/// <see cref="LayeredWidget"/>: yazılımla çizilen katmanlı widget penceresi (her karede bütün kart yeniden işlenir).
/// <see cref="Ring"/>: kısayolla açılan halka (aşmalı geçişin tek yeri).
/// </summary>
public enum MotionSurface { Window, LayeredWidget, Ring }

/// <summary>Hareketin türü (ne oynadığı); sayıları ve düşürmeleri <see cref="MotionPolicy.Plan(MotionLevel, MotionKind, MotionSurface, int, long)"/> verir.</summary>
public enum MotionKind { Fade, Pop, Exit, Rise, Drop, Page, Hover, Press, Attention, RingOpen, RingSwap, RingClose, TileEnter }

/// <summary>Yumuşatma: Out (yavaşlayarak varır), In (hızlanarak gider), InOut, Overshoot (hedefi biraz aşıp döner; yalnızca halka).</summary>
public enum MotionEase { Out, In, InOut, Overshoot }

/// <summary>
/// Bir geçişin bütün sayıları. Giriş (<c>Way In</c>) "uzak" değerlerden (saydamlık 0, <see cref="Scale"/>, <see cref="OffsetY"/>)
/// öğenin kendi değerlerine gelir; çıkış tersine gider. <see cref="Fade"/>: öğenin saydamlığı da oynar. <see cref="DelayMs"/>
/// kademeli girişte bekleme (toplam <see cref="DelayMs"/> + <see cref="Ms"/> en çok <see cref="MotionPolicy.MaxMs"/>).
/// <see cref="ReturnMs"/>: basılı düğmenin geri dönüşü.
/// </summary>
public sealed record MotionPlan(
    MotionKind Kind,
    MotionSurface Surface,
    int Ms,
    int Fps,
    bool Fade,
    double Scale,
    double OffsetY,
    MotionEase Ease,
    int DelayMs = 0,
    int ReturnMs = 0)
{
    /// <summary>Boyut ya da yer (yalnızca RenderTransform ile) oynuyor mu?</summary>
    public bool Moves => Scale != 1 || OffsetY != 0;

    /// <summary>Beklemeyle birlikte toplam süre.</summary>
    public int TotalMs => DelayMs + Ms;
}

/// <summary>
/// Hareketin bütün kararları ve sayıları (2.2 Hareket 2.0); uygulaması yalnızca <c>Views.Motion</c>. Saf: test edilir.
/// <list type="bullet">
/// <item>Windows'un "Animasyon efektleri" kapalıysa ya da Ayarlar'da Kapalı seçildiyse hiçbir şey oynamaz: son duruma hemen
/// atlanır (<see cref="Plan(MotionLevel, MotionKind, MotionSurface, int, long)"/> null). Girdi hiçbir zaman beklemez.</item>
/// <item>Sakin: her şey 150 ms'lik solmadır; fareyle zemin, basma, dikkat nabzı ve kutucuk girişi oynamaz.</item>
/// <item>Canlı: kısa büyüme/kayma. Her geçiş kademesiyle birlikte en çok <see cref="MaxMs"/>; döngü, tekrar, yanıp sönme yok.</item>
/// <item>Yalnızca saydamlık, RenderTransform (ölçek, kayma) ve fırça oynar; pencere sınırları, LayoutTransform, Margin asla.</item>
/// <item>Ölçek aralığı yüzeye göre: pencere ve widget <see cref="MinWindowScale"/>–1, halka <see cref="MinRingScale"/>–1 ve
/// aşma. Aşma (BackEase) yalnızca halkada.</item>
/// <item>Katmanlı widget pencereleri 30 kare/sn; aynı anda en çok <see cref="MaxPoppedWindows"/> büyüme (çok büyük kart yalnızca
/// solar), <see cref="MaxFadedWindows"/> solma; <see cref="MaxActiveLayered"/>'den çok etkin pencerede her şey hemen.</item>
/// </list>
/// </summary>
public static class MotionPolicy
{
    // ---------------------------------------------------------------- süreler (ms)

    /// <summary>Hiçbir geçiş (kademeli beklemesi dahil) bundan uzun sürmez.</summary>
    public const int MaxMs = 300;

    /// <summary>Solma; Sakin düzeyde her geçiş budur.</summary>
    public const int FadeMs = 150;

    /// <summary>Giriş: büyüyerek (Pop), yükselerek (Rise), inerek (Drop) belirme ve kutucuk girişi.</summary>
    public const int EnterMs = 200;

    /// <summary>Çıkış (Exit): girişten kısa, kullanıcı beklemesin.</summary>
    public const int ExitMs = 120;

    /// <summary>Ana penceredeki sayfa girişi.</summary>
    public const int PageMs = 220;

    /// <summary>Fareyle zemin renginin solması (yalnızca zemin; hiçbir öğe belirmez, büyümez, kaymaz).</summary>
    public const int HoverMs = 100;

    /// <summary>Basınca içerik küçülür / bırakınca geri gelir.</summary>
    public const int PressDownMs = 60, PressUpMs = 140;

    /// <summary>Dikkat nabzı ("Bul", zamanlayıcı bitti): tek nabız, aynı öğede en çok <see cref="AttentionGapMs"/>'de bir.</summary>
    public const int AttentionMs = 240;

    /// <summary>Aynı öğede iki dikkat nabzı arasındaki en kısa süre (yanıp sönme olmasın).</summary>
    public const int AttentionGapMs = 1000;

    /// <summary>Yeni widget'ın vurgu ışımasının sönmesi.</summary>
    public const int GlowMs = 200;

    /// <summary>Halka: açılış, alt halkaya geçiş, kapanış.</summary>
    public const int RingOpenMs = 220, RingSwapMs = 240, RingCloseMs = 100;

    /// <summary>Halka öğeleri arasındaki kademe ve kademeli öğe sayısı (sonrakiler sonuncuyla birlikte gelir).</summary>
    public const int RingStaggerMs = 10, RingStaggerMax = 8;

    /// <summary>Liste/kutucuk kademeli girişi (<c>Motion.Cascade</c>).</summary>
    public const int CascadeStepMs = 20, CascadeMax = 10;

    // ---------------------------------------------------------------- kare hızı ve sınırlar

    /// <summary>Katmanlı widget penceresinde saniyedeki kare (her karede bütün kart yazılımla yeniden çizilir).</summary>
    public const int WidgetFrameRate = 30;

    /// <summary>Donanımla çizilen pencerelerde ve halkada saniyedeki kare.</summary>
    public const int WindowFrameRate = 60, RingFrameRate = 60;

    /// <summary>Aynı anda en çok bu kadar widget solar; daha çoğu (masaüstünü gizle/göz at) hemen gizlenip gösterilir.</summary>
    public const int MaxFadedWindows = 8;

    /// <summary>Aynı anda en çok bu kadar widget büyüyerek belirir; fazlası yalnızca solar.</summary>
    public const int MaxPoppedWindows = 3;

    /// <summary>Bundan büyük kart (fiziksel piksel, en × boy) büyümez ve kaymaz, yalnızca solar.</summary>
    public const long MaxPopPixels = 700_000;

    /// <summary>Bundan çok katmanlı pencere aynı anda oynuyorsa yeni geçiş hemen uygulanır.</summary>
    public const int MaxActiveLayered = 12;

    /// <summary>
    /// Widget pencerelerinde fareyle zemin solması açık mı. Ölçüm (perf-run "motion"/"hover") pahalı bulursa false yapılır:
    /// widget düğmelerinin zemini fareyle yine değişir ama hemen.
    /// </summary>
    public const bool LayeredHoverFade = true;

    // ---------------------------------------------------------------- ölçek ve kayma

    /// <summary>Pencere ve widget yüzeyinde en küçük ölçek; üstü 1 (kart gölge payından taşmaz).</summary>
    public const double MinWindowScale = 0.9;

    /// <summary>Halkada en küçük ölçek; aşmayla kısa süre 1'in biraz üstüne çıkabilir.</summary>
    public const double MinRingScale = 0.8;

    public const double PopScale = 0.96, ExitScale = 0.97, PressScale = 0.97, AttentionScale = 0.95, TileEnterScale = 0.9;

    /// <summary>Halka: öğeler, orta ve kapanış ölçeği; öğeler ortaya doğru bu oranda yakından başlar.</summary>
    public const double RingItemScale = 0.8, RingCenterScale = 0.85, RingCloseScale = 0.9, RingTravel = 0.3;

    /// <summary>Aşmalı yumuşatmanın (BackEase) genliği; yalnızca halka.</summary>
    public const double OvershootAmplitude = 0.3;

    /// <summary>
    /// Kayma (DIP): şerit aşağıdan yükselir, sayfa aşağıdan gelir, göz atma çubuğunun içeriği yukarıdan iner (çubuk ~40 DIP
    /// ve opak: iniş, içerik çerçevenin dolgusundan taşmayacak kadar kısa).
    /// </summary>
    public const double RiseDip = 16, PageDip = 12, DropDip = 6;

    // ---------------------------------------------------------------- düzey

    /// <summary>
    /// Düzey: Windows'ta animasyon efektleri kapalıysa ya da uygulamada Kapalı seçildiyse Kapalı; Sakin seçildiyse, enerji
    /// tasarrufu açıksa ya da uzak masaüstü oturumundaysa Sakin; değilse Canlı.
    /// </summary>
    public static MotionLevel Level(bool windowsAnimations, bool appOff, bool calm, bool energySaver, bool remote) =>
        !windowsAnimations || appOff ? MotionLevel.Off
        : calm || energySaver || remote ? MotionLevel.Calm
        : MotionLevel.Lively;

    /// <summary>Ayarlar'da seçili düğme: <c>AnimationsOff</c> (2.1) baskındır, sonra <c>CalmMotion</c>.</summary>
    public static MotionLevel Chosen(bool animationsOff, bool calmMotion) =>
        animationsOff ? MotionLevel.Off : calmMotion ? MotionLevel.Calm : MotionLevel.Lively;

    /// <summary>
    /// Seçimin ayar bayrakları. Kapalı yalnızca <c>AnimationsOff</c>'u açar, <c>CalmMotion</c>'a dokunmaz (2.1 ile gidip gelen
    /// kullanıcının seçimi korunsun); Canlı ve Sakin <c>AnimationsOff</c>'u kapatır.
    /// </summary>
    public static (bool AnimationsOff, bool CalmMotion) Flags(MotionLevel choice, bool calmNow) => choice switch
    {
        MotionLevel.Off => (true, calmNow),
        MotionLevel.Calm => (false, true),
        _ => (false, false),
    };

    // ---------------------------------------------------------------- plan

    /// <summary>Saniyedeki kare: katmanlı widget penceresinde 30, öbürlerinde 60.</summary>
    public static int FrameRate(MotionSurface surface) => surface switch
    {
        MotionSurface.LayeredWidget => WidgetFrameRate,
        MotionSurface.Ring => RingFrameRate,
        _ => WindowFrameRate,
    };

    /// <summary>Yüzeyin en küçük ölçeği (üst sınır 1; yalnızca halkanın aşması kısa süre üstüne çıkar).</summary>
    public static double MinScale(MotionSurface surface) => surface == MotionSurface.Ring ? MinRingScale : MinWindowScale;

    /// <summary>Hareketin planı; null = değişiklik hemen uygulanır (hiçbir zamanlayıcı kurulmaz).</summary>
    /// <param name="activeLayered">Katmanlı yüzeyde bununla birlikte aynı anda oynayacak pencere sayısı (bu dahil).</param>
    /// <param name="pixels">Katmanlı pencerenin fiziksel piksel alanı (en × boy); bilinmiyorsa 0.</param>
    public static MotionPlan? Plan(MotionLevel level, MotionKind kind, MotionSurface surface, int activeLayered = 0, long pixels = 0) =>
        Plan(level, kind, surface, activeLayered, pixels, out _);

    /// <summary>
    /// <see cref="Plan(MotionLevel, MotionKind, MotionSurface, int, long)"/>; <paramref name="note"/> atlamanın ya da düşürmenin
    /// ASCII nedeni (başarım günlüğü "Motion.Skip.&lt;neden&gt;"): Off, Calm, Busy, Crowd, Large, Hover.
    /// </summary>
    public static MotionPlan? Plan(MotionLevel level, MotionKind kind, MotionSurface surface, int activeLayered, long pixels, out string? note)
    {
        note = null;
        if (level == MotionLevel.Off)
        {
            note = "Off";
            return null;
        }
        var layered = surface == MotionSurface.LayeredWidget;
        if (layered && activeLayered > MaxActiveLayered)
        {
            note = "Busy";
            return null;
        }
        if (layered && kind == MotionKind.Hover && !LayeredHoverFade)
        {
            note = "Hover";
            return null;
        }
        // Çok büyük kartta fareyle zemin solması da bütün pencereyi birkaç kez yeniden çizdirir: renk hemen değişir.
        if (layered && kind == MotionKind.Hover && pixels > MaxPopPixels)
        {
            note = "Large";
            return null;
        }

        MotionPlan plan;
        if (level == MotionLevel.Calm)
        {
            if (kind is MotionKind.Hover or MotionKind.Press or MotionKind.Attention or MotionKind.TileEnter)
            {
                note = "Calm";
                return null;
            }
            plan = new MotionPlan(MotionKind.Fade, surface, FadeMs, FrameRate(surface), Fade: true, Scale: 1, OffsetY: 0, MotionEase.InOut);
        }
        else
        {
            plan = Lively(kind, surface);
            // Aşma yalnızca halkada; ölçek yüzeyin aralığında.
            if (plan.Ease == MotionEase.Overshoot && surface != MotionSurface.Ring) plan = plan with { Ease = MotionEase.Out };
            if (plan.Scale < MinScale(surface)) plan = plan with { Scale = MinScale(surface) };
            if (plan.Scale > 1) plan = plan with { Scale = 1 };
            // Katmanlı pencerede büyüme/kayma pahalı: çok büyük kartta ya da aynı anda çok büyümede yalnızca solma kalır.
            if (layered && plan.Moves)
            {
                var large = pixels > MaxPopPixels;
                var crowded = kind == MotionKind.Pop && activeLayered > MaxPoppedWindows;
                if (large || crowded)
                {
                    note = large ? "Large" : "Crowd";
                    if (!plan.Fade) return null;
                    plan = plan with { Kind = MotionKind.Fade, Scale = 1, OffsetY = 0, Ms = Math.Min(plan.Ms, FadeMs), Ease = MotionEase.InOut };
                }
            }
        }
        // Pencere solması: aynı anda en çok MaxFadedWindows.
        if (layered && plan.Fade && activeLayered > MaxFadedWindows)
        {
            note = "Crowd";
            return null;
        }
        return plan;
    }

    /// <summary>Canlı düzeyde türün sayıları (yüzey sınırlarından önce).</summary>
    private static MotionPlan Lively(MotionKind kind, MotionSurface surface)
    {
        var fps = FrameRate(surface);
        return kind switch
        {
            MotionKind.Pop => new(kind, surface, EnterMs, fps, Fade: true, PopScale, 0, MotionEase.Out),
            MotionKind.Exit => new(kind, surface, ExitMs, fps, Fade: true, ExitScale, 0, MotionEase.In),
            MotionKind.Rise => new(kind, surface, EnterMs, fps, Fade: true, 1, RiseDip, MotionEase.Out),
            MotionKind.Drop => new(kind, surface, EnterMs, fps, Fade: true, 1, -DropDip, MotionEase.Out),
            MotionKind.Page => new(kind, surface, PageMs, fps, Fade: true, 1, PageDip, MotionEase.Out),
            MotionKind.Hover => new(kind, surface, HoverMs, fps, Fade: false, 1, 0, MotionEase.Out),
            MotionKind.Press => new(kind, surface, PressDownMs, fps, Fade: false, PressScale, 0, MotionEase.Out, ReturnMs: PressUpMs),
            MotionKind.Attention => new(kind, surface, AttentionMs, fps, Fade: false, AttentionScale, 0, MotionEase.InOut),
            MotionKind.RingOpen => new(kind, surface, RingOpenMs, fps, Fade: true, RingItemScale, 0, MotionEase.Overshoot),
            MotionKind.RingSwap => new(kind, surface, RingSwapMs, fps, Fade: true, RingItemScale, 0, MotionEase.Overshoot),
            MotionKind.RingClose => new(kind, surface, RingCloseMs, fps, Fade: true, RingCloseScale, 0, MotionEase.In),
            MotionKind.TileEnter => new(kind, surface, EnterMs, fps, Fade: false, TileEnterScale, 0, MotionEase.Out),
            _ => new(MotionKind.Fade, surface, FadeMs, fps, Fade: true, 1, 0, MotionEase.InOut),
        };
    }

    /// <summary>Bekleyerek başlayan geçiş: toplam süre yine en çok <see cref="MaxMs"/> (bekleme uzadıkça geçiş kısalır).</summary>
    public static MotionPlan Delay(MotionPlan plan, int delayMs)
    {
        var delay = Math.Clamp(delayMs, 0, MaxMs / 2);
        return plan with { DelayMs = delay, Ms = Math.Max(0, Math.Min(plan.Ms, MaxMs - delay)) };
    }

    /// <summary>
    /// Kademeli girişte <paramref name="index"/>. öğenin planı: halkada <see cref="RingStaggerMs"/>, öbürlerinde
    /// <see cref="CascadeStepMs"/> arayla; sınırdan sonrakiler sonuncuyla birlikte gelir. Sakin düzeyde hepsi birlikte solar.
    /// Toplam süre en çok <see cref="MaxMs"/>: geçiş süresi min(plan, MaxMs − bekleme).
    /// </summary>
    public static MotionPlan Stagger(MotionLevel level, MotionPlan plan, int index)
    {
        if (level != MotionLevel.Lively || index <= 0) return plan;
        var ring = plan.Kind is MotionKind.RingOpen or MotionKind.RingSwap;
        var (step, max) = ring ? (RingStaggerMs, RingStaggerMax) : (CascadeStepMs, CascadeMax);
        var delay = Math.Min(index, max - 1) * step;
        return plan with { DelayMs = delay, Ms = Math.Max(0, Math.Min(plan.Ms, MaxMs - delay)) };
    }

    // ---------------------------------------------------------------- 2.1'den kalanlar

    /// <summary>Sağ tık menüsü solarak açılsın mı (düzey en az Sakin ve Windows'un menü animasyonu açık; kayma hiç kullanılmaz).</summary>
    public static bool MenuFade(bool levelAtLeastCalm, bool windowsMenuAnimation) => levelAtLeastCalm && windowsMenuAnimation;

    /// <summary>Işıma (RevealGlow) çizilsin mi: Saydamlık efektleri açık ve yüksek karşıtlık kapalı.</summary>
    public static bool GlowAllowed(bool transparencyOn, bool highContrast) => transparencyOn && !highContrast;

    /// <summary>İstenen süre, sınır içinde (0..<see cref="MaxMs"/>).</summary>
    public static int Duration(int milliseconds) => Math.Clamp(milliseconds, 0, MaxMs);

    /// <summary>
    /// Katmanlı bir widget geçişi oynarken (<paramref name="layeredActive"/> &gt; 0) başlayan pencere ya da halka geçişi de
    /// <see cref="WidgetFrameRate"/> kare/sn'dir. WPF'te kare hızı kök zaman çizelgesi başınadır: yanında 60 kare isteyen bir
    /// saat (eklerken basılan kutucuk, Widget'lar sayfasındaki yeni satır, göz atma çubuğu) yazılımla çizilen kartı gereğinden
    /// sık yeniden çizdirmesin (perf-run "motion" ölçer). Katmanlı planın kendisi zaten 30'dur.
    /// </summary>
    public static MotionPlan Beside(MotionPlan plan, int layeredActive) =>
        plan.Surface != MotionSurface.LayeredWidget && layeredActive > 0 && plan.Fps > WidgetFrameRate
            ? plan with { Fps = WidgetFrameRate }
            : plan;
}
