using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Duzenleme.Core;
using Microsoft.Win32;
using Wpf.Ui.Animations;

namespace Duzenleme.Views;

/// <summary>
/// Uygulamadaki bütün hareketin tek yeri (2.2 Hareket 2.0); bütün kararlar ve sayılar <see cref="MotionPolicy"/>'de, burada
/// yalnızca uygulanır. Başka yerde BeginAnimation/Storyboard/*Animation kullanılmaz (MotionTests denetler).
/// <list type="bullet">
/// <item>Düzey: Kapalı / Sakin / Canlı (<see cref="Level"/>). Windows'un "Animasyon efektleri" kapalıysa her zaman Kapalı:
/// hiçbir zamanlayıcı kurulmaz, son duruma hemen atlanır. Enerji tasarrufu ve uzak masaüstü Sakin'e indirir. Hepsi canlı
/// izlenir (<see cref="Refresh"/>).</item>
/// <item>Yalnızca saydamlık, RenderTransform (ölçek/kayma) ve fırça oynar. Pencere sınırları, LayoutTransform ve kenar
/// boşluğu asla. Dönüşüm yalnızca öğenin RenderTransform'u boşsa (yok ya da Identity) kullanılır, değilse yalnızca solar.</item>
/// <item>Bitince dönüşüm kalkar (yazı yeniden keskin/ClearType çizilir) ve öğenin kendi değeri geçerli olur: girişler taban
/// değere varır; çıkışın son değeri yalnızca gizleme anına dek tutulur.</item>
/// <item>Girdi hiç beklemez: IsHitTestVisible/IsEnabled'a dokunulmaz. Yeni istek aynı öğedeki bekleyeni iptal eder.</item>
/// <item>Döngü, tekrar, yanıp sönme yok; dikkat nabzı tek ve aynı öğede saniyede en çok bir.</item>
/// <item>Fareyle yalnızca zemin rengi solar (<see cref="HoverFillProperty"/>); hiçbir öğe belirip kaybolmaz, büyümez, kaymaz.</item>
/// </list>
/// Başarım günlüğü (NESTDESK_PERF_LOG): "Motion.&lt;Kind&gt;" ve "Motion.Skip.&lt;neden&gt;" sayaçları.
/// </summary>
internal static class Motion
{
    private static bool _initialized;
    private static int _refreshQueued;

    /// <summary>Şu an oynayan katmanlı pencere geçişleri (MotionPolicy.MaxActiveLayered için).</summary>
    private static int _activeLayered;

    /// <summary>Şu anki hareket düzeyi (açılıştan önce Kapalı).</summary>
    public static MotionLevel Level { get; private set; } = MotionLevel.Off;

    /// <summary>Herhangi bir geçiş oynuyor mu (Sakin ya da Canlı)?</summary>
    public static bool Enabled => Level != MotionLevel.Off;

    /// <summary>Windows'un "Animasyon efektleri" açık mı (Ayarlar'daki not için)?</summary>
    public static bool WindowsAllows { get; private set; } = true;

    /// <summary>Işıma gibi saydamlık efektleri çizilsin mi: Windows'ta Saydamlık efektleri açık ve yüksek karşıtlık kapalı.</summary>
    public static bool EffectsAllowed { get; private set; } = true;

    /// <summary>Enerji (pil) tasarrufu açık: Canlı seçiliyken bile Sakin oynar.</summary>
    public static bool EnergySaver { get; private set; }

    /// <summary>Uzak masaüstü oturumu: Canlı seçiliyken bile Sakin oynar.</summary>
    public static bool RemoteSession { get; private set; }

    /// <summary>Düzey ya da sinyallerden biri değişti. UI iş parçacığında.</summary>
    public static event Action? Changed;

    /// <summary>Açılışta bir kez (ayarlar okunduktan sonra): düzeyi okur, menü solmasını ayarlar ve değişiklikleri izler.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Read();
        ApplyResources();
        // Windows'un ayarları değişince (Erişilebilirlik > Görsel efektler, yüksek karşıtlık) WPF önbelleğini tazeler ve bildirir.
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.MenuAnimation)
                or nameof(SystemParameters.HighContrast) or "" or null)
                Post();
        };
        // Saydamlık efektleri, enerji tasarrufu, uzak masaüstü: ayrı iş parçacığından gelir, UI'ya aktarılır.
        SystemEvents.UserPreferenceChanged += (_, _) => Post();
        SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == PowerModes.StatusChange) Post();
        };
        SystemEvents.SessionSwitch += (_, e) =>
        {
            if (e.Reason is SessionSwitchReason.RemoteConnect or SessionSwitchReason.RemoteDisconnect
                or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.ConsoleDisconnect)
                Post();
        };
        AppHost.SettingsChanged += Refresh;
    }

    /// <summary>Ayar ya da Windows'un ayarlarından biri değişmiş olabilir: durum yeniden okunur. UI iş parçacığında çağır.</summary>
    public static void Refresh()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            Post();
            return;
        }
        var before = (Level, WindowsAllows, EffectsAllowed, EnergySaver, RemoteSession);
        Read();
        ApplyResources();
        if (before == (Level, WindowsAllows, EffectsAllowed, EnergySaver, RemoteSession)) return;
        DebugLog.Write($"hareket: {Level} (Windows animasyonu {(WindowsAllows ? "açık" : "kapalı")}, efektler {(EffectsAllowed ? "açık" : "kapalı")}, " +
                       $"enerji tasarrufu {(EnergySaver ? "açık" : "kapalı")}, uzak masaüstü {(RemoteSession ? "evet" : "hayır")})");
        Changed?.Invoke();
    }

    /// <summary>Başka iş parçacığından gelen bildirimleri UI'da birleştirerek bir kez işler.</summary>
    private static void Post()
    {
        if (Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher) return;
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
        dispatcher.InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            Refresh();
        }, DispatcherPriority.Background);
    }

    /// <summary>Sinyalleri okur: hepsi eşzamanlı ve ucuz (WPF önbelleği, HKCU'da bir değer, GetSystemPowerStatus).</summary>
    private static void Read()
    {
        var windows = SystemParameters.ClientAreaAnimation;
        var saver = ReadEnergySaver();
        var remote = ReadRemoteSession();
        var settings = AppHost.Settings;
        WindowsAllows = windows;
        EnergySaver = saver;
        RemoteSession = remote;
        EffectsAllowed = MotionPolicy.GlowAllowed(ReadTransparency(), SystemParameters.HighContrast);
        Level = MotionPolicy.Level(windows, settings.AnimationsOff, settings.CalmMotion, saver, remote);
    }

    /// <summary>Windows > Kişiselleştirme > Renkler > Saydamlık efektleri (değer yoksa açık sayılır).</summary>
    private static bool ReadTransparency()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool ReadEnergySaver()
    {
        try { return Desktop.SystemStats.EnergySaverOn(); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    private static bool ReadRemoteSession()
    {
        try { return SystemParameters.IsRemoteSession; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    /// <summary>
    /// Sağ tık menüleri açılır pencerelerinin geçişini bu kaynaktan alır: düzey en az Sakin ve Windows'un menü animasyonu da
    /// açıkken solma, değilse hiçbiri (Windows'un kayma seçeneği kullanılmaz). Alt menüler fareyle açıldığı için hiç solmaz
    /// (QuietStyles). Uygulamanın kendi sözlüğüne yazılır: birleştirilmiş sözlüklerdeki varsayılanı (None) ezer.
    /// </summary>
    private static void ApplyResources()
    {
        if (Application.Current?.Resources is not { } resources) return;
        var menu = MotionPolicy.MenuFade(Level != MotionLevel.Off, SystemParameters.MenuAnimation) ? PopupAnimation.Fade : PopupAnimation.None;
        if (!Equals(resources[SystemParameters.MenuPopupAnimationKey], menu)) resources[SystemParameters.MenuPopupAnimationKey] = menu;
    }

    // ---------------------------------------------------------------- ana pencere sayfaları

    private static readonly ConditionalWeakTable<Wpf.Ui.Controls.NavigationView, object> Navigations = [];

    /// <summary>
    /// Ana penceredeki sayfa geçişi: WPF-UI'nin kendi geçişi (ayarı bilmez) hep kapalı; her gezinmede sayfa
    /// <see cref="PageEnter"/> ile girer. Birden çok çağrılabilir: gezinmeye bir kez abone olunur.
    /// </summary>
    public static void ApplyTo(Wpf.Ui.Controls.NavigationView navigation)
    {
        navigation.Transition = Transition.None;
        if (Navigations.TryGetValue(navigation, out _)) return;
        Navigations.Add(navigation, new object());
        navigation.Navigated += (_, e) =>
        {
            if (e.Page is FrameworkElement page) PageEnter(page);
        };
    }

    /// <summary>Sayfa tek yönden girer: 12 DIP aşağıdan kayarak ve solarak (Sakin: yalnızca solma). Henüz yüklenmediyse yüklenince.</summary>
    public static void PageEnter(FrameworkElement page)
    {
        if (page.IsLoaded)
        {
            Enter(page, MotionKind.Page);
            return;
        }
        var asked = Environment.TickCount64;
        RoutedEventHandler? once = null;
        once = (_, _) =>
        {
            page.Loaded -= once;
            // Gezinmeden çok sonra gelen yükleme (pencere gizlenip açıldı) oynamaz.
            if (Environment.TickCount64 - asked < 1000) Enter(page, MotionKind.Page);
        };
        page.Loaded += once;
    }

    // ---------------------------------------------------------------- öğe durumu

    /// <summary>Öğe başına durum: son istenen geçişin sırası (sonradan gelen istek bekleyeni iptal eder) ve Motion'ın dönüşümü.</summary>
    private sealed class State
    {
        public int Generation;
        /// <summary>Solarak kayboluyor (gizleme bekliyor).</summary>
        public bool Fading;
        /// <summary>Katmanlı pencere sayacında.</summary>
        public bool Counted;
        public bool Pressed;
        public MotionPlan? PressPlan;
        /// <summary>Bu öğeye Motion'ın koyduğu RenderTransform ve ondan önceki yerel değerler (geri konur).</summary>
        public TransformGroup? Owned;
        public object? OldTransform;
        public object? OldOrigin;
        /// <summary>Bu öğenin (pencerenin) saydamlığıyla birlikte dönüşen içerik: durdurulunca o da durur.</summary>
        public UIElement? Mover;
        public long LastAttention = long.MinValue / 2;
    }

    private static readonly ConditionalWeakTable<UIElement, State> States = [];

    private static State StateOf(UIElement element) => States.GetOrCreateValue(element);

    private static void Count(State state, MotionPlan plan)
    {
        if (plan.Surface != MotionSurface.LayeredWidget || state.Counted) return;
        state.Counted = true;
        _activeLayered++;
    }

    private static void Uncount(State state)
    {
        if (!state.Counted) return;
        state.Counted = false;
        _activeLayered = Math.Max(0, _activeLayered - 1);
    }

    /// <summary>Planı ister ve başarım günlüğüne yazar. Katmanlı yüzeyde aynı anda oynayanlar (bu dahil) hesaba katılır.</summary>
    private static MotionPlan? Decide(MotionKind kind, MotionSurface surface, long pixels = 0, int batch = 1, State? state = null)
    {
        var active = surface == MotionSurface.LayeredWidget ? Math.Max(batch, _activeLayered + (state is { Counted: true } ? 0 : 1)) : 0;
        var plan = MotionPolicy.Plan(Level, kind, surface, active, pixels, out var note);
        if (PerfLog.Enabled)
        {
            if (note is not null) PerfLog.Count("Motion.Skip." + note);
            if (plan is not null) PerfLog.Count("Motion." + plan.Kind);
        }
        return plan;
    }

    private static void Skip(string reason)
    {
        if (PerfLog.Enabled) PerfLog.Count("Motion.Skip." + reason);
    }

    /// <summary>Pencere dönüşmez (WPF desteklemez): onun yerine içeriği. Başka öğe kendisi.</summary>
    private static UIElement? MoverOf(UIElement element) =>
        element is Window window ? window.Content as UIElement : element;

    /// <summary>Öğenin bulunduğu pencereye göre yüzey ve (katmanlıysa) pencerenin fiziksel piksel alanı.</summary>
    private static (MotionSurface Surface, long Pixels) Where(UIElement element)
    {
        if (Window.GetWindow(element) is not { AllowsTransparency: true } window) return (MotionSurface.Window, 0);
        var device = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        return (MotionSurface.LayeredWidget, (long)(window.ActualWidth * device.M11 * window.ActualHeight * device.M22));
    }

    // ---------------------------------------------------------------- yumuşatma ve animasyon

    private static readonly IEasingFunction OutEase = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction InEase = Frozen(new CubicEase { EasingMode = EasingMode.EaseIn });
    private static readonly IEasingFunction InOutEase = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });
    private static readonly IEasingFunction OvershootEase =
        Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = MotionPolicy.OvershootAmplitude });
    private static readonly IEasingFunction PulseEase = new Pulse();

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    /// <summary>Aşma yalnızca dönüşümde (saydamlık öğenin kendi değerini aşmasın).</summary>
    private static IEasingFunction EaseOf(MotionEase ease, bool opacity) => ease switch
    {
        MotionEase.In => InEase,
        MotionEase.InOut => InOutEase,
        MotionEase.Overshoot => opacity ? OutEase : OvershootEase,
        _ => OutEase,
    };

    /// <summary>0 → 1 → 0: tek nabız (AutoReverse/tekrar kullanılmaz).</summary>
    private sealed class Pulse : IEasingFunction
    {
        public double Ease(double normalizedTime) => Math.Sin(Math.PI * Math.Clamp(normalizedTime, 0, 1));
    }

    /// <summary>Kademeli giriş: sürenin ilk <paramref name="lead"/> kısmında "uzak" uçta bekler, sonra içteki yumuşatmayla gelir.</summary>
    private sealed class Delayed(double lead, IEasingFunction inner) : IEasingFunction
    {
        public double Ease(double normalizedTime) =>
            normalizedTime <= lead ? 0 : inner.Ease((normalizedTime - lead) / (1 - lead));
    }

    /// <summary>
    /// Tek değerin geçişi. To verilmezse taban değere varılır (öğenin kendi değeri bu arada değişse de ona). Bekleme
    /// (<see cref="MotionPlan.DelayMs"/>) yumuşatmanın içindedir: bekleme sırasında değer "uzak" uçta durur.
    /// </summary>
    private static DoubleAnimation Animation(double? from, double? to, MotionPlan plan, int ms, IEasingFunction ease, FillBehavior fill)
    {
        var delay = plan.DelayMs;
        var total = MotionPolicy.Duration(delay + ms);
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(total)),
            FillBehavior = fill,
            EasingFunction = delay > 0 && total > delay ? new Delayed((double)delay / total, ease) : ease,
        };
        Timeline.SetDesiredFrameRate(animation, plan.Fps);
        return animation;
    }

    // ---------------------------------------------------------------- dönüşüm

    private static ScaleTransform ScaleOf(TransformGroup group) => (ScaleTransform)group.Children[0];

    private static TranslateTransform MoveOf(TransformGroup group) => (TranslateTransform)group.Children[1];

    /// <summary>
    /// Öğenin boştaki RenderTransform'unu (yok ya da Identity) Motion'ın ölçek+kayma dönüşümüyle değiştirir; öğenin kendi
    /// dönüşümü varsa null (dokunulmaz, yalnızca solar). Önceki yerel değerler <see cref="Release"/>'te geri konur.
    /// </summary>
    private static TransformGroup? Acquire(UIElement element, State state)
    {
        if (state.Owned is { } owned)
        {
            if (ReferenceEquals(element.RenderTransform, owned)) return owned;
            state.Owned = null;
        }
        if (element is Window) return null;
        var current = element.RenderTransform;
        if (current is not null && !ReferenceEquals(current, Transform.Identity)) return null;
        var local = element.ReadLocalValue(UIElement.RenderTransformProperty);
        if (local is BindingExpressionBase) return null;
        state.OldTransform = local;
        state.OldOrigin = element.ReadLocalValue(UIElement.RenderTransformOriginProperty);
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform());
        group.Children.Add(new TranslateTransform());
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = group;
        state.Owned = group;
        return group;
    }

    /// <summary>Motion'ın dönüşümünü kaldırır ve öğenin önceki değerlerini geri koyar (yazı yeniden piksele oturur).</summary>
    private static void Release(UIElement element, State state)
    {
        if (state.Owned is not { } group) return;
        state.Owned = null;
        var scale = ScaleOf(group);
        var move = MoveOf(group);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        move.BeginAnimation(TranslateTransform.XProperty, null);
        move.BeginAnimation(TranslateTransform.YProperty, null);
        if (!ReferenceEquals(element.RenderTransform, group)) return;
        Restore(element, UIElement.RenderTransformProperty, state.OldTransform);
        Restore(element, UIElement.RenderTransformOriginProperty, state.OldOrigin);
        state.OldTransform = state.OldOrigin = null;
    }

    private static void Restore(UIElement element, DependencyProperty property, object? old)
    {
        if (old == DependencyProperty.UnsetValue) element.ClearValue(property);
        else element.SetValue(property, old);
    }

    /// <summary>Öğedeki bütün geçişleri durdurur: kendi saydamlığına ve dönüşümsüz hâline döner (bekleyen gizleme iptal).</summary>
    private static void StopCore(UIElement element, State state)
    {
        state.Fading = false;
        state.Pressed = false;
        Uncount(state);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        Release(element, state);
        if (state.Mover is { } mover)
        {
            state.Mover = null;
            var moverState = StateOf(mover);
            moverState.Generation++;
            Release(mover, moverState);
            Uncount(moverState);
        }
    }

    // ---------------------------------------------------------------- giriş ve çıkış

    /// <summary>
    /// Giriş: saydamlık 0'dan (plan solma içeriyorsa) ve dönüşüm "uzak" uçtan öğenin kendi değerlerine. Çağıran
    /// <paramref name="state"/>'in sırasını artırmış olmalı.
    /// </summary>
    /// <param name="resume">Solarak kaybolurken geri istendi: saydamlık ve dönüşüm kaldıkları yerden döner.</param>
    /// <param name="travel">Ek başlangıç kayması (halka öğeleri ortaya doğru yakından başlar).</param>
    /// <param name="count">Katmanlı pencere sayacına yazılsın mı (kademede yalnızca ilk öğe).</param>
    private static void BeginIn(UIElement primary, State state, UIElement? mover, MotionPlan plan, bool resume, Vector travel = default, bool count = true)
    {
        var generation = state.Generation;
        Uncount(state);
        state.Fading = false;

        var opacity = plan.Fade
            ? Animation(resume ? primary.Opacity : 0, null, plan, plan.Ms, EaseOf(plan.Ease, opacity: true), FillBehavior.Stop)
            : null;

        State? moverState = null;
        TransformGroup? group = null;
        var moverGeneration = generation;
        if (mover is not null && (plan.Moves || travel != default || resume))
        {
            moverState = ReferenceEquals(mover, primary) ? state : StateOf(mover);
            if (!ReferenceEquals(moverState, state))
            {
                moverGeneration = ++moverState.Generation;
                state.Mover = mover;
            }
            group = resume
                ? moverState.Owned is { } owned && ReferenceEquals(mover.RenderTransform, owned) ? owned : null
                : Acquire(mover, moverState);
            if (group is null && !resume) Skip("Transform");
        }

        var moves = new List<(Animatable Target, DependencyProperty Property, DoubleAnimation Animation)>(4);
        if (group is not null)
        {
            var scale = ScaleOf(group);
            var move = MoveOf(group);
            var ease = EaseOf(plan.Ease, opacity: false);
            var fromScale = resume ? scale.ScaleX : plan.Scale;
            var fromX = resume ? move.X : travel.X;
            var fromY = resume ? move.Y : plan.OffsetY + travel.Y;
            // Taban yerinde (1, 0): animasyon bitince ya da durunca öğe yerindedir.
            scale.ScaleX = scale.ScaleY = 1;
            move.X = move.Y = 0;
            if (fromScale != 1)
            {
                moves.Add((scale, ScaleTransform.ScaleXProperty, Animation(fromScale, null, plan, plan.Ms, ease, FillBehavior.Stop)));
                moves.Add((scale, ScaleTransform.ScaleYProperty, Animation(fromScale, null, plan, plan.Ms, ease, FillBehavior.Stop)));
            }
            if (fromX != 0) moves.Add((move, TranslateTransform.XProperty, Animation(fromX, null, plan, plan.Ms, ease, FillBehavior.Stop)));
            if (fromY != 0) moves.Add((move, TranslateTransform.YProperty, Animation(fromY, null, plan, plan.Ms, ease, FillBehavior.Stop)));
        }

        Timeline? lead = opacity ?? (moves.Count > 0 ? moves[0].Animation : null);
        if (lead is null)
        {
            if (group is not null && moverState is not null) Release(mover!, moverState);
            return;
        }
        lead.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            state.Fading = false;
            Uncount(state);
            if (group is not null && moverState is not null && moverState.Generation == moverGeneration) Release(mover!, moverState);
            if (ReferenceEquals(state.Mover, mover) && !ReferenceEquals(mover, primary)) state.Mover = null;
        };
        if (count) Count(state, plan);
        if (opacity is not null) primary.BeginAnimation(UIElement.OpacityProperty, opacity);
        foreach (var (target, property, animation) in moves) target.BeginAnimation(property, animation);
    }

    /// <summary>
    /// Çıkış: saydamlık 0'a (gizleme anına dek tutulur), dönüşüm "uzak" uca; bitince <paramref name="hide"/>, sonra her şey
    /// kalkar. Arada yeni bir istek gelirse gizleme yapılmaz.
    /// </summary>
    private static void BeginOut(UIElement primary, State state, UIElement? mover, MotionPlan plan, Action hide)
    {
        var generation = state.Generation;
        Uncount(state);

        TransformGroup? group = null;
        if (mover is not null && plan.Moves)
        {
            var moverState = ReferenceEquals(mover, primary) ? state : StateOf(mover);
            if (!ReferenceEquals(moverState, state)) moverState.Generation++;
            group = Acquire(mover, moverState);
            if (group is null) Skip("Transform");
            else if (!ReferenceEquals(moverState, state)) state.Mover = mover;
        }

        var opacity = Animation(null, 0, plan, plan.Ms, EaseOf(plan.Ease, opacity: true), FillBehavior.HoldEnd);
        opacity.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            try { hide(); }
            finally { StopCore(primary, state); }
        };
        state.Fading = true;
        Count(state, plan);
        primary.BeginAnimation(UIElement.OpacityProperty, opacity);
        if (group is null) return;

        var scale = ScaleOf(group);
        var move = MoveOf(group);
        var ease = EaseOf(plan.Ease, opacity: false);
        var (fromScale, fromY) = (scale.ScaleX, move.Y);
        // Taban "uzak" uçta: dönüşüm gizleme anına dek orada kalır, sonra bütünüyle kalkar.
        scale.ScaleX = scale.ScaleY = plan.Scale;
        move.Y = plan.OffsetY;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(fromScale, null, plan, plan.Ms, ease, FillBehavior.Stop));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(fromScale, null, plan, plan.Ms, ease, FillBehavior.Stop));
        if (fromY != plan.OffsetY) move.BeginAnimation(TranslateTransform.YProperty, Animation(fromY, null, plan, plan.Ms, ease, FillBehavior.Stop));
    }

    private static void AppearCore(UIElement fade, Action show, MotionPlan? plan, UIElement? mover)
    {
        var state = StateOf(fade);
        // Zaten görünüyor ve kaybolmuyor: oynayan giriş (varsa) sürsün.
        if (plan is not null && fade.IsVisible && !state.Fading)
        {
            show();
            return;
        }
        state.Generation++;
        if (plan is null)
        {
            StopCore(fade, state);
            show();
            return;
        }
        // Görünürken buraya yalnızca solarak kaybolurken gelinir: kaldığı yerden döner. Gizliyse ilk kare saydam çizilir.
        BeginIn(fade, state, mover, plan, resume: fade.IsVisible);
        show();
    }

    private static void DisappearCore(UIElement fade, Action hide, MotionPlan? plan, UIElement? mover)
    {
        var state = StateOf(fade);
        state.Generation++;
        if (plan is null || !plan.Fade || !fade.IsVisible)
        {
            StopCore(fade, state);
            hide();
            return;
        }
        BeginOut(fade, state, mover, plan, hide);
    }

    private static void EnterCore(UIElement element, State state, MotionPlan? plan, Vector travel, bool count = true)
    {
        state.Generation++;
        if (plan is null)
        {
            StopCore(element, state);
            return;
        }
        BeginIn(element, state, MoverOf(element), plan, resume: false, travel, count);
    }

    // ---------------------------------------------------------------- 2.1 imzaları (solma)

    private static MotionSurface SurfaceFor(int? frameRate) =>
        frameRate is { } fps && fps <= MotionPolicy.WidgetFrameRate ? MotionSurface.LayeredWidget : MotionSurface.Window;

    private static MotionPlan? Override(MotionPlan? plan, int milliseconds, int? frameRate) =>
        plan is null ? null : plan with { Ms = MotionPolicy.Duration(milliseconds), Fps = frameRate ?? plan.Fps };

    /// <summary>
    /// Öğeyi saydamdan kendi saydamlığına soldurarak gösterir (bitince animasyon kalkar, öğenin kendi değeri geçerlidir).
    /// Kapalıyken bir şey yapmaz. Bekleyen <see cref="Disappear(UIElement, Action, bool, int, int?)"/> iptal olur.
    /// </summary>
    public static void FadeIn(UIElement element, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var state = StateOf(element);
        var plan = Override(Decide(MotionKind.Fade, SurfaceFor(frameRate), state: state), milliseconds, frameRate);
        state.Generation++;
        if (plan is null)
        {
            StopCore(element, state);
            return;
        }
        BeginIn(element, state, null, plan, resume: false);
    }

    /// <summary>
    /// Gizli öğeyi (pencereyi) <paramref name="show"/> ile gösterir; <paramref name="fade"/> ve geçişler açıksa saydamdan
    /// soldurarak (ilk kare saydam çizilir). Solarak kaybolmaktaysa kaldığı yerden geri gelir, gizleme yapılmaz.
    /// </summary>
    public static void Appear(UIElement element, Action show, bool fade = true, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var plan = fade ? Decide(MotionKind.Fade, SurfaceFor(frameRate), state: StateOf(element)) : null;
        AppearCore(element, show, Override(plan, milliseconds, frameRate), mover: null);
    }

    /// <summary>
    /// Öğeyi soldurup bitince <paramref name="hide"/> ile gizler; geçişler kapalıysa, <paramref name="fade"/> false ise ya da
    /// öğe zaten görünmüyorsa hemen gizler. Arada <see cref="Appear(UIElement, Action, bool, int, int?)"/>/<see cref="FadeIn"/>
    /// gelirse gizleme yapılmaz.
    /// </summary>
    public static void Disappear(UIElement element, Action hide, bool fade = true, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var plan = fade && element.IsVisible ? Decide(MotionKind.Fade, SurfaceFor(frameRate), state: StateOf(element)) : null;
        DisappearCore(element, hide, Override(plan, milliseconds, frameRate), mover: null);
    }

    /// <summary>Öğedeki geçişi durdurur: kendi saydamlığına döner, Motion'ın dönüşümü kalkar (bekleyen gizleme iptal).</summary>
    public static void Stop(UIElement element)
    {
        var state = StateOf(element);
        state.Generation++;
        StopCore(element, state);
    }

    // ---------------------------------------------------------------- 2.2 girişler ve çıkışlar

    /// <summary>
    /// Gizli öğeyi <paramref name="kind"/> geçişiyle gösterir: <paramref name="fade"/> solar, <paramref name="transform"/>
    /// (verilmezse öğenin kendisi; pencere için içeriği) büyür ya da kayar. Plan yoksa hemen gösterir.
    /// </summary>
    /// <param name="pixels">Katmanlı pencerenin fiziksel piksel alanı (büyük kart yalnızca solar); bilinmiyorsa 0.</param>
    /// <param name="batch">Birlikte gösterilen pencere sayısı (masaüstünü göster): çoksa büyüme solmaya, daha çoksa hemen gösterime düşer.</param>
    public static void Appear(UIElement fade, Action show, MotionKind kind, UIElement? transform = null,
                              MotionSurface surface = MotionSurface.Window, long pixels = 0, int batch = 1) =>
        AppearCore(fade, show, Decide(kind, surface, pixels, batch, StateOf(fade)), transform ?? MoverOf(fade));

    /// <summary>
    /// Öğeyi <paramref name="kind"/> geçişiyle gizler; <paramref name="hide"/> geçiş bitince (plan yoksa hemen) çalışır. Arada
    /// gelen gösterme isteği gizlemeyi iptal eder.
    /// </summary>
    public static void Disappear(UIElement fade, Action hide, MotionKind kind, UIElement? transform = null,
                                 MotionSurface surface = MotionSurface.Window, long pixels = 0, int batch = 1) =>
        DisappearCore(fade, hide, fade.IsVisible ? Decide(kind, surface, pixels, batch, StateOf(fade)) : null, transform ?? MoverOf(fade));

    /// <summary>Yeni ya da geri gelen widget: hafifçe büyüyerek ve solarak belirir (aynı anda en çok 3; büyük kart yalnızca solar).</summary>
    public static void Pop(UIElement fade, Action show, UIElement? transform = null, MotionSurface surface = MotionSurface.LayeredWidget,
                           long pixels = 0, int batch = 1) =>
        Appear(fade, show, MotionKind.Pop, transform, surface, pixels, batch);

    /// <summary>Kaldırılan widget: kısa küçülme ve solma, sonra <paramref name="hide"/> (girdiyi çağıran kapatır).</summary>
    public static void Exit(UIElement fade, Action hide, UIElement? transform = null, MotionSurface surface = MotionSurface.LayeredWidget,
                            long pixels = 0) =>
        Disappear(fade, hide, MotionKind.Exit, transform, surface, pixels);

    /// <summary>
    /// Görünen öğenin girişi (Rise: aşağıdan, Drop: yukarıdan, Page, TileEnter: yalnızca ölçek…). <paramref name="delayMs"/>
    /// beklemesiyle birlikte en çok <see cref="MotionPolicy.MaxMs"/>. Plan yoksa öğe hemen son hâlindedir.
    /// </summary>
    public static void Enter(UIElement element, MotionKind kind, MotionSurface surface = MotionSurface.Window, int delayMs = 0, long pixels = 0)
    {
        var state = StateOf(element);
        var plan = Decide(kind, surface, pixels, state: state);
        EnterCore(element, state, plan is null || delayMs <= 0 ? plan : MotionPolicy.Delay(plan, delayMs), default);
    }

    /// <summary>Kademeli giriş (ör. "Widget ekle" kutucukları): toplam süre en çok <see cref="MotionPolicy.MaxMs"/>; Sakin'de hepsi birlikte solar.</summary>
    public static void Cascade(IReadOnlyList<UIElement> elements, MotionKind kind, MotionSurface surface = MotionSurface.Window, long pixels = 0)
    {
        if (elements.Count == 0) return;
        var plan = Decide(kind, surface, pixels);
        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            EnterCore(element, StateOf(element), plan is null ? null : MotionPolicy.Stagger(Level, plan, i), default, count: i == 0);
        }
    }

    /// <summary>
    /// Dikkat nabzı ("Bul", zamanlayıcı bitti): içerik bir kez hafifçe küçülüp döner; aynı öğede saniyede en çok bir, yanıp
    /// sönme yok. Öğe başka bir geçişteyse ya da basılıysa oynamaz. Pencere verilirse içeriği.
    /// </summary>
    public static void Attention(UIElement element, MotionSurface surface = MotionSurface.Window, long pixels = 0)
    {
        if (MoverOf(element) is not { } target) return;
        var state = StateOf(target);
        var now = Environment.TickCount64;
        if (now - state.LastAttention < MotionPolicy.AttentionGapMs)
        {
            Skip("Repeat");
            return;
        }
        if (state.Pressed || state.Owned is not null)
        {
            Skip("Transform");
            return;
        }
        var plan = Decide(MotionKind.Attention, surface, pixels, state: state);
        if (plan is null) return;
        if (Acquire(target, state) is not { } group)
        {
            Skip("Transform");
            return;
        }
        state.LastAttention = now;
        var generation = ++state.Generation;
        Uncount(state);
        var scale = ScaleOf(group);
        var x = Animation(1, plan.Scale, plan, plan.Ms, PulseEase, FillBehavior.Stop);
        var y = Animation(1, plan.Scale, plan, plan.Ms, PulseEase, FillBehavior.Stop);
        x.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            Release(target, state);
            Uncount(state);
        };
        Count(state, plan);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, y);
    }

    // ---------------------------------------------------------------- halka (sözleşme)

    /// <summary>
    /// Halkanın açılışı: <paramref name="fade"/> (açılır pencere) solarak <paramref name="show"/> ile görünür; orta aşmayla
    /// büyür, öğeler ortadan dışarı doğru kademeli (10 ms) büyüyerek gelir. <paramref name="towardCenter"/>: her öğeden halkanın
    /// ortasına vektör (DIP); öğeler bunun <see cref="MotionPolicy.RingTravel"/> kadarından başlar. Girdi beklemez: isabet
    /// testi açıyla yapılmalı. Kapalıyken hepsi hemen son hâlinde.
    /// </summary>
    public static void RingOpen(UIElement fade, Action show, UIElement? center, IReadOnlyList<UIElement> items, IReadOnlyList<Vector>? towardCenter = null)
    {
        var plan = Decide(MotionKind.RingOpen, MotionSurface.Ring, state: StateOf(fade));
        AppearCore(fade, show, plan is null ? null : plan with { Scale = 1, OffsetY = 0, DelayMs = 0 }, mover: null);
        // Sakin: pencerenin solması yeter, öğeler ayrıca solmaz (hemen yerinde).
        RingItems(plan is { Moves: true } ? plan : null, center, items, towardCenter);
    }

    /// <summary>Alt halkaya geçiş (ya da geri): yeni öğeler ortadan kayarak gelir, orta aşmayla yerine oturur.</summary>
    public static void RingSwap(UIElement? center, IReadOnlyList<UIElement> items, IReadOnlyList<Vector>? towardCenter = null) =>
        RingItems(Decide(MotionKind.RingSwap, MotionSurface.Ring), center, items, towardCenter);

    /// <summary>
    /// Halkanın kapanışı: kısa solma (orta biraz küçülür); <paramref name="hide"/> pencere gerçekten kapanınca (plan yoksa
    /// hemen) çalışır. Tuş gönderme gibi işler bu geri çağırmadan sonra yapılmalı.
    /// </summary>
    public static void RingClose(UIElement fade, Action hide, UIElement? center = null) =>
        DisappearCore(fade, hide, fade.IsVisible ? Decide(MotionKind.RingClose, MotionSurface.Ring, state: StateOf(fade)) : null, center);

    private static void RingItems(MotionPlan? plan, UIElement? center, IReadOnlyList<UIElement> items, IReadOnlyList<Vector>? towardCenter)
    {
        if (center is not null)
            EnterCore(center, StateOf(center), plan is { Moves: true } ? plan with { Scale = MotionPolicy.RingCenterScale, Fade = false } : null, default);
        for (var i = 0; i < items.Count; i++)
        {
            var travel = plan is { Moves: true } && towardCenter is not null && i < towardCenter.Count ? towardCenter[i] * MotionPolicy.RingTravel : default;
            EnterCore(items[i], StateOf(items[i]), plan is null ? null : MotionPolicy.Stagger(Level, plan, i), travel, count: false);
        }
    }

    // ---------------------------------------------------------------- basma (ekli özellik)

    /// <summary>
    /// Basınca içerik (öğenin ilk görsel çocuğu, ör. düğme şablonunun kökü) 0,97'ye küçülür, bırakınca döner. Düğmenin kendi
    /// sınırları ve tıklaması değişmez. Sakin ve Kapalı'da hiç dönüşüm konmaz.
    /// </summary>
    public static readonly DependencyProperty PressProperty = DependencyProperty.RegisterAttached(
        "Press", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnPressChanged));

    public static bool GetPress(DependencyObject element) => (bool)element.GetValue(PressProperty);

    public static void SetPress(DependencyObject element, bool value) => element.SetValue(PressProperty, value);

    private static void OnPressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        element.PreviewMouseLeftButtonDown -= OnPressDown;
        element.PreviewMouseLeftButtonUp -= OnPressUp;
        element.LostMouseCapture -= OnPressLost;
        element.MouseLeave -= OnPressLost;
        if (e.NewValue is not true)
        {
            PressUp(element, instant: true);
            return;
        }
        element.PreviewMouseLeftButtonDown += OnPressDown;
        element.PreviewMouseLeftButtonUp += OnPressUp;
        element.LostMouseCapture += OnPressLost;
        element.MouseLeave += OnPressLost;
    }

    private static void OnPressDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element) PressDown(element);
    }

    private static void OnPressUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element) PressUp(element, instant: false);
    }

    private static void OnPressLost(object sender, MouseEventArgs e)
    {
        if (sender is UIElement element) PressUp(element, instant: false);
    }

    private static UIElement? PressTarget(UIElement element) =>
        element is Window ? null
        : VisualTreeHelper.GetChildrenCount(element) > 0 && VisualTreeHelper.GetChild(element, 0) is UIElement child ? child
        : element;

    private static void PressDown(UIElement element)
    {
        if (PressTarget(element) is not { } target) return;
        var state = StateOf(target);
        if (state.Pressed) return;
        var (surface, pixels) = Where(element);
        var plan = Decide(MotionKind.Press, surface, pixels, state: state);
        if (plan is null) return;
        if (Acquire(target, state) is not { } group)
        {
            Skip("Transform");
            return;
        }
        var generation = ++state.Generation;
        Uncount(state);
        state.Pressed = true;
        state.PressPlan = plan;
        var scale = ScaleOf(group);
        var from = scale.ScaleX;
        // Taban basılı ölçekte: basılı kaldıkça orada durur (animasyon tutulmaz).
        scale.ScaleX = scale.ScaleY = plan.Scale;
        var x = Animation(from, null, plan, plan.Ms, OutEase, FillBehavior.Stop);
        var y = Animation(from, null, plan, plan.Ms, OutEase, FillBehavior.Stop);
        x.Completed += (_, _) =>
        {
            if (state.Generation == generation) Uncount(state);
        };
        Count(state, plan);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, y);
    }

    private static void PressUp(UIElement element, bool instant)
    {
        if (PressTarget(element) is not { } target) return;
        var state = StateOf(target);
        if (!state.Pressed) return;
        state.Pressed = false;
        var generation = ++state.Generation;
        Uncount(state);
        if (state.Owned is not { } group || instant || !Enabled || state.PressPlan is not { } plan)
        {
            Release(target, state);
            return;
        }
        var scale = ScaleOf(group);
        var from = scale.ScaleX;
        scale.ScaleX = scale.ScaleY = 1;
        var x = Animation(from, null, plan, plan.ReturnMs, OutEase, FillBehavior.Stop);
        var y = Animation(from, null, plan, plan.ReturnMs, OutEase, FillBehavior.Stop);
        x.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            Release(target, state);
            Uncount(state);
        };
        Count(state, plan);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, y);
    }

    // ---------------------------------------------------------------- fareyle zemin (ekli özellik)

    /// <summary>
    /// Fareyle zemin rengi: hep yerinde duran, isabet testine katılmayan bir katmana (şablondaki Border/Panel/Shape) verilir.
    /// Motion katmana kendi fırçasını koyar ve şablonun sahibi (ya da katmanın üst öğesi) fareyle üstüne gelinince fırçanın
    /// saydamlığını en çok <see cref="MotionPolicy.HoverMs"/>'de 0↔1 yapar; Sakin ve Kapalı'da hemen. Görünürlük hiç değişmez.
    /// </summary>
    public static readonly DependencyProperty HoverFillProperty = DependencyProperty.RegisterAttached(
        "HoverFill", typeof(Color), typeof(Motion), new PropertyMetadata(Colors.Transparent, OnHoverFillChanged));

    public static Color GetHoverFill(DependencyObject element) => (Color)element.GetValue(HoverFillProperty);

    public static void SetHoverFill(DependencyObject element, Color value) => element.SetValue(HoverFillProperty, value);

    private sealed class HoverLayer
    {
        public required SolidColorBrush Brush;
        public UIElement? Host;
        public int Generation;
        public MouseEventHandler? OnMouse;
        public DependencyPropertyChangedEventHandler? OnEnabled;
    }

    private static readonly ConditionalWeakTable<FrameworkElement, HoverLayer> Hovers = [];

    private static void OnHoverFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement layer) return;
        var color = (Color)e.NewValue;
        if (Hovers.TryGetValue(layer, out var hover))
        {
            hover.Brush.Color = color;
            return;
        }
        hover = new HoverLayer { Brush = new SolidColorBrush(color) { Opacity = 0 } };
        switch (layer)
        {
            case Border border: border.Background = hover.Brush; break;
            case Panel panel: panel.Background = hover.Brush; break;
            case Shape shape: shape.Fill = hover.Brush; break;
            case Control control: control.Background = hover.Brush; break;
            default: return;
        }
        Hovers.Add(layer, hover);
        hover.OnMouse = (_, _) => UpdateHover(layer, hover, instant: false);
        hover.OnEnabled = (_, _) => UpdateHover(layer, hover, instant: false);
        layer.Loaded += (_, _) => AttachHover(layer, hover);
        layer.Unloaded += (_, _) => DetachHover(layer, hover);
        if (layer.IsLoaded) AttachHover(layer, hover);
    }

    private static void AttachHover(FrameworkElement layer, HoverLayer hover)
    {
        var host = layer.TemplatedParent as UIElement ?? VisualTreeHelper.GetParent(layer) as UIElement;
        if (host is null || ReferenceEquals(host, hover.Host)) return;
        DetachHover(layer, hover);
        hover.Host = host;
        host.MouseEnter += hover.OnMouse;
        host.MouseLeave += hover.OnMouse;
        host.IsEnabledChanged += hover.OnEnabled;
        UpdateHover(layer, hover, instant: true);
    }

    private static void DetachHover(FrameworkElement layer, HoverLayer hover)
    {
        if (hover.Host is { } host)
        {
            host.MouseEnter -= hover.OnMouse;
            host.MouseLeave -= hover.OnMouse;
            host.IsEnabledChanged -= hover.OnEnabled;
        }
        hover.Host = null;
        hover.Generation++;
        Uncount(StateOf(layer));
        hover.Brush.BeginAnimation(Brush.OpacityProperty, null);
        hover.Brush.Opacity = 0;
    }

    private static void UpdateHover(FrameworkElement layer, HoverLayer hover, bool instant)
    {
        var brush = hover.Brush;
        var target = hover.Host is { IsMouseOver: true, IsEnabled: true } ? 1.0 : 0.0;
        var current = brush.Opacity;
        if (current == target && !brush.HasAnimatedProperties) return;
        var generation = ++hover.Generation;
        var state = StateOf(layer);
        Uncount(state);
        brush.Opacity = target;
        MotionPlan? plan = null;
        if (!instant && current != target)
        {
            var (surface, pixels) = Where(layer);
            plan = Decide(MotionKind.Hover, surface, pixels, state: state);
        }
        if (plan is null)
        {
            brush.BeginAnimation(Brush.OpacityProperty, null);
            return;
        }
        var animation = Animation(current, null, plan, plan.Ms, OutEase, FillBehavior.Stop);
        animation.Completed += (_, _) =>
        {
            if (hover.Generation == generation) Uncount(state);
        };
        Count(state, plan);
        brush.BeginAnimation(Brush.OpacityProperty, animation);
    }
}
