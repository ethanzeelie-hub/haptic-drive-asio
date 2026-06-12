using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed record RoadTextureEffectOptions(
    bool IsEnabled,
    float Gain,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph,
    float MaximumAmplitude,
    float SuspensionMotionGain,
    float VerticalGDeviationGain,
    TimeSpan ResponseSmoothingTime,
    uint MaximumTelemetryFrameLag,
    IReadOnlyDictionary<byte, RoadTextureSurfaceProfile> SurfaceProfiles)
{
    public static RoadTextureEffectOptions Default { get; } = new(
        IsEnabled: true,
        Gain: 0.05f,
        MinimumSpeedKph: 5f,
        FullIntensitySpeedKph: 160f,
        MaximumAmplitude: 0.16f,
        SuspensionMotionGain: 0.25f,
        VerticalGDeviationGain: 0.2f,
        ResponseSmoothingTime: TimeSpan.FromMilliseconds(20),
        MaximumTelemetryFrameLag: 120,
        SurfaceProfiles: RoadTextureEvaluatorOptions.CreateDefaultSurfaceProfiles());

    public RoadTextureEvaluatorOptions ToEvaluatorOptions()
    {
        return RoadTextureEvaluatorOptions.Default with
        {
            IsEnabled = IsEnabled,
            MinimumSpeedKph = MinimumSpeedKph,
            FullIntensitySpeedKph = FullIntensitySpeedKph,
            MaximumIntensity = MaximumAmplitude <= 0f ? RoadTextureEvaluatorOptions.Default.MaximumIntensity : Math.Min(MaximumAmplitude / Math.Max(0.0001f, Gain), 1f),
            RoughnessContribution = Math.Clamp(SuspensionMotionGain + VerticalGDeviationGain, 0f, 1f),
            MaximumTelemetryFrameLag = MaximumTelemetryFrameLag,
            SurfaceProfiles = SurfaceProfiles ?? RoadTextureEvaluatorOptions.CreateDefaultSurfaceProfiles()
        };
    }
}

public sealed record RoadTextureEffectSnapshot(
    bool IsEnabled,
    bool IsActive,
    byte? DominantSurfaceTypeId,
    string DominantSurfaceName,
    float CurrentFrequencyHz,
    float CurrentAmplitude,
    float SurfaceMix,
    float PeakLevel,
    RoadTextureSignal Signal,
    float RmsLevel);
