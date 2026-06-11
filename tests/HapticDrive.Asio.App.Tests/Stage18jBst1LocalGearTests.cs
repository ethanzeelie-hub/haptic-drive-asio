using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class Stage18jBst1LocalGearTests
{
    [Fact]
    public void AsioStatusFormatter_DoesNotSayTrueAsioNoWhenReadyButStopped()
    {
        var text = Bst1AsioStatusFormatter.Format(Snapshot(
            outputMode: AudioOutputDeviceKind.Asio.ToString(),
            selectedChannel: 1,
            armed: true,
            running: false,
            callbackActive: false));

        Assert.Contains("ASIO ready: YES", text);
        Assert.Contains("ASIO active: NO - stream stopped", text);
        Assert.DoesNotContain("True ASIO: NO", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AsioStatusFormatter_ReportsLastManualAndGearPulseProofSeparately()
    {
        var text = Bst1AsioStatusFormatter.Format(Snapshot(
            lastManualPulseUsedAsio: true,
            lastGearPulseUsedAsio: true,
            lastPulseUsedAsio: true));

        Assert.Contains("Last manual pulse used ASIO: YES", text);
        Assert.Contains("Last gear pulse used ASIO: YES", text);
        Assert.Contains("Last pulse: ASIO hardware path used", text);
    }

    [Fact]
    public void SharedDuration_AppliesToBothPhprPedals()
    {
        var brake = PHprRealGearPulseSettings.Default with { DurationMs = 35 };
        var throttle = PHprRealGearPulseSettings.Default with { DurationMs = 70 };

        var shared = Bst1GearPulseDurationSync.ResolveSharedDuration(brake, throttle);
        var syncedBrake = Bst1GearPulseDurationSync.WithSharedDuration(brake, 45);
        var syncedThrottle = Bst1GearPulseDurationSync.WithSharedDuration(throttle, 45);

        Assert.Equal(70, shared);
        Assert.Equal(45, syncedBrake.DurationMs);
        Assert.Equal(45, syncedThrottle.DurationMs);
    }

    [Fact]
    public void Bst1SyncMode_UsesSharedDurationAndIgnoresStaleCustomValue()
    {
        var synced = Bst1GearPulseDurationSync.ResolveBst1Duration(
            syncToPhpr: true,
            sharedPhprDurationMs: 45,
            customBst1DurationMs: 250);
        var custom = Bst1GearPulseDurationSync.ResolveBst1Duration(
            syncToPhpr: false,
            sharedPhprDurationMs: 45,
            customBst1DurationMs: 80);

        Assert.Equal(45, synced);
        Assert.Equal(80, custom);
    }

    [Fact]
    public void LocalGearTestReady_DoesNotRequireStartHapticsOrTelemetry()
    {
        var readiness = LocalGearTestReadiness.Evaluate(
            isEnabled: true,
            autoStartListener: true,
            ListeningPaddleSnapshot(),
            selectionBlocker: null,
            hasLeftMapping: true,
            hasRightMapping: true,
            phprDirectReady: true,
            bst1Enabled: true,
            bst1AsioReady: true);

        Assert.True(readiness.IsReady);
        Assert.Contains("Start Haptics and F1 telemetry are not required", readiness.Message);
    }

    [Fact]
    public void LocalGearTest_BlocksWhenListenerStoppedButCanStartListener()
    {
        var readiness = LocalGearTestReadiness.Evaluate(
            isEnabled: true,
            autoStartListener: true,
            WheelPaddleInputSnapshot.NotConfigured with
            {
                Status = InputListenerStatus.Stopped,
                Mapping = Mapping()
            },
            selectionBlocker: null,
            hasLeftMapping: true,
            hasRightMapping: true,
            phprDirectReady: true,
            bst1Enabled: true,
            bst1AsioReady: true);

        Assert.False(readiness.IsReady);
        Assert.True(readiness.CanStartListener);
        Assert.Contains("paddle listener stopped", readiness.Message);
    }

    [Fact]
    public void LocalGearTest_RequiresValidMapping()
    {
        var readiness = LocalGearTestReadiness.Evaluate(
            isEnabled: true,
            autoStartListener: true,
            ListeningPaddleSnapshot() with
            {
                Mapping = WheelPaddleMapping.Default
            },
            selectionBlocker: null,
            hasLeftMapping: false,
            hasRightMapping: false,
            phprDirectReady: true,
            bst1Enabled: true,
            bst1AsioReady: true);

        Assert.False(readiness.IsReady);
        Assert.Contains("map both left and right paddles", readiness.Message);
    }

    [Fact]
    public void LocalGearTest_BlocksWhenBst1EnabledButAsioNotReady()
    {
        var readiness = LocalGearTestReadiness.Evaluate(
            isEnabled: true,
            autoStartListener: true,
            ListeningPaddleSnapshot(),
            selectionBlocker: null,
            hasLeftMapping: true,
            hasRightMapping: true,
            phprDirectReady: true,
            bst1Enabled: true,
            bst1AsioReady: false);

        Assert.False(readiness.IsReady);
        Assert.Contains("BST-1 ASIO not ready", readiness.Message);
    }

    private static WheelPaddleInputSnapshot ListeningPaddleSnapshot()
    {
        return WheelPaddleInputSnapshot.NotConfigured with
        {
            Status = InputListenerStatus.Listening,
            Mapping = Mapping()
        };
    }

    private static WheelPaddleMapping Mapping()
    {
        return WheelPaddleMapping.Default with
        {
            LeftPaddleButtonId = 14,
            RightPaddleButtonId = 13
        };
    }

    private static ManualAsioHardwareTestSnapshot Snapshot(
        string outputMode = "Asio",
        int? selectedChannel = 1,
        bool armed = true,
        bool running = false,
        bool callbackActive = false,
        bool lastPulseUsedAsio = false,
        bool lastManualPulseUsedAsio = false,
        bool lastGearPulseUsedAsio = false)
    {
        return new ManualAsioHardwareTestSnapshot(
            IsActive: false,
            TestMode: "ASIO Hardware",
            OutputMode: outputMode,
            SelectedAsioDriver: "M-Audio M-Track Solo and Duo ASIO",
            SelectedOutputChannel: selectedChannel,
            AsioRunning: running,
            AsioArmed: armed,
            AsioCallbackActive: callbackActive,
            HapticsRunning: false,
            EmergencyMute: false,
            NormalMute: false,
            OutputPeakLevel: 0f,
            FramesSubmitted: 0,
            FramesRendered: 0,
            RenderCallbackCount: 0,
            SubmittedFrameCount: 0,
            DroppedFrameCount: 0,
            BackendCallbackCount: 0,
            LastPulseUsedAsio: lastPulseUsedAsio,
            LastManualPulseUsedAsio: lastManualPulseUsedAsio,
            LastGearPulseUsedAsio: lastGearPulseUsedAsio,
            LastPulseBlocked: false,
            LimiterApplied: false,
            PulseGenerationId: 0,
            StaleStopIgnoredCount: 0,
            BlockedReason: null,
            LastTestSignal: null,
            LastTestDuration: null,
            LastStrengthPercent: null,
            LastFrequencyHz: null,
            LastDurationMs: null,
            LastSource: null,
            LastDurationMode: null,
            ManualPulsePeak: 0f,
            FlightRecorderPath: "disabled",
            LastError: null);
    }
}
