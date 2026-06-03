namespace HapticDrive.Asio.Core.Audio;

public sealed record AudioOutputConfiguration(
    int SampleRate,
    int ChannelCount,
    int BufferSize,
    string? RequestedDeviceName = null,
    int? SelectedOutputChannel = null,
    bool IsHardwareArmed = false)
{
    public static AudioOutputConfiguration Default { get; } = new(
        SampleRate: 48_000,
        ChannelCount: 1,
        BufferSize: 128);
}
