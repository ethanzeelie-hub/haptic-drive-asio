using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed class F125VehicleStateNormalizer : IVehicleStateNormalizer
{
    public static GameIntegrationId IntegrationId { get; } = new("f1-25");

    public GameIntegrationId GameId => IntegrationId;

    public HapticFrame Normalize(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy freshnessPolicy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(freshnessPolicy);

        var telemetryFreshness = VehicleStateFreshness.EvaluateTelemetry(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var motionFreshness = VehicleStateFreshness.EvaluateMotion(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var sessionFreshness = VehicleStateFreshness.EvaluateSession(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var lapFreshness = VehicleStateFreshness.EvaluateLap(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var carStatusFreshness = VehicleStateFreshness.EvaluateCarStatus(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var damageFreshness = VehicleStateFreshness.EvaluateDamage(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var motionExFreshness = VehicleStateFreshness.EvaluateMotionEx(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var eventFreshness = VehicleStateFreshness.EvaluateLastEvent(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);

        var identity = new HapticFrameIdentity(
            IntegrationId,
            state.Frame.Source ?? "unknown",
            state.Frame.SessionUid,
            state.Frame.OverallFrameIdentifier,
            state.Frame.PlayerCarIndex,
            nowUtc,
            nowTimestamp);

        var signals = new HapticTelemetrySignals(
            SpeedMetersPerSecond: state.Telemetry is null ? null : state.Telemetry.Value.SpeedKph / 3.6f,
            Throttle: state.Telemetry?.Value.Throttle,
            Brake: state.Telemetry?.Value.Brake,
            Steer: state.Telemetry?.Value.Steer,
            Gear: state.Telemetry?.Value.Gear,
            EngineRpm: state.Telemetry?.Value.EngineRpm,
            IdleRpm: state.CarStatus?.Value.IdleRpm,
            MaxRpm: state.CarStatus?.Value.MaxRpm,
            SurfaceKinds: state.Telemetry is null ? null : MapSurfaceKinds(state.Telemetry.Value.SurfaceTypeIds),
            TyreSlip: state.MotionEx is null ? null : MapFloatWheels(state.MotionEx.Value.WheelSlipRatio),
            SuspensionVelocity: state.MotionEx is null ? null : MapFloatWheels(state.MotionEx.Value.SuspensionVelocity),
            BrakeTemperatureCelsius: state.Telemetry is null ? null : MapIntWheelsToFloat(state.Telemetry.Value.BrakeTemperatureCelsius));

        var context = BuildDrivingContext(state, telemetryFreshness, sessionFreshness, lapFreshness, carStatusFreshness);

        return new HapticFrame(
            identity,
            signals,
            context,
            new Dictionary<string, VehicleSignalFreshness>(StringComparer.Ordinal)
            {
                [HapticFrameSignalNames.Telemetry] = telemetryFreshness,
                [HapticFrameSignalNames.Motion] = motionFreshness,
                [HapticFrameSignalNames.Session] = sessionFreshness,
                [HapticFrameSignalNames.Lap] = lapFreshness,
                [HapticFrameSignalNames.CarStatus] = carStatusFreshness,
                [HapticFrameSignalNames.Damage] = damageFreshness,
                [HapticFrameSignalNames.MotionEx] = motionExFreshness,
                [HapticFrameSignalNames.Event] = eventFreshness
            });
    }

    private static HapticDrivingContext BuildDrivingContext(
        VehicleState state,
        VehicleSignalFreshness telemetryFreshness,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness,
        VehicleSignalFreshness carStatusFreshness)
    {
        var isPaused = (sessionFreshness.IsFresh && state.Session?.Value.GamePaused is > 0)
            || (carStatusFreshness.IsFresh && state.CarStatus?.Value.NetworkPaused is > 0);
        var pitState = ResolvePitState(state);
        var playerControlled = ResolvePlayerControlled(state);
        var allowsDrivingOutput = telemetryFreshness.IsFresh && !isPaused && playerControlled;
        var drivingPhase = ResolveDrivingPhase(state, isPaused, pitState, playerControlled, allowsDrivingOutput, sessionFreshness, lapFreshness);
        return new HapticDrivingContext(drivingPhase, pitState, isPaused, playerControlled, allowsDrivingOutput);
    }

    private static DrivingPhase ResolveDrivingPhase(
        VehicleState state,
        bool isPaused,
        PitState pitState,
        bool playerControlled,
        bool allowsDrivingOutput,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness)
    {
        if (isPaused)
        {
            return DrivingPhase.Paused;
        }

        if (pitState is PitState.Pitting or PitState.InPitArea)
        {
            return DrivingPhase.PitLane;
        }

        if (lapFreshness.IsFresh && state.Lap?.Value.DriverStatus == 0)
        {
            return DrivingPhase.Garage;
        }

        if (lapFreshness.IsFresh && state.Lap?.Value.ResultStatus is 0 or 1)
        {
            return DrivingPhase.Garage;
        }

        if (sessionFreshness.IsFresh && state.Session?.Value.SessionType == 12)
        {
            return DrivingPhase.Formation;
        }

        if (allowsDrivingOutput)
        {
            return DrivingPhase.Driving;
        }

        return playerControlled ? DrivingPhase.Unknown : DrivingPhase.Spectating;
    }

    private static PitState ResolvePitState(VehicleState state)
    {
        return state.Lap?.Value.PitStatus switch
        {
            0 => PitState.None,
            1 => PitState.Pitting,
            2 => PitState.InPitArea,
            null => PitState.Unknown,
            _ => PitState.Unknown
        };
    }

    private static bool ResolvePlayerControlled(VehicleState state)
    {
        return state.Lap?.Value.DriverStatus is not 0;
    }

    private static HapticWheelSignals<SurfaceKind> MapSurfaceKinds(VehicleWheelData<byte> raw)
    {
        return new HapticWheelSignals<SurfaceKind>(
            MapSurfaceKind(raw.RearLeft),
            MapSurfaceKind(raw.RearRight),
            MapSurfaceKind(raw.FrontLeft),
            MapSurfaceKind(raw.FrontRight));
    }

    private static HapticWheelSignals<float> MapFloatWheels(VehicleWheelData<float> values)
    {
        return new HapticWheelSignals<float>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static HapticWheelSignals<float> MapIntWheelsToFloat(VehicleWheelData<ushort> values)
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
