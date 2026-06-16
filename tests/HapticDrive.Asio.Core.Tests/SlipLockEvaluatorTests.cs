using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Core.Tests;

public sealed class SlipLockEvaluatorTests
{
    private readonly SlipLockEvaluator _evaluator = new();

    [Fact]
    public void MissingVehicleState_SuppressesBothSignals()
    {
        var result = _evaluator.Evaluate(SlipLockEvaluationInput.FromVehicleState(null));

        Assert.False(result.WheelSlip.IsActive);
        Assert.False(result.WheelLock.IsActive);
        Assert.Equal(SlipLockSuppressionReason.MissingVehicleState, result.WheelSlip.SuppressionReason);
        Assert.Equal(SlipLockSuppressionReason.MissingVehicleState, result.WheelLock.SuppressionReason);
    }

    [Fact]
    public void FrameLagFreshness_SuppressesStaleTelemetryAndMotionSamples()
    {
        var state = CreateVehicleState(
            frame: 200,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f),
            telemetryFrame: 1,
            motionFrame: 1);

        var result = _evaluator.Evaluate(SlipLockEvaluationInput.FromVehicleState(state));

        Assert.False(result.TelemetryFresh);
        Assert.False(result.MotionExFresh);
        Assert.Equal(SlipLockSuppressionReason.TelemetryStale, result.WheelSlip.SuppressionReason);
        Assert.Equal(SlipLockSuppressionReason.TelemetryStale, result.WheelLock.SuppressionReason);
    }

    [Fact]
    public void LowSpeed_SuppressesBothSignals()
    {
        var result = Evaluate(
            speedKph: 3,
            throttle: 1f,
            brake: 1f,
            wheelSlipRatio: Wheels(1f),
            wheelSlipAngle: Wheels(1f),
            wheelSpeed: Wheels(0.1f));

        Assert.Equal(SlipLockSuppressionReason.BelowMinimumSpeed, result.WheelSlip.SuppressionReason);
        Assert.Equal(SlipLockSuppressionReason.BelowMinimumSpeed, result.WheelLock.SuppressionReason);
        Assert.Equal(0f, result.SpeedScale);
    }

    [Fact]
    public void NormalDrivingWithoutSlipOrLock_StaysInactive()
    {
        var result = Evaluate(
            speedKph: 120,
            throttle: 0.4f,
            brake: 0.1f,
            wheelSlipRatio: Wheels(0.01f),
            wheelSlipAngle: Wheels(0.01f),
            wheelSpeed: Wheels(30f));

        Assert.False(result.WheelSlip.IsActive);
        Assert.False(result.WheelLock.IsActive);
        Assert.Equal(SlipLockSuppressionReason.BelowSlipThreshold, result.WheelSlip.SuppressionReason);
        Assert.Equal(SlipLockSuppressionReason.BelowLockThreshold, result.WheelLock.SuppressionReason);
    }

    [Fact]
    public void ThrottleWheelSlip_UsesExpectedNormalizedIntensity()
    {
        var result = Evaluate(
            speedKph: 120,
            throttle: 0.8f,
            brake: 0f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(30f));

        Assert.True(result.WheelSlip.IsActive);
        Assert.Equal(0.918919f, result.WheelSlip.Intensity01, precision: 6);
        Assert.False(result.WheelLock.IsActive);
    }

    [Fact]
    public void BrakeWheelLock_UsesExpectedNormalizedIntensity()
    {
        var result = Evaluate(
            speedKph: 120,
            throttle: 0f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(1f));

        Assert.True(result.WheelLock.IsActive);
        Assert.Equal(0.914286f, result.WheelLock.Intensity01, precision: 6);
        Assert.False(result.WheelSlip.IsActive);
    }

    [Fact]
    public void TractionControlAndAbs_AttenuateSignalsWithoutChangingActivation()
    {
        var slip = Evaluate(
            speedKph: 120,
            throttle: 0.8f,
            brake: 0f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(30f),
            tractionControl: 1);
        var wheelLock = Evaluate(
            speedKph: 120,
            throttle: 0f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.40f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(1f),
            antiLockBrakes: 1);

        Assert.True(slip.WheelSlip.IsActive);
        Assert.True(slip.WheelSlip.IsAssistedAttenuated);
        Assert.Equal(0.689189f, slip.WheelSlip.Intensity01, precision: 6);
        Assert.True(wheelLock.WheelLock.IsActive);
        Assert.True(wheelLock.WheelLock.IsAssistedAttenuated);
        Assert.Equal(0.685714f, wheelLock.WheelLock.Intensity01, precision: 6);
    }

    [Fact]
    public void InvalidValues_AreSanitizedAndWheelOrderIsPreserved()
    {
        var result = Evaluate(
            speedKph: 120,
            throttle: 1f,
            brake: 1f,
            wheelSlipRatio: new VehicleWheelData<float>(-10f, float.NaN, 0.2f, float.PositiveInfinity),
            wheelSlipAngle: new VehicleWheelData<float>(0.05f, -0.4f, float.PositiveInfinity, 0.2f),
            wheelSpeed: new VehicleWheelData<float>(-1f, float.NaN, 3f, float.PositiveInfinity));

        Assert.Equal(3f, result.MaximumSlipRatio);
        Assert.Equal(0.4f, result.MaximumSlipAngleRadians);
        Assert.True(float.IsFinite(result.WheelSlip.Intensity01));
        Assert.True(float.IsFinite(result.WheelLock.Intensity01));
        Assert.Equal(3f, result.WheelContributions.RearLeft.SlipRatio);
        Assert.Equal(0f, result.WheelContributions.RearRight.SlipRatio);
        Assert.Equal(0.2f, result.WheelContributions.FrontLeft.SlipRatio);
        Assert.Equal(0f, result.WheelContributions.FrontRight.SlipRatio);
        Assert.Equal(1f, result.WheelContributions.RearLeft.WheelSpeedMetersPerSecond);
        Assert.Null(result.WheelContributions.RearRight.WheelSpeedMetersPerSecond);
        Assert.Equal(3f, result.WheelContributions.FrontLeft.WheelSpeedMetersPerSecond);
        Assert.Null(result.WheelContributions.FrontRight.WheelSpeedMetersPerSecond);
    }

    private SlipLockEvaluationResult Evaluate(
        ushort speedKph,
        float throttle,
        float brake,
        VehicleWheelData<float> wheelSlipRatio,
        VehicleWheelData<float> wheelSlipAngle,
        VehicleWheelData<float> wheelSpeed,
        byte tractionControl = 0,
        byte antiLockBrakes = 0)
    {
        var state = CreateVehicleState(
            frame: 1,
            speedKph,
            throttle,
            brake,
            wheelSlipRatio,
            wheelSlipAngle,
            wheelSpeed,
            telemetryFrame: 1,
            motionFrame: 1,
            tractionControl,
            antiLockBrakes);

        return _evaluator.Evaluate(SlipLockEvaluationInput.FromVehicleState(state));
    }

    private static VehicleState CreateVehicleState(
        uint frame,
        ushort speedKph,
        float throttle,
        float brake,
        VehicleWheelData<float> wheelSlipRatio,
        VehicleWheelData<float> wheelSlipAngle,
        VehicleWheelData<float> wheelSpeed,
        uint telemetryFrame,
        uint motionFrame,
        byte tractionControl = 0,
        byte antiLockBrakes = 0)
    {
        var frameStamp = new VehicleStateStamp("test", 7, 12.5f, frame, frame, 0);
        var telemetryStamp = new VehicleStateStamp("test", 7, 12.5f, telemetryFrame, telemetryFrame, 0);
        var motionStamp = new VehicleStateStamp("test", 7, 12.5f, motionFrame, motionFrame, 0);

        return new VehicleState(
            new VehicleStateFrame(7, 12.5f, frame, frame, 0, "test"),
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
                    GamePaused: 0,
                    SafetyCarStatus: 0,
                    NetworkGame: 1,
                    GameMode: 0),
                frameStamp),
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
                    DriverStatus: 1,
                    ResultStatus: 2,
                    CurrentLapInvalid: 0),
                frameStamp),
            Participant: null,
            Telemetry: new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: speedKph,
                    Throttle: throttle,
                    Steer: 0f,
                    Brake: brake,
                    Clutch: 0,
                    Gear: 4,
                    EngineRpm: 9_500,
                    Drs: 0,
                    RevLightsPercent: 0,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 90,
                    SuggestedGear: 0,
                    BrakeTemperatureCelsius: Wheels<ushort>(300),
                    TyreSurfaceTemperatureCelsius: Wheels((byte)80),
                    TyreInnerTemperatureCelsius: Wheels((byte)80),
                    TyrePressurePsi: Wheels(22f),
                    SurfaceTypeIds: Wheels((byte)0)),
                telemetryStamp),
            CarStatus: new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: tractionControl,
                    AntiLockBrakes: antiLockBrakes,
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
                    NetworkPaused: 0),
                frameStamp),
            Damage: null,
            MotionEx: new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    SuspensionPosition: Wheels(0f),
                    SuspensionVelocity: Wheels(0f),
                    SuspensionAcceleration: Wheels(0f),
                    WheelSpeed: wheelSpeed,
                    WheelSlipRatio: wheelSlipRatio,
                    WheelSlipAngle: wheelSlipAngle,
                    WheelLatForce: Wheels(0f),
                    WheelLongForce: Wheels(0f),
                    HeightOfCogAboveGround: 0.2f,
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
                    WheelVertForce: Wheels(0f),
                    FrontAeroHeight: 0f,
                    RearAeroHeight: 0f,
                    FrontRollAngle: 0f,
                    RearRollAngle: 0f,
                    ChassisYaw: 0f,
                    ChassisPitch: 0f,
                    WheelCamber: Wheels(0f),
                    WheelCamberGain: Wheels(0f)),
                motionStamp),
            LastEvent: null);
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }
}
