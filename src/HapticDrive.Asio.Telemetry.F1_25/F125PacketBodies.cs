namespace HapticDrive.Asio.Telemetry.F1_25;

public abstract record F125PacketBody(F125PacketKind Kind);

public sealed record F125WheelData<T>(T RearLeft, T RearRight, T FrontLeft, T FrontRight)
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

public sealed record F125MotionPacketBody(
    IReadOnlyList<F125CarMotionData> CarMotionData)
    : F125PacketBody(F125PacketKind.Motion);

public sealed record F125CarMotionData(
    float WorldPositionX,
    float WorldPositionY,
    float WorldPositionZ,
    float WorldVelocityX,
    float WorldVelocityY,
    float WorldVelocityZ,
    short WorldForwardDirX,
    short WorldForwardDirY,
    short WorldForwardDirZ,
    short WorldRightDirX,
    short WorldRightDirY,
    short WorldRightDirZ,
    float GForceLateral,
    float GForceLongitudinal,
    float GForceVertical,
    float Yaw,
    float Pitch,
    float Roll);

public sealed record F125SessionPacketBody(
    byte Weather,
    sbyte TrackTemperature,
    sbyte AirTemperature,
    byte TotalLaps,
    ushort TrackLength,
    byte SessionType,
    sbyte TrackId,
    byte Formula,
    ushort SessionTimeLeft,
    ushort SessionDuration,
    byte PitSpeedLimit,
    byte GamePaused,
    byte IsSpectating,
    byte SpectatorCarIndex,
    byte SliProNativeSupport,
    byte NumMarshalZones,
    IReadOnlyList<F125MarshalZone> MarshalZones,
    byte SafetyCarStatus,
    byte NetworkGame,
    byte NumWeatherForecastSamples,
    IReadOnlyList<F125WeatherForecastSample> WeatherForecastSamples,
    byte ForecastAccuracy,
    byte AiDifficulty,
    uint SeasonLinkIdentifier,
    uint WeekendLinkIdentifier,
    uint SessionLinkIdentifier,
    byte PitStopWindowIdealLap,
    byte PitStopWindowLatestLap,
    byte PitStopRejoinPosition,
    byte SteeringAssist,
    byte BrakingAssist,
    byte GearboxAssist,
    byte PitAssist,
    byte PitReleaseAssist,
    byte ErsAssist,
    byte DrsAssist,
    byte DynamicRacingLine,
    byte DynamicRacingLineType,
    byte GameMode,
    byte RuleSet,
    uint TimeOfDay,
    byte SessionLength,
    byte SpeedUnitsLeadPlayer,
    byte TemperatureUnitsLeadPlayer,
    byte SpeedUnitsSecondaryPlayer,
    byte TemperatureUnitsSecondaryPlayer,
    byte NumSafetyCarPeriods,
    byte NumVirtualSafetyCarPeriods,
    byte NumRedFlagPeriods,
    byte EqualCarPerformance,
    byte RecoveryMode,
    byte FlashbackLimit,
    byte SurfaceType,
    byte LowFuelMode,
    byte RaceStarts,
    byte TyreTemperature,
    byte PitLaneTyreSim,
    byte CarDamage,
    byte CarDamageRate,
    byte Collisions,
    byte CollisionsOffForFirstLapOnly,
    byte MpUnsafePitRelease,
    byte MpOffForGriefing,
    byte CornerCuttingStringency,
    byte ParcFermeRules,
    byte PitStopExperience,
    byte SafetyCar,
    byte SafetyCarExperience,
    byte FormationLap,
    byte FormationLapExperience,
    byte RedFlags,
    byte AffectsLicenceLevelSolo,
    byte AffectsLicenceLevelMp,
    byte NumSessionsInWeekend,
    IReadOnlyList<byte> WeekendStructure,
    float Sector2LapDistanceStart,
    float Sector3LapDistanceStart)
    : F125PacketBody(F125PacketKind.Session);

public sealed record F125MarshalZone(float ZoneStart, sbyte ZoneFlag);

public sealed record F125WeatherForecastSample(
    byte SessionType,
    byte TimeOffset,
    byte Weather,
    sbyte TrackTemperature,
    sbyte TrackTemperatureChange,
    sbyte AirTemperature,
    sbyte AirTemperatureChange,
    byte RainPercentage);

public sealed record F125LapDataPacketBody(
    IReadOnlyList<F125LapData> LapData,
    byte TimeTrialPbCarIndex,
    byte TimeTrialRivalCarIndex)
    : F125PacketBody(F125PacketKind.LapData);

public sealed record F125LapData(
    uint LastLapTimeInMs,
    uint CurrentLapTimeInMs,
    ushort Sector1TimeMsPart,
    byte Sector1TimeMinutesPart,
    ushort Sector2TimeMsPart,
    byte Sector2TimeMinutesPart,
    ushort DeltaToCarInFrontMsPart,
    byte DeltaToCarInFrontMinutesPart,
    ushort DeltaToRaceLeaderMsPart,
    byte DeltaToRaceLeaderMinutesPart,
    float LapDistance,
    float TotalDistance,
    float SafetyCarDelta,
    byte CarPosition,
    byte CurrentLapNum,
    byte PitStatus,
    byte NumPitStops,
    byte Sector,
    byte CurrentLapInvalid,
    byte Penalties,
    byte TotalWarnings,
    byte CornerCuttingWarnings,
    byte NumUnservedDriveThroughPens,
    byte NumUnservedStopGoPens,
    byte GridPosition,
    byte DriverStatus,
    byte ResultStatus,
    byte PitLaneTimerActive,
    ushort PitLaneTimeInLaneInMs,
    ushort PitStopTimerInMs,
    byte PitStopShouldServePen,
    float SpeedTrapFastestSpeed,
    byte SpeedTrapFastestLap);

public sealed record F125EventPacketBody(
    string EventCode,
    IReadOnlyList<byte> EventCodeBytes,
    F125EventDetails EventDetails,
    IReadOnlyList<byte> EventDetailsRaw)
    : F125PacketBody(F125PacketKind.Event);

public abstract record F125EventDetails(string EventCode);

public sealed record F125EmptyEventDetails(string Code) : F125EventDetails(Code);

public sealed record F125UnknownEventDetails(string Code) : F125EventDetails(Code);

public sealed record F125FastestLapEventDetails(byte VehicleIndex, float LapTime)
    : F125EventDetails("FTLP");

public sealed record F125RetirementEventDetails(byte VehicleIndex, byte Reason)
    : F125EventDetails("RTMT");

public sealed record F125DrsDisabledEventDetails(byte Reason)
    : F125EventDetails("DRSD");

public sealed record F125TeamMateInPitsEventDetails(byte VehicleIndex)
    : F125EventDetails("TMPT");

public sealed record F125RaceWinnerEventDetails(byte VehicleIndex)
    : F125EventDetails("RCWN");

public sealed record F125PenaltyEventDetails(
    byte PenaltyType,
    byte InfringementType,
    byte VehicleIndex,
    byte OtherVehicleIndex,
    byte Time,
    byte LapNum,
    byte PlacesGained)
    : F125EventDetails("PENA");

public sealed record F125SpeedTrapEventDetails(
    byte VehicleIndex,
    float Speed,
    byte IsOverallFastestInSession,
    byte IsDriverFastestInSession,
    byte FastestVehicleIndexInSession,
    float FastestSpeedInSession)
    : F125EventDetails("SPTP");

public sealed record F125StartLightsEventDetails(byte NumLights)
    : F125EventDetails("STLG");

public sealed record F125DriveThroughPenaltyServedEventDetails(byte VehicleIndex)
    : F125EventDetails("DTSV");

public sealed record F125StopGoPenaltyServedEventDetails(byte VehicleIndex, float StopTime)
    : F125EventDetails("SGSV");

public sealed record F125FlashbackEventDetails(uint FlashbackFrameIdentifier, float FlashbackSessionTime)
    : F125EventDetails("FLBK");

public sealed record F125ButtonsEventDetails(uint ButtonStatus)
    : F125EventDetails("BUTN");

public sealed record F125OvertakeEventDetails(byte OvertakingVehicleIndex, byte BeingOvertakenVehicleIndex)
    : F125EventDetails("OVTK");

public sealed record F125SafetyCarEventDetails(byte SafetyCarType, byte EventType)
    : F125EventDetails("SCAR");

public sealed record F125CollisionEventDetails(byte Vehicle1Index, byte Vehicle2Index)
    : F125EventDetails("COLL");

public sealed record F125ParticipantsPacketBody(
    byte NumActiveCars,
    IReadOnlyList<F125ParticipantData> Participants)
    : F125PacketBody(F125PacketKind.Participants);

public sealed record F125LiveryColour(byte Red, byte Green, byte Blue);

public sealed record F125ParticipantData(
    byte AiControlled,
    byte DriverId,
    byte NetworkId,
    byte TeamId,
    byte MyTeam,
    byte RaceNumber,
    byte Nationality,
    IReadOnlyList<byte> NameBytes,
    string Name,
    byte YourTelemetry,
    byte ShowOnlineNames,
    ushort TechLevel,
    byte Platform,
    byte NumColours,
    IReadOnlyList<F125LiveryColour> LiveryColours);

public sealed record F125CarTelemetryPacketBody(
    IReadOnlyList<F125CarTelemetryData> CarTelemetryData,
    byte MfdPanelIndex,
    byte MfdPanelIndexSecondaryPlayer,
    sbyte SuggestedGear)
    : F125PacketBody(F125PacketKind.CarTelemetry);

public sealed record F125CarTelemetryData(
    ushort Speed,
    float Throttle,
    float Steer,
    float Brake,
    byte Clutch,
    sbyte Gear,
    ushort EngineRpm,
    byte Drs,
    byte RevLightsPercent,
    ushort RevLightsBitValue,
    F125WheelData<ushort> BrakesTemperature,
    F125WheelData<byte> TyresSurfaceTemperature,
    F125WheelData<byte> TyresInnerTemperature,
    ushort EngineTemperature,
    F125WheelData<float> TyresPressure,
    F125WheelData<byte> SurfaceType);

public sealed record F125CarStatusPacketBody(
    IReadOnlyList<F125CarStatusData> CarStatusData)
    : F125PacketBody(F125PacketKind.CarStatus);

public sealed record F125CarStatusData(
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
    float EnginePowerIce,
    float EnginePowerMguk,
    float ErsStoreEnergy,
    byte ErsDeployMode,
    float ErsHarvestedThisLapMguk,
    float ErsHarvestedThisLapMguh,
    float ErsDeployedThisLap,
    byte NetworkPaused);

public sealed record F125CarDamagePacketBody(
    IReadOnlyList<F125CarDamageData> CarDamageData)
    : F125PacketBody(F125PacketKind.CarDamage);

public sealed record F125CarDamageData(
    F125WheelData<float> TyresWear,
    F125WheelData<byte> TyresDamage,
    F125WheelData<byte> BrakesDamage,
    F125WheelData<byte> TyreBlisters,
    byte FrontLeftWingDamage,
    byte FrontRightWingDamage,
    byte RearWingDamage,
    byte FloorDamage,
    byte DiffuserDamage,
    byte SidepodDamage,
    byte DrsFault,
    byte ErsFault,
    byte GearBoxDamage,
    byte EngineDamage,
    byte EngineMguhWear,
    byte EngineEsWear,
    byte EngineCeWear,
    byte EngineIceWear,
    byte EngineMgukWear,
    byte EngineTcWear,
    byte EngineBlown,
    byte EngineSeized);

public sealed record F125MotionExPacketBody(
    F125WheelData<float> SuspensionPosition,
    F125WheelData<float> SuspensionVelocity,
    F125WheelData<float> SuspensionAcceleration,
    F125WheelData<float> WheelSpeed,
    F125WheelData<float> WheelSlipRatio,
    F125WheelData<float> WheelSlipAngle,
    F125WheelData<float> WheelLatForce,
    F125WheelData<float> WheelLongForce,
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
    float FrontWheelsAngle,
    F125WheelData<float> WheelVertForce,
    float FrontAeroHeight,
    float RearAeroHeight,
    float FrontRollAngle,
    float RearRollAngle,
    float ChassisYaw,
    float ChassisPitch,
    F125WheelData<float> WheelCamber,
    F125WheelData<float> WheelCamberGain)
    : F125PacketBody(F125PacketKind.MotionEx);
