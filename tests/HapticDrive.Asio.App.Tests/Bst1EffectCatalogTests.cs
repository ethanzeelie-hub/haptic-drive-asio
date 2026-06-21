namespace HapticDrive.Asio.App.Tests;

public sealed class Bst1EffectCatalogTests
{
    [Fact]
    public void Catalog_ExposesStableOrdersForCurrentShippedEffects()
    {
        Assert.Equal(
            ["engine", "gear", "kerb", "impact", "road", "slip", "lock"],
            Bst1EffectCatalog.DiagnosticsOrderKeys);
        Assert.Equal(
            ["gear", "road", "engine", "kerb", "impact", "slip", "lock"],
            Bst1EffectCatalog.RoutingOrderKeys);
        Assert.Equal(
            ["engine", "gear", "kerb", "impact", "road", "slip", "lock"],
            Bst1EffectCatalog.EffectsPageOrderKeys);
    }

    [Fact]
    public void GetRequired_ReturnsDescriptorForKnownEffect()
    {
        var descriptor = Bst1EffectCatalog.GetRequired("road");

        Assert.Equal("road", descriptor.Key);
        Assert.Equal("road-texture", descriptor.SourceEffectKey);
        Assert.Equal("road", descriptor.DisplayName);
        Assert.Equal(4, descriptor.DiagnosticsOrder);
        Assert.Equal(1, descriptor.RoutingOrder);
    }
}
