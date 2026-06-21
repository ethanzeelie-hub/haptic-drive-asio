using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Safety;

namespace HapticDrive.Asio.Audio.Pipeline;

public readonly record struct AudioRenderPipelineSnapshot(
    bool IsRunning,
    bool IsMuted,
    bool EmergencyMute,
    int ActiveSourceCount,
    float MixerPeakLevel,
    float OutputPeakLevel,
    int SanitizedSampleCount,
    int LimitedSampleCount,
    int ClippedSampleCount,
    AudioMixerSnapshot Mixer,
    AudioSafetyProcessorSnapshot Safety);
