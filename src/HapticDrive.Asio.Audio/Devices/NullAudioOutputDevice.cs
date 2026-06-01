using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class NullAudioOutputDevice : AudioOutputDeviceBase
{
    public NullAudioOutputDevice()
        : base(AudioOutputDeviceKind.Null, "Null Output")
    {
    }

    public override bool RequiresPhysicalHardware => false;

    public override ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Configuration = configuration;
        DeviceName = "NullAudioOutputDevice";
        State = AudioOutputDeviceState.Open;
        StatusMessage = "Null output ready. Audio samples are discarded deterministically.";
        return SuccessAsync(StatusMessage);
    }
}
