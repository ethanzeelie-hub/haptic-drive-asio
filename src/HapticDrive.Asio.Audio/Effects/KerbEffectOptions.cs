namespace HapticDrive.Asio.Audio.Effects;

public sealed record KerbEffectOptions(
    bool IsEnabled,
    float Gain,
    float BaseFrequencyHz,
    bool HighFrequencyEnabled,
    float HighFrequencyHz,
    float HighFrequencyGain,
    float NoiseAmount,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph,
    float MaximumAmplitude,
    TimeSpan ResponseSmoothingTime,
    uint MaximumTelemetryFrameLag)
{
    public static KerbEffectOptions Default { get; } = new(
        IsEnabled: true,
        Gain: 0.12f,
        BaseFrequencyHz: 20f,
        HighFrequencyEnabled: true,
        HighFrequencyHz: 44f,
        HighFrequencyGain: 0.25f,
        NoiseAmount: 0.08f,
        MinimumSpeedKph: 5f,
        FullIntensitySpeedKph: 120f,
        MaximumAmplitude: 0.22f,
        ResponseSmoothingTime: TimeSpan.FromMilliseconds(12),
        MaximumTelemetryFrameLag: 120);
}

public readonly record struct KerbEffectSnapshot(
    bool IsEnabled,
    bool IsActive,
    byte? DominantSurfaceTypeId,
    string DominantSurfaceName,
    float CurrentFrequencyHz,
    float CurrentAmplitude,
    int ActiveWheelCount,
    float PeakLevel);
