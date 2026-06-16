using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed record RoadTextureEffectOptions(
    bool IsEnabled,
    bool Bst1OutputEnabled,
    float Gain,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph,
    float Bst1LowSpeedFrequencyHz,
    float Bst1HighSpeedFrequencyHz,
    float Bst1SpeedFrequencyInfluence,
    float Bst1GrainAmount,
    float MaximumAmplitude,
    float SuspensionMotionGain,
    float VerticalGDeviationGain,
    TimeSpan ResponseSmoothingTime,
    uint MaximumTelemetryFrameLag,
    IReadOnlyDictionary<byte, RoadTextureSurfaceProfile> SurfaceProfiles)
{
    public static RoadTextureEffectOptions Default { get; } = new(
        IsEnabled: true,
        Bst1OutputEnabled: true,
        Gain: 0.05f,
        MinimumSpeedKph: 5f,
        FullIntensitySpeedKph: 330f,
        Bst1LowSpeedFrequencyHz: 40f,
        Bst1HighSpeedFrequencyHz: 68f,
        Bst1SpeedFrequencyInfluence: 0.75f,
        Bst1GrainAmount: 0.18f,
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
            Bst1LowSpeedFrequencyHz = Bst1LowSpeedFrequencyHz,
            Bst1HighSpeedFrequencyHz = Bst1HighSpeedFrequencyHz,
            Bst1SpeedFrequencyInfluence = Bst1SpeedFrequencyInfluence,
            Bst1GrainAmount = Bst1GrainAmount,
            MaximumIntensity = MaximumAmplitude <= 0f ? RoadTextureEvaluatorOptions.Default.MaximumIntensity : Math.Min(MaximumAmplitude / Math.Max(0.0001f, Gain), 1f),
            RoughnessContribution = Math.Clamp(SuspensionMotionGain + VerticalGDeviationGain, 0f, 1f),
            MaximumTelemetryFrameLag = MaximumTelemetryFrameLag,
            SurfaceProfiles = SurfaceProfiles ?? RoadTextureEvaluatorOptions.CreateDefaultSurfaceProfiles()
        };
    }
}

public sealed record RoadTextureEffectSnapshot(
    bool IsEnabled,
    bool Bst1OutputEnabled,
    bool IsActive,
    byte? DominantSurfaceTypeId,
    string DominantSurfaceName,
    float CurrentFrequencyHz,
    float CurrentAmplitude,
    float SurfaceMix,
    float PeakLevel,
    RoadTextureSignal Signal,
    float RmsLevel);
