using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Actuation.Driving;

internal static class LegacyActuationHapticFrameFactory
{
    public static HapticFrame FromVehicleState(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        var freshness = new Dictionary<string, VehicleSignalFreshness>(StringComparer.Ordinal)
        {
            [HapticFrameSignalNames.Telemetry] = Present(vehicleState.Telemetry is not null),
            [HapticFrameSignalNames.Motion] = Present(vehicleState.Motion is not null),
            [HapticFrameSignalNames.Session] = Present(vehicleState.Session is not null),
            [HapticFrameSignalNames.Lap] = Present(vehicleState.Lap is not null),
            [HapticFrameSignalNames.Participant] = Present(vehicleState.Participant is not null),
            [HapticFrameSignalNames.CarStatus] = Present(vehicleState.CarStatus is not null),
            [HapticFrameSignalNames.Damage] = Present(vehicleState.Damage is not null),
            [HapticFrameSignalNames.MotionEx] = Present(vehicleState.MotionEx is not null),
            [HapticFrameSignalNames.Event] = Present(vehicleState.LastEvent is not null)
        };

        return new HapticFrame(
            new HapticFrameIdentity(
                new GameIntegrationId("f1-25"),
                vehicleState.Frame.Source ?? "legacy",
                vehicleState.Frame.SessionUid,
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
                EngineRpm: vehicleState.Telemetry?.Value.EngineRpm,
                IdleRpm: vehicleState.CarStatus?.Value.IdleRpm,
                MaxRpm: vehicleState.CarStatus?.Value.MaxRpm,
                SurfaceKinds: vehicleState.Telemetry is null ? null : MapSurfaceKinds(vehicleState.Telemetry.Value.SurfaceTypeIds),
                TyreSlip: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.WheelSlipRatio),
                SuspensionVelocity: vehicleState.MotionEx is null ? null : MapFloatWheels(vehicleState.MotionEx.Value.SuspensionVelocity),
                BrakeTemperatureCelsius: vehicleState.Telemetry is null ? null : MapBrakeTemps(vehicleState.Telemetry.Value.BrakeTemperatureCelsius)),
            new HapticDrivingContext(
                vehicleState.Session?.Value.GamePaused is > 0 || vehicleState.CarStatus?.Value.NetworkPaused is > 0
                    ? DrivingPhase.Paused
                    : vehicleState.Lap?.Value.DriverStatus == 0 || vehicleState.Lap?.Value.ResultStatus is 0 or 1
                        ? DrivingPhase.Garage
                        : vehicleState.Lap?.Value.PitStatus is 1 or 2
                            ? DrivingPhase.PitLane
                            : DrivingPhase.Driving,
                vehicleState.Lap?.Value.PitStatus switch
                {
                    0 => PitState.None,
                    1 => PitState.Pitting,
                    2 => PitState.InPitArea,
                    _ => PitState.Unknown
                },
                IsPaused: vehicleState.Session?.Value.GamePaused is > 0 || vehicleState.CarStatus?.Value.NetworkPaused is > 0,
                IsPlayerControlled: vehicleState.Lap?.Value.DriverStatus is not 0,
                AllowsDrivingOutput: vehicleState.Telemetry is not null
                    && vehicleState.Session?.Value.GamePaused is not > 0
                    && vehicleState.CarStatus?.Value.NetworkPaused is not > 0
                    && vehicleState.Lap?.Value.DriverStatus is not 0
                    && vehicleState.Lap?.Value.ResultStatus is not 0 and not 1),
            freshness);
    }

    private static VehicleSignalFreshness Present(bool value)
    {
        return new VehicleSignalFreshness(value, true, true, true, true, TimeSpan.Zero, 0);
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
