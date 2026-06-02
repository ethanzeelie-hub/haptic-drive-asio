namespace HapticDrive.Asio.Audio.Effects;

public sealed record ImpactEffectOptions(
    bool IsEnabled,
    float Gain,
    float PulseFrequencyHz,
    TimeSpan PulseDuration,
    TimeSpan CooldownDuration,
    uint MinimumFrameGap,
    float VerticalGDeltaThreshold,
    float WheelVerticalForceDeltaThreshold,
    float SuspensionAccelerationDeltaThreshold,
    float CollisionEventIntensity,
    float MaximumAmplitude,
    uint MaximumTelemetryFrameLag)
{
    public static ImpactEffectOptions Default { get; } = new(
        IsEnabled: true,
        Gain: 0.2f,
        PulseFrequencyHz: 44f,
        PulseDuration: TimeSpan.FromMilliseconds(90),
        CooldownDuration: TimeSpan.FromMilliseconds(120),
        MinimumFrameGap: 3,
        VerticalGDeltaThreshold: 0.75f,
        WheelVerticalForceDeltaThreshold: 4_000f,
        SuspensionAccelerationDeltaThreshold: 35f,
        CollisionEventIntensity: 1f,
        MaximumAmplitude: 0.3f,
        MaximumTelemetryFrameLag: 120);
}

public sealed record ImpactEffectSnapshot(
    bool IsEnabled,
    bool IsActive,
    uint? LastImpactFrameIdentifier,
    float? LastImpactSessionTime,
    float CurrentIntensity,
    int RemainingPulseFrames,
    float PeakLevel);
