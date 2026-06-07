using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprSafetyLayerTests
{
    [Fact]
    public void SafetyLimits_DefaultsRemainConservativeForStage2L()
    {
        var limits = PHprSafetyLimits.Default;

        Assert.False(limits.AllowRealDeviceWrites);
        Assert.Equal(0.10d, limits.MaxStrength01, precision: 6);
        Assert.Equal(100, limits.MaxDurationMs);
        Assert.Equal(5d, limits.MinFrequencyHz, precision: 6);
        Assert.Equal(250d, limits.MaxFrequencyHz, precision: 6);
        Assert.Equal(10, limits.MaxCommandsPerSecond);
        Assert.Equal(500, limits.MaxContinuousDurationMs);
    }

    [Fact]
    public void Limiter_ClampsStrengthDurationAndHighFrequency()
    {
        var limiter = new PHprSafetyLimiter();

        var decision = limiter.Evaluate(Pulse(PHprModuleId.Brake, 0.8d, 999d, 2_000));
        var snapshot = limiter.GetSnapshot();

        Assert.Equal(PHprSafetyDecisionKind.AcceptedWithClamp, decision.Kind);
        Assert.NotNull(decision.Command);
        Assert.Equal(PHprSafetyLimits.Default.MaxStrength01, decision.Command.Strength01, precision: 6);
        Assert.Equal(PHprSafetyLimits.Default.MaxDurationMs, decision.Command.DurationMs);
        Assert.Equal(PHprSafetyLimits.Default.MaxFrequencyHz, decision.Command.FrequencyHz, precision: 6);
        Assert.Contains(decision.Violations, violation => violation.Code == PHprSafetyViolationCode.StrengthExceeded);
        Assert.Contains(decision.Violations, violation => violation.Code == PHprSafetyViolationCode.DurationExceeded);
        Assert.Contains(decision.Violations, violation => violation.Code == PHprSafetyViolationCode.FrequencyTooHigh);
        Assert.Equal(1, snapshot.AcceptedCount);
        Assert.Equal(1, snapshot.AcceptedWithClampCount);
        Assert.Equal(PHprSafetyViolationCode.StrengthExceeded, snapshot.LastViolation?.Code);
    }

    [Fact]
    public void Limiter_ClampsLowFrequency()
    {
        var decision = new PHprSafetyLimiter().Evaluate(Pulse(PHprModuleId.Brake, 0.05d, 1d, 50));

        Assert.Equal(PHprSafetyDecisionKind.AcceptedWithClamp, decision.Kind);
        Assert.NotNull(decision.Command);
        Assert.Equal(PHprSafetyLimits.Default.MinFrequencyHz, decision.Command.FrequencyHz, precision: 6);
        Assert.Contains(decision.Violations, violation => violation.Code == PHprSafetyViolationCode.FrequencyTooLow);
    }

    [Fact]
    public async Task SafetyLimitedOutput_ForwardsAcceptedAndClampedCommandsToMockOutput()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);

        var accepted = await output.SendAsync(Pulse(PHprModuleId.Brake, 0.05d, 50d, 60));
        var clamped = await output.SendAsync(Pulse(PHprModuleId.Throttle, 0.8d, 999d, 2_000));

        Assert.True(accepted.Succeeded, accepted.Message);
        Assert.True(clamped.Succeeded, clamped.Message);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.Equal(PHprSafetyLimits.Default.MaxStrength01, inner.CommandHistory[1].Strength01, precision: 6);
        Assert.Equal(PHprSafetyLimits.Default.MaxDurationMs, inner.CommandHistory[1].DurationMs);
        Assert.True(inner.CommandHistory[1].SafetyFlags.HasFlag(PHprSafetyFlags.ClampedStrength));
        Assert.True(inner.CommandHistory[1].SafetyFlags.HasFlag(PHprSafetyFlags.MockOnly));
        Assert.Equal(2, output.SafetySnapshot.AcceptedCount);
        Assert.Equal(1, output.SafetySnapshot.AcceptedWithClampCount);
    }

    [Fact]
    public void CommandRateLimit_RejectsExcessiveStartsAndRecoversAfterFakeTimeAdvances()
    {
        var clock = new FakeSafetyClock();
        var limiter = new PHprSafetyLimiter(
            PHprSafetyLimits.Default with { MaxCommandsPerSecond = 2, MaxContinuousDurationMs = 2_000 },
            clock);

        var first = limiter.Evaluate(Pulse(PHprModuleId.Brake));
        var second = limiter.Evaluate(Pulse(PHprModuleId.Brake));
        var third = limiter.Evaluate(Pulse(PHprModuleId.Brake));
        clock.Advance(TimeSpan.FromSeconds(1));
        var recovered = limiter.Evaluate(Pulse(PHprModuleId.Brake));

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.False(third.Accepted);
        Assert.Equal(PHprSafetyViolationCode.CommandRateExceeded, third.Violation.Code);
        Assert.True(recovered.Accepted);
        Assert.Equal(3, limiter.GetSnapshot().AcceptedCount);
    }

    [Fact]
    public void ContinuousDurationLimit_RejectsSustainedStartsAndEmergencyStopResetsEstimate()
    {
        var clock = new FakeSafetyClock();
        var limiter = new PHprSafetyLimiter(
            PHprSafetyLimits.Default with { MaxContinuousDurationMs = 150, MaxCommandsPerSecond = 10 },
            clock);

        var first = limiter.Evaluate(Pulse(PHprModuleId.Brake, durationMs: 100));
        var second = limiter.Evaluate(Pulse(PHprModuleId.Brake, durationMs: 100));
        var beforeStop = limiter.GetSnapshot();
        var emergency = limiter.RecordEmergencyStop();
        var afterStop = limiter.GetSnapshot();
        limiter.ClearEmergencyStop();
        var afterClear = limiter.Evaluate(Pulse(PHprModuleId.Brake, durationMs: 100));

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal(PHprSafetyViolationCode.ContinuousDurationExceeded, second.Violation.Code);
        Assert.Equal(100, beforeStop.CurrentContinuousDurationEstimateMs);
        Assert.Equal(PHprSafetyDecisionKind.EmergencyStopped, emergency.Kind);
        Assert.Equal(0, afterStop.CurrentContinuousDurationEstimateMs);
        Assert.True(afterStop.IsEmergencyStopActive);
        Assert.True(afterClear.Accepted);
    }

    [Fact]
    public async Task EmergencyStop_ClearsPendingStopsBlocksStartsAndClearAllowsStartsAgain()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);

        await output.SendAsync(Pulse(PHprModuleId.Brake, durationMs: 100));
        await output.EmergencyStopAsync();
        var afterEmergency = output.GetSnapshot();
        var blocked = await output.SendAsync(Pulse(PHprModuleId.Brake));
        output.ClearEmergencyStop();
        var acceptedAfterClear = await output.SendAsync(Pulse(PHprModuleId.Brake));

        var snapshot = output.GetSnapshot();
        Assert.False(blocked.Succeeded);
        Assert.Equal(PHprCommandStatus.RejectedEmergencyStop, blocked.Status);
        Assert.True(acceptedAfterClear.Succeeded, acceptedAfterClear.Message);
        Assert.False(snapshot.IsEmergencyStopActive);
        Assert.Equal(0, afterEmergency.PendingScheduledStopCount);
        Assert.Contains(inner.FrameHistory, frame => frame.State == PHprMockProtocolState.EmergencyStop && frame.TargetModule == PHprModuleId.Brake);
        Assert.Contains(inner.FrameHistory, frame => frame.State == PHprMockProtocolState.EmergencyStop && frame.TargetModule == PHprModuleId.Throttle);
    }

    [Fact]
    public async Task DisconnectedDevice_RejectsStartsButRecordsSafeStopsAndEmergencyStop()
    {
        await using var inner = new MockPhprOutputDevice();
        inner.SetConnected(false);
        await using var output = new SafetyLimitedPhprOutputDevice(inner);

        var start = await output.SendAsync(Pulse(PHprModuleId.Brake));
        var afterStart = output.SafetySnapshot;
        var stop = await output.SendAsync(Stop(PHprModuleId.Brake));
        await output.EmergencyStopAsync();
        var snapshot = output.GetSnapshot();

        Assert.False(start.Succeeded);
        Assert.Equal(PHprCommandStatus.RejectedSafetyLimit, start.Status);
        Assert.Equal(PHprSafetyViolationCode.DeviceDisconnected, afterStart.LastViolation?.Code);
        Assert.True(stop.Succeeded, stop.Message);
        Assert.Contains(inner.CommandHistory, command => command.TargetModule == PHprModuleId.Brake && command.DurationMs == 0);
        Assert.True(snapshot.IsEmergencyStopActive);
        Assert.Equal(1, snapshot.EmergencyStopCount);
    }

    [Fact]
    public async Task UnavailableModules_RejectMatchingStartTargets()
    {
        await using var inner = new MockPhprOutputDevice();
        inner.SetModuleAvailability(brakeAvailable: false, throttleAvailable: true);
        await using var output = new SafetyLimitedPhprOutputDevice(inner);

        var brake = await output.SendAsync(Pulse(PHprModuleId.Brake));
        var throttle = await output.SendAsync(Pulse(PHprModuleId.Throttle));
        var both = await output.SendAsync(Pulse(PHprModuleId.Both));

        Assert.False(brake.Succeeded);
        Assert.Equal(PHprSafetyViolationCode.ModuleUnavailable, output.SafetySnapshot.LastViolation?.Code);
        Assert.True(throttle.Succeeded, throttle.Message);
        Assert.False(both.Succeeded);
        Assert.DoesNotContain(inner.CommandHistory, command => command.TargetModule == PHprModuleId.Brake);
    }

    [Theory]
    [MemberData(nameof(RestrictiveContexts))]
    public void ContextGates_RejectStartsWithSpecificViolation(PHprSafetyContext context, PHprSafetyViolationCode expected)
    {
        var limiter = new PHprSafetyLimiter();

        var decision = limiter.Evaluate(Pulse(PHprModuleId.Brake), context);

        Assert.False(decision.Accepted);
        Assert.Equal(expected, decision.Violation.Code);
    }

    [Fact]
    public void StopCommands_AreAllowedInRestrictiveContext()
    {
        var context = PHprSafetyContext.DefaultMock with
        {
            TelemetryStale = true,
            HapticsStopped = true,
            EmergencyMuteActive = true,
            DrivingArmed = false,
            SoftwareConflictStatus = PHprSoftwareConflictStatus.ActiveConflict,
            RequiresRealDeviceWrites = true
        };

        var decision = new PHprSafetyLimiter().Evaluate(Stop(PHprModuleId.Both), context);

        Assert.True(decision.Accepted, decision.Message);
        Assert.Equal(PHprSafetyDecisionKind.Accepted, decision.Kind);
    }

    [Fact]
    public void SafetySnapshot_ReportsRealWriteModeBlockedByDefault()
    {
        var snapshot = new PHprSafetyLimiter().GetSnapshot();

        Assert.False(snapshot.RealWritesAllowed);
        Assert.True(snapshot.RealWriteModeBlocked);
    }

    [Fact]
    public void SafetyLayer_DoesNotExposeHidOrUsbWriteApis()
    {
        var forbiddenTerms = new[] { "WriteFile", "HidD", "SetFeature", "DeviceHandle", "OpenDevice", "UsbReport" };
        var methodNames = typeof(PHprSafetyLimiter).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".Safety", StringComparison.Ordinal) == true
                || type == typeof(SafetyLimitedPhprOutputDevice))
            .SelectMany(type => type.GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .Distinct()
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static IEnumerable<object[]> RestrictiveContexts()
    {
        yield return [PHprSafetyContext.DefaultMock with { TelemetryStale = true }, PHprSafetyViolationCode.TelemetryStale];
        yield return [PHprSafetyContext.DefaultMock with { HapticsStopped = true }, PHprSafetyViolationCode.HapticsStopped];
        yield return [PHprSafetyContext.DefaultMock with { EmergencyMuteActive = true }, PHprSafetyViolationCode.EmergencyMuteActive];
        yield return [PHprSafetyContext.DefaultMock with { DrivingArmed = false }, PHprSafetyViolationCode.DrivingNotArmed];
        yield return [PHprSafetyContext.DefaultMock with { SoftwareConflictStatus = PHprSoftwareConflictStatus.ActiveConflict }, PHprSafetyViolationCode.SimProConflict];
        yield return [PHprSafetyContext.DefaultMock with { RequiresRealDeviceWrites = true }, PHprSafetyViolationCode.RealWritesNotAllowed];
    }

    private static PHprCommand Pulse(
        PHprModuleId targetModule,
        double strength01 = 0.05d,
        double frequencyHz = 50d,
        int durationMs = 50)
    {
        return PHprCommand.Create(targetModule, strength01, frequencyHz, durationMs, PHprCommandSource.TestBench);
    }

    private static PHprCommand Stop(PHprModuleId targetModule)
    {
        return PHprCommand.Create(targetModule, 0d, PHprSafetyLimits.Default.MinFrequencyHz, 0, PHprCommandSource.TestBench);
    }

    private sealed class FakeSafetyClock : IPHprSafetyClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan duration)
        {
            UtcNow += duration;
        }
    }
}
