using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class EffectDescriptorTests
{
    [Fact]
    public void DefaultSettingsValidate()
    {
        foreach (var descriptor in BuiltInHapticEffectRegistry.Instance.All)
        {
            var errors = descriptor.Validate(descriptor.CreateDefaultSettings());
            Assert.Empty(errors);
        }
    }

    [Fact]
    public void OutOfRangeParameterReportsValidationError()
    {
        var descriptor = BuiltInHapticEffectRegistry.Instance.GetRequired("engine-rpm");
        var invalid = descriptor.CreateDefaultSettings() with
        {
            Parameters = new Dictionary<string, double>(descriptor.CreateDefaultSettings().Parameters, StringComparer.OrdinalIgnoreCase)
            {
                ["gain"] = 99d
            }
        };

        var errors = descriptor.Validate(invalid);

        Assert.Contains(errors, error => error.Contains("gain", StringComparison.OrdinalIgnoreCase));
    }
}
