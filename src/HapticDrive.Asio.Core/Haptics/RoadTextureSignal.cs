using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureSignal(
    DateTimeOffset TimestampUtc,
    ulong? SessionUid,
    float? SessionTime,
    uint? FrameIdentifier,
    uint? OverallFrameIdentifier,
    bool RoadEffectEnabled,
    bool TelemetryFresh,
    bool HapticsRunning,
    bool DrivingArmed,
    ushort SpeedKph,
    float SpeedScale,
    VehicleWheelData<byte> SurfaceTypeIds,
    VehicleWheelData<float> SuspensionAcceleration,
    VehicleWheelData<float> WheelVertForce,
    float? VerticalG,
    RoadTextureSurfaceClass SurfaceClass,
    string SurfaceName,
    float SurfaceMix,
    float RawIntensity,
    float SmoothedIntensity,
    float OutputIntensity,
    float SuspensionAccelerationContribution,
    float WheelVertForceContribution,
    float VerticalGContribution,
    float RoughnessMetric,
    float FrequencyHintHz,
    float Bst1FrequencyHz,
    float PHprFrequencyHz,
    float NoiseAmount,
    bool GearDuckingActive,
    float DuckingGain,
    string? SuppressedReason)
{
    public bool IsActive => OutputIntensity > 0f && SuppressedReason is null;

    public static RoadTextureSignal Inactive(DateTimeOffset timestampUtc, string? reason = null)
    {
        return new RoadTextureSignal(
            timestampUtc,
            SessionUid: null,
            SessionTime: null,
            FrameIdentifier: null,
            OverallFrameIdentifier: null,
            RoadEffectEnabled: false,
            TelemetryFresh: false,
            HapticsRunning: false,
            DrivingArmed: false,
            SpeedKph: 0,
            SpeedScale: 0f,
            SurfaceTypeIds: Wheels<byte>(0),
            SuspensionAcceleration: Wheels(0f),
            WheelVertForce: Wheels(0f),
            VerticalG: null,
            RoadTextureSurfaceClass.None,
            "None",
            SurfaceMix: 0f,
            RawIntensity: 0f,
            SmoothedIntensity: 0f,
            OutputIntensity: 0f,
            SuspensionAccelerationContribution: 0f,
            WheelVertForceContribution: 0f,
            VerticalGContribution: 0f,
            RoughnessMetric: 0f,
            FrequencyHintHz: 0f,
            Bst1FrequencyHz: 0f,
            PHprFrequencyHz: 0f,
            NoiseAmount: 0f,
            GearDuckingActive: false,
            DuckingGain: 1f,
            SuppressedReason: reason);
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }
}
