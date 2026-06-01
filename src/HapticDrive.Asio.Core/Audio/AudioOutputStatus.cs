namespace HapticDrive.Asio.Core.Audio;

public sealed record AudioOutputStatus(
    AudioOutputDeviceKind Kind,
    AudioOutputDeviceState State,
    string DisplayName,
    string StatusMessage,
    string? DeviceName,
    int SampleRate,
    int ChannelCount,
    int BufferSize,
    bool RequiresPhysicalHardware,
    bool IsManualDebugOnly,
    bool IsAvailable);
