using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprWorkflowStatusPresenterTests
{
    [Fact]
    public void Build_FormatsWorkflowStatusAndItemsEquivalentToMainWindowOutput()
    {
        var snapshot = CreateSnapshot();

        var presentation = PhprWorkflowStatusPresenter.Build(snapshot);

        Assert.Equal(
            "P-HPR mode: Real Direct Control; telemetry input LiveUdp; replay source test-session.hdrec; selected output selected for this session; coexistence Clear; direct control enabled; emergency stop False; validation ready.",
            presentation.StatusText);
        Assert.Equal(
        [
            "Real direct-control state is runtime-only; profile/app settings do not save enable, private device path, emergency stop, or write history.",
            "Replay validation: input LiveUdp; replay source test-session.hdrec; replay packets 5; replay does not synthesize gear-paddle events.",
            "Profiles: audio default.hdprofile.json auto-saves current rig tuning/defaults; P-HPR p-hpr.hdphprprofile.json is a manual effect-preferences snapshot only.",
            "Instant gear pulse: brake on 55%/52 Hz/45 ms; throttle off 0%/0 Hz/0 ms; last latency routed true.",
            "Road vibration: enabled; brake on strength 20-60%; freq 40-80 Hz; duration 60 ms; throttle off strength 0-0%; freq 0-0 Hz; duration 0 ms; last brake routed.",
            "Slip/lock: disabled; slip on target Brake; strength 10-70%; freq 30-90 Hz; duration 55 ms; lock off target Throttle; strength 0-0%; freq 0-0 Hz; duration 0 ms; last slip inactive.",
            "Mock routing: gear disabled target Both; pedal effects enabled; shared mock commands 12; pending stops 3.",
            "Real output counters: writes 8; failures 1; connection Open; last error none."
        ], presentation.Items);

        var expectedValidation = PhprLiveF1ValidationGuide.Build(CreateLiveValidationSnapshot());
        Assert.Equal(expectedValidation.Summary, presentation.ValidationStatusText);
        Assert.Equal(expectedValidation.Checklist, presentation.ValidationItems);
    }

    [Fact]
    public void Build_NullSnapshot_ReturnsSafeFallbackPresentation()
    {
        var presentation = PhprWorkflowStatusPresenter.Build(null);

        Assert.Equal(
            "P-HPR mode: Disabled; telemetry input Unknown; replay source none; selected output not selected; coexistence Unknown; direct control disabled; emergency stop False; validation blocked.",
            presentation.StatusText);
        Assert.Contains(
            presentation.Items,
            item => string.Equals(
                item,
                "Real direct control is currently disabled; mock routing and diagnostics remain hardware-safe.",
                StringComparison.Ordinal));
        Assert.Equal(8, presentation.Items.Count);
        Assert.Contains("pending live validation gates", presentation.ValidationStatusText, StringComparison.Ordinal);
        Assert.Equal(12, presentation.ValidationItems.Count);
    }

    [Fact]
    public void Build_WhitespaceFieldsFallBackToSafeDefaultText()
    {
        var presentation = PhprWorkflowStatusPresenter.Build(CreateSnapshot() with
        {
            AudioProfileName = "   ",
            PhprProfileName = "",
            CoexistenceStatus = " ",
            BrakePulseText = "",
            GearPulseLatencyText = " ",
            RoadBrakeText = "",
            RoadThrottleText = "",
            LastRoadRoutingText = " ",
            SlipEffectText = " ",
            LockEffectText = "",
            LastSlipLockRoutingText = " ",
            MockGearTarget = "",
            RealConnectionState = "",
            RealLastError = " "
        });

        Assert.Equal(
            "P-HPR mode: Real Direct Control; telemetry input LiveUdp; replay source test-session.hdrec; selected output selected for this session; coexistence Unknown; direct control enabled; emergency stop False; validation ready.",
            presentation.StatusText);
        Assert.Contains("Profiles: audio default.hdprofile.json", presentation.Items[2], StringComparison.Ordinal);
        Assert.Contains("P-HPR p-hpr.hdphprprofile.json", presentation.Items[2], StringComparison.Ordinal);
        Assert.Contains("brake off 0%/0 Hz/0 ms", presentation.Items[3], StringComparison.Ordinal);
        Assert.Contains("last latency none", presentation.Items[3], StringComparison.Ordinal);
        Assert.Contains("last error none", presentation.Items[7], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_PreservesEmergencyStopStateInWorkflowAndValidationOutput()
    {
        var liveValidation = CreateLiveValidationSnapshot() with
        {
            EmergencyStopActive = true
        };

        var presentation = PhprWorkflowStatusPresenter.Build(CreateSnapshot() with
        {
            EmergencyStopActive = true,
            ValidationBlocked = true,
            LiveValidation = liveValidation
        });

        Assert.Contains("emergency stop True; validation blocked.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("emergency stop latched", presentation.ValidationStatusText, StringComparison.Ordinal);
        Assert.Contains(
            presentation.ValidationItems,
            item => item.Contains("Emergency stop: current emergency stop latched", StringComparison.Ordinal));
    }

    private static PhprWorkflowStatusSnapshot CreateSnapshot()
    {
        return new PhprWorkflowStatusSnapshot(
            new PhprWorkflowDiagnosticsSnapshot(
                "Real Direct Control",
                "LiveUdp",
                "test-session.hdrec",
                ReplayPacketsReplayed: 5,
                RealDirectControlEnabled: true,
                RealDirectControlArmed: true,
                SelectedOutputIsConfigured: true,
                MockGearRoutingEnabled: false,
                MockPedalEffectsEnabled: true,
                RealRoadVibrationEnabled: true,
                RealSlipLockEnabled: false),
            "default.hdprofile.json",
            "p-hpr.hdphprprofile.json",
            "Clear",
            EmergencyStopActive: false,
            ValidationBlocked: false,
            BrakePulseText: "on 55%/52 Hz/45 ms",
            ThrottlePulseText: "off 0%/0 Hz/0 ms",
            GearPulseLatencyText: "routed true",
            RealRoadVibrationEnabled: true,
            RoadBrakeText: "on strength 20-60%; freq 40-80 Hz; duration 60 ms",
            RoadThrottleText: "off strength 0-0%; freq 0-0 Hz; duration 0 ms",
            LastRoadRoutingText: "brake routed",
            RealSlipLockEnabled: false,
            SlipEffectText: "on target Brake; strength 10-70%; freq 30-90 Hz; duration 55 ms",
            LockEffectText: "off target Throttle; strength 0-0%; freq 0-0 Hz; duration 0 ms",
            LastSlipLockRoutingText: "slip inactive",
            MockGearTarget: "Both",
            SharedMockAcceptedCommandCount: 12,
            SharedMockPendingStopCount: 3,
            RealReportWriteCount: 8,
            RealFailedReportWriteCount: 1,
            RealConnectionState: "Open",
            RealLastError: "none",
            CreateLiveValidationSnapshot());
    }

    private static PhprLiveF1ValidationSnapshot CreateLiveValidationSnapshot()
    {
        return new PhprLiveF1ValidationSnapshot(
            TelemetryInputSource: "LiveUdp",
            PipelineRunning: true,
            UdpReceiverRunning: true,
            UdpPacketCount: 42,
            ParserSuccessCount: 40,
            TelemetryAge: TimeSpan.FromMilliseconds(20),
            TelemetryTimedOutMuted: false,
            DrivingArmed: true,
            DrivingArmedReason: "DrivingArmed",
            PaddleListenerStatus: "Running",
            ShiftIntentEnabled: true,
            AcceptedShiftIntentCount: 3,
            SuppressedShiftIntentCount: 1,
            OutputMode: "Real Direct Control",
            MockGearRoutingEnabled: true,
            DirectControlEnabled: true,
            DirectControlArmed: true,
            SelectedOutputConfigured: true,
            CoexistenceStatus: "Clear",
            EmergencyStopActive: false,
            RealRoadVibrationEnabled: true,
            RealSlipLockEnabled: false);
    }
}
