using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Boyut: Küçük · Orta · Büyük" (<see cref="SizePresets"/>): üç ölçek; kayıtlı başka bir ölçek kaybolmaz, "Özel (%150)" olarak
/// seçili görünür.
/// </summary>
public class SizePresetsTests
{
    [Fact]
    public void Three_sizes_map_to_scales()
    {
        Assert.Equal(["Small", "Medium", "Large"], SizePresets.All.Select(p => p.Id));
        Assert.Equal([0.85, 1.0, 1.25], SizePresets.All.Select(p => p.Scale));
        Assert.Equal(["Küçük", "Orta", "Büyük"], SizePresets.All.Select(p => p.Label));
        using var _ = L.Use(Lang.En);
        Assert.Equal(["Small", "Medium", "Large"], SizePresets.All.Select(p => p.Label));
    }

    [Theory]
    [InlineData(0.85, "Small")]
    [InlineData(1.0, "Medium")]
    [InlineData(1.25, "Large")]
    [InlineData(1.2501, "Large")]
    [InlineData(1.5, null)]
    [InlineData(0.7, null)]
    [InlineData(1.1, null)]
    public void Saved_scale_matches_a_size_or_none(double scale, string? expected) =>
        Assert.Equal(expected, SizePresets.Match(scale)?.Id);

    [Fact]
    public void Other_saved_scales_show_as_custom_with_their_percent()
    {
        Assert.Equal("Özel (%150)", SizePresets.CustomLabel(1.5));
        Assert.Equal("Özel (%70)", SizePresets.CustomLabel(0.7));
        Assert.Null(SizePresets.CustomLabel(1.0));
        Assert.Null(SizePresets.CustomLabel(0.85));
        using var _ = L.Use(Lang.En);
        Assert.Equal("Custom (150%)", SizePresets.CustomLabel(1.5));
    }
}
