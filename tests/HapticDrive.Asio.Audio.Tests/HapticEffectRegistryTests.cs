using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticEffectRegistryTests
{
    [Fact]
    public void ContainsAllExistingEffects()
    {
        var registry = BuiltInHapticEffectRegistry.Instance;

        Assert.Equal(
            [
                "diagnostic-test",
                "engine-rpm",
                "gear-shift",
                "impact",
                "kerb",
                "road-texture",
                "slip-lock"
            ],
            registry.All
                .Select(descriptor => descriptor.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void EffectKeysAreUniqueAndStable()
    {
        var registry = BuiltInHapticEffectRegistry.Instance;
        var keys = registry.All.Select(descriptor => descriptor.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("engine-rpm", keys);
        Assert.Contains("road-texture", keys);
        Assert.Contains("kerb", keys);
        Assert.Contains("slip-lock", keys);
        Assert.Contains("gear-shift", keys);
        Assert.Contains("impact", keys);
        Assert.Contains("diagnostic-test", keys);
    }
}
