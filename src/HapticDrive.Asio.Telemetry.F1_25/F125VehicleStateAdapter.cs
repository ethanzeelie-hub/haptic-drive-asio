using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed class F125VehicleStateAdapter
{
    private readonly object _gate = new();
    private VehicleState _current = VehicleState.Empty;
    private string? _sourceIdentity;

    public VehicleState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Reset(VehicleStateResetReason reason)
    {
        lock (_gate)
        {
            _current = VehicleState.Empty;
            _sourceIdentity = null;
        }
    }

    public F125VehicleStateUpdateResult Apply(UdpTelemetryPacket packet, F125PacketParseResult parseResult)
    {
        if (!parseResult.Succeeded || parseResult.Packet is null)
        {
            return F125VehicleStateUpdateResult.Ignored(Current, parseResult.Message);
        }

        return Apply(packet, parseResult.Packet);
    }

    public F125VehicleStateUpdateResult Apply(F125PacketParseResult parseResult)
    {
        return Apply(CreateSyntheticTelemetryPacket(), parseResult);
    }

    public F125VehicleStateUpdateResult Apply(UdpTelemetryPacket telemetryPacket, F125Packet packet)
    {
        lock (_gate)
        {
            var resetReason = ResetStateIfRequired(telemetryPacket, packet.Header);
            if (ShouldIgnoreOlderFrame(packet.Header))
            {
                return F125VehicleStateUpdateResult.Ignored(
                    _current,
                    $"{packet.Definition.Name} packet frame {packet.Header.OverallFrameIdentifier} is older than the current VehicleState frame.",
                    VehicleStateUpdatedSignals.None,
                    resetReason);
            }

            var stamp = CreateStamp(telemetryPacket, packet.Header, packet.Definition);
            var frame = CreateFrame(packet.Header, packet.Definition);
            var updatedSignals = VehicleStateUpdatedSignals.None;

            switch (packet.Body)
            {
                case F125MotionPacketBody body:
                    if (!TryGetPlayerData(body.CarMotionData, packet.Header.PlayerCarIndex, out var motion))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.CarMotionData.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        Motion = new VehicleStateSample<VehicleMotionState>(MapMotion(motion), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Motion;
                    break;

                case F125SessionPacketBody body:
                    _current = _current with
                    {
                        Frame = frame,
                        Session = new VehicleStateSample<VehicleSessionState>(MapSession(body), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Session;
                    break;

                case F125LapDataPacketBody body:
                    if (!TryGetPlayerData(body.LapData, packet.Header.PlayerCarIndex, out var lap))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.LapData.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        Lap = new VehicleStateSample<VehicleLapState>(MapLap(lap), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Lap;
                    break;

                case F125ParticipantsPacketBody body:
                    if (!TryGetPlayerData(body.Participants, packet.Header.PlayerCarIndex, out var participant))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.Participants.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        Participant = new VehicleStateSample<VehicleParticipantState>(MapParticipant(participant), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Participant;
                    break;

                case F125CarTelemetryPacketBody body:
                    if (!TryGetPlayerData(body.CarTelemetryData, packet.Header.PlayerCarIndex, out var telemetry))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.CarTelemetryData.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        Telemetry = new VehicleStateSample<VehicleTelemetryState>(MapTelemetry(telemetry, body.SuggestedGear), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Telemetry;
                    break;

                case F125CarStatusPacketBody body:
                    if (!TryGetPlayerData(body.CarStatusData, packet.Header.PlayerCarIndex, out var carStatus))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.CarStatusData.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        CarStatus = new VehicleStateSample<VehicleCarStatusState>(MapCarStatus(carStatus), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.CarStatus;
                    break;

                case F125CarDamagePacketBody body:
                    if (!TryGetPlayerData(body.CarDamageData, packet.Header.PlayerCarIndex, out var damage))
                    {
                        return IgnoreInvalidPlayerIndex(packet, body.CarDamageData.Count);
                    }

                    _current = _current with
                    {
                        Frame = frame,
                        Damage = new VehicleStateSample<VehicleDamageState>(MapDamage(damage), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Damage;
                    break;

                case F125MotionExPacketBody body:
                    _current = _current with
                    {
                        Frame = frame,
                        MotionEx = new VehicleStateSample<VehicleMotionExState>(MapMotionEx(body), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.MotionEx;
                    break;

                case F125EventPacketBody body:
                    _current = _current with
                    {
                        Frame = frame,
                        LastEvent = new VehicleStateSample<VehicleEventState>(MapEvent(body, packet.Header.PlayerCarIndex), stamp)
                    };
                    updatedSignals = VehicleStateUpdatedSignals.Event;
                    break;

                default:
                    return F125VehicleStateUpdateResult.Ignored(
                        _current,
                        $"{packet.Definition.Name} packet body is not mapped to VehicleState in Stage 08.",
                        VehicleStateUpdatedSignals.None,
                        resetReason);
            }

            return F125VehicleStateUpdateResult.Applied(
                _current,
                $"{packet.Definition.Name} packet updated VehicleState.",
                updatedSignals,
                resetReason);
        }
    }

    public F125VehicleStateUpdateResult Apply(F125Packet packet)
    {
        return Apply(CreateSyntheticTelemetryPacket(), packet);
    }

    private static VehicleStateStamp CreateStamp(UdpTelemetryPacket telemetryPacket, F125PacketHeader header, F125PacketDefinition definition)
    {
        return new(
            definition.Name,
            header.SessionUid,
            header.SessionTime,
            header.FrameIdentifier,
            header.OverallFrameIdentifier,
            header.PlayerCarIndex,
            telemetryPacket.ReceivedAtUtc,
            telemetryPacket.ReceivedAtTimestamp);
    }

    private static VehicleStateFrame CreateFrame(F125PacketHeader header, F125PacketDefinition definition)
    {
        return new(
            header.SessionUid,
            header.SessionTime,
            header.FrameIdentifier,
            header.OverallFrameIdentifier,
            header.PlayerCarIndex,
            definition.Name);
    }

    private static bool TryGetPlayerData<T>(IReadOnlyList<T> values, byte playerCarIndex, out T value)
    {
        if (playerCarIndex < values.Count)
        {
            value = values[playerCarIndex];
            return true;
        }

        value = default!;
        return false;
    }

    private F125VehicleStateUpdateResult IgnoreInvalidPlayerIndex(F125Packet packet, int entryCount)
    {
        return F125VehicleStateUpdateResult.Ignored(
            _current,
            $"Player car index {packet.Header.PlayerCarIndex} is outside {entryCount} {packet.Definition.Name} entries.");
    }

    private VehicleStateResetReason ResetStateIfRequired(UdpTelemetryPacket telemetryPacket, F125PacketHeader header)
    {
        var sourceIdentity = $"F1 25|{telemetryPacket.RemoteEndPoint.Address}";

        if (_sourceIdentity is not null && !string.Equals(_sourceIdentity, sourceIdentity, StringComparison.Ordinal))
        {
            ResetLocked(sourceIdentity);
            return VehicleStateResetReason.SourceChanged;
        }

        if (_current.Frame.SessionUid is { } currentSessionUid && currentSessionUid != header.SessionUid)
        {
            ResetLocked(sourceIdentity);
            return VehicleStateResetReason.SessionUidChanged;
        }

        if (_current.Frame.PlayerCarIndex is { } currentPlayerCarIndex && currentPlayerCarIndex != header.PlayerCarIndex)
        {
            ResetLocked(sourceIdentity);
            return VehicleStateResetReason.PlayerCarChanged;
        }

        _sourceIdentity = sourceIdentity;
        return VehicleStateResetReason.None;
    }

    private bool ShouldIgnoreOlderFrame(F125PacketHeader header)
    {
        return _current.Frame.SessionUid == header.SessionUid
            && _current.Frame.OverallFrameIdentifier is { } currentFrame
            && header.OverallFrameIdentifier < currentFrame;
    }

    private void ResetLocked(string sourceIdentity)
    {
        _current = VehicleState.Empty;
        _sourceIdentity = sourceIdentity;
    }

    private static UdpTelemetryPacket CreateSyntheticTelemetryPacket()
    {
        return new UdpTelemetryPacket(
            0,
            [],
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 20778),
            DateTimeOffset.UtcNow,
            TimeProvider.System.GetTimestamp());
    }

    private static VehicleMotionState MapMotion(F125CarMotionData data)
    {
        return new(
            data.WorldPositionX,
            data.WorldPositionY,
            data.WorldPositionZ,
            data.WorldVelocityX,
            data.WorldVelocityY,
            data.WorldVelocityZ,
            data.GForceLateral,
            data.GForceLongitudinal,
            data.GForceVertical,
            data.Yaw,
            data.Pitch,
            data.Roll);
    }

    private static VehicleSessionState MapSession(F125SessionPacketBody data)
    {
        return new(
            data.Weather,
            data.TrackTemperature,
            data.AirTemperature,
            data.TotalLaps,
            data.TrackLength,
            data.SessionType,
            data.TrackId,
            data.GamePaused,
            data.SafetyCarStatus,
            data.NetworkGame,
            data.GameMode);
    }

    private static VehicleLapState MapLap(F125LapData data)
    {
        return new(
            data.LastLapTimeInMs,
            data.CurrentLapTimeInMs,
            data.LapDistance,
            data.TotalDistance,
            data.CarPosition,
            data.CurrentLapNum,
            data.PitStatus,
            data.Sector,
            data.DriverStatus,
            data.ResultStatus,
            data.CurrentLapInvalid);
    }

    private static VehicleParticipantState MapParticipant(F125ParticipantData data)
    {
        return new(
            data.AiControlled,
            data.DriverId,
            data.TeamId,
            data.RaceNumber,
            data.Name,
            data.YourTelemetry,
            data.TechLevel,
            data.Platform);
    }

    private static VehicleTelemetryState MapTelemetry(F125CarTelemetryData data, sbyte suggestedGear)
    {
        return new(
            data.Speed,
            data.Throttle,
            data.Steer,
            data.Brake,
            data.Clutch,
            data.Gear,
            data.EngineRpm,
            data.Drs,
            data.RevLightsPercent,
            data.RevLightsBitValue,
            data.EngineTemperature,
            suggestedGear,
            ToVehicleWheelData(data.BrakesTemperature),
            ToVehicleWheelData(data.TyresSurfaceTemperature),
            ToVehicleWheelData(data.TyresInnerTemperature),
            ToVehicleWheelData(data.TyresPressure),
            ToVehicleWheelData(data.SurfaceType));
    }

    private static VehicleCarStatusState MapCarStatus(F125CarStatusData data)
    {
        return new(
            data.TractionControl,
            data.AntiLockBrakes,
            data.FuelMix,
            data.FrontBrakeBias,
            data.PitLimiterStatus,
            data.FuelInTank,
            data.FuelCapacity,
            data.FuelRemainingLaps,
            data.MaxRpm,
            data.IdleRpm,
            data.MaxGears,
            data.DrsAllowed,
            data.DrsActivationDistance,
            data.ActualTyreCompound,
            data.VisualTyreCompound,
            data.TyresAgeLaps,
            data.VehicleFiaFlags,
            data.EnginePowerIce,
            data.EnginePowerMguk,
            data.ErsStoreEnergy,
            data.ErsDeployMode,
            data.ErsHarvestedThisLapMguk,
            data.ErsHarvestedThisLapMguh,
            data.ErsDeployedThisLap,
            data.NetworkPaused);
    }

    private static VehicleDamageState MapDamage(F125CarDamageData data)
    {
        return new(
            ToVehicleWheelData(data.TyresWear),
            ToVehicleWheelData(data.TyresDamage),
            ToVehicleWheelData(data.BrakesDamage),
            ToVehicleWheelData(data.TyreBlisters),
            data.FrontLeftWingDamage,
            data.FrontRightWingDamage,
            data.RearWingDamage,
            data.FloorDamage,
            data.DiffuserDamage,
            data.SidepodDamage,
            data.DrsFault,
            data.ErsFault,
            data.GearBoxDamage,
            data.EngineDamage,
            data.EngineMguhWear,
            data.EngineEsWear,
            data.EngineCeWear,
            data.EngineIceWear,
            data.EngineMgukWear,
            data.EngineTcWear,
            data.EngineBlown,
            data.EngineSeized);
    }

    private static VehicleMotionExState MapMotionEx(F125MotionExPacketBody data)
    {
        return new(
            ToVehicleWheelData(data.SuspensionPosition),
            ToVehicleWheelData(data.SuspensionVelocity),
            ToVehicleWheelData(data.SuspensionAcceleration),
            ToVehicleWheelData(data.WheelSpeed),
            ToVehicleWheelData(data.WheelSlipRatio),
            ToVehicleWheelData(data.WheelSlipAngle),
            ToVehicleWheelData(data.WheelLatForce),
            ToVehicleWheelData(data.WheelLongForce),
            data.HeightOfCogAboveGround,
            data.LocalVelocityX,
            data.LocalVelocityY,
            data.LocalVelocityZ,
            data.AngularVelocityX,
            data.AngularVelocityY,
            data.AngularVelocityZ,
            data.AngularAccelerationX,
            data.AngularAccelerationY,
            data.AngularAccelerationZ,
            data.FrontWheelsAngle,
            ToVehicleWheelData(data.WheelVertForce),
            data.FrontAeroHeight,
            data.RearAeroHeight,
            data.FrontRollAngle,
            data.RearRollAngle,
            data.ChassisYaw,
            data.ChassisPitch,
            ToVehicleWheelData(data.WheelCamber),
            ToVehicleWheelData(data.WheelCamberGain));
    }

    private static VehicleEventState MapEvent(F125EventPacketBody data, byte playerCarIndex)
    {
        var (primaryVehicleIndex, secondaryVehicleIndex) = GetEventVehicleIndexes(data.EventDetails);

        return new(
            data.EventCode,
            data.EventCodeBytes.ToArray(),
            data.EventDetailsRaw.ToArray(),
            primaryVehicleIndex,
            secondaryVehicleIndex,
            primaryVehicleIndex == playerCarIndex || secondaryVehicleIndex == playerCarIndex);
    }

    private static (byte? Primary, byte? Secondary) GetEventVehicleIndexes(F125EventDetails details)
    {
        return details switch
        {
            F125FastestLapEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125RetirementEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125TeamMateInPitsEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125RaceWinnerEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125PenaltyEventDetails eventDetails => (eventDetails.VehicleIndex, eventDetails.OtherVehicleIndex),
            F125SpeedTrapEventDetails eventDetails => (eventDetails.VehicleIndex, eventDetails.FastestVehicleIndexInSession),
            F125DriveThroughPenaltyServedEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125StopGoPenaltyServedEventDetails eventDetails => (eventDetails.VehicleIndex, null),
            F125OvertakeEventDetails eventDetails => (eventDetails.OvertakingVehicleIndex, eventDetails.BeingOvertakenVehicleIndex),
            F125CollisionEventDetails eventDetails => (eventDetails.Vehicle1Index, eventDetails.Vehicle2Index),
            _ => (null, null)
        };
    }

    private static VehicleWheelData<T> ToVehicleWheelData<T>(F125WheelData<T> data)
    {
        return new(data.RearLeft, data.RearRight, data.FrontLeft, data.FrontRight);
    }
}

public enum F125VehicleStateUpdateStatus
{
    Applied,
    Ignored
}

public sealed record F125VehicleStateUpdateResult(
    F125VehicleStateUpdateStatus Status,
    VehicleState State,
    string Message,
    VehicleStateUpdatedSignals UpdatedSignals,
    VehicleStateResetReason ResetReason = VehicleStateResetReason.None)
{
    public bool WasApplied => Status == F125VehicleStateUpdateStatus.Applied;

    public bool WasIgnored => Status == F125VehicleStateUpdateStatus.Ignored;

    public static F125VehicleStateUpdateResult Applied(
        VehicleState state,
        string message,
        VehicleStateUpdatedSignals updatedSignals,
        VehicleStateResetReason resetReason = VehicleStateResetReason.None)
    {
        return new(F125VehicleStateUpdateStatus.Applied, state, message, updatedSignals, resetReason);
    }

    public static F125VehicleStateUpdateResult Ignored(
        VehicleState state,
        string message,
        VehicleStateUpdatedSignals updatedSignals = VehicleStateUpdatedSignals.None,
        VehicleStateResetReason resetReason = VehicleStateResetReason.None)
    {
        return new(F125VehicleStateUpdateStatus.Ignored, state, message, updatedSignals, resetReason);
    }
}
