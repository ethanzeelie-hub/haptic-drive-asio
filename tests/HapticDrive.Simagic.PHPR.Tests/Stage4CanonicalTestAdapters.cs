using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

internal static class Stage4CanonicalTestAdapters
{
    public static ValueTask<PHprRoadVibrationRoutingResult> RouteAsync(
        this PHprRoadVibrationRouter router,
        VehicleState? vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);

        if (vehicleState is null)
        {
            return router.RouteAsync(
                frame: null,
                drivingContext: null,
                safetyContext,
                nowUtc,
                cancellationToken);
        }

        var frame = vehicleState.ToCanonicalHapticFrame();
        var drivingContext = ActuationDrivingContextFactory.FromHapticFrame(frame, safetyContext?.DrivingArmed ?? true);
        return router.RouteAsync(frame, drivingContext, safetyContext, nowUtc, cancellationToken);
    }

    public static ValueTask<PHprSlipLockRoutingResult> RouteAsync(
        this PHprSlipLockRouter router,
        VehicleState? vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);

        if (vehicleState is null)
        {
            return router.RouteAsync(
                frame: null,
                drivingContext: null,
                safetyContext,
                nowUtc,
                cancellationToken);
        }

        var frame = vehicleState.ToCanonicalHapticFrame();
        var drivingContext = ActuationDrivingContextFactory.FromHapticFrame(frame, safetyContext?.DrivingArmed ?? true);
        return router.RouteAsync(frame, drivingContext, safetyContext, nowUtc, cancellationToken);
    }

    private static HapticFrame ToCanonicalHapticFrame(this VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        return new HapticFrame(
            new HapticFrameIdentity(
                new GameIntegrationId("f1-25"),
                vehicleState.Frame.Source ?? "test",
                vehicleState.Frame.SessionUid,
                vehicleState.Frame.SessionTime,
                vehicleState.Frame.FrameIdentifier,
                vehicleState.Frame.OverallFrameIdentifier,
                vehicleState.Frame.PlayerCarIndex,
                DateTimeOffset.UtcNow,
                0)
            {
                SourceIdentity = vehicleState.Frame.SourceIdentity
            },
            new HapticTelemetrySignals(
                SpeedMetersPerSecond: vehicleState.Telemetry is null ? null : vehicleState.Telemetry.Value.SpeedKph / 3.6f,
                Throttle: vehicleState.Telemetry?.Value.Throttle,
                Brake: vehicleState.Telemetry?.Value.Brake,
                Steer: vehicleState.Telemetry?.Value.Steer,
                Gear: vehicleState.Telemetry?.Value.Gear,
                SuggestedGear: vehicleState.Telemetry?.Value.SuggestedGear,
                EngineRpm: vehicleState.Telemetry?.Value.EngineRpm,
                IdleRpm: vehicleState.CarStatus?.Value.IdleRpm,
                MaxRpm: vehicleState.CarStatus?.Value.MaxRpm,
                MaxGears: vehicleState.CarStatus?.Value.MaxGears,
                TractionControlActive: vehicleState.CarStatus is null ? null : vehicleState.CarStatus.Value.TractionControl > 0,
                AntiLockBrakesActive: vehicleState.CarStatus is null ? null : vehicleState.CarStatus.Value.AntiLockBrakes > 0,
                SurfaceTypeIds: vehicleState.Telemetry is null ? null : MapByteWheels(vehicleState.Telemetry.Value.SurfaceTypeIds),
                SurfaceKinds: vehicleState.Telemetry is null ? null : MapSurfaceKinds(vehicleState.Telemetry.Value.SurfaceTypeIds),
                TyreSlip: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.WheelSlipRatio),
                TyreSlipAngle: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.WheelSlipAngle),
                WheelSpeedMetersPerSecond: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.WheelSpeed),
                SuspensionVelocity: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.SuspensionVelocity),
                SuspensionAcceleration: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.SuspensionAcceleration),
                WheelVerticalForce: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.WheelVertForce),
                BrakeTemperatureCelsius: vehicleState.Telemetry is null ? null : MapBrakeTemps(vehicleState.Telemetry.Value.BrakeTemperatureCelsius),
                VerticalG: vehicleState.Motion?.Value.GForceVertical,
                Event: null),
            new HapticDrivingContext(DrivingPhase.Driving, PitState.None, false, true, true),
            new HapticFrameSignalStamps(
                Telemetry: CreateSignalStamp(vehicleState.Telemetry),
                Motion: CreateSignalStamp(vehicleState.Motion),
                Session: CreateSignalStamp(vehicleState.Session),
                Lap: CreateSignalStamp(vehicleState.Lap),
                Participant: CreateSignalStamp(vehicleState.Participant),
                CarStatus: CreateSignalStamp(vehicleState.CarStatus),
                Damage: CreateSignalStamp(vehicleState.Damage),
                MotionEx: CreateSignalStamp(vehicleState.MotionEx),
                Event: CreateSignalStamp(vehicleState.LastEvent)));
    }

    private static HapticSignalStamp? CreateSignalStamp<T>(VehicleStateSample<T>? sample)
    {
        if (sample is null)
        {
            return null;
        }

        var stamp = sample.Stamp;
        return new HapticSignalStamp(
            stamp.Source,
            stamp.PacketKind,
            stamp.SessionUid,
            stamp.SessionTime,
            stamp.FrameIdentifier,
            stamp.OverallFrameIdentifier,
            stamp.PlayerCarIndex,
            stamp.ReceivedAtUtc,
            stamp.ReceivedAtTimestamp)
        {
            SourceIdentity = stamp.SourceIdentity
        };
    }

    private static HapticWheelSignals<byte> MapByteWheels(VehicleWheelData<byte> values)
    {
        return new HapticWheelSignals<byte>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static HapticWheelSignals<SurfaceKind> MapSurfaceKinds(VehicleWheelData<byte> values)
    {
        return new HapticWheelSignals<SurfaceKind>(
            MapSurfaceKind(values.RearLeft),
            MapSurfaceKind(values.RearRight),
            MapSurfaceKind(values.FrontLeft),
            MapSurfaceKind(values.FrontRight));
    }

    private static HapticWheelSignals<float> MapFloatWheels(VehicleWheelData<float> values)
    {
        return new HapticWheelSignals<float>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static HapticWheelSignals<float> MapBrakeTemps(VehicleWheelData<ushort> values)
    {
        return new HapticWheelSignals<float>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static SurfaceKind MapSurfaceKind(byte value)
    {
        return value switch
        {
            0 => SurfaceKind.Tarmac,
            1 => SurfaceKind.RumbleStrip,
            2 => SurfaceKind.Concrete,
            3 => SurfaceKind.Rock,
            4 => SurfaceKind.Gravel,
            5 => SurfaceKind.Mud,
            6 => SurfaceKind.Sand,
            7 => SurfaceKind.Grass,
            8 => SurfaceKind.Water,
            9 => SurfaceKind.Cobblestone,
            10 => SurfaceKind.Metal,
            11 => SurfaceKind.Ridged,
            _ => SurfaceKind.Unknown
        };
    }
}
