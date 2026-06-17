using System.IO;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void RealPhprGearPulseDefaultsUseTenPercentFiftyHzFiftyMs()
    {
        var brake = AppSettings.Default.RealPhprGearPulseRouting.Brake;
        var throttle = AppSettings.Default.RealPhprGearPulseRouting.Throttle;

        Assert.Equal(0.10d, brake.Strength01);
        Assert.Equal(50d, brake.FrequencyHz);
        Assert.Equal(50, brake.DurationMs);
        Assert.Equal(0.10d, throttle.Strength01);
        Assert.Equal(50d, throttle.FrequencyHz);
        Assert.Equal(50, throttle.DurationMs);
    }

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
                    FrequencyHz = 50d,
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
        Assert.Equal(50d, loaded.RealPhprGearPulseRouting.Brake.FrequencyHz);
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
                    FrequencyHz = 50d,
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
        Assert.Equal(50d, loaded.RealPhprRoadVibrationRouting.Brake.FrequencyHz);
        Assert.Equal(PHprRoadVibrationPedalSettings.MinimumRoadDurationMs, loaded.RealPhprRoadVibrationRouting.Brake.DurationMs);
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
                    FrequencyHz = 50d,
                    DurationMs = 70
                },
                WheelLock = new RealPhprSlipLockEffectSetting
                {
                    IsEnabled = false,
                    TargetModule = PHprGearPulseTarget.Throttle,
                    MinimumStrength01 = 0.03d,
                    Strength01 = 0.08d,
                    MinimumFrequencyHz = 50d,
                    FrequencyHz = 50d,
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
        Assert.Equal(50d, loaded.RealPhprSlipLockRouting.WheelSlip.FrequencyHz);
        Assert.Equal(PHprSlipLockEffectSettings.MinimumContinuousDurationMs, loaded.RealPhprSlipLockRouting.WheelSlip.DurationMs);
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

        Assert.Equal(1.0d, loaded.RealPhprGearPulseRouting.Brake.Strength01);
        Assert.Equal(50d, loaded.RealPhprGearPulseRouting.Brake.FrequencyHz);
        Assert.Equal(1_000, loaded.RealPhprGearPulseRouting.Brake.DurationMs);
        Assert.Equal(0.10d, loaded.RealPhprGearPulseRouting.Throttle.Strength01);
        Assert.Equal(50d, loaded.RealPhprGearPulseRouting.Throttle.FrequencyHz);
        Assert.Equal(10, loaded.RealPhprGearPulseRouting.Throttle.DurationMs);
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
        Assert.Equal(1.0d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumStrength01);
        Assert.Equal(1.0d, loaded.RealPhprRoadVibrationRouting.Brake.Strength01);
        Assert.Equal(50d, loaded.RealPhprRoadVibrationRouting.Brake.MinimumFrequencyHz);
        Assert.Equal(50d, loaded.RealPhprRoadVibrationRouting.Brake.FrequencyHz);
        Assert.Equal(1_000, loaded.RealPhprRoadVibrationRouting.Brake.DurationMs);
        Assert.Equal(0.01d, loaded.RealPhprRoadVibrationRouting.Throttle.MinimumStrength01);
        Assert.Equal(0.04d, loaded.RealPhprRoadVibrationRouting.Throttle.Strength01);
        Assert.Equal(25d, loaded.RealPhprRoadVibrationRouting.Throttle.MinimumFrequencyHz);
        Assert.Equal(45d, loaded.RealPhprRoadVibrationRouting.Throttle.FrequencyHz);
        Assert.Equal(PHprRoadVibrationPedalSettings.MinimumRoadDurationMs, loaded.RealPhprRoadVibrationRouting.Throttle.DurationMs);
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
        Assert.Equal(1.0d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumStrength01);
        Assert.Equal(1.0d, loaded.RealPhprSlipLockRouting.WheelSlip.Strength01);
        Assert.Equal(50d, loaded.RealPhprSlipLockRouting.WheelSlip.MinimumFrequencyHz);
        Assert.Equal(50d, loaded.RealPhprSlipLockRouting.WheelSlip.FrequencyHz);
        Assert.Equal(1_000, loaded.RealPhprSlipLockRouting.WheelSlip.DurationMs);
        Assert.Equal(PHprGearPulseTarget.Brake, loaded.RealPhprSlipLockRouting.WheelLock.TargetModule);
        Assert.Equal(0.04d, loaded.RealPhprSlipLockRouting.WheelLock.MinimumStrength01);
        Assert.Equal(0.10d, loaded.RealPhprSlipLockRouting.WheelLock.Strength01);
        Assert.Equal(50d, loaded.RealPhprSlipLockRouting.WheelLock.MinimumFrequencyHz);
        Assert.Equal(50d, loaded.RealPhprSlipLockRouting.WheelLock.FrequencyHz);
        Assert.Equal(PHprSlipLockEffectSettings.MinimumContinuousDurationMs, loaded.RealPhprSlipLockRouting.WheelLock.DurationMs);
    }

    [Fact]
    public void OutputReplayAndBst1LocalGearPreferencesPersist()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        store.Save(new AppSettings
        {
            PreferredOutputMode = AudioOutputDeviceKind.Asio,
            LastAsioDriverName = "M-Audio Test Driver",
            LastAsioOutputChannel = 1,
            ArmAsioPreference = true,
            ReplayTimingPreference = ReplayTimingPreference.FastDebug,
            PaddleInputMapping = new PaddleInputMappingSetting
            {
                SelectedDeviceId = "wheel-1",
                SelectedMethod = InputDiscoveryMethod.WindowsGameController,
                LeftPaddleButtonId = 14,
                RightPaddleButtonId = 13,
                DebounceMilliseconds = 6
            },
            Bst1PaddleGearPulse = new Bst1PaddleGearPulseSetting
            {
                IsEnabled = true,
                StrengthPercent = 55f,
                FrequencyHz = 62.5f,
                UseSharedDuration = false,
                CustomDurationMs = 70
            }
        });

        var loaded = store.Load();

        Assert.Equal(AudioOutputDeviceKind.Asio, loaded.PreferredOutputMode);
        Assert.Equal("M-Audio Test Driver", loaded.LastAsioDriverName);
        Assert.Equal(1, loaded.LastAsioOutputChannel);
        Assert.True(loaded.ArmAsioPreference);
        Assert.Equal(ReplayTimingPreference.FastDebug, loaded.ReplayTimingPreference);
        Assert.Equal("wheel-1", loaded.PaddleInputMapping.SelectedDeviceId);
        Assert.Equal(14, loaded.PaddleInputMapping.LeftPaddleButtonId);
        Assert.Equal(13, loaded.PaddleInputMapping.RightPaddleButtonId);
        Assert.Equal(6, loaded.PaddleInputMapping.DebounceMilliseconds);
        Assert.True(loaded.Bst1PaddleGearPulse.IsEnabled);
        Assert.Equal(55f, loaded.Bst1PaddleGearPulse.StrengthPercent, precision: 6);
        Assert.Equal(62.5f, loaded.Bst1PaddleGearPulse.FrequencyHz, precision: 6);
        Assert.False(loaded.Bst1PaddleGearPulse.UseSharedDuration);
        Assert.Equal(70, loaded.Bst1PaddleGearPulse.CustomDurationMs);
    }

    [Fact]
    public void Bst1LocalGearSettingsAreClampedToSafeRanges()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        store.Save(new AppSettings
        {
            Bst1PaddleGearPulse = new Bst1PaddleGearPulseSetting
            {
                IsEnabled = true,
                StrengthPercent = 500f,
                FrequencyHz = 5_000f,
                UseSharedDuration = false,
                CustomDurationMs = -10
            }
        });

        var loaded = store.Load();

        Assert.True(loaded.Bst1PaddleGearPulse.IsEnabled);
        Assert.Equal(100f, loaded.Bst1PaddleGearPulse.StrengthPercent, precision: 6);
        Assert.Equal(ManualAsioHardwareTestRequest.MaximumFrequencyHz, loaded.Bst1PaddleGearPulse.FrequencyHz, precision: 6);
        Assert.Equal(ManualAsioHardwareTestRequest.MinimumDurationMilliseconds, loaded.Bst1PaddleGearPulse.CustomDurationMs);
    }

    [Fact]
    public void MissingNewFieldsLoadToSafeDefaultsWithoutCrashing()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        File.WriteAllText(path, """{"UseLightTheme":true,"LastAsioDriverName":"Existing Driver"}""");
        var store = new AppSettingsStore(path);

        var loaded = store.Load();

        Assert.True(loaded.UseLightTheme);
        Assert.Equal("Existing Driver", loaded.LastAsioDriverName);
        Assert.Null(loaded.PreferredOutputMode);
        Assert.False(loaded.ArmAsioPreference);
        Assert.Equal(ReplayTimingPreference.RealTime, loaded.ReplayTimingPreference);
        Assert.True(loaded.Bst1PaddleGearPulse.IsEnabled);
        Assert.Equal(50f, loaded.Bst1PaddleGearPulse.StrengthPercent, precision: 6);
        Assert.Equal(50f, loaded.Bst1PaddleGearPulse.FrequencyHz, precision: 6);
        Assert.True(loaded.Bst1PaddleGearPulse.UseSharedDuration);
        Assert.Equal(Bst1GearPulseDurationSync.DefaultGearDurationMs, loaded.Bst1PaddleGearPulse.CustomDurationMs);
    }

    [Fact]
    public void AppSettingsJson_DoesNotPersistRuntimeOnlyStates()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings
        {
            PreferredOutputMode = AudioOutputDeviceKind.Asio,
            ArmAsioPreference = true,
            Bst1PaddleGearPulse = new Bst1PaddleGearPulseSetting { IsEnabled = true }
        });

        var json = File.ReadAllText(path);

        Assert.Contains("ArmAsioPreference", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioArmed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("HapticsStarted", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyMute", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivePulse", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingStop", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PaddleGearBench", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FlightRecorder", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettingsJson_DoesNotPersistPrivatePathsValidationOrCaptureArtifacts()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings
        {
            PreferredOutputMode = AudioOutputDeviceKind.Asio,
            LastAsioDriverName = "M-Audio",
            PaddleInputMapping = new PaddleInputMappingSetting
            {
                SelectedDeviceId = "wheel-1",
                SelectedMethod = InputDiscoveryMethod.WindowsGameController,
                LeftPaddleButtonId = 14,
                RightPaddleButtonId = 13,
                DebounceMilliseconds = 6
            }
        });

        var json = File.ReadAllText(path);

        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\?\hid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local-validation-results", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capture-metadata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pcap", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ValidationResult", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvancedDiagnosticsPreferenceDefaultsOffAndPersists()
    {
        using var directory = new TempDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Path, "appsettings.json"));

        Assert.False(store.Load().AdvancedDiagnosticsEnabled);

        store.Save(new AppSettings
        {
            AdvancedDiagnosticsEnabled = true
        });

        Assert.True(store.Load().AdvancedDiagnosticsEnabled);
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
