namespace HapticDrive.Asio.Audio.Safety;

public sealed record AudioSafetyProcessorSnapshot(
    bool EmergencyMute,
    bool LimiterEnabled,
    float OutputGain,
    float OutputGainCeiling,
    float InputPeakLevel,
    float OutputPeakLevel,
    int SanitizedSampleCount,
    int LimitedSampleCount,
    int ClippedSampleCount);
