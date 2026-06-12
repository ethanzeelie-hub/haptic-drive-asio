namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureEvaluatorOptions(
    bool IsEnabled,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph,
    float MaximumIntensity,
    float SmoothSurfaceFloor,
    float SuspensionAccelerationThreshold,
    float SuspensionAccelerationFullScale,
    float WheelVertForceDeltaThreshold,
    float WheelVertForceDeltaFullScale,
    float RoughnessContribution,
    float AttackSmoothing,
    float ReleaseSmoothing,
    TimeSpan GearDuckingWindow,
    float GearDuckingGain,
    uint MaximumTelemetryFrameLag,
    IReadOnlyDictionary<byte, RoadTextureSurfaceProfile> SurfaceProfiles)
{
    public static RoadTextureEvaluatorOptions Default { get; } = new(
        IsEnabled: true,
        MinimumSpeedKph: 5f,
        FullIntensitySpeedKph: 220f,
        MaximumIntensity: 0.72f,
        SmoothSurfaceFloor: 0.015f,
        SuspensionAccelerationThreshold: 7.5f,
        SuspensionAccelerationFullScale: 45f,
        WheelVertForceDeltaThreshold: 1_500f,
        WheelVertForceDeltaFullScale: 8_500f,
        RoughnessContribution: 0.35f,
        AttackSmoothing: 0.35f,
        ReleaseSmoothing: 0.08f,
        GearDuckingWindow: TimeSpan.FromMilliseconds(120),
        GearDuckingGain: 0.22f,
        MaximumTelemetryFrameLag: 120,
        SurfaceProfiles: CreateDefaultSurfaceProfiles());

    public RoadTextureEvaluatorOptions Normalize()
    {
        return this with
        {
            MinimumSpeedKph = Clamp(MinimumSpeedKph, 0f, 120f),
            FullIntensitySpeedKph = Math.Max(Clamp(FullIntensitySpeedKph, 20f, 360f), Clamp(MinimumSpeedKph, 0f, 120f) + 1f),
            MaximumIntensity = Clamp(MaximumIntensity, 0f, 1f),
            SmoothSurfaceFloor = Clamp(SmoothSurfaceFloor, 0f, 0.1f),
            SuspensionAccelerationThreshold = Clamp(SuspensionAccelerationThreshold, 0f, 100f),
            SuspensionAccelerationFullScale = Math.Max(Clamp(SuspensionAccelerationFullScale, 1f, 200f), Clamp(SuspensionAccelerationThreshold, 0f, 100f) + 1f),
            WheelVertForceDeltaThreshold = Clamp(WheelVertForceDeltaThreshold, 0f, 20_000f),
            WheelVertForceDeltaFullScale = Math.Max(Clamp(WheelVertForceDeltaFullScale, 1f, 50_000f), Clamp(WheelVertForceDeltaThreshold, 0f, 20_000f) + 1f),
            RoughnessContribution = Clamp(RoughnessContribution, 0f, 1f),
            AttackSmoothing = Clamp(AttackSmoothing, 0f, 1f),
            ReleaseSmoothing = Clamp(ReleaseSmoothing, 0f, 1f),
            GearDuckingWindow = GearDuckingWindow < TimeSpan.Zero ? TimeSpan.Zero : GearDuckingWindow,
            GearDuckingGain = Clamp(GearDuckingGain, 0f, 1f),
            SurfaceProfiles = SurfaceProfiles ?? CreateDefaultSurfaceProfiles()
        };
    }

    public static IReadOnlyDictionary<byte, RoadTextureSurfaceProfile> CreateDefaultSurfaceProfiles()
    {
        return new Dictionary<byte, RoadTextureSurfaceProfile>
        {
            [0] = new(0, "Tarmac", RoadTextureSurfaceClass.SmoothTrack, 0.08f, 44f, 25f, 0.08f),
            [1] = new(1, "Rumble strip", RoadTextureSurfaceClass.RumbleStrip, 0.88f, 24f, 42f, 0.22f),
            [2] = new(2, "Concrete", RoadTextureSurfaceClass.SmoothTrack, 0.16f, 42f, 28f, 0.1f),
            [3] = new(3, "Rock", RoadTextureSurfaceClass.HardRough, 0.55f, 34f, 38f, 0.3f),
            [4] = new(4, "Gravel", RoadTextureSurfaceClass.Loose, 0.68f, 32f, 36f, 0.34f),
            [5] = new(5, "Mud", RoadTextureSurfaceClass.Soft, 0.36f, 28f, 30f, 0.25f),
            [6] = new(6, "Sand", RoadTextureSurfaceClass.Soft, 0.32f, 30f, 29f, 0.24f),
            [7] = new(7, "Grass", RoadTextureSurfaceClass.Soft, 0.38f, 28f, 31f, 0.28f),
            [8] = new(8, "Water", RoadTextureSurfaceClass.Wet, 0.18f, 26f, 26f, 0.2f),
            [9] = new(9, "Cobblestone", RoadTextureSurfaceClass.HardRough, 0.70f, 36f, 40f, 0.18f),
            [10] = new(10, "Metal", RoadTextureSurfaceClass.HardRough, 0.48f, 48f, 45f, 0.12f),
            [11] = new(11, "Ridged", RoadTextureSurfaceClass.Ridged, 0.82f, 22f, 43f, 0.2f)
        };
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        if (!float.IsFinite(value))
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
