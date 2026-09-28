using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Ana penceredeki listelerin dosya/klasör/kabuk nesnesi simgesi. Simge, Image'in genişliği × ekran ölçeği kadar
/// pikselle (1:1 çizilsin, bulanıklaşmasın) arka planda yüklenir: arayüz iş parçacığında kabuk ya da disk çağrısı yapılmaz.
/// Pencere ölçeği farklı bir monitöre geçince yeniden istenir. Kullanım:
/// <c>&lt;Image views:ShellIconImage.Path="{Binding IconPath}" Width="28" Height="28" /&gt;</c>;
/// simge dosyada değişince (ör. klasör simgesi) <see cref="VersionProperty"/> artırılır.
/// </summary>
public static class ShellIconImage
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(ShellIconImage), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty VersionProperty = DependencyProperty.RegisterAttached(
        "Version", typeof(int), typeof(ShellIconImage), new PropertyMetadata(0, OnChanged));

    // En son istenen "yol|piksel|sürüm": aynı istek tekrarlanmaz, geç gelen eski sonuç yenisinin üstüne yazılmaz.
    private static readonly DependencyProperty RequestedProperty = DependencyProperty.RegisterAttached(
        "Requested", typeof(string), typeof(ShellIconImage), new PropertyMetadata(null));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked", typeof(bool), typeof(ShellIconImage), new PropertyMetadata(false));

    public static string? GetPath(DependencyObject d) => (string?)d.GetValue(PathProperty);
    public static void SetPath(DependencyObject d, string? value) => d.SetValue(PathProperty, value);
    public static int GetVersion(DependencyObject d) => (int)d.GetValue(VersionProperty);
    public static void SetVersion(DependencyObject d, int value) => d.SetValue(VersionProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        if (!(bool)image.GetValue(HookedProperty))
        {
            image.SetValue(HookedProperty, true);
            image.DpiChanged += (_, _) => Load(image);
            image.Loaded += (_, _) => Load(image);
        }
        Load(image);
    }

    private static void Load(Image image)
    {
        var path = GetPath(image);
        if (string.IsNullOrEmpty(path))
        {
            image.ClearValue(RequestedProperty);
            image.Source = null;
            return;
        }
        // Pencereye bağlanınca (Loaded) ekran ölçeği kesinleşir; önce yanlış boyutta istenmesin.
        if (PresentationSource.FromVisual(image) is null) return;
        var dip = double.IsNaN(image.Width) ? 32 : image.Width;
        var pixels = IconSizing.DevicePixels(dip, VisualTreeHelper.GetDpi(image).PixelsPerDip);
        var key = $"{path}|{pixels}|{GetVersion(image)}";
        var previous = (string?)image.GetValue(RequestedProperty);
        if (previous == key) return;
        // Başka bir dosyanın simgesi yenisi gelene dek görünmesin (aynı dosyanın eski boyutu/sürümü görünebilir).
        if (previous is null || !previous.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase)) image.Source = null;
        image.SetValue(RequestedProperty, key);
        ShellIcons.Request(path, pixels, preview: false, icon =>
        {
            if ((string?)image.GetValue(RequestedProperty) == key) image.Source = icon;
        });
    }
}
