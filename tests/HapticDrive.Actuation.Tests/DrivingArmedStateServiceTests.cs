using System.Net;
using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Actuation.Tests;

public sealed class DrivingArmedStateServiceTests
{
    [Fact]
    public void Service_DefaultsToNotArmedUntilTelemetryArrives()
    {
        var service = new DrivingArmedStateService();

        var snapshot = service.GetSnapshot();

        Assert.False(service.Current.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.NoTelemetry, snapshot.LastSuppressionReason);
    }

    [Fact]
    public void ActiveFreshTelemetry_ArmsDrivingGate()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext());

        Assert.True(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.None, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void DrivingArmed_MissingRequiredContextFailsClosed()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState() with { Participant = null },
            FreshContext());

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.NoTelemetry, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void PausedGame_SuppressesDrivingGate()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(gamePaused: 1),
            FreshContext());

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.Paused, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void NetworkPause_SuppressesDrivingGate()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(networkPaused: 1),
            FreshContext());

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.Paused, service.GetSnapshot().LastSuppressionReason);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    public void GarageOrInactiveResult_SuppressesDrivingGate(byte driverStatus, byte resultStatus)
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(driverStatus: driverStatus, resultStatus: resultStatus),
            FreshContext());

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.GarageMenuOrResultState, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void StaleTelemetry_SuppressesDrivingGate()
    {
        var service = new DrivingArmedStateService(new DrivingArmedStateServiceOptions
        {
            TelemetryFreshnessThreshold = TimeSpan.FromMilliseconds(100)
        });

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext() with { TelemetryAge = TimeSpan.FromMilliseconds(101) });

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.StaleTelemetry, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void HapticsStopped_SuppressesDrivingGate()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext() with { HapticsRunning = false });

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.HapticsStopped, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void EmergencyMute_SuppressesDrivingGate()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext() with { EmergencyMute = true });

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.EmergencyMute, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void ZeroSpeedActiveDriving_IsAllowedByDefault()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(speedKph: 0, rpm: 7_000, gear: 1),
            FreshContext());

        Assert.True(state.IsArmed);
    }

    [Fact]
    public void ZeroSpeedInactiveVehicle_CanBeSuppressedByOption()
    {
        var service = new DrivingArmedStateService(new DrivingArmedStateServiceOptions
        {
            AllowZeroSpeedActiveDriving = false
        });

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(speedKph: 0, rpm: 0, gear: 0),
            FreshContext());

        Assert.False(state.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.NotMovingAndNotActive, service.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public void MenuSafeModeDisabled_ArmsFromFreshTelemetryOnly()
    {
        var service = new DrivingArmedStateService(new DrivingArmedStateServiceOptions
        {
            MenuSafeModeEnabled = false
        });

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext());

        Assert.True(state.IsArmed);
        Assert.Contains("fresh", state.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateFromVehicleState_UsesProvidedTelemetryAge()
    {
        var service = new DrivingArmedStateService();

        var state = service.UpdateFromVehicleState(
            CreateVehicleState(),
            FreshContext() with { TelemetryAge = TimeSpan.FromMilliseconds(16) },
            DateTimeOffset.UtcNow);

        Assert.True(state.IsArmed);
        Assert.Equal(TimeSpan.FromMilliseconds(16), service.GetSnapshot().LastTelemetryAge);
    }

    [Fact]
    public void StateChangedEvent_FiresWhenGateChanges()
    {
        var service = new DrivingArmedStateService();
        var changes = new List<bool>();
        service.DrivingArmedChanged += (_, state) => changes.Add(state.IsArmed);

        service.UpdateFromVehicleState(CreateVehicleState(), FreshContext());
        service.UpdateFromVehicleState(CreateVehicleState(gamePaused: 1), FreshContext());

        Assert.Equal([true, false], changes);
    }

    private static DrivingArmedEvaluationContext FreshContext()
    {
        return new DrivingArmedEvaluationContext
        {
            HapticsRunning = true,
            EmergencyMute = false,
            HasRecentTelemetry = true,
            LastVehicleStateUpdateAtUtc = DateTimeOffset.UtcNow,
            TelemetryAge = TimeSpan.FromMilliseconds(16),
            TelemetryTimedOutMuted = false
        };
    }

    private static VehicleState CreateVehicleState(
        ushort speedKph = 90,
        ushort rpm = 9_500,
        sbyte gear = 4,
        byte gamePaused = 0,
        byte networkPaused = 0,
        byte driverStatus = 4,
        byte resultStatus = 2)
    {
        var sourceIdentity = TelemetrySourceIdentity.Create(
            new GameIntegrationId("f1-25"),
            new IPEndPoint(IPAddress.Loopback, 20_778),
            123,
            0,
            0);
        var receivedAtUtc = DateTimeOffset.UtcNow;
        var stamp = new VehicleStateStamp(
            "test",
            123,
            10f,
            42,
            84,
            0,
            receivedAtUtc,
            0)
        {
            SourceIdentity = sourceIdentity
        };

        return new VehicleState(
            new VehicleStateFrame(123, 10f, 42, 84, 0, "test")
            {
                SourceIdentity = sourceIdentity
            },
            Motion: null,
            Session: new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(
                    Weather: 0,
                    TrackTemperatureCelsius: 30,
                    AirTemperatureCelsius: 25,
                    TotalLaps: 10,
                    TrackLengthMeters: 5_000,
                    SessionType: 10,
                    TrackId: 1,
                    GamePaused: gamePaused,
                    SafetyCarStatus: 0,
                    NetworkGame: 1,
                    GameMode: 0),
                stamp),
            Lap: new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(
                    LastLapTimeInMs: 0,
                    CurrentLapTimeInMs: 0,
                    LapDistanceMeters: 100f,
                    TotalDistanceMeters: 100f,
                    CarPosition: 1,
                    CurrentLapNumber: 1,
                    PitStatus: 0,
                    Sector: 0,
                    DriverStatus: driverStatus,
                    ResultStatus: resultStatus,
                    CurrentLapInvalid: 0),
                stamp),
            Participant: new VehicleStateSample<VehicleParticipantState>(
                new VehicleParticipantState(
                    AiControlled: 0,
                    DriverId: 0,
                    TeamId: 0,
                    RaceNumber: 1,
                    Name: "Player",
                    YourTelemetry: 1,
                    TechLevel: 0,
                    Platform: 0),
                stamp),
            Telemetry: new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: speedKph,
                    Throttle: 0.5f,
                    Steer: 0f,
                    Brake: 0f,
                    Clutch: 0,
                    Gear: gear,
                    EngineRpm: rpm,
                    Drs: 0,
                    RevLightsPercent: 0,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 90,
                    SuggestedGear: 0,
                    BrakeTemperatureCelsius: new VehicleWheelData<ushort>(300, 300, 300, 300),
                    TyreSurfaceTemperatureCelsius: new VehicleWheelData<byte>(80, 80, 80, 80),
                    TyreInnerTemperatureCelsius: new VehicleWheelData<byte>(80, 80, 80, 80),
                    TyrePressurePsi: new VehicleWheelData<float>(22f, 22f, 22f, 22f),
                    SurfaceTypeIds: new VehicleWheelData<byte>(0, 0, 0, 0)),
                stamp),
            CarStatus: new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: 0,
                    AntiLockBrakes: 0,
                    FuelMix: 0,
                    FrontBrakeBias: 55,
                    PitLimiterStatus: 0,
                    FuelInTank: 20f,
                    FuelCapacity: 100f,
                    FuelRemainingLaps: 10f,
                    MaxRpm: 12_000,
                    IdleRpm: 4_000,
                    MaxGears: 8,
                    DrsAllowed: 0,
                    DrsActivationDistance: 0,
                    ActualTyreCompound: 16,
                    VisualTyreCompound: 16,
                    TyresAgeLaps: 1,
                    VehicleFiaFlags: 0,
                    EnginePowerIceWatts: 500_000f,
                    EnginePowerMgukWatts: 120_000f,
                    ErsStoreEnergyJoules: 3_000_000f,
                    ErsDeployMode: 0,
                    ErsHarvestedThisLapMgukJoules: 0f,
                    ErsHarvestedThisLapMguhJoules: 0f,
                    ErsDeployedThisLapJoules: 0f,
                    NetworkPaused: networkPaused),
                stamp),
            Damage: null,
            MotionEx: null,
            LastEvent: null);
    }
}
