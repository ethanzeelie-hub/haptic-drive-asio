using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Tests;

public sealed class HapticFrameFreshnessEvaluatorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshnessEvaluator_ComputesTypedFreshnessWithoutAllocation()
    {
        var timeProvider = new FixedTimeProvider(BaseTime, 10_000);
        var frame = new HapticFrame(
            new HapticFrameIdentity(
                new GameIntegrationId("f1-25"),
                "test",
                SessionUid: 42,
                SessionTime: 5f,
                FrameIdentifier: 100,
                OverallFrameIdentifier: 100,
                PlayerCarIndex: 0,
                CreatedAtUtc: BaseTime,
                CreatedAtTimestamp: 10_000),
            new HapticTelemetrySignals(
                SpeedMetersPerSecond: 50f,
                Throttle: 0.5f,
                Brake: 0f,
                Steer: 0f,
                Gear: 4,
                SuggestedGear: 4,
                EngineRpm: 10_500,
                IdleRpm: 3_000,
                MaxRpm: 12_000,
                MaxGears: 8,
                TractionControlActive: false,
                AntiLockBrakesActive: false,
                SurfaceTypeIds: null,
                SurfaceKinds: null,
                TyreSlip: null,
                TyreSlipAngle: null,
                WheelSpeedMetersPerSecond: null,
                SuspensionVelocity: null,
                SuspensionAcceleration: null,
                WheelVerticalForce: null,
                BrakeTemperatureCelsius: null,
                VerticalG: null,
                Event: null),
            new HapticDrivingContext(DrivingPhase.Driving, PitState.None, false, true, true),
            new HapticFrameSignalStamps(
                Telemetry: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(10), 9_990, 100),
                Motion: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(15), 9_985, 100),
                Session: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(20), 9_980, 100),
                Lap: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(20), 9_980, 100),
                Participant: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(20), 9_980, 100),
                CarStatus: CreateStamp(BaseTime - TimeSpan.FromMilliseconds(20), 9_980, 100),
                Damage: null,
                MotionEx: CreateStamp(BaseTime - TimeSpan.FromSeconds(5), 5_000, 70),
                Event: null));

        var snapshot = HapticFrameFreshnessEvaluator.Evaluate(frame, timeProvider, TelemetryFreshnessPolicy.Default);
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.Core",
            "Haptics",
            "HapticFrameFreshnessEvaluator.cs"));

        Assert.True(snapshot.Telemetry.IsFresh);
        Assert.True(snapshot.Motion.IsFresh);
        Assert.True(snapshot.Session.IsFresh);
        Assert.True(snapshot.Lap.IsFresh);
        Assert.True(snapshot.Participant.IsFresh);
        Assert.True(snapshot.CarStatus.IsFresh);
        Assert.False(snapshot.MotionEx.IsFresh);
        Assert.Equal(VehicleSignalFreshness.Missing, snapshot.Event);
        Assert.DoesNotContain("Dictionary<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IReadOnlyDictionary<", source, StringComparison.Ordinal);
    }

    private static HapticSignalStamp CreateStamp(
        DateTimeOffset receivedAtUtc,
        long receivedAtTimestamp,
        uint overallFrameIdentifier)
    {
        return new HapticSignalStamp(
            "test",
            new TelemetryPacketKind(1, "test"),
            SessionUid: 42,
            SessionTime: 5f,
            FrameIdentifier: overallFrameIdentifier,
            OverallFrameIdentifier: overallFrameIdentifier,
            PlayerCarIndex: 0,
            ReceivedAtUtc: receivedAtUtc,
            ReceivedAtTimestamp: receivedAtTimestamp);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow, long timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;
    }
}
