namespace HapticDrive.Asio.App;

internal sealed record TelemetryUdpStatusSnapshot(
    string ReplayTimingModeHelpText,
    bool RecordingActive,
    string? RecordingFileName,
    TimeSpan? RecordingLastPacketRelativeTime,
    string? RecordingError,
    bool ReplayActive,
    string ReplayModeLabel,
    string? ReplaySourceFileName,
    long ReplayPacketCount,
    string ReplayStatusMessage,
    string? ReplayError,
    string ListenerStatusText,
    int ListenerPort,
    long ForwardedDatagramCount,
    long RecordingPacketCount,
    long ParserSuccessCount,
    long VehicleStateUpdateCount,
    int ForwardingDestinationCount,
    int ForwardingEnabledDestinationCount,
    int ListenerDefaultPort,
    int? RecordingQueueCapacityPackets = null,
    int RecordingQueuedPacketCount = 0,
    long RecordingDroppedPacketCount = 0);

internal sealed record TelemetryUdpStatusPresentation(
    string ReplayTimingModeHelpText,
    string RecordingsStartStopButtonText,
    string ReplayStartStopButtonText,
    string RecordingsDetailText,
    string ReplayDetailText,
    string ForwardingDestinationsSummaryText,
    string TelemetryUdpPageStatusText);

internal static class TelemetryUdpStatusPresenter
{
    public static TelemetryUdpStatusPresentation Build(TelemetryUdpStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var recordingsDetailText = snapshot.RecordingError is not null
            ? snapshot.RecordingError
            : snapshot.RecordingActive
                ? BuildRecordingDetailText(snapshot)
                : "Ready to capture F1 25 UDP packets to replay files.";

        var replayDetailText = snapshot.ReplayError is not null
            ? $"{snapshot.ReplayError} Replay mode: {snapshot.ReplayModeLabel}."
            : snapshot.ReplayActive
                ? $"Replay active from {snapshot.ReplaySourceFileName}; mode {snapshot.ReplayModeLabel}; {snapshot.ReplayPacketCount:N0} packet(s)."
                : $"Replay idle; mode {snapshot.ReplayModeLabel}; {snapshot.ReplayPacketCount:N0} packet(s) were replayed last time. {snapshot.ReplayStatusMessage}";

        var forwardingDestinationsSummaryText = snapshot.ForwardingDestinationCount == 0
            ? "No forwarding destinations configured. Recording and parsing still work normally."
            : $"{snapshot.ForwardingEnabledDestinationCount}/{snapshot.ForwardingDestinationCount} destination(s) enabled. Loopback to UDP {snapshot.ListenerDefaultPort} is blocked.";

        return new TelemetryUdpStatusPresentation(
            ReplayTimingModeHelpText: snapshot.ReplayTimingModeHelpText,
            RecordingsStartStopButtonText: snapshot.RecordingActive ? "Stop Recording" : "Start Recording",
            ReplayStartStopButtonText: snapshot.ReplayActive ? "Stop Replay" : "Replay Latest",
            RecordingsDetailText: recordingsDetailText,
            ReplayDetailText: replayDetailText,
            ForwardingDestinationsSummaryText: forwardingDestinationsSummaryText,
            TelemetryUdpPageStatusText: BuildPageStatusText(snapshot));
    }

    private static string BuildRecordingDetailText(TelemetryUdpStatusSnapshot snapshot)
    {
        var detail = snapshot.RecordingLastPacketRelativeTime is null
            ? $"Writing {snapshot.RecordingFileName}; waiting for first packet."
            : $"Writing {snapshot.RecordingFileName}; last packet {snapshot.RecordingLastPacketRelativeTime.Value.TotalSeconds:0.000}s.";

        if (snapshot.RecordingDroppedPacketCount > 0)
        {
            detail += $" Queue {snapshot.RecordingQueuedPacketCount:N0}/{snapshot.RecordingQueueCapacityPackets ?? 0:N0}; dropped {snapshot.RecordingDroppedPacketCount:N0}.";
        }

        return detail;
    }

    private static string BuildPageStatusText(TelemetryUdpStatusSnapshot snapshot)
    {
        var recordingSummary = snapshot.RecordingDroppedPacketCount > 0
            ? $"{snapshot.RecordingPacketCount:N0} accepted, {snapshot.RecordingDroppedPacketCount:N0} dropped"
            : $"{snapshot.RecordingPacketCount:N0}";
        return $"{snapshot.ListenerStatusText} on port {snapshot.ListenerPort}; forwarding {snapshot.ForwardedDatagramCount:N0} packet(s); recording {recordingSummary}; parsed {snapshot.ParserSuccessCount:N0}; vehicle samples {snapshot.VehicleStateUpdateCount:N0}.";
    }
}
