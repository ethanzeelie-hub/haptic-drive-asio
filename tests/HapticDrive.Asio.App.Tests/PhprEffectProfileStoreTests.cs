using System.IO;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprEffectProfileStoreTests
{
    [Fact]
    public async Task PhprProfileSaveLoad_RoundTripsSafeEffectPreferences()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "p-hpr.hdphprprofile.json");
        var store = new PhprEffectProfileStore();
        var profile = PhprEffectProfile.FromAppSettings(
            "Race profile",
            new AppSettings
            {
                MockGearPulseRouting = new MockGearPulseRoutingSetting
                {
                    IsEnabled = false,
                    TargetModule = PHprGearPulseTarget.Brake,
                    Strength01 = 0.07d,
                    FrequencyHz = 50d,
                    DurationMs = 64
                },
                RealPhprRoadVibrationRouting = new RealPhprRoadVibrationRoutingSetting
                {
                    IsEnabled = true,
                    Brake = new RealPhprRoadVibrationPedalSetting
                    {
                        IsEnabled = false,
                        MinimumStrength01 = 0.02d,
                        Strength01 = 0.06d,
                        MinimumFrequencyHz = 30d,
                        FrequencyHz = 50d,
                        DurationMs = 70
                    }
                },
                RealPhprSlipLockRouting = new RealPhprSlipLockRoutingSetting
                {
                    IsEnabled = true,
                    WheelSlip = new RealPhprSlipLockEffectSetting
                    {
                        TargetModule = PHprGearPulseTarget.Both,
                        Strength01 = 0.06d,
                        FrequencyHz = 50d
                    }
                }
            });

        var saveResult = await store.SaveAsync(profile, path);
        var loadResult = await store.LoadAsync(path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Profile);
        Assert.Equal("Race profile", loadResult.Profile.Name);
        Assert.False(loadResult.Profile.MockGearPulseRouting.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, loadResult.Profile.MockGearPulseRouting.TargetModule);
        Assert.True(loadResult.Profile.RealPhprRoadVibrationRouting.IsEnabled);
        Assert.False(loadResult.Profile.RealPhprRoadVibrationRouting.Brake.IsEnabled);
        Assert.Equal(0.06d, loadResult.Profile.RealPhprRoadVibrationRouting.Brake.Strength01);
        Assert.True(loadResult.Profile.RealPhprSlipLockRouting.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Both, loadResult.Profile.RealPhprSlipLockRouting.WheelSlip.TargetModule);
    }

    [Fact]
    public async Task PhprProfileSave_DoesNotPersistRuntimeDirectControlStateOrPrivateDevicePath()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "p-hpr.hdphprprofile.json");
        var store = new PhprEffectProfileStore();
        var profile = PhprEffectProfile.FromAppSettings(
            "Safe only",
            new AppSettings
            {
                RealPhprGearPulseRouting = new RealPhprGearPulseRoutingSetting
                {
                    Brake = new RealPhprGearPulseSetting
                    {
                        Strength01 = 0.08d,
                        FrequencyHz = 50d,
                        DurationMs = 60
                    }
                }
            });

        var saveResult = await store.SaveAsync(profile, path);
        var json = await File.ReadAllTextAsync(path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.DoesNotContain("DirectControlEnabled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PhprProfileSave_RefreshesLastKnownGoodBackup()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "p-hpr.hdphprprofile.json");
        var backupPath = DocumentBackupFile.GetBackupPath(path);
        var store = new PhprEffectProfileStore();

        var saveResult = await store.SaveAsync(PhprEffectProfile.Default, path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(await File.ReadAllTextAsync(path), await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task PhprProfileSave_ClampsUnsafeEffectPreferences()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "p-hpr.hdphprprofile.json");
        var store = new PhprEffectProfileStore();
        var profile = PhprEffectProfile.Default with
        {
            Name = "Unsafe input",
            RealPhprSlipLockRouting = new RealPhprSlipLockRoutingSetting
            {
                IsEnabled = true,
                WheelSlip = new RealPhprSlipLockEffectSetting
                {
                    TargetModule = (PHprGearPulseTarget)999,
                    Strength01 = 50d,
                    FrequencyHz = 10_000d,
                    DurationMs = 10_000
                }
            }
        };

        var saveResult = await store.SaveAsync(profile, path);
        var loadResult = await store.LoadAsync(path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Profile);
        Assert.True(saveResult.WasRepaired);
        Assert.Equal(PHprGearPulseTarget.Throttle, loadResult.Profile.RealPhprSlipLockRouting.WheelSlip.TargetModule);
        Assert.Equal(1.0d, loadResult.Profile.RealPhprSlipLockRouting.WheelSlip.Strength01);
        Assert.Equal(50d, loadResult.Profile.RealPhprSlipLockRouting.WheelSlip.FrequencyHz);
        Assert.Equal(1_000, loadResult.Profile.RealPhprSlipLockRouting.WheelSlip.DurationMs);
    }

    [Fact]
    public async Task PhprProfileLoad_FailsSafelyForMissingCorruptAndUnsupportedFiles()
    {
        using var directory = new TempDirectory();
        var missingPath = Path.Combine(directory.Path, "missing.json");
        var corruptPath = Path.Combine(directory.Path, "corrupt.json");
        var futurePath = Path.Combine(directory.Path, "future.json");
        await File.WriteAllTextAsync(corruptPath, "{ no");
        await File.WriteAllTextAsync(futurePath, """{"Version":99,"Name":"Future"}""");
        var store = new PhprEffectProfileStore();

        var missing = await store.LoadAsync(missingPath);
        var corrupt = await store.LoadAsync(corruptPath);
        var future = await store.LoadAsync(futurePath);

        Assert.Equal(PhprEffectProfileLoadStatus.FileNotFound, missing.Status);
        Assert.Equal(PhprEffectProfileLoadStatus.Corrupt, corrupt.Status);
        Assert.Equal(PhprEffectProfileLoadStatus.UnsupportedVersion, future.Status);
    }

    [Fact]
    public async Task PhprProfileLoad_CorruptPrimary_RecoversFromBackupSnapshot()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "p-hpr.hdphprprofile.json");
        var store = new PhprEffectProfileStore();
        var profile = PhprEffectProfile.Default with { Name = "Backup P-HPR" };
        Assert.True((await store.SaveAsync(profile, path)).Succeeded);
        await File.WriteAllTextAsync(path, "{ broken");

        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal("Backup P-HPR", result.Profile.Name);
        Assert.Equal("P-HPR profile recovered from backup snapshot.", result.Message);
    }

    [Fact]
    public async Task PhprProfileLoad_VersionlessLegacyProfileMigratesToCurrentVersion()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "legacy.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Name": "Legacy P-HPR",
              "ShiftIntent": {
                "IsEnabled": true,
                "Mode": 0
              }
            }
            """);
        var store = new PhprEffectProfileStore();

        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal(PhprEffectProfile.CurrentVersion, result.Profile.Version);
        Assert.True(result.WasRepaired);
        Assert.Contains(result.ValidationMessages, message => message.Contains("migrated to version 1", StringComparison.Ordinal));
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
