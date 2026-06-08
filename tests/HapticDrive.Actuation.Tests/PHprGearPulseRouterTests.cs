using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PHprGearPulseRouterTests
{
    [Fact]
    public async Task AcceptedShiftIntent_RoutesThroughSafetyLimitedMockOutput()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        var result = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));
        var snapshot = router.GetSnapshot();

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(1, snapshot.AcceptedRouteCount);
        Assert.Equal(0, snapshot.IgnoredRouteCount);
        Assert.Equal(PHprModuleId.Both, result.Command?.TargetModule);
        Assert.Equal(PHprCommandSource.PaddleShiftIntent, result.Command?.Source);
        Assert.Single(inner.CommandHistory);
    }

    [Fact]
    public async Task SuppressedShiftIntent_DoesNotRoute()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);
        var notArmed = CreateShiftIntent(ShiftIntentDirection.Downshift, drivingArmed: false);

        var result = await router.RouteAsync(notArmed);

        Assert.Equal(PHprGearPulseRoutingStatus.IgnoredDrivingNotArmed, result.Status);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().IgnoredRouteCount);
    }

    [Fact]
    public async Task DisabledRouter_DoesNotRoute()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(
            output,
            PHprGearPulseRouterOptions.Default with { IsEnabled = false });

        var result = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));

        Assert.Equal(PHprGearPulseRoutingStatus.IgnoredDisabled, result.Status);
        Assert.Empty(inner.CommandHistory);
    }

    [Fact]
    public async Task MissingShiftIntent_DoesNotRoute()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        var result = await router.RouteAsync(null);

        Assert.Equal(PHprGearPulseRoutingStatus.IgnoredMissingShiftIntent, result.Status);
        Assert.Empty(inner.CommandHistory);
    }

    [Fact]
    public async Task DefaultBothTarget_CreatesBrakeAndThrottleMockFrames()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));
        var frames = inner.FrameHistory;

        Assert.Contains(frames, frame => frame.TargetModule == PHprModuleId.Brake && frame.State == PHprMockProtocolState.Start);
        Assert.Contains(frames, frame => frame.TargetModule == PHprModuleId.Throttle && frame.State == PHprMockProtocolState.Start);
        Assert.Contains(frames, frame => frame.TargetModule == PHprModuleId.Brake && frame.State == PHprMockProtocolState.Stop);
        Assert.Contains(frames, frame => frame.TargetModule == PHprModuleId.Throttle && frame.State == PHprMockProtocolState.Stop);
    }

    [Fact]
    public async Task DefaultPulse_UsesConservativeGearPulseSettings()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        var result = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Downshift));

        var command = Assert.IsType<PHprCommand>(result.Command);
        Assert.Equal(0.05d, command.Strength01, precision: 6);
        Assert.Equal(50d, command.FrequencyHz, precision: 6);
        Assert.Equal(50, command.DurationMs);
        Assert.Equal(100, command.Priority);
        Assert.Equal(PHprCommandSource.PaddleShiftIntent, command.Source);
    }

    [Fact]
    public async Task UpshiftAndDownshift_UseSameDefaultPulse()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        var upshift = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));
        var downshift = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Downshift, sequenceNumber: 2));

        Assert.Equal(upshift.Command?.TargetModule, downshift.Command?.TargetModule);
        Assert.Equal(upshift.Command?.Strength01, downshift.Command?.Strength01);
        Assert.Equal(upshift.Command?.FrequencyHz, downshift.Command?.FrequencyHz);
        Assert.Equal(upshift.Command?.DurationMs, downshift.Command?.DurationMs);
    }

    [Fact]
    public async Task SafetyLimiter_ClampsExcessiveStrength()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(
            output,
            PHprGearPulseRouterOptions.Default with
            {
                Profile = PHprGearPulseProfile.Default with { Strength01 = 0.7d }
            });

        var result = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.IsType<PHprCommand>(result.Command);
        Assert.Equal(PHprSafetyLimits.Default.MaxStrength01, command.Strength01, precision: 6);
        Assert.True(command.SafetyFlags.HasFlag(PHprSafetyFlags.ClampedStrength));
        Assert.Equal(PHprSafetyDecisionKind.AcceptedWithClamp, result.SafetySnapshot?.LastDecision?.Kind);
    }

    [Theory]
    [MemberData(nameof(BlockingContexts))]
    public async Task SafetyContext_BlocksStartCommands(PHprSafetyContext context, PHprSafetyViolationCode expected)
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        var result = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift), context);

        Assert.Equal(PHprGearPulseRoutingStatus.RejectedBySafety, result.Status);
        Assert.Equal(expected, result.SafetySnapshot?.LastViolation?.Code);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().SafetyRejectedCount);
    }

    [Fact]
    public async Task EmergencyStop_BlocksLaterGearPulsesUntilCleared()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        await router.EmergencyStopAsync();
        var blocked = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift));
        router.ClearEmergencyStop();
        var accepted = await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Upshift, sequenceNumber: 2));

        Assert.Equal(PHprGearPulseRoutingStatus.RejectedBySafety, blocked.Status);
        Assert.Equal(PHprSafetyViolationCode.EmergencyStopActive, blocked.SafetySnapshot?.LastViolation?.Code);
        Assert.True(accepted.WasRouted, accepted.Message);
    }

    [Fact]
    public async Task MockOutputCommandFrameAndPendingStopCounts_Update()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        await router.RouteAsync(CreateShiftIntent(ShiftIntentDirection.Downshift));
        var outputSnapshot = router.GetSnapshot().OutputSnapshot;

        Assert.Equal(1, outputSnapshot.AcceptedCommandCount);
        Assert.Equal(4, outputSnapshot.GeneratedFrameCount);
        Assert.Equal(2, outputSnapshot.PendingScheduledStopCount);
    }

    [Fact]
    public void RouterSurface_DoesNotReferenceRealOutputUsbHidAsioOrVehicleRouting()
    {
        var methodNames = typeof(PHprGearPulseRouter)
            .GetMethods()
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .ToArray();
        var constructorParameterNames = typeof(PHprGearPulseRouter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("GearShiftEffect", constructorParameterNames);
        Assert.DoesNotContain("HapticEffectEngine", constructorParameterNames);
        Assert.DoesNotContain("VehicleState", constructorParameterNames);
        Assert.DoesNotContain("IAudioOutputDevice", constructorParameterNames);
        Assert.DoesNotContain(methodNames, name => name.Contains("Usb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Hid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Write", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Road", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Slip", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Lock", StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> BlockingContexts()
    {
        yield return [PHprSafetyContext.DefaultMock with { TelemetryStale = true }, PHprSafetyViolationCode.TelemetryStale];
        yield return [PHprSafetyContext.DefaultMock with { EmergencyMuteActive = true }, PHprSafetyViolationCode.EmergencyMuteActive];
        yield return [PHprSafetyContext.DefaultMock with { DrivingArmed = false }, PHprSafetyViolationCode.DrivingNotArmed];
        yield return [PHprSafetyContext.DefaultMock with { HapticsStopped = true }, PHprSafetyViolationCode.HapticsStopped];
    }

    private static ShiftIntentEvent CreateShiftIntent(
        ShiftIntentDirection direction,
        bool drivingArmed = true,
        long sequenceNumber = 1)
    {
        var side = direction == ShiftIntentDirection.Downshift
            ? PaddleSide.Left
            : PaddleSide.Right;
        var state = drivingArmed
            ? DrivingArmedState.Armed("Synthetic active driving.")
            : DrivingArmedState.NotArmed("Synthetic menu gate.");
        return ShiftIntentEvent.CreatePaddlePress(
            side,
            state,
            new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(sequenceNumber),
            sequenceNumber,
            "synthetic-wheel",
            4,
            direction,
            ShiftIntentSource.WheelPaddle,
            ShiftIntentMode.InstantPaddleOnly,
            10_000 + sequenceNumber,
            direction == ShiftIntentDirection.Downshift ? 7 : 8,
            123,
            9_500,
            12.5f,
            123_456u);
    }
}
