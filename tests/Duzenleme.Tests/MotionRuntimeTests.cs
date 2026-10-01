using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Views;

namespace Duzenleme.Tests;

/// <summary>
/// Views.Motion'ın çalışma anı sözleşmesi. Kapalı düzeyde (Windows'un "Animasyon efektleri" kapalı ya da Ayarlar'da Kapalı;
/// Store erişilebilirliği) hiçbir zaman çizelgesi kurulmaz, göster/gizle hemen ve eşzamanlı çalışır, RenderTransform'a
/// dokunulmaz. Canlı'da katmanlı geçiş sayacı kaymaz, birlikte gösterilen pencereler aynı planı alır, saydamlığı oynamayan
/// giriş çıkışın solmasını kaldırır ve durdurunca her şey kalkar.
/// Öğeler hiç gösterilmeyen (WS_VISIBLE yok, ekran dışında, etkinleşmez) bir HwndSource'ta "görünür" sayılır; uygulama nesnesi
/// kurulmaz, zaman ilerletilmez (yalnızca Canlı testinde, düzey yansımayla verilir ve geri alınır).
/// </summary>
public class MotionRuntimeTests
{
    [Fact]
    public void Off_applies_every_change_at_once_without_clocks() => OnSta(() =>
    {
        using var level = UseLevel(MotionLevel.Off);
        Settled();
        var root = new StackPanel();
        T Add<T>(T element) where T : UIElement
        {
            root.Children.Add(element);
            return element;
        }
        var hidden = Add(new Border { Visibility = Visibility.Collapsed });
        var hidden21 = Add(new Border { Visibility = Visibility.Collapsed });
        var visible = Add(new Border { Child = new TextBlock() });
        var visible21 = Add(new Border());
        var leaving = Add(new Border());
        var card = Add(new Border { Visibility = Visibility.Collapsed });
        var surface = new Border();
        card.Child = surface;
        var entering = Add(new Border());
        var page = Add(new Border());
        var cascade = new UIElement[] { Add(new Border()), Add(new Border()), Add(new Border()) };
        var pulse = Add(new Border());
        var ring = Add(new Grid { Visibility = Visibility.Collapsed });
        var center = new Border();
        var item = new Border();
        ring.Children.Add(center);
        ring.Children.Add(item);
        var pressable = Add(new Border { Child = new TextBlock() });
        var hoverHost = Add(new Grid());
        var hoverLayer = new Border();
        hoverHost.Children.Add(hoverLayer);
        using var source = Host(root);
        Pump();
        Assert.True(visible.IsVisible);
        Assert.True(leaving.IsVisible);

        var calls = new List<string>();
        Motion.Appear(hidden, () => { hidden.Visibility = Visibility.Visible; calls.Add("appear"); }, MotionKind.Pop);
        Motion.Disappear(leaving, () => { leaving.Visibility = Visibility.Collapsed; calls.Add("disappear"); }, MotionKind.Fade);
        Motion.Pop(card, () => { card.Visibility = Visibility.Visible; calls.Add("pop"); }, surface);
        Motion.Exit(visible, () => calls.Add("exit"), surface: MotionSurface.Window);
        Motion.Appear(hidden21, () => { hidden21.Visibility = Visibility.Visible; calls.Add("appear21"); });
        Motion.Disappear(visible21, () => calls.Add("disappear21"), fade: true, frameRate: MotionPolicy.WidgetFrameRate);
        Assert.Equal(["appear", "disappear", "pop", "exit", "appear21", "disappear21"], calls);

        Motion.FadeIn(entering);
        Motion.Enter(entering, MotionKind.Rise);
        Motion.Enter(entering, MotionKind.Page, delayMs: 40);
        Motion.Enter(entering, MotionKind.TileEnter, MotionSurface.LayeredWidget, pixels: 100_000);
        Motion.PageEnter(page);
        Motion.Cascade(cascade, MotionKind.Pop);
        Motion.Attention(pulse);
        Motion.Attention(surface, MotionSurface.LayeredWidget, 100_000);
        Motion.AfterEntry(entering, () => calls.Add("after"));
        Motion.RingOpen(ring, () => { ring.Visibility = Visibility.Visible; calls.Add("ring-open"); }, center, [item], [new Vector(10, 0)]);
        Motion.RingSwap(center, [item], [new Vector(0, 10)]);
        Motion.RingClose(ring, () => calls.Add("ring-close"), center);
        Assert.Equal(["appear", "disappear", "pop", "exit", "appear21", "disappear21", "after", "ring-open", "ring-close"], calls);

        // Basma ve fareyle zemin: olaylar elle (gerçek fare yok); Kapalı'da hiçbir şey küçülmez ya da solmaz.
        Motion.SetPress(pressable, true);
        pressable.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        Assert.True(Untouched(((Border)pressable).Child));
        pressable.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        Motion.SetHoverFill(hoverLayer, Colors.Red);
        Pump();
        hoverHost.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
        hoverHost.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
        var fill = Assert.IsType<SolidColorBrush>(hoverLayer.Background);
        Assert.False(fill.HasAnimatedProperties);

        foreach (var element in Descendants(root))
        {
            Assert.True(Untouched(element), $"{element.GetType().Name}: animasyon ya da Motion dönüşümü kaldı");
            Assert.Equal(1.0, element.Opacity);
        }
        Assert.Equal(0, Motion.ActiveLayered);
    });

    [Fact]
    public void Lively_keeps_its_books_straight() => OnSta(() =>
    {
        using var level = UseLevel(MotionLevel.Lively);
        Settled();
        var root = new StackPanel();
        (Border Card, Border Surface) Card(bool shown)
        {
            var inner = new Border();
            var outer = new Border { Child = inner, Visibility = shown ? Visibility.Visible : Visibility.Collapsed };
            root.Children.Add(outer);
            return (outer, inner);
        }
        var (cardA, surfaceA) = Card(shown: false);
        var batch = Enumerable.Range(0, 3).Select(i => Card(shown: false)).ToList();
        var (_, other) = Card(shown: true);
        var fading = new Border();
        root.Children.Add(fading);
        using var source = Host(root);
        Pump();
        Assert.Equal(0, Motion.ActiveLayered);

        // Kartın yüzeyinde dikkat nabzı oynarken kart büyüyerek gösterilir: nabız iptal olur ve sayaçtan düşer (eskiden
        // tamamlanması atlandığı için sayaçta kalıcı olarak kalırdı; 3 kayma her büyümeyi solmaya çevirirdi).
        Motion.Attention(surfaceA, MotionSurface.LayeredWidget, 100_000);
        Assert.Equal(1, Motion.ActiveLayered);
        Motion.Appear(cardA, () => cardA.Visibility = Visibility.Visible, MotionKind.Pop, surfaceA, MotionSurface.LayeredWidget, 100_000);
        Assert.Equal(1, Motion.ActiveLayered);
        Assert.IsType<TransformGroup>(surfaceA.RenderTransform);
        Motion.Stop(cardA);
        Assert.Equal(0, Motion.ActiveLayered);
        Assert.True(Untouched(cardA) && Untouched(surfaceA));

        // Birlikte gösterilen pencereler tek karar: ilgisiz bir geçiş oynarken de üçü de büyür (sondakiler solmaya düşmez).
        Motion.Attention(other, MotionSurface.LayeredWidget, 100_000);
        foreach (var (card, surface) in batch)
            Motion.Appear(card, () => card.Visibility = Visibility.Visible, MotionKind.Pop, surface, MotionSurface.LayeredWidget, 100_000, batch.Count);
        Assert.All(batch, b => Assert.IsType<TransformGroup>(b.Surface.RenderTransform));
        Assert.Equal(1 + batch.Count, Motion.ActiveLayered);

        // Solarak kaybolurken saydamlığı oynamayan bir giriş: çıkışın son değerini tutan solma kalkar, gizleme yapılmaz.
        var hidden = false;
        Motion.Disappear(fading, () => hidden = true, MotionKind.Fade);
        Assert.True(fading.HasAnimatedProperties);
        Motion.Enter(fading, MotionKind.TileEnter);
        Assert.False(fading.HasAnimatedProperties);
        Assert.IsType<TransformGroup>(fading.RenderTransform);

        // Durdurunca her şey kalkar: animasyon yok, dönüşüm öğenin kendi (boş) değerine döner, sayaç sıfır.
        foreach (var (card, _) in batch) Motion.Stop(card);
        Motion.Stop(other);
        Motion.Stop(fading);
        Assert.False(hidden);
        Assert.Equal(0, Motion.ActiveLayered);
        foreach (var element in Descendants(root))
            Assert.True(Untouched(element), $"{element.GetType().Name}: animasyon ya da Motion dönüşümü kaldı");
    });

    [Fact]
    public void Stop_gives_back_the_element_s_own_transform() => OnSta(() =>
    {
        using var level = UseLevel(MotionLevel.Lively);
        Settled();
        var own = new Border { RenderTransformOrigin = new Point(0, 1) };
        var busy = new Border { RenderTransform = new RotateTransform(5) };
        var root = new StackPanel { Children = { own, busy } };
        using var source = Host(root);
        Pump();

        Motion.Enter(own, MotionKind.TileEnter);
        Assert.IsType<TransformGroup>(own.RenderTransform);
        Motion.Stop(own);
        Assert.Equal(DependencyProperty.UnsetValue, own.ReadLocalValue(UIElement.RenderTransformProperty));
        Assert.Equal(new Point(0, 1), own.RenderTransformOrigin);

        // Kendi dönüşümü olan öğeye dokunulmaz (yalnızca solabilirdi; TileEnter solmaz: hiçbir şey oynamaz).
        var rotate = busy.RenderTransform;
        Motion.Enter(busy, MotionKind.TileEnter);
        Assert.Same(rotate, busy.RenderTransform);
        Assert.False(busy.HasAnimatedProperties);
        Assert.Equal(0, Motion.ActiveLayered);
    });

    // ---------------------------------------------------------------- yardımcılar

    /// <summary>Animasyonu yok ve RenderTransform'u Motion'dan kalmamış (yerel değeri boş, Identity).</summary>
    private static bool Untouched(UIElement element) =>
        !element.HasAnimatedProperties
        && element.ReadLocalValue(UIElement.RenderTransformProperty) == DependencyProperty.UnsetValue
        && ReferenceEquals(element.RenderTransform, Transform.Identity);

    private static IEnumerable<UIElement> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (VisualTreeHelper.GetChild(parent, i) is not UIElement child) continue;
            yield return child;
            foreach (var below in Descendants(child)) yield return below;
        }
    }

    /// <summary>Öğeleri görünür sayan ama hiç gösterilmeyen kaynak: WS_POPUP (WS_VISIBLE yok), ekran dışında, etkinleşmez.</summary>
    private static HwndSource Host(Visual root)
    {
        const int WS_POPUP = unchecked((int)0x80000000);
        const int WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        var parameters = new HwndSourceParameters("NestDesk motion test")
        {
            WindowStyle = WS_POPUP,
            ExtendedWindowStyle = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            PositionX = -32000,
            PositionY = -32000,
            Width = 400,
            Height = 400,
        };
        return new HwndSource(parameters) { RootVisual = root };
    }

    /// <summary>
    /// Önceki bir testten (başarısız olup durdurulmamış geçişler) sayaçta kalan kayıtlar süreleri dolunca düşer: testler
    /// birbirini etkilemesin.
    /// </summary>
    private static void Settled() =>
        Assert.True(SpinWait.SpinUntil(() => Motion.ActiveLayered == 0, TimeSpan.FromSeconds(2)), "katmanlı geçiş sayacı sıfırlanmadı");

    /// <summary>Kuyruktaki yerleşim ve Loaded işlerini çalıştırır (zaman çizelgeleri kurulmadan önce çağrılır).</summary>
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

    /// <summary>Motion.Level yalnızca Initialize/Refresh'te yazılır: test süresince verilir, sonra geri alınır.</summary>
    private static IDisposable UseLevel(MotionLevel level)
    {
        var property = typeof(Motion).GetProperty(nameof(Motion.Level))!;
        var before = (MotionLevel)property.GetValue(null)!;
        property.SetValue(null, level);
        return new Undo(() => property.SetValue(null, before));
    }

    private sealed class Undo(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    /// <summary>WPF öğeleri STA iş parçacığında kurulur (uygulama nesnesi yok).</summary>
    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA testi zaman aşımına uğradı");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
