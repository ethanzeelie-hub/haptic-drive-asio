namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureSurfaceProfile(
    byte SurfaceTypeId,
    string Name,
    RoadTextureSurfaceClass SurfaceClass,
    float BaseGain,
    float Bst1FrequencyHz,
    float PHprFrequencyHz,
    float NoiseAmount);
