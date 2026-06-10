using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PaddleGearBenchTestControllerTests
{
    [Fact]
    public void NormalShiftIntent_StillSuppressesWithoutRecentTelemetry()
    {
        var provider = new FakeDrivingArmedProvider(
            DrivingArmedState.NotArmed("No recent valid telemetry has been observed."));
        var processor = new ShiftIntentProcessor(provider);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right, buttonId: 13));

        Assert.False(result.WasAccepted);
        Assert.Contains("No recent valid telemetry", result.SuppressionReason, StringComparison.Ordinal);
        Assert.Equal(1, processor.GetDiagnosticsSnapshot().SuppressedShiftIntentCount);
    }

    [Fact]
    public void BenchMode_AcceptsMappedPaddleWithoutTelemetry()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions());

        var result = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Right, buttonId: 13),
            Mapping());
        var snapshot = controller.GetSnapshot();

        Assert.True(result.Accepted, result.Message);
        Assert.NotNull(result.ShiftIntentEvent);
        Assert.True(result.ShiftIntentEvent.IsAcceptedByDrivingGate);
        Assert.Equal(ShiftIntentSource.Test, result.ShiftIntentEvent.Source);
        Assert.Equal(1, snapshot.AcceptedBenchGearEventCount);
        Assert.Equal(1, snapshot.RightPaddleAcceptedCount);
        Assert.Equal(0, snapshot.SuppressedBenchGearEventCount);
    }

    [Fact]
    public void BenchModeDisabled_SuppressesBenchEvents()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions() with { IsEnabled = false });

        var result = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Left, buttonId: 14),
            Mapping());

        Assert.False(result.Accepted);
        Assert.Contains("disabled", result.SuppressionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, controller.GetSnapshot().SuppressedBenchGearEventCount);
    }

    [Fact]
    public void BenchModeEnabled_AutoArmsBenchEvents()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions() with { IsArmed = false });

        var result = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Right, buttonId: 13),
            Mapping());

        Assert.True(result.Accepted, result.Message);
        Assert.True(controller.GetSnapshot().IsArmed);
    }

    [Fact]
    public void UnmappedPaddleEvent_IsSuppressed()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions());

        var result = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Right, buttonId: 99),
            Mapping());

        Assert.False(result.Accepted);
        Assert.Contains("mapped", result.SuppressionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, controller.GetSnapshot().SuppressedBenchGearEventCount);
    }

    [Fact]
    public async Task MockBenchRouting_UsesMockOutputAndDoesNotRequireHidReports()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions());
        var bench = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Left, buttonId: 14),
            Mapping());
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);
        var routeOptions = new PHprGearPulseRouterOptions
        {
            IsEnabled = true,
            TargetModule = PHprGearPulseTarget.Brake,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = 0.10d,
                FrequencyHz = 50d,
                DurationMs = 50
            }
        };

        var route = await router.RouteAsync(
            bench.ShiftIntentEvent,
            routeOptions,
            PHprSafetyContext.DefaultMock with
            {
                DrivingArmed = true,
                HapticsStopped = false,
                TelemetryStale = false
            });

        Assert.True(route.WasRouted, route.Message);
        Assert.Single(inner.CommandHistory);
        Assert.Contains(inner.FrameHistory, frame => frame.State == PHprMockProtocolState.Start);
        Assert.Equal(1, router.GetSnapshot().AcceptedRouteCount);
    }

    [Fact]
    public async Task EmergencyStop_BlocksBenchMockOutput()
    {
        var controller = new PaddleGearBenchTestController(EnabledArmedOptions());
        var bench = controller.HandlePaddleInput(
            CreatePaddleEvent(PaddleSide.Right, buttonId: 13),
            Mapping());
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprGearPulseRouter(output);

        await router.EmergencyStopAsync();
        var route = await router.RouteAsync(
            bench.ShiftIntentEvent,
            new PHprGearPulseRouterOptions { IsEnabled = true },
            PHprSafetyContext.DefaultMock with
            {
                DrivingArmed = true,
                HapticsStopped = false,
                TelemetryStale = false
            });

        Assert.Equal(PHprGearPulseRoutingStatus.RejectedBySafety, route.Status);
        Assert.Equal(PHprSafetyViolationCode.EmergencyStopActive, route.SafetySnapshot?.LastViolation?.Code);
    }

    private static PaddleGearBenchTestOptions EnabledArmedOptions()
    {
        return new PaddleGearBenchTestOptions
        {
            IsEnabled = true,
            IsArmed = true,
            OutputMode = PaddleGearBenchTestOutputMode.Mock,
            TargetModule = PHprGearPulseTarget.Brake
        };
    }

    private static WheelPaddleMapping Mapping()
    {
        return new WheelPaddleMapping
        {
            LeftPaddleButtonId = 14,
            RightPaddleButtonId = 13
        };
    }

    private static WheelPaddleInputEvent CreatePaddleEvent(PaddleSide side, int buttonId)
    {
        return new WheelPaddleInputEvent(
            side,
            new InputDeviceSelection(
                "windowsgamecontroller:gt-neo",
                "Synthetic GT Neo wheel input",
                InputDiscoveryMethod.WindowsGameController,
                NativeDeviceIndex: 0,
                ButtonCount: 16),
            buttonId,
            new InputEventTimestamp(
                new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(buttonId),
                10_000 + buttonId),
            buttonId);
    }

    private sealed class FakeDrivingArmedProvider : IDrivingArmedStateProvider
    {
        public FakeDrivingArmedProvider(DrivingArmedState state)
        {
            Current = state;
        }

        public event EventHandler<DrivingArmedState>? DrivingArmedChanged;

        public DrivingArmedState Current { get; }
    }
}
