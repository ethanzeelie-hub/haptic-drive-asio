using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.TestBench;

public sealed record AudioTestBenchSnapshot(
    bool IsActive,
    AudioTestSignalKind SelectedSignal,
    string SelectedSignalName,
    int SampleRate,
    int ChannelCount,
    int BufferSize,
    bool IsMuted,
    bool EmergencyMute,
    long RenderedBufferCount,
    long RenderedFrameCount,
    float MixerPeakLevel,
    float OutputPeakLevel,
    int SanitizedSampleCount,
    int LimitedSampleCount,
    int ClippedSampleCount,
    AudioOutputDeviceKind OutputKind,
    string OutputDisplayName,
    AudioOutputDeviceState OutputState,
    bool OutputRequiresPhysicalHardware,
    bool OutputIsManualDebugOnly,
    string StatusMessage);
