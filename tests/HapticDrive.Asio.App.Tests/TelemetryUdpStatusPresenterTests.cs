namespace HapticDrive.Asio.App.Tests;

public sealed class TelemetryUdpStatusPresenterTests
{
    [Fact]
    public void Build_WhenIdle_ShowsReadyRecordingReplayAndForwardingSummaries()
    {
        var presentation = TelemetryUdpStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Real time replay timing.", presentation.ReplayTimingModeHelpText);
        Assert.Equal("Start Recording", presentation.RecordingsStartStopButtonText);
        Assert.Equal("Replay Latest", presentation.ReplayStartStopButtonText);
        Assert.Equal("Loopback-only telemetry listening on UDP 20778. Received 0; ignored remotes 0; oversized 0; haptic drops 0; recording drops 0; forwarding drops 0.", presentation.ListenerDetailText);
        Assert.Equal("Ready to capture F1 25 UDP packets to replay files.", presentation.RecordingsDetailText);
        Assert.Equal("Replay idle; mode Real Time; 0 packet(s) were replayed last time. Idle. Replay road effects require Start Haptics unless replay can open the current output safely.", presentation.ReplayDetailText);
        Assert.Equal("No forwarding destinations configured. Recording and parsing still work normally.", presentation.ForwardingDestinationsSummaryText);
        Assert.Equal("No packets yet on port 20778; forwarding 0 packet(s); forwarding drops 0; recording 0; parsed 0; vehicle samples 0.", presentation.TelemetryUdpPageStatusText);
    }

    [Fact]
    public void Build_WhenRecordingAndReplayAreActive_ShowsLiveStatus()
    {
        var presentation = TelemetryUdpStatusPresenter.Build(CreateSnapshot(
            recordingActive: true,
            recordingFileName: "session.hdrec",
            recordingLastPacketRelativeTime: TimeSpan.FromSeconds(1.234),
            replayActive: true,
            hapticsRunning: true,
            replayModeLabel: "Fast Debug",
            replaySourceFileName: "latest.hdrec",
            replayPacketCount: 456,
            replayStatusMessage: "Done",
            listenerStatusText: "Listening",
            forwardedDatagramCount: 42,
            recordingPacketCount: 78,
            parserSuccessCount: 88,
            vehicleStateUpdateCount: 91,
            forwardingDestinationCount: 3,
            forwardingEnabledDestinationCount: 2,
            allowLanTelemetry: true,
            hasAllowedRemoteAddresses: true,
            receivedPacketCount: 120,
            ignoredRemotePacketCount: 3,
            oversizedDatagramCount: 1));

        Assert.Equal("Stop Recording", presentation.RecordingsStartStopButtonText);
        Assert.Equal("Stop Replay", presentation.ReplayStartStopButtonText);
        Assert.Equal("LAN telemetry enabled on UDP 20778 with an allowlist. Received 120; ignored remotes 3; oversized 1; haptic drops 0; recording drops 0; forwarding drops 0.", presentation.ListenerDetailText);
        Assert.Equal("Writing session.hdrec; last packet 1.234s.", presentation.RecordingsDetailText);
        Assert.Equal("Replay active from latest.hdrec; mode Fast Debug; 456 packet(s). Haptics running; BST-1 and P-HPR road effects can route when their road settings and safety gates are clear.", presentation.ReplayDetailText);
        Assert.Equal("2/3 destination(s) enabled. Loopback to UDP 20778 is blocked.", presentation.ForwardingDestinationsSummaryText);
        Assert.Equal("Listening on port 20778; forwarding 42 packet(s); forwarding drops 0; recording 78; parsed 88; vehicle samples 91.", presentation.TelemetryUdpPageStatusText);
    }

    [Fact]
    public void Build_WhenReplayRunsWithoutHaptics_ShowsTelemetryOnlyRoadWarning()
    {
        var presentation = TelemetryUdpStatusPresenter.Build(CreateSnapshot(
            replayActive: true,
            replaySourceFileName: "latest.hdrec",
            replayPacketCount: 12,
            replayModeLabel: "Real Time"));

        Assert.Equal(
            "Replay active from latest.hdrec; mode Real Time; 12 packet(s). Replay is feeding telemetry only. Press Start Haptics to feel road effects.",
            presentation.ReplayDetailText);
    }

    [Fact]
    public void Build_WhenErrorsExist_PrefersErrorMessages()
    {
        var presentation = TelemetryUdpStatusPresenter.Build(CreateSnapshot(
            recordingError: "Recording failed.",
            replayError: "Replay failed.",
            replayModeLabel: "Real Time"));

        Assert.Equal("Recording failed.", presentation.RecordingsDetailText);
        Assert.Equal("Replay failed. Replay mode: Real Time.", presentation.ReplayDetailText);
    }

    private static TelemetryUdpStatusSnapshot CreateSnapshot(
        string replayTimingModeHelpText = "Real time replay timing.",
        bool recordingActive = false,
        string? recordingFileName = null,
        TimeSpan? recordingLastPacketRelativeTime = null,
        string? recordingError = null,
        bool replayActive = false,
        bool hapticsRunning = false,
        string replayModeLabel = "Real Time",
        string? replaySourceFileName = null,
        long replayPacketCount = 0,
        string replayStatusMessage = "Idle.",
        string? replayError = null,
        string listenerStatusText = "No packets yet",
        int listenerPort = 20778,
        bool allowLanTelemetry = false,
        bool hasAllowedRemoteAddresses = false,
        string? lanWarningText = null,
        long receivedPacketCount = 0,
        long hapticDroppedPacketCount = 0,
        long forwardingDroppedPacketCount = 0,
        long ignoredRemotePacketCount = 0,
        long oversizedDatagramCount = 0,
        long forwardedDatagramCount = 0,
        long recordingPacketCount = 0,
        long parserSuccessCount = 0,
        long vehicleStateUpdateCount = 0,
        int forwardingDestinationCount = 0,
        int forwardingEnabledDestinationCount = 0,
        int listenerDefaultPort = 20778,
        bool recordingIncomplete = false)
    {
        return new TelemetryUdpStatusSnapshot(
            ReplayTimingModeHelpText: replayTimingModeHelpText,
            RecordingActive: recordingActive,
            RecordingFileName: recordingFileName,
            RecordingLastPacketRelativeTime: recordingLastPacketRelativeTime,
            RecordingError: recordingError,
            ReplayActive: replayActive,
            HapticsRunning: hapticsRunning,
            ReplayModeLabel: replayModeLabel,
            ReplaySourceFileName: replaySourceFileName,
            ReplayPacketCount: replayPacketCount,
            ReplayStatusMessage: replayStatusMessage,
            ReplayError: replayError,
            ListenerStatusText: listenerStatusText,
            ListenerPort: listenerPort,
            AllowLanTelemetry: allowLanTelemetry,
            HasAllowedRemoteAddresses: hasAllowedRemoteAddresses,
            LanWarningText: lanWarningText,
            ReceivedPacketCount: receivedPacketCount,
            HapticDroppedPacketCount: hapticDroppedPacketCount,
            ForwardingDroppedPacketCount: forwardingDroppedPacketCount,
            IgnoredRemotePacketCount: ignoredRemotePacketCount,
            OversizedDatagramCount: oversizedDatagramCount,
            ForwardedDatagramCount: forwardedDatagramCount,
            RecordingPacketCount: recordingPacketCount,
            ParserSuccessCount: parserSuccessCount,
            VehicleStateUpdateCount: vehicleStateUpdateCount,
            ForwardingDestinationCount: forwardingDestinationCount,
            ForwardingEnabledDestinationCount: forwardingEnabledDestinationCount,
            ListenerDefaultPort: listenerDefaultPort,
            RecordingIncomplete: recordingIncomplete);
    }
}
