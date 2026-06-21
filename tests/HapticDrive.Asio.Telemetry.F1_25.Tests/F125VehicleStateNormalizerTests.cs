using HapticDrive.Asio.Core.Haptics;
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

    private static VehicleState CreateState(byte surfaceTypeId, DateTimeOffset receivedAt, long receivedTimestamp)
    {
        var stamp = new VehicleStateStamp("Car Telemetry", 42, 5f, 10, 10, 0, receivedAt, receivedTimestamp);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, 5f, 10, 10, 0, "Car Telemetry"),
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
                new VehicleLapState(
                    LastLapTimeInMs: 0,
                    CurrentLapTimeInMs: 0,
                    LapDistanceMeters: 0,
                    TotalDistanceMeters: 0,
                    CarPosition: 0,
                    CurrentLapNumber: 0,
                    PitStatus: 0,
                    Sector: 0,
                    DriverStatus: 1,
                    ResultStatus: 2,
                    CurrentLapInvalid: 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: 120,
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
                    SurfaceTypeIds: new VehicleWheelData<byte>(surfaceTypeId, surfaceTypeId, surfaceTypeId, surfaceTypeId)),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: 0,
                    AntiLockBrakes: 0,
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
                stamp)
        };
    }
}
