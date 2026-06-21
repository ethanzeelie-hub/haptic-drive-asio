using System.Text.Json;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class ProfileSchemaV2Tests
{
    [Fact]
    public async Task V1ProfileMigratesToV2EffectDictionary()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Version": 1,
              "Name": "Legacy V1",
              "Effects": {
                "Engine": {
                  "IsEnabled": true,
                  "Gain": 0.42,
                  "MinimumFrequencyHz": 30,
                  "MaximumFrequencyHz": 61
                }
              },
              "Mixer": {
                "MasterGain": 0.75,
                "IsMuted": false
              },
              "Safety": {
                "OutputGain": 0.8,
                "OutputGainCeiling": 1.0,
                "LimiterEnabled": true
              }
            }
            """);

        var store = new HapticProfileStore();
        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal(HapticDriveProfile.CurrentVersion, result.Profile.Version);
        Assert.Equal(HapticDriveProfile.CurrentVersion, result.Profile.SchemaVersion);
        Assert.Equal(0.42d, result.Profile.EffectSettings["engine-rpm"].Parameters["gain"], precision: 6);
        Assert.Contains("diagnostic-test", result.Profile.EffectSettings.Keys);
        Assert.Contains(
            result.ValidationMessages,
            message => message.Contains("migrated to version 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnknownEffectKeyIsPreservedButNotRendered()
    {
        var path = CreateTempProfilePath();
        var document = """
            {
              "SchemaVersion": 2,
              "Name": "Future Profile",
              "Effects": {
                "engine-rpm": {
                  "EffectKey": "engine-rpm",
                  "Enabled": true,
                  "Parameters": {
                    "gain": 0.22,
                    "minimum-frequency-hz": 34,
                    "maximum-frequency-hz": 55,
                    "high-frequency-enabled": 1,
                    "high-frequency-hz": 50,
                    "high-frequency-gain": 0.25,
                    "frequency-jitter-hz": 0,
                    "idle-throttle-gain": 0.35,
                    "pit-gain-multiplier": 0.35
                  }
                },
                "custom-future-effect": {
                  "EffectKey": "custom-future-effect",
                  "Enabled": true,
                  "Parameters": {
                    "mystery": 123
                  }
                }
              },
              "Mixer": {
                "MasterGain": 0.9,
                "IsMuted": false
              },
              "Safety": {
                "OutputGain": 1.0,
                "OutputGainCeiling": 1.0,
                "LimiterEnabled": true
              }
            }
            """;
        await File.WriteAllTextAsync(path, document);

        var store = new HapticProfileStore();
        var load = await store.LoadAsync(path);
        var roundTripPath = CreateTempProfilePath();
        var save = await store.SaveAsync(load.Profile!, roundTripPath);
        var savedJson = await File.ReadAllTextAsync(roundTripPath);

        Assert.True(load.Succeeded, load.Message);
        Assert.NotNull(load.Profile);
        Assert.DoesNotContain("custom-future-effect", load.Profile.EffectSettings.Keys);
        Assert.Contains("custom-future-effect", load.Profile.UnknownEffectSettings.Keys);
        Assert.True(save.Succeeded, save.Message);
        Assert.Contains("custom-future-effect", savedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavesSchemaVersion2()
    {
        var path = CreateTempProfilePath();
        var store = new HapticProfileStore();

        var result = await store.SaveAsync(HapticDriveProfile.Default, path);
        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("Effects", out _));
        Assert.False(document.RootElement.TryGetProperty("Version", out _));
    }

    [Fact]
    public async Task InvalidEffectSettingsAreReplacedByDescriptorDefaults()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "SchemaVersion": 2,
              "Name": "Broken Effect Settings",
              "Effects": {
                "engine-rpm": {
                  "EffectKey": "engine-rpm",
                  "Enabled": true,
                  "Parameters": {
                    "gain": 2.5
                  }
                }
              },
              "Mixer": {
                "MasterGain": 0.8,
                "IsMuted": false
              },
              "Safety": {
                "OutputGain": 1.0,
                "OutputGainCeiling": 1.0,
                "LimiterEnabled": true
              }
            }
            """);

        var store = new HapticProfileStore();
        var result = await store.LoadAsync(path);
        var defaults = BuiltInHapticEffectRegistry.Instance.GetRequired("engine-rpm").CreateDefaultSettings();

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal(defaults.Enabled, result.Profile.EffectSettings["engine-rpm"].Enabled);
        Assert.Equal(defaults.Parameters["gain"], result.Profile.EffectSettings["engine-rpm"].Parameters["gain"], precision: 6);
        Assert.Contains(
            result.ValidationMessages,
            message => message.Contains("engine-rpm", StringComparison.OrdinalIgnoreCase)
                       && message.Contains("defaults were used", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTempProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Audio.Tests", "Profiles");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdprofile.json");
    }
}
