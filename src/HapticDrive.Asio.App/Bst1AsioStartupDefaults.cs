using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.App;

internal static class Bst1AsioStartupDefaults
{
    public const int ValidatedBst1Channel = 1;

    public static Bst1AsioStartupDefaultSelection Resolve(IReadOnlyList<string> driverNames)
    {
        ArgumentNullException.ThrowIfNull(driverNames);

        var preferredDriver = driverNames.FirstOrDefault(driver =>
            string.Equals(driver, AsioAudioOutputDevice.PreferredDriverName, StringComparison.OrdinalIgnoreCase));

        if (preferredDriver is null)
        {
            return new Bst1AsioStartupDefaultSelection(
                AudioOutputDeviceKind.Null,
                DriverName: null,
                OutputChannel: null,
                Armed: false,
                "M-Audio ASIO driver not available; Null output selected.");
        }

        return new Bst1AsioStartupDefaultSelection(
            AudioOutputDeviceKind.Asio,
            preferredDriver,
            ValidatedBst1Channel,
            Armed: true,
            "M-Audio ASIO driver available; ASIO Output, channel 1, and Arm ASIO selected without starting output.");
    }
}

internal sealed record Bst1AsioStartupDefaultSelection(
    AudioOutputDeviceKind OutputKind,
    string? DriverName,
    int? OutputChannel,
    bool Armed,
    string Message);
