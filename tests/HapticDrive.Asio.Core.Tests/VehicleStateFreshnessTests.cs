using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Tests;

public sealed class VehicleStateFreshnessTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshSessionPacketDoesNotRefreshStaleCarTelemetry()
    {
        var nowTimestamp = TimeProvider.System.GetTimestamp();
        var staleTelemetryTimestamp = nowTimestamp - TimeProvider.System.TimestampFrequency / 2;
        var freshSessionTimestamp = nowTimestamp - TimeProvider.System.TimestampFrequency / 100;
        var state = CreateState(
            frameSessionUid: 10,
            currentOverallFrame: 10,
            telemetryStamp: CreateStamp("Car Telemetry", 10, 8, BaseTime.AddMilliseconds(-300), staleTelemetryTimestamp),
            sessionStamp: CreateStamp("Session", 10, 10, BaseTime.AddMilliseconds(-10), freshSessionTimestamp));

        var telemetryFreshness = VehicleStateFreshness.EvaluateTelemetry(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);
        var sessionFreshness = VehicleStateFreshness.EvaluateSession(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(telemetryFreshness.IsFresh);
        Assert.True(sessionFreshness.IsFresh);
        Assert.True(telemetryFreshness.Age > TelemetryFreshnessPolicy.Default.MaxTelemetryAge);
    }

    [Fact]
    public void FutureFrameSampleIsNotFresh()
    {
        var nowTimestamp = TimeProvider.System.GetTimestamp();
        var state = CreateState(
            frameSessionUid: 10,
            currentOverallFrame: 10,
            telemetryStamp: CreateStamp("Car Telemetry", 10, 11, BaseTime.AddMilliseconds(-10), nowTimestamp));

        var freshness = VehicleStateFreshness.EvaluateTelemetry(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(freshness.IsFresh);
        Assert.False(freshness.IsNotFutureFrame);
    }

    [Fact]
    public void WrongSessionSampleIsNotFresh()
    {
        var nowTimestamp = TimeProvider.System.GetTimestamp();
        var state = CreateState(
            frameSessionUid: 10,
            currentOverallFrame: 10,
            telemetryStamp: CreateStamp("Car Telemetry", 11, 10, BaseTime.AddMilliseconds(-10), nowTimestamp));

        var freshness = VehicleStateFreshness.EvaluateTelemetry(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(freshness.IsFresh);
        Assert.False(freshness.IsSameSession);
    }

    [Fact]
    public void FrameLagAbovePolicyIsNotFresh()
    {
        var nowTimestamp = TimeProvider.System.GetTimestamp();
        var state = CreateState(
            frameSessionUid: 10,
            currentOverallFrame: 10,
            telemetryStamp: CreateStamp("Car Telemetry", 10, 7, BaseTime.AddMilliseconds(-10), nowTimestamp));

        var freshness = VehicleStateFreshness.EvaluateTelemetry(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(freshness.IsFresh);
        Assert.Equal((uint)3, freshness.FrameLag);
        Assert.False(freshness.IsWithinFrameLag);
    }

    [Fact]
    public void MonotonicAgeAbovePolicyIsNotFresh()
    {
        var nowTimestamp = TimeProvider.System.GetTimestamp();
        var staleTimestamp = nowTimestamp - TimeProvider.System.TimestampFrequency / 2;
        var state = CreateState(
            frameSessionUid: 10,
            currentOverallFrame: 10,
            telemetryStamp: CreateStamp("Car Telemetry", 10, 10, BaseTime.AddMilliseconds(-300), staleTimestamp));

        var freshness = VehicleStateFreshness.EvaluateTelemetry(state, BaseTime, nowTimestamp, TimeProvider.System, TelemetryFreshnessPolicy.Default);

        Assert.False(freshness.IsFresh);
        Assert.NotNull(freshness.Age);
        Assert.True(freshness.Age > TelemetryFreshnessPolicy.Default.MaxTelemetryAge);
    }

    private static VehicleState CreateState(
        ulong frameSessionUid,
        uint currentOverallFrame,
        VehicleStateStamp? telemetryStamp = null,
        VehicleStateStamp? sessionStamp = null)
    {
        var frame = new VehicleStateFrame(frameSessionUid, 10f, currentOverallFrame, currentOverallFrame, 0, "Frame");
        return VehicleState.Empty with
        {
            Frame = frame,
            Telemetry = telemetryStamp is null
                ? null
                : new VehicleStateSample<VehicleTelemetryState>(
                    CreateTelemetryState(),
                    telemetryStamp),
            Session = sessionStamp is null
                ? null
                : new VehicleStateSample<VehicleSessionState>(
                    new VehicleSessionState(0, 30, 25, 10, 5_000, 10, 1, 0, 0, 1, 0),
                    sessionStamp)
        };
    }

    private static VehicleStateStamp CreateStamp(
        string source,
        ulong sessionUid,
        uint overallFrameIdentifier,
        DateTimeOffset receivedAtUtc,
        long receivedAtTimestamp)
    {
        return new VehicleStateStamp(
            source,
            sessionUid,
            10f,
            overallFrameIdentifier,
            overallFrameIdentifier,
            0,
            receivedAtUtc,
            receivedAtTimestamp);
    }

    private static VehicleTelemetryState CreateTelemetryState()
    {
        return new VehicleTelemetryState(
            SpeedKph: 100,
            Throttle: 0.7f,
            Steer: 0f,
            Brake: 0f,
            Clutch: 0,
            Gear: 5,
            EngineRpm: 11_000,
            Drs: 0,
            RevLightsPercent: 0,
            RevLightsBitValue: 0,
            EngineTemperatureCelsius: 90,
            SuggestedGear: 0,
            BrakeTemperatureCelsius: new VehicleWheelData<ushort>(300, 300, 300, 300),
            TyreSurfaceTemperatureCelsius: new VehicleWheelData<byte>(80, 80, 80, 80),
            TyreInnerTemperatureCelsius: new VehicleWheelData<byte>(80, 80, 80, 80),
            TyrePressurePsi: new VehicleWheelData<float>(22f, 22f, 22f, 22f),
            SurfaceTypeIds: new VehicleWheelData<byte>(0, 0, 0, 0));
    }
}
