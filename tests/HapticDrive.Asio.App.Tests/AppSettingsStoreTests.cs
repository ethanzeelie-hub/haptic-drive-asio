using System.IO;
using HapticDrive.Actuation.PHpr;
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
    public void RealPhprRoadVibrationSettingsPersistWithoutUnsafeDirectControlState()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings
        {
            RealPhprRoadVibrationRouting = new RealPhprRoadVibrationRoutingSetting
            {
                IsEnabled = true,
                Brake = new RealPhprRoadVibrationPedalSetting
                {
                    IsEnabled = true,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 30d,
                    FrequencyHz = 60d,
                    DurationMs = 70
                },
                Throttle = new RealPhprRoadVibrationPedalSetting
                {
                    IsEnabled = false,
                    MinimumStrength01 = 0.01d,
                    Strength01 = 0.03d,
                    MinimumFrequencyHz = 25d,
                    FrequencyHz = 45d,
                    DurationMs = 40
                }
            }
        });

        var loaded = store.Load();
        var json = File.ReadAllText(path);

        Assert.True(loaded.RealPhprRoadVibrationRouting.IsEnabled);
        Assert.True(loaded.RealPhprRoadVibrationRouting.Brake.IsEnabled);
        Assert.Equal(0.02d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumStrength01);
        Assert.Equal(0.06d, loaded.RealPhprRoadVibrationRouting.Brake.Strength01);
        Assert.Equal(30d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumFrequencyHz);
        Assert.Equal(60d, loaded.RealPhprRoadVibrationRouting.Brake.FrequencyHz);
        Assert.Equal(70, loaded.RealPhprRoadVibrationRouting.Brake.DurationMs);
        Assert.False(loaded.RealPhprRoadVibrationRouting.Throttle.IsEnabled);
        Assert.DoesNotContain("DirectControlEnabled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RealPhprSlipLockSettingsPersistWithoutUnsafeDirectControlState()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings
        {
            RealPhprSlipLockRouting = new RealPhprSlipLockRoutingSetting
            {
                IsEnabled = true,
                WheelSlip = new RealPhprSlipLockEffectSetting
                {
                    IsEnabled = true,
                    TargetModule = PHprGearPulseTarget.Brake,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 35d,
                    FrequencyHz = 65d,
                    DurationMs = 70
                },
                WheelLock = new RealPhprSlipLockEffectSetting
                {
                    IsEnabled = false,
                    TargetModule = PHprGearPulseTarget.Throttle,
                    MinimumStrength01 = 0.03d,
                    Strength01 = 0.08d,
                    MinimumFrequencyHz = 55d,
                    FrequencyHz = 85d,
                    DurationMs = 60
                }
            }
        });

        var loaded = store.Load();
        var json = File.ReadAllText(path);

        Assert.True(loaded.RealPhprSlipLockRouting.IsEnabled);
        Assert.True(loaded.RealPhprSlipLockRouting.WheelSlip.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, loaded.RealPhprSlipLockRouting.WheelSlip.TargetModule);
        Assert.Equal(0.02d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumStrength01);
        Assert.Equal(0.06d, loaded.RealPhprSlipLockRouting.WheelSlip.Strength01);
        Assert.Equal(35d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumFrequencyHz);
        Assert.Equal(65d, loaded.RealPhprSlipLockRouting.WheelSlip.FrequencyHz);
        Assert.Equal(70, loaded.RealPhprSlipLockRouting.WheelSlip.DurationMs);
        Assert.False(loaded.RealPhprSlipLockRouting.WheelLock.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Throttle, loaded.RealPhprSlipLockRouting.WheelLock.TargetModule);
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

    [Fact]
    public void RealPhprRoadVibrationSettingsAreClampedToDirectControlSafetyLimits()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        store.Save(new AppSettings
        {
            RealPhprRoadVibrationRouting = new RealPhprRoadVibrationRoutingSetting
            {
                IsEnabled = true,
                Brake = new RealPhprRoadVibrationPedalSetting
                {
                    MinimumStrength01 = 5d,
                    Strength01 = 10d,
                    MinimumFrequencyHz = 10_000d,
                    FrequencyHz = 12_000d,
                    DurationMs = 10_000
                },
                Throttle = new RealPhprRoadVibrationPedalSetting
                {
                    MinimumStrength01 = double.NaN,
                    Strength01 = double.NaN,
                    MinimumFrequencyHz = double.NaN,
                    FrequencyHz = double.NaN,
                    DurationMs = -5
                }
            }
        });

        var loaded = store.Load();

        Assert.True(loaded.RealPhprRoadVibrationRouting.IsEnabled);
        Assert.Equal(0.10d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumStrength01);
        Assert.Equal(0.10d, loaded.RealPhprRoadVibrationRouting.Brake.Strength01);
        Assert.Equal(250d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumFrequencyHz);
        Assert.Equal(250d, loaded.RealPhprRoadVibrationRouting.Brake.FrequencyHz);
        Assert.Equal(100, loaded.RealPhprRoadVibrationRouting.Brake.DurationMs);
        Assert.Equal(0.01d, loaded.RealPhprRoadVibrationRouting.Throttle.MinimumStrength01);
        Assert.Equal(0.04d, loaded.RealPhprRoadVibrationRouting.Throttle.Strength01);
        Assert.Equal(25d, loaded.RealPhprRoadVibrationRouting.Throttle.MinimumFrequencyHz);
        Assert.Equal(45d, loaded.RealPhprRoadVibrationRouting.Throttle.FrequencyHz);
        Assert.Equal(0, loaded.RealPhprRoadVibrationRouting.Throttle.DurationMs);
    }

    [Fact]
    public void RealPhprSlipLockSettingsAreClampedToDirectControlSafetyLimits()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        store.Save(new AppSettings
        {
            RealPhprSlipLockRouting = new RealPhprSlipLockRoutingSetting
            {
                IsEnabled = true,
                WheelSlip = new RealPhprSlipLockEffectSetting
                {
                    TargetModule = (PHprGearPulseTarget)999,
                    MinimumStrength01 = 5d,
                    Strength01 = 10d,
                    MinimumFrequencyHz = 10_000d,
                    FrequencyHz = 12_000d,
                    DurationMs = 10_000
                },
                WheelLock = new RealPhprSlipLockEffectSetting
                {
                    MinimumStrength01 = double.NaN,
                    Strength01 = double.NaN,
                    MinimumFrequencyHz = double.NaN,
                    FrequencyHz = double.NaN,
                    DurationMs = -5
                }
            }
        });

        var loaded = store.Load();

        Assert.True(loaded.RealPhprSlipLockRouting.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Throttle, loaded.RealPhprSlipLockRouting.WheelSlip.TargetModule);
        Assert.Equal(0.10d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumStrength01);
        Assert.Equal(0.10d, loaded.RealPhprSlipLockRouting.WheelSlip.Strength01);
        Assert.Equal(250d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumFrequencyHz);
        Assert.Equal(250d, loaded.RealPhprSlipLockRouting.WheelSlip.FrequencyHz);
        Assert.Equal(100, loaded.RealPhprSlipLockRouting.WheelSlip.DurationMs);
        Assert.Equal(PHprGearPulseTarget.Brake, loaded.RealPhprSlipLockRouting.WheelLock.TargetModule);
        Assert.Equal(0.04d, loaded.RealPhprSlipLockRouting.WheelLock.MinimumStrength01);
        Assert.Equal(0.10d, loaded.RealPhprSlipLockRouting.WheelLock.Strength01);
        Assert.Equal(60d, loaded.RealPhprSlipLockRouting.WheelLock.MinimumFrequencyHz);
        Assert.Equal(90d, loaded.RealPhprSlipLockRouting.WheelLock.FrequencyHz);
        Assert.Equal(0, loaded.RealPhprSlipLockRouting.WheelLock.DurationMs);
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
