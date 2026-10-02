using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Duzenleme.Views;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Özel vurgu rengi seçici (2.2, Görünüm ▸ Vurgu rengi ▸ Özel renk…): pencere kurulur (gösterilmez), otomasyon kimlikleri
/// yerindedir, açılışta önizleme yoktur, renk kutusu ve renk kodu önizler, okunamayan kod Kaydet'i kapatır, Kaydet yazar.
/// </summary>
public class ColorPickerTests
{
    [Fact]
    public void Picker_builds_previews_and_saves() => OnSta(() =>
    {
        var previewed = new List<string?>();
        string? saved = null;
        var picker = new ColorPickerDialog(new NativeMethods.POINT { X = 0, Y = 0 }, () => null, "#22d3ee", previewed.Add, h => saved = h);

        Assert.Equal("ColorPicker", AutomationProperties.GetAutomationId(picker));
        var byId = Descendants(picker).OfType<FrameworkElement>()
            .Where(e => AutomationProperties.GetAutomationId(e) is { Length: > 0 })
            .ToDictionary(AutomationProperties.GetAutomationId);
        foreach (var id in new[] { "ColorPicker.Hex", "ColorPicker.Windows", "ColorPicker.Save", "ColorPicker.Cancel" })
            Assert.Contains(id, byId.Keys);
        Assert.All(Enumerable.Range(1, 24), n => Assert.Contains("ColorPicker.Swatch." + n, byId.Keys));
        Assert.DoesNotContain("ColorPicker.Swatch.25", byId.Keys);

        var hex = (TextBox)byId["ColorPicker.Hex"];
        var save = (ButtonBase)byId["ColorPicker.Save"];
        Assert.Equal("#22D3EE", hex.Text);
        Assert.Empty(previewed); // açılışta kayıtlı renk görünür, önizleme yok
        Assert.True(save.IsEnabled);

        byId["ColorPicker.Swatch.1"].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal("#E5484D", previewed[^1]);
        Assert.Equal("#E5484D", hex.Text);

        hex.Text = "#12";
        Assert.False(save.IsEnabled);
        hex.Text = "#123456";
        Assert.Equal("#123456", previewed[^1]);
        Assert.True(save.IsEnabled);

        save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal("#123456", saved);
        Assert.Null(previewed[^1]); // kapanınca önizleme bırakılır (kayıtlı renk aynısı)
    });

    /// <summary>
    /// Her satır renk kutularının genişliğine sığar (Türkçe ve İngilizce): "Windows vurgu rengi" düğmesi renk kodunun yanında
    /// kırpılıyordu ("Winc"); artık kendi satırında.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_row_fits_the_swatch_width(bool english) => OnSta(() =>
    {
        using var _ = english ? Duzenleme.Localization.L.Use(Duzenleme.Localization.Lang.En) : null;
        var picker = new ColorPickerDialog(new NativeMethods.POINT { X = 0, Y = 0 }, () => null, "#22d3ee", _ => { }, _ => { });
        var system = Descendants(picker).OfType<FrameworkElement>().Single(e => AutomationProperties.GetAutomationId(e) == "ColorPicker.Windows");
        var body = (StackPanel)LogicalTreeHelper.GetParent(system);
        Assert.False(double.IsNaN(body.Width));
        foreach (var row in body.Children.OfType<FrameworkElement>().Where(r => r is not TextBlock { TextWrapping: TextWrapping.Wrap }))
        {
            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(row.DesiredSize.Width <= body.Width + 0.5, $"{row.GetType().Name} {row.DesiredSize.Width} > {body.Width}");
        }
        var hex = Descendants(picker).OfType<FrameworkElement>().Single(e => AutomationProperties.GetAutomationId(e) == "ColorPicker.Hex");
        Assert.NotSame(LogicalTreeHelper.GetParent(hex), body); // kod satırı ayrı, düğme ayrı
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA testi zaman aşımına uğradı");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
