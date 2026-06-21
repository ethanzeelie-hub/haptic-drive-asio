namespace HapticDrive.Asio.Audio.Safety;

public readonly record struct AudioSafetyProcessorSnapshot(
    bool EmergencyMute,
    bool LimiterEnabled,
    float OutputGain,
    float OutputGainCeiling,
    float InputPeakLevel,
    float OutputPeakLevel,
    int SanitizedSampleCount,
    int LimitedSampleCount,
    int ClippedSampleCount);
