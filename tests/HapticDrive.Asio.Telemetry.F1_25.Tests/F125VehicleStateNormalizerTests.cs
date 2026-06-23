using System.Net;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Telemetry.F1_25.Tests;

public sealed class F125VehicleStateNormalizerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapsVerifiedSurfaceIds()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 1, receivedAt: BaseTime, receivedTimestamp: 10);

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.Equal(SurfaceKind.RumbleStrip, frame.Signals.SurfaceKinds!.FrontLeft);
        Assert.Equal(SurfaceKind.RumbleStrip, frame.Signals.SurfaceKinds.RearRight);
    }

    [Fact]
    public void UnknownSurfaceIdMapsToUnknown()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 99, receivedAt: BaseTime, receivedTimestamp: 10);

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.Equal(SurfaceKind.Unknown, frame.Signals.SurfaceKinds!.FrontLeft);
    }

    [Fact]
    public void DoesNotMarkDrivingOutputAllowedWhenTelemetryStale()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10);
        var staleNow = BaseTime + TimeSpan.FromSeconds(2);

        var frame = normalizer.Normalize(
            state,
            staleNow,
            10 + (2 * TimeSpan.TicksPerSecond),
            TimeProvider.System,
            TelemetryFreshnessPolicy.Default);

        Assert.False(frame.Context.AllowsDrivingOutput);
    }

    [Fact]
    public void Normalizer_MissingLapDataIsNotPlayerControlled()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10) with
        {
            Lap = null
        };

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(frame.Context.IsPlayerControlled);
        Assert.False(frame.Context.AllowsDrivingOutput);
        Assert.Equal(DrivingPhase.Unknown, frame.Context.DrivingPhase);
    }

    [Fact]
    public void Normalizer_StaleLapDataIsNotPlayerControlled()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10) with
        {
            Lap = new VehicleStateSample<VehicleLapState>(
                CreateLapState(),
                CreateStamp("Lap Data", BaseTime.AddSeconds(-2), 0))
        };

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(frame.Context.IsPlayerControlled);
        Assert.False(frame.Context.AllowsDrivingOutput);
    }

    [Fact]
    public void Normalizer_ActiveDrivingRequiresFreshSessionLapParticipantTelemetry()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10) with
        {
            Participant = new VehicleStateSample<VehicleParticipantState>(
                CreateParticipantState(),
                CreateStamp("Participants", BaseTime.AddSeconds(-3), 0))
        };

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(frame.Context.IsPlayerControlled);
        Assert.False(frame.Context.AllowsDrivingOutput);
        var freshness = HapticFrameFreshnessEvaluator.Evaluate(frame, TimeProvider.System, TelemetryFreshnessPolicy.Default);
        Assert.False(freshness.Participant.IsFresh);
    }

    [Fact]
    public void Normalizer_ZeroSpeedAllowedOnlyWhenActiveDrivingContextFresh()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var freshState = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10, speedKph: 0);
        var staleParticipantState = freshState with
        {
            Participant = new VehicleStateSample<VehicleParticipantState>(
                CreateParticipantState(),
                CreateStamp("Participants", BaseTime.AddSeconds(-3), 0))
        };

        var freshFrame = normalizer.Normalize(freshState, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);
        var staleFrame = normalizer.Normalize(staleParticipantState, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.True(freshFrame.Context.AllowsDrivingOutput);
        Assert.False(staleFrame.Context.AllowsDrivingOutput);
    }

    [Fact]
    public void Normalizer_MapsMaxGearsTcAbsMotionExAndEventIntoCanonicalFrame()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(
            surfaceTypeId: 3,
            receivedAt: BaseTime,
            receivedTimestamp: 10,
            tractionControl: 1,
            antiLockBrakes: 1,
            wheelSlipRatio: Wheels(11f, 22f, 33f, 44f),
            wheelSlipAngle: Wheels(0.11f, 0.22f, 0.33f, 0.44f),
            wheelSpeed: Wheels(1f, 2f, 3f, 4f),
            suspensionAcceleration: Wheels(5f, 6f, 7f, 8f),
            wheelVertForce: Wheels(9f, 10f, 11f, 12f),
            eventCode: "COLL",
            eventInvolvesPlayer: true);

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.Equal(8, frame.Signals.MaxGears);
        Assert.True(frame.Signals.TractionControlActive);
        Assert.True(frame.Signals.AntiLockBrakesActive);
        Assert.Equal(0.11f, frame.Signals.TyreSlipAngle!.RearLeft);
        Assert.Equal(0.22f, frame.Signals.TyreSlipAngle.RearRight);
        Assert.Equal(0.33f, frame.Signals.TyreSlipAngle.FrontLeft);
        Assert.Equal(0.44f, frame.Signals.TyreSlipAngle.FrontRight);
        Assert.Equal(1f, frame.Signals.WheelSpeedMetersPerSecond!.RearLeft);
        Assert.Equal(8f, frame.Signals.SuspensionAcceleration!.FrontRight);
        Assert.Equal(9f, frame.Signals.WheelVerticalForce!.RearLeft);
        Assert.Equal(1.25f, frame.Signals.VerticalG);
        Assert.Equal(HapticEventKind.Collision, frame.Signals.Event!.Kind);
        Assert.True(frame.Signals.Event.InvolvesPlayer);
        Assert.Equal((byte)4, frame.Signals.Event.OtherVehicleIndex);
    }

    [Fact]
    public void Normalizer_UsesTypedSignalStampsWithoutStringDictionary()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(surfaceTypeId: 0, receivedAt: BaseTime, receivedTimestamp: 10);

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);
        var normalizerSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.Telemetry.F1_25",
            "F125VehicleStateNormalizer.cs"));

        Assert.NotNull(frame.SignalStamps.Telemetry);
        Assert.NotNull(frame.SignalStamps.Session);
        Assert.NotNull(frame.SignalStamps.Lap);
        Assert.NotNull(frame.SignalStamps.Participant);
        Assert.NotNull(frame.SignalStamps.CarStatus);
        Assert.Equal(typeof(HapticFrameSignalStamps), typeof(HapticFrame).GetProperty(nameof(HapticFrame.SignalStamps))!.PropertyType);
        Assert.DoesNotContain("new Dictionary<string, VehicleSignalFreshness>", normalizerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CollisionEvent_MapsToHapticEventSignals()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(
            surfaceTypeId: 0,
            receivedAt: BaseTime,
            receivedTimestamp: 10,
            eventCode: "COLL",
            eventInvolvesPlayer: true);

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.NotNull(frame.Signals.Event);
        Assert.Equal(HapticEventKind.Collision, frame.Signals.Event!.Kind);
        Assert.True(frame.Signals.Event.InvolvesPlayer);
        Assert.Equal((byte)4, frame.Signals.Event.OtherVehicleIndex);
    }

    [Fact]
    public void WheelOrder_RemainsRlRrFlFr()
    {
        var normalizer = new F125VehicleStateNormalizer();
        var state = CreateState(
            surfaceTypeId: 0,
            receivedAt: BaseTime,
            receivedTimestamp: 10,
            surfaceTypeIds: Wheels((byte)10, (byte)11, (byte)1, (byte)7),
            wheelSlipRatio: Wheels(10f, 20f, 30f, 40f),
            wheelSpeed: Wheels(100f, 200f, 300f, 400f));

        var frame = normalizer.Normalize(state, BaseTime, 10, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.Equal((byte)10, frame.Signals.SurfaceTypeIds!.RearLeft);
        Assert.Equal((byte)11, frame.Signals.SurfaceTypeIds.RearRight);
        Assert.Equal((byte)1, frame.Signals.SurfaceTypeIds.FrontLeft);
        Assert.Equal((byte)7, frame.Signals.SurfaceTypeIds.FrontRight);
        Assert.Equal(10f, frame.Signals.TyreSlip!.RearLeft);
        Assert.Equal(20f, frame.Signals.TyreSlip.RearRight);
        Assert.Equal(30f, frame.Signals.TyreSlip.FrontLeft);
        Assert.Equal(40f, frame.Signals.TyreSlip.FrontRight);
        Assert.Equal(100f, frame.Signals.WheelSpeedMetersPerSecond!.RearLeft);
        Assert.Equal(400f, frame.Signals.WheelSpeedMetersPerSecond.FrontRight);
    }

    private static VehicleState CreateState(
        byte surfaceTypeId,
        DateTimeOffset receivedAt,
        long receivedTimestamp,
        ushort speedKph = 120,
        byte tractionControl = 0,
        byte antiLockBrakes = 0,
        VehicleWheelData<byte>? surfaceTypeIds = null,
        VehicleWheelData<float>? wheelSlipRatio = null,
        VehicleWheelData<float>? wheelSlipAngle = null,
        VehicleWheelData<float>? wheelSpeed = null,
        VehicleWheelData<float>? suspensionAcceleration = null,
        VehicleWheelData<float>? wheelVertForce = null,
        string? eventCode = null,
        bool eventInvolvesPlayer = false)
    {
        var sourceIdentity = TelemetrySourceIdentity.Create(
            new HapticDrive.Asio.Core.Games.GameIntegrationId("f1-25"),
            new IPEndPoint(IPAddress.Loopback, 20_778),
            42,
            0,
            0);
        var stamp = CreateStamp("Car Telemetry", receivedAt, receivedTimestamp, sourceIdentity);
        var surfaces = surfaceTypeIds ?? Wheels(surfaceTypeId, surfaceTypeId, surfaceTypeId, surfaceTypeId);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, 5f, 10, 10, 0, "Car Telemetry")
            {
                SourceIdentity = sourceIdentity
            },
            Motion = new VehicleStateSample<VehicleMotionState>(
                new VehicleMotionState(
                    WorldPositionX: 0f,
                    WorldPositionY: 0f,
                    WorldPositionZ: 0f,
                    WorldVelocityX: 0f,
                    WorldVelocityY: 0f,
                    WorldVelocityZ: 0f,
                    GForceLateral: 0f,
                    GForceLongitudinal: 0f,
                    GForceVertical: 1.25f,
                    Yaw: 0f,
                    Pitch: 0f,
                    Roll: 0f),
                stamp),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(
                    Weather: 0,
                    TrackTemperatureCelsius: 0,
                    AirTemperatureCelsius: 0,
                    TotalLaps: 10,
                    TrackLengthMeters: 0,
                    SessionType: 0,
                    TrackId: 0,
                    GamePaused: 0,
                    SafetyCarStatus: 0,
                    NetworkGame: 0,
                    GameMode: 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                CreateLapState(),
                CreateStamp("Lap Data", receivedAt, receivedTimestamp, sourceIdentity)),
            Participant = new VehicleStateSample<VehicleParticipantState>(
                CreateParticipantState(),
                CreateStamp("Participants", receivedAt, receivedTimestamp, sourceIdentity)),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: speedKph,
                    Throttle: 0.7f,
                    Steer: 0.2f,
                    Brake: 0.1f,
                    Clutch: 0,
                    Gear: 6,
                    EngineRpm: 11000,
                    Drs: 0,
                    RevLightsPercent: 0,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 100,
                    SuggestedGear: 6,
                    BrakeTemperatureCelsius: new VehicleWheelData<ushort>(400, 400, 400, 400),
                    TyreSurfaceTemperatureCelsius: new VehicleWheelData<byte>(90, 90, 90, 90),
                    TyreInnerTemperatureCelsius: new VehicleWheelData<byte>(90, 90, 90, 90),
                    TyrePressurePsi: new VehicleWheelData<float>(20, 20, 20, 20),
                    SurfaceTypeIds: surfaces),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: tractionControl,
                    AntiLockBrakes: antiLockBrakes,
                    FuelMix: 0,
                    FrontBrakeBias: 55,
                    PitLimiterStatus: 0,
                    FuelInTank: 0,
                    FuelCapacity: 0,
                    FuelRemainingLaps: 0,
                    MaxRpm: 12000,
                    IdleRpm: 3000,
                    MaxGears: 8,
                    DrsAllowed: 0,
                    DrsActivationDistance: 0,
                    ActualTyreCompound: 0,
                    VisualTyreCompound: 0,
                    TyresAgeLaps: 0,
                    VehicleFiaFlags: 0,
                    EnginePowerIceWatts: 0,
                    EnginePowerMgukWatts: 0,
                    ErsStoreEnergyJoules: 0,
                    ErsDeployMode: 0,
                    ErsHarvestedThisLapMgukJoules: 0,
                    ErsHarvestedThisLapMguhJoules: 0,
                    ErsDeployedThisLapJoules: 0,
                    NetworkPaused: 0),
                stamp),
            MotionEx = new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    SuspensionPosition: Wheels(0f, 0f, 0f, 0f),
                    SuspensionVelocity: Wheels(0f, 0f, 0f, 0f),
                    SuspensionAcceleration: suspensionAcceleration ?? Wheels(0f, 0f, 0f, 0f),
                    WheelSpeed: wheelSpeed ?? Wheels(0f, 0f, 0f, 0f),
                    WheelSlipRatio: wheelSlipRatio ?? Wheels(0f, 0f, 0f, 0f),
                    WheelSlipAngle: wheelSlipAngle ?? Wheels(0f, 0f, 0f, 0f),
                    WheelLatForce: Wheels(0f, 0f, 0f, 0f),
                    WheelLongForce: Wheels(0f, 0f, 0f, 0f),
                    HeightOfCogAboveGround: 0f,
                    LocalVelocityX: 0f,
                    LocalVelocityY: 0f,
                    LocalVelocityZ: 0f,
                    AngularVelocityX: 0f,
                    AngularVelocityY: 0f,
                    AngularVelocityZ: 0f,
                    AngularAccelerationX: 0f,
                    AngularAccelerationY: 0f,
                    AngularAccelerationZ: 0f,
                    FrontWheelsAngleRadians: 0f,
                    WheelVertForce: wheelVertForce ?? Wheels(0f, 0f, 0f, 0f),
                    FrontAeroHeight: 0f,
                    RearAeroHeight: 0f,
                    FrontRollAngle: 0f,
                    RearRollAngle: 0f,
                    ChassisYaw: 0f,
                    ChassisPitch: 0f,
                    WheelCamber: Wheels(0f, 0f, 0f, 0f),
                    WheelCamberGain: Wheels(0f, 0f, 0f, 0f)),
                stamp),
            LastEvent = eventCode is null
                ? null
                : new VehicleStateSample<VehicleEventState>(
                    new VehicleEventState(
                        eventCode,
                        eventCode.Select(character => (byte)character).ToArray(),
                        Array.Empty<byte>(),
                        eventInvolvesPlayer ? (byte)0 : (byte)1,
                        eventInvolvesPlayer ? (byte)4 : (byte)5,
                        eventInvolvesPlayer),
                    stamp)
        };
    }

    private static VehicleStateStamp CreateStamp(
        string source,
        DateTimeOffset receivedAt,
        long receivedTimestamp,
        TelemetrySourceIdentity? sourceIdentity = null)
    {
        sourceIdentity ??= TelemetrySourceIdentity.Create(
            new HapticDrive.Asio.Core.Games.GameIntegrationId("f1-25"),
            new IPEndPoint(IPAddress.Loopback, 20_778),
            42,
            0,
            0);

        return new VehicleStateStamp(source, 42, 5f, 10, 10, 0, receivedAt, receivedTimestamp)
        {
            SourceIdentity = sourceIdentity,
            PacketKind = new HapticDrive.Asio.Core.Games.TelemetryPacketKind(0, source)
        };
    }

    private static VehicleLapState CreateLapState()
    {
        return new VehicleLapState(
            LastLapTimeInMs: 0,
            CurrentLapTimeInMs: 0,
            LapDistanceMeters: 100f,
            TotalDistanceMeters: 100f,
            CarPosition: 0,
            CurrentLapNumber: 1,
            PitStatus: 0,
            Sector: 0,
            DriverStatus: 1,
            ResultStatus: 2,
            CurrentLapInvalid: 0);
    }

    private static VehicleParticipantState CreateParticipantState()
    {
        return new VehicleParticipantState(
            AiControlled: 0,
            DriverId: 0,
            TeamId: 0,
            RaceNumber: 1,
            Name: "Player",
            YourTelemetry: 1,
            TechLevel: 0,
            Platform: 0);
    }

    private static VehicleWheelData<T> Wheels<T>(T rearLeft, T rearRight, T frontLeft, T frontRight)
    {
        return new VehicleWheelData<T>(rearLeft, rearRight, frontLeft, frontRight);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
