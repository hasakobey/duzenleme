using System.Windows.Markup;

namespace Duzenleme.Localization;

/// <summary>
/// XAML'de çeviri: <c>Text="{l:T 'Widget\'lar'}"</c> (kök öğede <c>xmlns:l="clr-namespace:Duzenleme.Localization"</c>).
/// Metni hep tek tırnakla yaz; içindeki <c>'</c> → <c>\'</c>, <c>"</c> → <c>&amp;quot;</c>. Bağlam:
/// <c>{l:T 'Gizli', Context=simge}</c>. Değer XAML yüklenirken bir kez hesaplanır: dil süreç başında belirlendiği için
/// yeterli (stil Setter'ları ilk kullanımda, DataTemplate içerikleri oluşturulurken değerlenir).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }

    public TExtension(string text) => Text = text;

    [ConstructorArgument("text")]
    public string Text { get; set; } = "";

    public string? Context { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Context is null ? L.Dyn(Text) : L.Dyn(Text, Context);
}
