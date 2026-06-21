using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class ProfileMigrationTests
{
    [Fact]
    public async Task MigratesV0ToV1ToV2InOrder()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Version": 0,
              "Name": "Legacy V0",
              "Effects": {
                "Engine": {
                  "IsEnabled": true,
                  "Gain": 0.31,
                  "MinimumFrequencyHz": 28,
                  "MaximumFrequencyHz": 58
                }
              },
              "Mixer": {
                "MasterGain": 0.8,
                "IsMuted": false
              },
              "Safety": {
                "OutputGain": 0.9,
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
        Assert.Collection(
            result.ValidationMessages.Take(2),
            message => Assert.Contains("migrated to version 1", message, StringComparison.OrdinalIgnoreCase),
            message => Assert.Contains("migrated to version 2", message, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0.31d, result.Profile.EffectSettings["engine-rpm"].Parameters["gain"], precision: 6);
    }

    private static string CreateTempProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Audio.Tests", "Profiles");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdprofile.json");
    }
}
