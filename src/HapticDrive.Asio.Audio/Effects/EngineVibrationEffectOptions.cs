namespace HapticDrive.Asio.Audio.Effects;

public sealed record EngineVibrationEffectOptions(
    bool IsEnabled,
    float Gain,
    float MinimumFrequencyHz,
    float MaximumFrequencyHz,
    bool HighFrequencyEnabled,
    float HighFrequencyHz,
    float HighFrequencyGain,
    float FrequencyJitterHz,
    float IdleThrottleGain,
    float PitGainMultiplier,
    ushort DefaultIdleRpm,
    ushort DefaultMaxRpm,
    ushort MaximumAllowedRpm)
{
    public static EngineVibrationEffectOptions Default { get; } = new(
        IsEnabled: true,
        Gain: 0.08f,
        MinimumFrequencyHz: 34f,
        MaximumFrequencyHz: 50f,
        HighFrequencyEnabled: true,
        HighFrequencyHz: 50f,
        HighFrequencyGain: 0.25f,
        FrequencyJitterHz: 0f,
        IdleThrottleGain: 0.35f,
        PitGainMultiplier: 0.35f,
        DefaultIdleRpm: 3_000,
        DefaultMaxRpm: 12_000,
        MaximumAllowedRpm: 30_000);
}

public sealed record EngineVibrationEffectSnapshot(
    bool IsEnabled,
    bool IsActive,
    ushort? LastRpm,
    float LastThrottle,
    float CurrentFrequencyHz,
    float CurrentAmplitude,
    float PeakLevel);
