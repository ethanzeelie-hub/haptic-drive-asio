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
    bool IsAvailable,
    bool IsHardwareArmed = false,
    int? SelectedOutputChannel = null,
    int? DeviceOutputChannelCount = null,
    long SubmittedBufferCount = 0,
    long DroppedBufferCount = 0,
    string? LastError = null);
