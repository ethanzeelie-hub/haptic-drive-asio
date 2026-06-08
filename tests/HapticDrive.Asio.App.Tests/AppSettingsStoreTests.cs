using System.IO;
using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void RealPhprGearPulseSettingsPersistWithoutUnsafeDirectControlState()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings
        {
            RealPhprGearPulseRouting = new RealPhprGearPulseRoutingSetting
            {
                Brake = new RealPhprGearPulseSetting
                {
                    IsEnabled = true,
                    Strength01 = 0.07d,
                    FrequencyHz = 55d,
                    DurationMs = 60
                },
                Throttle = new RealPhprGearPulseSetting
                {
                    IsEnabled = false,
                    Strength01 = 0.04d,
                    FrequencyHz = 45d,
                    DurationMs = 40
                }
            }
        });

        var loaded = store.Load();
        var json = File.ReadAllText(path);

        Assert.True(loaded.RealPhprGearPulseRouting.Brake.IsEnabled);
        Assert.Equal(0.07d, loaded.RealPhprGearPulseRouting.Brake.Strength01);
        Assert.Equal(55d, loaded.RealPhprGearPulseRouting.Brake.FrequencyHz);
        Assert.Equal(60, loaded.RealPhprGearPulseRouting.Brake.DurationMs);
        Assert.False(loaded.RealPhprGearPulseRouting.Throttle.IsEnabled);
        Assert.Equal(0.04d, loaded.RealPhprGearPulseRouting.Throttle.Strength01);
        Assert.Equal(45d, loaded.RealPhprGearPulseRouting.Throttle.FrequencyHz);
        Assert.Equal(40, loaded.RealPhprGearPulseRouting.Throttle.DurationMs);
        Assert.DoesNotContain("DirectControlEnabled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RealPhprGearPulseSettingsAreClampedToDirectControlSafetyLimits()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        store.Save(new AppSettings
        {
            RealPhprGearPulseRouting = new RealPhprGearPulseRoutingSetting
            {
                Brake = new RealPhprGearPulseSetting
                {
                    Strength01 = 5d,
                    FrequencyHz = 10_000d,
                    DurationMs = 10_000
                },
                Throttle = new RealPhprGearPulseSetting
                {
                    Strength01 = double.NaN,
                    FrequencyHz = double.NaN,
                    DurationMs = -5
                }
            }
        });

        var loaded = store.Load();

        Assert.Equal(0.10d, loaded.RealPhprGearPulseRouting.Brake.Strength01);
        Assert.Equal(250d, loaded.RealPhprGearPulseRouting.Brake.FrequencyHz);
        Assert.Equal(100, loaded.RealPhprGearPulseRouting.Brake.DurationMs);
        Assert.Equal(0.05d, loaded.RealPhprGearPulseRouting.Throttle.Strength01);
        Assert.Equal(50d, loaded.RealPhprGearPulseRouting.Throttle.FrequencyHz);
        Assert.Equal(0, loaded.RealPhprGearPulseRouting.Throttle.DurationMs);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HapticDrive.Asio.App.Tests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
