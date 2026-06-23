using System.Text.Json;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class ProfileSchemaV2Tests
{
    [Fact]
    public async Task ProfileV2LoadsAndCreatesRuntimeGraph()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "SchemaVersion": 2,
              "Name": "Runtime Profile",
              "Effects": {
                "engine-rpm": {
                  "EffectKey": "engine-rpm",
                  "Enabled": true,
                  "Parameters": {
                    "gain": 0.42,
                    "minimum-frequency-hz": 30,
                    "maximum-frequency-hz": 61,
                    "high-frequency-enabled": 1,
                    "high-frequency-hz": 50,
                    "high-frequency-gain": 0.25,
                    "frequency-jitter-hz": 0,
                    "idle-throttle-gain": 0.35,
                    "pit-gain-multiplier": 0.35
                  }
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
        var engine = new HapticEffectEngine(new(1_000, 1, 200));

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal(HapticDriveProfile.CurrentVersion, result.Profile.Version);
        Assert.Equal(HapticDriveProfile.CurrentVersion, result.Profile.SchemaVersion);
        Assert.Contains("road-texture", result.Profile.EffectSettings.Keys);
        Assert.Equal(0.42d, result.Profile.EffectSettings["engine-rpm"].Parameters["gain"], precision: 6);
        engine.UpdateEffectSettings(result.Profile.ToEffectSettings());

        Assert.True(engine.Options.Engine.IsEnabled);
        Assert.True(engine.GetSnapshot().Engine.IsEnabled);
        Assert.Equal(0.42f, engine.Options.Engine.Gain, precision: 6);
    }

    [Fact]
    public async Task UnknownEffectKeysRoundTripButDoNotRender()
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
        var engine = new HapticEffectEngine(new(1_000, 1, 200));

        Assert.True(load.Succeeded, load.Message);
        Assert.NotNull(load.Profile);
        Assert.DoesNotContain("custom-future-effect", load.Profile.EffectSettings.Keys);
        Assert.Contains("custom-future-effect", load.Profile.UnknownEffectSettings.Keys);
        engine.UpdateEffectSettings(load.Profile.ToEffectSettings());
        Assert.True(save.Succeeded, save.Message);
        Assert.Contains("custom-future-effect", savedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("custom-future-effect", engine.EffectSettings.Keys);
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
    public async Task InvalidParametersAreRepairedBeforeRuntimeCreation()
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
        var engine = new HapticEffectEngine(new(1_000, 1, 200));

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.True(result.WasRepaired);
        Assert.True(result.Profile.EffectSettings["engine-rpm"].Enabled);
        Assert.Equal(1d, result.Profile.EffectSettings["engine-rpm"].Parameters["gain"], precision: 6);

        engine.UpdateEffectSettings(result.Profile.ToEffectSettings());

        Assert.True(engine.Options.Engine.IsEnabled);
        Assert.Equal(1f, engine.Options.Engine.Gain, precision: 6);
        Assert.Contains(
            result.ValidationMessages,
            message => message.Contains("Engine RPM", StringComparison.OrdinalIgnoreCase)
                       && message.Contains("parameter 'gain' repaired", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTempProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Audio.Tests", "Profiles");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdprofile.json");
    }
}
