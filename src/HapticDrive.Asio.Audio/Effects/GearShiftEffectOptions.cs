namespace HapticDrive.Asio.Audio.Effects;

public enum GearShiftDetectionMode
{
    ForwardGearChangesOnly
}

public sealed record GearShiftEffectOptions(
    bool IsEnabled,
    float Gain,
    float PulseFrequencyHz,
    TimeSpan PulseDuration,
    TimeSpan EngagingDebounceDuration,
    bool ModulateGainByRpm,
    GearShiftDetectionMode DetectionMode,
    ushort DefaultIdleRpm,
    ushort DefaultMaxRpm)
{
    public static GearShiftEffectOptions Default { get; } = new(
        IsEnabled: true,
        Gain: 0.18f,
        PulseFrequencyHz: 15f,
        PulseDuration: TimeSpan.FromMilliseconds(80),
        EngagingDebounceDuration: TimeSpan.FromMilliseconds(100),
        ModulateGainByRpm: false,
        DetectionMode: GearShiftDetectionMode.ForwardGearChangesOnly,
        DefaultIdleRpm: 3_000,
        DefaultMaxRpm: 12_000);
}

public sealed record GearShiftEffectSnapshot(
    bool IsEnabled,
    bool IsActive,
    sbyte? LastObservedGear,
    sbyte? LastForwardGear,
    uint? LastShiftFrameIdentifier,
    float? LastShiftSessionTime,
    int RemainingPulseFrames,
    float PeakLevel);
