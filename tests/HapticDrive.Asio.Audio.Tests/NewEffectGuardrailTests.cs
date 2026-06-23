using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class NewEffectGuardrailTests
{
    [Fact]
    public void AddingNewEffectRequiresDescriptorRuntimeAndTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedRuntimeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine-rpm"] = "EngineEffectRuntime.cs",
            ["gear-shift"] = "GearShiftEffectRuntime.cs",
            ["kerb"] = "KerbEffectRuntime.cs",
            ["impact"] = "ImpactEffectRuntime.cs",
            ["road-texture"] = "RoadTextureEffectRuntime.cs",
            ["slip-lock"] = "SlipLockEffectRuntime.cs"
        };
        var registry = BuiltInHapticEffectRegistry.Instance;
        var testSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(Path.Combine(repositoryRoot, "tests", "HapticDrive.Asio.Audio.Tests"), "*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        var requiredTests = new[]
        {
            "EveryBuiltInDescriptorCreatesFunctionalRuntime",
            "NoBuiltInDescriptorUsesMetadataOnlyRuntime",
            "DescriptorRequiredSignalsAreTyped",
            "ParameterDescriptorsIncludeKindAndDecimalPlaces",
            "ProfileV2LoadsAndCreatesRuntimeGraph",
            "UnknownEffectKeysRoundTripButDoNotRender",
            "InvalidParametersAreRepairedBeforeRuntimeCreation",
            "DisabledEffectsDoNotRender",
            "AddingNewEffectRequiresDescriptorRuntimeAndTests"
        };

        Assert.Equal(
            expectedRuntimeFiles.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            registry.All.Select(descriptor => descriptor.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray());

        foreach (var runtimeFile in expectedRuntimeFiles.Values)
        {
            Assert.True(
                File.Exists(Path.Combine(repositoryRoot, "src", "HapticDrive.Asio.Audio", "Effects", runtimeFile)),
                $"Expected runtime file '{runtimeFile}' was not found.");
        }

        foreach (var requiredTest in requiredTests)
        {
            Assert.Contains(requiredTest, testSource, StringComparison.Ordinal);
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
