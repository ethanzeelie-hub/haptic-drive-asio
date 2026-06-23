using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed class F125VehicleStateNormalizer : IVehicleStateNormalizer
{
    private readonly TimeProvider _timeProvider;

    public F125VehicleStateNormalizer(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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
        ArgumentNullException.ThrowIfNull(freshnessPolicy);
        timeProvider ??= _timeProvider;

        var telemetryFreshness = VehicleStateFreshness.EvaluateTelemetry(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var motionFreshness = VehicleStateFreshness.EvaluateMotion(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var sessionFreshness = VehicleStateFreshness.EvaluateSession(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var lapFreshness = VehicleStateFreshness.EvaluateLap(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var participantFreshness = VehicleStateFreshness.EvaluateParticipant(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var carStatusFreshness = VehicleStateFreshness.EvaluateCarStatus(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var damageFreshness = VehicleStateFreshness.EvaluateDamage(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var motionExFreshness = VehicleStateFreshness.EvaluateMotionEx(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);
        var eventFreshness = VehicleStateFreshness.EvaluateLastEvent(state, nowUtc, nowTimestamp, timeProvider, freshnessPolicy);

        var identity = new HapticFrameIdentity(
            IntegrationId,
            state.Frame.Source ?? "unknown",
            state.Frame.SessionUid,
            state.Frame.SessionTime,
            state.Frame.FrameIdentifier,
            state.Frame.OverallFrameIdentifier,
            state.Frame.PlayerCarIndex,
            nowUtc,
            nowTimestamp)
        {
            SourceIdentity = state.Frame.SourceIdentity
        };

        var telemetry = telemetryFreshness.IsFresh ? state.Telemetry?.Value : null;
        var motion = motionFreshness.IsFresh ? state.Motion?.Value : null;
        var carStatus = carStatusFreshness.IsFresh ? state.CarStatus?.Value : null;
        var motionEx = motionExFreshness.IsFresh ? state.MotionEx?.Value : null;
        var signals = new HapticTelemetrySignals(
            SpeedMetersPerSecond: telemetry is null ? null : telemetry.SpeedKph / 3.6f,
            Throttle: telemetry?.Throttle,
            Brake: telemetry?.Brake,
            Steer: telemetry?.Steer,
            Gear: telemetry?.Gear,
            SuggestedGear: telemetry?.SuggestedGear,
            EngineRpm: telemetry?.EngineRpm,
            IdleRpm: carStatus?.IdleRpm,
            MaxRpm: carStatus?.MaxRpm,
            MaxGears: carStatus?.MaxGears,
            TractionControlActive: carStatus is null ? null : carStatus.TractionControl > 0,
            AntiLockBrakesActive: carStatus is null ? null : carStatus.AntiLockBrakes > 0,
            SurfaceTypeIds: telemetry is null ? null : MapByteWheels(telemetry.SurfaceTypeIds),
            SurfaceKinds: telemetry is null ? null : MapSurfaceKinds(telemetry.SurfaceTypeIds),
            TyreSlip: motionEx is null ? null : MapFloatWheels(motionEx.WheelSlipRatio),
            TyreSlipAngle: motionEx is null ? null : MapFloatWheels(motionEx.WheelSlipAngle),
            WheelSpeedMetersPerSecond: motionEx is null ? null : MapFloatWheels(motionEx.WheelSpeed),
            SuspensionVelocity: motionEx is null ? null : MapFloatWheels(motionEx.SuspensionVelocity),
            SuspensionAcceleration: motionEx is null ? null : MapFloatWheels(motionEx.SuspensionAcceleration),
            WheelVerticalForce: motionEx is null ? null : MapFloatWheels(motionEx.WheelVertForce),
            BrakeTemperatureCelsius: telemetry is null ? null : MapIntWheelsToFloat(telemetry.BrakeTemperatureCelsius),
            VerticalG: motion?.GForceVertical,
            Event: MapEventSignals(state, eventFreshness));

        var context = BuildDrivingContext(state, telemetryFreshness, sessionFreshness, lapFreshness, participantFreshness, carStatusFreshness);

        return new HapticFrame(
            identity,
            signals,
            context,
            new HapticFrameSignalStamps(
                Telemetry: CreateSignalStamp(state.Telemetry),
                Motion: CreateSignalStamp(state.Motion),
                Session: CreateSignalStamp(state.Session),
                Lap: CreateSignalStamp(state.Lap),
                Participant: CreateSignalStamp(state.Participant),
                CarStatus: CreateSignalStamp(state.CarStatus),
                Damage: CreateSignalStamp(state.Damage),
                MotionEx: CreateSignalStamp(state.MotionEx),
                Event: CreateSignalStamp(state.LastEvent)));
    }

    private static HapticDrivingContext BuildDrivingContext(
        VehicleState state,
        VehicleSignalFreshness telemetryFreshness,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness,
        VehicleSignalFreshness participantFreshness,
        VehicleSignalFreshness carStatusFreshness)
    {
        var isPaused = (sessionFreshness.IsFresh && state.Session?.Value.GamePaused is > 0)
            || (carStatusFreshness.IsFresh && state.CarStatus?.Value.NetworkPaused is > 0);
        var pitState = ResolvePitState(state, lapFreshness);
        var playerControlled = ResolvePlayerControlled(state, telemetryFreshness, sessionFreshness, lapFreshness, participantFreshness, carStatusFreshness);
        var allowsDrivingOutput = AllowsDrivingOutput(
            state,
            telemetryFreshness,
            sessionFreshness,
            lapFreshness,
            participantFreshness,
            carStatusFreshness,
            isPaused,
            playerControlled);
        var drivingPhase = ResolveDrivingPhase(
            state,
            isPaused,
            pitState,
            playerControlled,
            allowsDrivingOutput,
            sessionFreshness,
            lapFreshness,
            participantFreshness);
        return new HapticDrivingContext(drivingPhase, pitState, isPaused, playerControlled, allowsDrivingOutput);
    }

    private static DrivingPhase ResolveDrivingPhase(
        VehicleState state,
        bool isPaused,
        PitState pitState,
        bool playerControlled,
        bool allowsDrivingOutput,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness,
        VehicleSignalFreshness participantFreshness)
    {
        if (isPaused)
        {
            return DrivingPhase.Paused;
        }

        if (sessionFreshness.IsFresh && state.Session?.Value.IsSpectating is > 0)
        {
            return DrivingPhase.Spectating;
        }

        if (state.Frame.SourceIdentity is null
            || state.Frame.SessionUid is null
            || state.Frame.PlayerCarIndex is null
            || !sessionFreshness.IsFresh
            || !lapFreshness.IsFresh
            || !participantFreshness.IsFresh)
        {
            return DrivingPhase.Unknown;
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

        return playerControlled ? DrivingPhase.Unknown : DrivingPhase.Garage;
    }

    private static PitState ResolvePitState(VehicleState state, VehicleSignalFreshness lapFreshness)
    {
        if (!lapFreshness.IsFresh)
        {
            return PitState.Unknown;
        }

        return state.Lap?.Value.PitStatus switch
        {
            0 => PitState.None,
            1 => PitState.Pitting,
            2 => PitState.InPitArea,
            null => PitState.Unknown,
            _ => PitState.Unknown
        };
    }

    private static bool ResolvePlayerControlled(
        VehicleState state,
        VehicleSignalFreshness telemetryFreshness,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness,
        VehicleSignalFreshness participantFreshness,
        VehicleSignalFreshness carStatusFreshness)
    {
        if (state.Frame.SourceIdentity is null
            || state.Frame.SessionUid is null
            || state.Frame.PlayerCarIndex is null
            || !telemetryFreshness.IsFresh
            || !sessionFreshness.IsFresh
            || !lapFreshness.IsFresh
            || !participantFreshness.IsFresh
            || !carStatusFreshness.IsFresh)
        {
            return false;
        }

        if (state.Session?.Value.IsSpectating is > 0)
        {
            return false;
        }

        var lap = state.Lap?.Value;
        if (lap is null)
        {
            return false;
        }

        return lap.DriverStatus != 0
            && lap.ResultStatus is not 0 and not 1;
    }

    private static bool AllowsDrivingOutput(
        VehicleState state,
        VehicleSignalFreshness telemetryFreshness,
        VehicleSignalFreshness sessionFreshness,
        VehicleSignalFreshness lapFreshness,
        VehicleSignalFreshness participantFreshness,
        VehicleSignalFreshness carStatusFreshness,
        bool isPaused,
        bool playerControlled)
    {
        if (!playerControlled || isPaused || state.Session?.Value.IsSpectating is > 0)
        {
            return false;
        }

        return state.Frame.SourceIdentity is not null
            && state.Frame.SessionUid is not null
            && state.Frame.PlayerCarIndex is not null
            && telemetryFreshness.IsFresh
            && sessionFreshness.IsFresh
            && lapFreshness.IsFresh
            && participantFreshness.IsFresh
            && carStatusFreshness.IsFresh;
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

    private static HapticEventSignals? MapEventSignals(
        VehicleState state,
        VehicleSignalFreshness eventFreshness)
    {
        if (!eventFreshness.IsFresh || state.LastEvent is null)
        {
            return null;
        }

        var lastEvent = state.LastEvent.Value;
        return new HapticEventSignals(
            MapEventKind(lastEvent.EventCode),
            lastEvent.InvolvesPlayer,
            ResolveOtherVehicleIndex(state.Frame.PlayerCarIndex, lastEvent));
    }

    private static HapticEventKind MapEventKind(string? eventCode)
    {
        return string.Equals(eventCode, "COLL", StringComparison.Ordinal)
            ? HapticEventKind.Collision
            : HapticEventKind.Unknown;
    }

    private static byte? ResolveOtherVehicleIndex(
        byte? playerCarIndex,
        VehicleEventState lastEvent)
    {
        if (!lastEvent.InvolvesPlayer)
        {
            return null;
        }

        if (playerCarIndex is null)
        {
            return null;
        }

        if (lastEvent.PrimaryVehicleIndex == playerCarIndex)
        {
            return lastEvent.SecondaryVehicleIndex;
        }

        if (lastEvent.SecondaryVehicleIndex == playerCarIndex)
        {
            return lastEvent.PrimaryVehicleIndex;
        }

        return null;
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

    private static HapticWheelSignals<byte> MapByteWheels(VehicleWheelData<byte> values)
    {
        return new HapticWheelSignals<byte>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
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
