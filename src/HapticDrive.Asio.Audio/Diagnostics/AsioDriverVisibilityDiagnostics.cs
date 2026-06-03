using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;

namespace HapticDrive.Asio.Audio.Diagnostics;

public sealed class AsioDriverVisibilityDiagnostics
{
    private static readonly string[] MTrackKeywords =
    [
        "m-audio",
        "maudio",
        "m track",
        "m-track"
    ];

    private readonly IAsioDriverCatalog _driverCatalog;

    public AsioDriverVisibilityDiagnostics(IAsioDriverCatalog driverCatalog)
    {
        _driverCatalog = driverCatalog ?? throw new ArgumentNullException(nameof(driverCatalog));
    }

    public async ValueTask<AsioDriverVisibilitySnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var drivers = await _driverCatalog.GetDriverNamesAsync(cancellationToken).ConfigureAwait(false);
            var normalizedDrivers = drivers
                .Where(driver => !string.IsNullOrWhiteSpace(driver))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var preferredDriver = normalizedDrivers.FirstOrDefault(driver =>
                string.Equals(driver, AsioAudioOutputDevice.PreferredDriverName, StringComparison.OrdinalIgnoreCase));
            var mAudioDriver = preferredDriver ?? normalizedDrivers.FirstOrDefault(ContainsMTrackKeyword);
            var message = mAudioDriver is null
                ? "No M-Audio / M-Track ASIO driver was reported by the app ASIO catalog."
                : $"ASIO catalog reports '{mAudioDriver}'. This is visibility only; ASIO output is not active unless explicitly selected and started.";

            return new AsioDriverVisibilitySnapshot(
                true,
                normalizedDrivers,
                mAudioDriver,
                mAudioDriver is not null,
                message,
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AsioDriverVisibilitySnapshot(
                false,
                Array.Empty<string>(),
                null,
                false,
                $"ASIO driver visibility check failed safely: {ex.Message}",
                ex.Message);
        }
    }

    private static bool ContainsMTrackKeyword(string driver)
    {
        foreach (var keyword in MTrackKeywords)
        {
            if (driver.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record AsioDriverVisibilitySnapshot(
    bool Succeeded,
    IReadOnlyList<string> DriverNames,
    string? MatchedMTrackDriverName,
    bool IsMTrackDriverVisible,
    string Message,
    string? ErrorMessage)
{
    public static AsioDriverVisibilitySnapshot NotChecked { get; } = new(
        false,
        Array.Empty<string>(),
        null,
        false,
        "ASIO driver visibility has not been checked yet.",
        null);
}

