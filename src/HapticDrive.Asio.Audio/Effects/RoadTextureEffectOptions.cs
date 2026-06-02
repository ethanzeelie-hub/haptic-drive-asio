namespace HapticDrive.Asio.Audio.Effects;

public sealed record RoadTextureSurfaceProfile(
    byte SurfaceTypeId,
    string Name,
    float GainMultiplier,
    float BaseFrequencyHz,
    float NoiseAmount);

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
        SurfaceProfiles: CreateDefaultSurfaceProfiles());

    public static IReadOnlyDictionary<byte, RoadTextureSurfaceProfile> CreateDefaultSurfaceProfiles()
    {
        return new Dictionary<byte, RoadTextureSurfaceProfile>
        {
            [VehicleSurfaceTypes.Tarmac] = new(VehicleSurfaceTypes.Tarmac, "Tarmac", 0.18f, 44f, 0.08f),
            [VehicleSurfaceTypes.RumbleStrip] = new(VehicleSurfaceTypes.RumbleStrip, "Rumble strip", 0.9f, 24f, 0.22f),
            [VehicleSurfaceTypes.Concrete] = new(VehicleSurfaceTypes.Concrete, "Concrete", 0.28f, 42f, 0.1f),
            [VehicleSurfaceTypes.Rock] = new(VehicleSurfaceTypes.Rock, "Rock", 0.55f, 34f, 0.3f),
            [VehicleSurfaceTypes.Gravel] = new(VehicleSurfaceTypes.Gravel, "Gravel", 0.65f, 32f, 0.34f),
            [VehicleSurfaceTypes.Mud] = new(VehicleSurfaceTypes.Mud, "Mud", 0.38f, 28f, 0.25f),
            [VehicleSurfaceTypes.Sand] = new(VehicleSurfaceTypes.Sand, "Sand", 0.32f, 30f, 0.24f),
            [VehicleSurfaceTypes.Grass] = new(VehicleSurfaceTypes.Grass, "Grass", 0.42f, 28f, 0.28f),
            [VehicleSurfaceTypes.Water] = new(VehicleSurfaceTypes.Water, "Water", 0.2f, 26f, 0.2f),
            [VehicleSurfaceTypes.Cobblestone] = new(VehicleSurfaceTypes.Cobblestone, "Cobblestone", 0.7f, 36f, 0.18f),
            [VehicleSurfaceTypes.Metal] = new(VehicleSurfaceTypes.Metal, "Metal", 0.5f, 48f, 0.12f),
            [VehicleSurfaceTypes.Ridged] = new(VehicleSurfaceTypes.Ridged, "Ridged", 0.85f, 22f, 0.2f)
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
    float PeakLevel);
