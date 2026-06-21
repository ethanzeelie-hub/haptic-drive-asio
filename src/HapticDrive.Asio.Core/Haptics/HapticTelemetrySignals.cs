namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticTelemetrySignals(
    float? SpeedMetersPerSecond,
    float? Throttle,
    float? Brake,
    float? Steer,
    int? Gear,
    int? EngineRpm,
    int? IdleRpm,
    int? MaxRpm,
    HapticWheelSignals<SurfaceKind>? SurfaceKinds,
    HapticWheelSignals<float>? TyreSlip,
    HapticWheelSignals<float>? SuspensionVelocity,
    HapticWheelSignals<float>? BrakeTemperatureCelsius);
