using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class WasapiDebugOutputDevice : AudioOutputDeviceBase
{
    public WasapiDebugOutputDevice()
        : base(AudioOutputDeviceKind.WasapiDebug, "WASAPI Debug Output")
    {
    }

    public override bool RequiresPhysicalHardware => false;

    public override bool IsManualDebugOnly => true;

    public override ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Configuration = configuration;
        DeviceName = "Default Windows audio endpoint";
        State = AudioOutputDeviceState.Open;
        StatusMessage = "WASAPI manual debug output placeholder ready. It must be selected manually and is not the ASIO target.";
        return SuccessAsync(StatusMessage);
    }
}
