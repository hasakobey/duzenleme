using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Duzenleme.Core;
using Wpf.Ui.Animations;

namespace Duzenleme.Views;

/// <summary>
/// Uygulamadaki bütün hareketin tek yeri (kurallar <see cref="MotionPolicy"/>): yalnızca kısa saydamlık geçişleri, yalnızca
/// Windows'un "Animasyon efektleri" açıkken ve Ayarlar'daki "Animasyonlar" kapatılmadıysa; boyut/konum hiç oynamaz, fareyle
/// üstüne gelmek hiçbir şey oynatmaz. Kullananlar: widget'ların gizlenip görünmesi (masaüstünü gizle, göz at), yeni widget'ın
/// belirmesi ve vurgusunun sönmesi, sağ tık menülerinin açılışı, ana penceredeki sayfa geçişi, göz atma çubuğu ve bildirim
/// şeridi. Başka yerde BeginAnimation/Storyboard kullanılmaz (test denetler).
/// </summary>
internal static class Motion
{
    private static bool _initialized;

    /// <summary>Geçişler şu an oynuyor mu?</summary>
    public static bool Enabled { get; private set; }

    /// <summary>Windows'un "Animasyon efektleri" açık mı (Ayarlar'daki not için)?</summary>
    public static bool WindowsAllows { get; private set; } = true;

    /// <summary><see cref="Enabled"/> ya da <see cref="WindowsAllows"/> değişti. UI iş parçacığında.</summary>
    public static event Action? Changed;

    /// <summary>Açılışta bir kez (ayarlar okunduktan sonra): menü solmasını ayarlar ve değişiklikleri izler.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        WindowsAllows = SystemParameters.ClientAreaAnimation;
        Enabled = Compute();
        ApplyResources();
        // Windows'un ayarı değişince (Ayarlar > Erişilebilirlik > Görsel efektler > Animasyon efektleri) WPF önbelleğini
        // tazeler ve bunu bildirir; uygulamanın ayarı SettingsChanged ile gelir.
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.MenuAnimation) or "")
                Application.Current?.Dispatcher.BeginInvoke(Refresh);
        };
        AppHost.SettingsChanged += Refresh;
    }

    /// <summary>Ayar ya da Windows'un ayarı değişmiş olabilir: durum yeniden hesaplanır.</summary>
    public static void Refresh()
    {
        var windows = SystemParameters.ClientAreaAnimation;
        var enabled = Compute();
        ApplyResources();
        if (enabled == Enabled && windows == WindowsAllows) return;
        Enabled = enabled;
        WindowsAllows = windows;
        DebugLog.Write($"animasyonlar {(enabled ? "açık" : "kapalı")} (Windows: {(windows ? "açık" : "kapalı")})");
        Changed?.Invoke();
    }

    private static bool Compute() => MotionPolicy.Enabled(SystemParameters.ClientAreaAnimation, AppHost.Settings.AnimationsOff);

    /// <summary>
    /// Sağ tık menüleri açılır pencerelerinin geçişini bu kaynaktan alır: açıkken ve Windows'un menü animasyonu da açıkken
    /// solma, değilse hiçbiri (Windows'un kayma seçeneği kullanılmaz). Alt menüler fareyle açıldığı için hiç solmaz
    /// (QuietStyles). Uygulamanın kendi sözlüğüne yazılır: birleştirilmiş sözlüklerdeki varsayılanı (None) ezer.
    /// </summary>
    private static void ApplyResources()
    {
        if (Application.Current?.Resources is not { } resources) return;
        var menu = MotionPolicy.MenuFade(Compute(), SystemParameters.MenuAnimation) ? PopupAnimation.Fade : PopupAnimation.None;
        if (!Equals(resources[SystemParameters.MenuPopupAnimationKey], menu)) resources[SystemParameters.MenuPopupAnimationKey] = menu;
    }

    /// <summary>Ana penceredeki sayfa geçişi: açıkken kısa solma, değilse yok.</summary>
    public static void ApplyTo(Wpf.Ui.Controls.NavigationView navigation)
    {
        navigation.Transition = Enabled ? Transition.FadeIn : Transition.None;
        navigation.TransitionDuration = MotionPolicy.FadeMs;
    }

    // ---------------------------------------------------------------- solma

    /// <summary>Öğe başına durum: son istenen geçişin sırası (sonradan gelen istek bekleyen gizlemeyi iptal eder).</summary>
    private sealed class State
    {
        public int Generation;
        public bool Fading;
    }

    private static readonly ConditionalWeakTable<UIElement, State> States = [];

    private static State StateOf(UIElement element) => States.GetOrCreateValue(element);

    private static Duration DurationOf(int milliseconds) => new(TimeSpan.FromMilliseconds(MotionPolicy.Duration(milliseconds)));

    /// <summary>
    /// Öğeyi saydamdan kendi saydamlığına soldurarak gösterir (bitince animasyon kalkar, öğenin kendi değeri geçerlidir).
    /// Kapalıyken bir şey yapmaz. Bekleyen <see cref="Disappear"/> iptal olur.
    /// </summary>
    public static void FadeIn(UIElement element, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var state = StateOf(element);
        state.Generation++;
        if (!Enabled)
        {
            Stop(element, state);
            return;
        }
        Begin(element, state, from: 0, milliseconds, frameRate);
    }

    /// <summary>
    /// Gizli öğeyi (pencereyi) <paramref name="show"/> ile gösterir; <paramref name="fade"/> ve geçişler açıksa saydamdan
    /// soldurarak (ilk kare saydam çizilir). Solarak kaybolmaktaysa kaldığı yerden geri gelir, gizleme yapılmaz.
    /// </summary>
    public static void Appear(UIElement element, Action show, bool fade = true, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var state = StateOf(element);
        state.Generation++;
        if (!Enabled || !fade)
        {
            Stop(element, state);
            show();
            return;
        }
        if (element.IsVisible)
        {
            // Solarak kaybolurken geri istendi: kaldığı saydamlıktan dönülür. Zaten görünüyorsa bir şey oynamaz.
            if (state.Fading) Begin(element, state, from: (double)element.GetValue(UIElement.OpacityProperty), milliseconds, frameRate);
            show();
            return;
        }
        Begin(element, state, from: 0, milliseconds, frameRate);
        show();
    }

    /// <summary>
    /// Öğeyi soldurup bitince <paramref name="hide"/> ile gizler; geçişler kapalıysa, <paramref name="fade"/> false ise ya da
    /// öğe zaten görünmüyorsa hemen gizler. Arada <see cref="Appear"/>/<see cref="FadeIn"/> gelirse gizleme yapılmaz.
    /// </summary>
    public static void Disappear(UIElement element, Action hide, bool fade = true, int milliseconds = MotionPolicy.FadeMs, int? frameRate = null)
    {
        var state = StateOf(element);
        var generation = ++state.Generation;
        if (!Enabled || !fade || !element.IsVisible)
        {
            Stop(element, state);
            hide();
            return;
        }
        var animation = new DoubleAnimation { To = 0, Duration = DurationOf(milliseconds), FillBehavior = FillBehavior.HoldEnd };
        if (frameRate is { } fps) Timeline.SetDesiredFrameRate(animation, fps);
        animation.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            hide();
            Stop(element, state);
        };
        state.Fading = true;
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>Öğedeki geçişi durdurur; öğe kendi saydamlığına döner (bekleyen gizleme iptal).</summary>
    public static void Stop(UIElement element)
    {
        var state = StateOf(element);
        state.Generation++;
        Stop(element, state);
    }

    private static void Stop(UIElement element, State state)
    {
        state.Fading = false;
        element.BeginAnimation(UIElement.OpacityProperty, null);
    }

    /// <summary>from'dan öğenin kendi (taban) saydamlığına: To verilmez, böylece taban değer bu arada değişse de ona varılır.</summary>
    private static void Begin(UIElement element, State state, double from, int milliseconds, int? frameRate)
    {
        var generation = state.Generation;
        var animation = new DoubleAnimation { From = from, Duration = DurationOf(milliseconds), FillBehavior = FillBehavior.Stop };
        if (frameRate is { } fps) Timeline.SetDesiredFrameRate(animation, fps);
        animation.Completed += (_, _) =>
        {
            if (state.Generation == generation) state.Fading = false;
        };
        state.Fading = false;
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
