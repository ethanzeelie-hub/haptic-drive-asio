using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class AsioAudioOutputDevice : AudioOutputDeviceBase
{
    public const string PreferredDriverName = "M-Audio M-Track Solo and Duo ASIO";

    private readonly IAsioDriverCatalog _driverCatalog;

    public AsioAudioOutputDevice()
        : this(new UnavailableAsioDriverCatalog())
    {
    }

    public AsioAudioOutputDevice(IAsioDriverCatalog driverCatalog)
        : base(AudioOutputDeviceKind.Asio, "ASIO Output")
    {
        _driverCatalog = driverCatalog;
    }

    public override bool RequiresPhysicalHardware => true;

    public override async ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Configuration = configuration;

        var drivers = await _driverCatalog.GetDriverNamesAsync(cancellationToken).ConfigureAwait(false);
        var requestedDriver = string.IsNullOrWhiteSpace(configuration.RequestedDeviceName)
            ? PreferredDriverName
            : configuration.RequestedDeviceName;

        var selectedDriver = FindDriver(drivers, requestedDriver)
            ?? FindDriver(drivers, PreferredDriverName);

        if (selectedDriver is null)
        {
            State = AudioOutputDeviceState.Faulted;
            DeviceName = requestedDriver;
            return await FailureAsync(
                $"ASIO driver unavailable. Requested '{requestedDriver}', but no matching ASIO driver was found.").ConfigureAwait(false);
        }

        DeviceName = selectedDriver;
        State = AudioOutputDeviceState.Open;
        StatusMessage = "ASIO driver selected. Real ASIO streaming is not implemented in Stage 02.";
        return await SuccessAsync(StatusMessage).ConfigureAwait(false);
    }

    private static string? FindDriver(IReadOnlyList<string> drivers, string? requestedDriver)
    {
        if (string.IsNullOrWhiteSpace(requestedDriver))
        {
            return null;
        }

        return drivers.FirstOrDefault(driver =>
            string.Equals(driver, requestedDriver, StringComparison.OrdinalIgnoreCase));
    }
}
