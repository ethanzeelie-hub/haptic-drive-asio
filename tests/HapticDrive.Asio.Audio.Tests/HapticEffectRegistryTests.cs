using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticEffectRegistryTests
{
    [Fact]
    public void EveryBuiltInDescriptorCreatesFunctionalRuntime()
    {
        var registry = BuiltInHapticEffectRegistry.Instance;

        Assert.Equal(
            ["engine-rpm", "gear-shift", "impact", "kerb", "road-texture", "slip-lock"],
            registry.All.Select(descriptor => descriptor.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray());

        foreach (var descriptor in registry.All)
        {
            var runtime = descriptor.CreateRuntime(descriptor.CreateDefaultSettings());

            Assert.NotNull(runtime);
            Assert.Equal(descriptor.Key, runtime.Key);
        }
    }

    [Fact]
    public void NoBuiltInDescriptorUsesMetadataOnlyRuntime()
    {
        var audioSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.Audio",
            "Effects",
            "Registry",
            "BuiltInHapticEffectRegistry.cs"));
        var audioDirectory = Path.Combine(FindRepositoryRoot(), "src", "HapticDrive.Asio.Audio");
        var offendingFiles = Directory
            .GetFiles(audioDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("MetadataOnlyRuntime", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains("runtimeFactory", audioSource, StringComparison.Ordinal);
        Assert.Empty(offendingFiles);
    }

    [Fact]
    public void DescriptorRequiredSignalsAreTyped()
    {
        foreach (var descriptor in BuiltInHapticEffectRegistry.Instance.All)
        {
            Assert.All(
                descriptor.RequiredSignals,
                signal =>
                {
                    Assert.NotEqual(HapticSignalKind.None, signal.Signal);
                    Assert.True(Enum.IsDefined(signal.Signal));
                });
        }
    }

    [Fact]
    public void ParameterDescriptorsIncludeKindAndDecimalPlaces()
    {
        foreach (var descriptor in BuiltInHapticEffectRegistry.Instance.All)
        {
            Assert.All(
                descriptor.Parameters,
                parameter =>
                {
                    Assert.True(Enum.IsDefined(parameter.Kind));
                    Assert.True(parameter.DecimalPlaces >= 0);
                    if (parameter.Kind is EffectParameterKind.Boolean or EffectParameterKind.Integer)
                    {
                        Assert.Equal(0, parameter.DecimalPlaces);
                    }
                });
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
