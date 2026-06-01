namespace HapticDrive.Asio.Core.Audio;

public sealed record AudioOutputConfiguration(
    int SampleRate,
    int ChannelCount,
    int BufferSize,
    string? RequestedDeviceName = null)
{
    public static AudioOutputConfiguration Default { get; } = new(
        SampleRate: 48_000,
        ChannelCount: 1,
        BufferSize: 128);
}
