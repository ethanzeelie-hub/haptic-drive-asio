namespace HapticDrive.Asio.Core.Vehicle;

public sealed record VehicleState(
    VehicleStateFrame Frame,
    VehicleStateSample<VehicleMotionState>? Motion,
    VehicleStateSample<VehicleSessionState>? Session,
    VehicleStateSample<VehicleLapState>? Lap,
    VehicleStateSample<VehicleParticipantState>? Participant,
    VehicleStateSample<VehicleTelemetryState>? Telemetry,
    VehicleStateSample<VehicleCarStatusState>? CarStatus,
    VehicleStateSample<VehicleDamageState>? Damage,
    VehicleStateSample<VehicleMotionExState>? MotionEx,
    VehicleStateSample<VehicleEventState>? LastEvent)
{
    public static VehicleState Empty { get; } = new(
        VehicleStateFrame.Empty,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}

public sealed record VehicleStateFrame(
    ulong? SessionUid,
    float? SessionTime,
    uint? FrameIdentifier,
    uint? OverallFrameIdentifier,
    byte? PlayerCarIndex,
    string? Source)
{
    public static VehicleStateFrame Empty { get; } = new(null, null, null, null, null, null);
}

public sealed record VehicleStateStamp(
    string Source,
    ulong SessionUid,
    float SessionTime,
    uint FrameIdentifier,
    uint OverallFrameIdentifier,
    byte PlayerCarIndex);

public sealed record VehicleStateSample<T>(T Value, VehicleStateStamp Stamp);

public sealed record VehicleWheelData<T>(T RearLeft, T RearRight, T FrontLeft, T FrontRight)
{
    public T this[int index] => index switch
    {
        0 => RearLeft,
        1 => RearRight,
        2 => FrontLeft,
        3 => FrontRight,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public IReadOnlyList<T> ToList()
    {
        return [RearLeft, RearRight, FrontLeft, FrontRight];
    }
}

public sealed record VehicleMotionState(
    float WorldPositionX,
    float WorldPositionY,
    float WorldPositionZ,
    float WorldVelocityX,
    float WorldVelocityY,
    float WorldVelocityZ,
    float GForceLateral,
    float GForceLongitudinal,
    float GForceVertical,
    float Yaw,
    float Pitch,
    float Roll);

public sealed record VehicleSessionState(
    byte Weather,
    sbyte TrackTemperatureCelsius,
    sbyte AirTemperatureCelsius,
    byte TotalLaps,
    ushort TrackLengthMeters,
    byte SessionType,
    sbyte TrackId,
    byte GamePaused,
    byte SafetyCarStatus,
    byte NetworkGame,
    byte GameMode);

public sealed record VehicleLapState(
    uint LastLapTimeInMs,
    uint CurrentLapTimeInMs,
    float LapDistanceMeters,
    float TotalDistanceMeters,
    byte CarPosition,
    byte CurrentLapNumber,
    byte PitStatus,
    byte Sector,
    byte DriverStatus,
    byte ResultStatus,
    byte CurrentLapInvalid);

public sealed record VehicleParticipantState(
    byte AiControlled,
    byte DriverId,
    byte TeamId,
    byte RaceNumber,
    string Name,
    byte YourTelemetry,
    ushort TechLevel,
    byte Platform);

public sealed record VehicleTelemetryState(
    ushort SpeedKph,
    float Throttle,
    float Steer,
    float Brake,
    byte Clutch,
    sbyte Gear,
    ushort EngineRpm,
    byte Drs,
    byte RevLightsPercent,
    ushort RevLightsBitValue,
    ushort EngineTemperatureCelsius,
    sbyte SuggestedGear,
    VehicleWheelData<ushort> BrakeTemperatureCelsius,
    VehicleWheelData<byte> TyreSurfaceTemperatureCelsius,
    VehicleWheelData<byte> TyreInnerTemperatureCelsius,
    VehicleWheelData<float> TyrePressurePsi,
    VehicleWheelData<byte> SurfaceTypeIds);

public sealed record VehicleCarStatusState(
    byte TractionControl,
    byte AntiLockBrakes,
    byte FuelMix,
    byte FrontBrakeBias,
    byte PitLimiterStatus,
    float FuelInTank,
    float FuelCapacity,
    float FuelRemainingLaps,
    ushort MaxRpm,
    ushort IdleRpm,
    byte MaxGears,
    byte DrsAllowed,
    ushort DrsActivationDistance,
    byte ActualTyreCompound,
    byte VisualTyreCompound,
    byte TyresAgeLaps,
    sbyte VehicleFiaFlags,
    float EnginePowerIceWatts,
    float EnginePowerMgukWatts,
    float ErsStoreEnergyJoules,
    byte ErsDeployMode,
    float ErsHarvestedThisLapMgukJoules,
    float ErsHarvestedThisLapMguhJoules,
    float ErsDeployedThisLapJoules,
    byte NetworkPaused);

public sealed record VehicleDamageState(
    VehicleWheelData<float> TyreWearPercent,
    VehicleWheelData<byte> TyreDamagePercent,
    VehicleWheelData<byte> BrakeDamagePercent,
    VehicleWheelData<byte> TyreBlistersPercent,
    byte FrontLeftWingDamagePercent,
    byte FrontRightWingDamagePercent,
    byte RearWingDamagePercent,
    byte FloorDamagePercent,
    byte DiffuserDamagePercent,
    byte SidepodDamagePercent,
    byte DrsFault,
    byte ErsFault,
    byte GearboxDamagePercent,
    byte EngineDamagePercent,
    byte EngineMguhWearPercent,
    byte EngineEsWearPercent,
    byte EngineCeWearPercent,
    byte EngineIceWearPercent,
    byte EngineMgukWearPercent,
    byte EngineTcWearPercent,
    byte EngineBlown,
    byte EngineSeized);

public sealed record VehicleMotionExState(
    VehicleWheelData<float> SuspensionPosition,
    VehicleWheelData<float> SuspensionVelocity,
    VehicleWheelData<float> SuspensionAcceleration,
    VehicleWheelData<float> WheelSpeed,
    VehicleWheelData<float> WheelSlipRatio,
    VehicleWheelData<float> WheelSlipAngle,
    VehicleWheelData<float> WheelLatForce,
    VehicleWheelData<float> WheelLongForce,
    float HeightOfCogAboveGround,
    float LocalVelocityX,
    float LocalVelocityY,
    float LocalVelocityZ,
    float AngularVelocityX,
    float AngularVelocityY,
    float AngularVelocityZ,
    float AngularAccelerationX,
    float AngularAccelerationY,
    float AngularAccelerationZ,
    float FrontWheelsAngleRadians,
    VehicleWheelData<float> WheelVertForce,
    float FrontAeroHeight,
    float RearAeroHeight,
    float FrontRollAngle,
    float RearRollAngle,
    float ChassisYaw,
    float ChassisPitch,
    VehicleWheelData<float> WheelCamber,
    VehicleWheelData<float> WheelCamberGain);

public sealed record VehicleEventState(
    string EventCode,
    IReadOnlyList<byte> EventCodeBytes,
    IReadOnlyList<byte> EventDetailsRaw,
    byte? PrimaryVehicleIndex,
    byte? SecondaryVehicleIndex,
    bool InvolvesPlayer);
