using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Safety;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.Runtime.Diagnostics;

public sealed class RuntimeHealthMonitor
{
    private readonly object _gate = new();
    private readonly IDiagnosticSink _diagnosticSink;
    private readonly DiagnosticCorrelationContext _correlationContext;
    private readonly TimeProvider _timeProvider;
    private string? _lastTelemetrySessionKey;
    private bool _lastTelemetryStale;
    private long _lastTelemetryIngressDropTotal = -1;
    private long _lastRecordingDropCount = -1;
    private bool _lastRecordingIncomplete;
    private string? _lastRecordingErrorMessage;
    private long _lastReplaySubscriberExceptionCount = -1;
    private long _lastParticipantFailureCount = -1;
    private long _lastOutputOverrunTotal = -1;
    private bool _lastOutputFaulted;
    private string? _lastOutputFaultMessage;
    private long _lastInterlockGeneration = -1;

    public RuntimeHealthMonitor(
        IDiagnosticSink diagnosticSink,
        DiagnosticCorrelationContext correlationContext,
        TimeProvider? timeProvider = null)
    {
        _diagnosticSink = diagnosticSink ?? throw new ArgumentNullException(nameof(diagnosticSink));
        _correlationContext = correlationContext ?? throw new ArgumentNullException(nameof(correlationContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void ObserveInterlock(OutputInterlockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DiagnosticEvent? diagnosticEvent = null;
        lock (_gate)
        {
            if (!snapshot.IsLatched || snapshot.Generation == _lastInterlockGeneration)
            {
                _lastInterlockGeneration = snapshot.Generation;
                return;
            }

            _lastInterlockGeneration = snapshot.Generation;
            var correlation = _correlationContext.Current;
            diagnosticEvent = CreateEvent(
                snapshot.ChangedAtUtc,
                "safety.interlock-trip",
                snapshot.Reason is OutputInterlockReason.StartupSafeDefault or OutputInterlockReason.UserEmergencyMute or OutputInterlockReason.Shutdown
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Error,
                "Safety",
                snapshot.Message,
                correlation.AppSessionId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reason"] = snapshot.Reason.ToString(),
                    ["generation"] = snapshot.Generation.ToString(),
                    ["allowsOutput"] = (!snapshot.IsLatched).ToString()
                });
        }

        Publish(diagnosticEvent);
    }

    public void ObserveSupervisor(OutputInterlockSupervisorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DiagnosticEvent? diagnosticEvent = null;
        lock (_gate)
        {
            if (snapshot.ParticipantFailureCount <= 0
                || snapshot.ParticipantFailureCount <= _lastParticipantFailureCount
                || string.IsNullOrWhiteSpace(snapshot.LastFailure))
            {
                _lastParticipantFailureCount = snapshot.ParticipantFailureCount;
                return;
            }

            _lastParticipantFailureCount = snapshot.ParticipantFailureCount;
            var correlation = _correlationContext.Current;
            diagnosticEvent = CreateEvent(
                _timeProvider.GetUtcNow(),
                "safety.participant-silence-failure",
                DiagnosticSeverity.Error,
                "Safety",
                snapshot.LastFailure,
                correlation.AppSessionId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["participantFailureCount"] = snapshot.ParticipantFailureCount.ToString(),
                    ["interlockGeneration"] = snapshot.Interlock.Generation.ToString()
                });
        }

        Publish(diagnosticEvent);
    }

    public void ObservePipeline(HapticPipelineSnapshot snapshot, string telemetrySessionKey, string outputSessionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(telemetrySessionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSessionKey);

        var events = new List<DiagnosticEvent>();
        lock (_gate)
        {
            var telemetryReset = !string.IsNullOrWhiteSpace(_lastTelemetrySessionKey)
                && !string.Equals(_lastTelemetrySessionKey, telemetrySessionKey, StringComparison.Ordinal);
            _lastTelemetrySessionKey = telemetrySessionKey;
            _correlationContext.ObserveTelemetrySession(telemetrySessionKey);
            _correlationContext.ObserveOutputSession(outputSessionKey);
            var correlation = _correlationContext.Current;

            if (telemetryReset)
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "telemetry.session-reset",
                        DiagnosticSeverity.Information,
                        "Telemetry",
                        "Telemetry session identity changed and diagnostics correlation rotated.",
                        correlation.TelemetrySessionId ?? correlation.AppSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["packetsObserved"] = snapshot.PacketsObserved.ToString()
                        }));
            }

            var telemetryStale = snapshot.TelemetryTimedOutMuted
                || (snapshot.VehicleStateUpdateCount > 0 && !snapshot.TelemetryFreshness.IsFresh);
            if (telemetryStale && (!_lastTelemetryStale || telemetryReset))
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "telemetry.stale",
                        DiagnosticSeverity.Warning,
                        "Telemetry",
                        "Telemetry freshness is stale for output-driving signals.",
                        correlation.TelemetrySessionId ?? correlation.AppSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["telemetryAgeMs"] = snapshot.TelemetryAge?.TotalMilliseconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) ?? "none",
                            ["telemetryFresh"] = snapshot.TelemetryFreshness.IsFresh.ToString(),
                            ["sessionFresh"] = snapshot.SessionFreshness.IsFresh.ToString()
                        }));
            }

            _lastTelemetryStale = telemetryStale;

            var outputOverrunTotal = snapshot.Output.UnderrunCount + snapshot.Output.DroppedBufferCount + snapshot.RenderOverrunCount;
            if (outputOverrunTotal > 0 && (_lastOutputOverrunTotal < 0 || outputOverrunTotal > _lastOutputOverrunTotal))
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "audio.output-overrun",
                        DiagnosticSeverity.Warning,
                        "Audio",
                        "Audio output reported underruns, dropped buffers, or render overruns.",
                        correlation.OutputSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["underrunCount"] = snapshot.Output.UnderrunCount.ToString(),
                            ["droppedBufferCount"] = snapshot.Output.DroppedBufferCount.ToString(),
                            ["renderOverrunCount"] = snapshot.RenderOverrunCount.ToString()
                        }));
            }

            _lastOutputOverrunTotal = outputOverrunTotal;

            var outputFaulted = snapshot.Output.State == AudioOutputDeviceState.Faulted || !string.IsNullOrWhiteSpace(snapshot.LastPipelineError);
            var outputFaultMessage = !string.IsNullOrWhiteSpace(snapshot.LastPipelineError)
                ? snapshot.LastPipelineError!.Trim()
                : snapshot.Output.StatusMessage;
            if (outputFaulted && (!_lastOutputFaulted || !string.Equals(_lastOutputFaultMessage, outputFaultMessage, StringComparison.Ordinal)))
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "audio.output-fault",
                        DiagnosticSeverity.Error,
                        "Audio",
                        outputFaultMessage,
                        correlation.OutputSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["state"] = snapshot.Output.State.ToString(),
                            ["kind"] = snapshot.Output.Kind.ToString()
                        }));
            }

            _lastOutputFaulted = outputFaulted;
            _lastOutputFaultMessage = outputFaultMessage;
        }

        Publish(events);
    }

    public void ObserveTelemetryIngress(TelemetryIngressWorkerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var events = new List<DiagnosticEvent>();
        lock (_gate)
        {
            var totalDrops = snapshot.HapticDroppedPacketCount + snapshot.ForwardingDroppedPacketCount + snapshot.RecordingDroppedPacketCount;
            if (totalDrops > 0 && (_lastTelemetryIngressDropTotal < 0 || totalDrops > _lastTelemetryIngressDropTotal))
            {
                var correlation = _correlationContext.Current;
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "telemetry.ingress-drop",
                        DiagnosticSeverity.Warning,
                        "Telemetry",
                        "Telemetry ingress dropped one or more packets.",
                        correlation.TelemetrySessionId ?? correlation.AppSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["hapticDroppedPackets"] = snapshot.HapticDroppedPacketCount.ToString(),
                            ["forwardingDroppedPackets"] = snapshot.ForwardingDroppedPacketCount.ToString(),
                            ["recordingDroppedPackets"] = snapshot.RecordingDroppedPacketCount.ToString()
                        }));
            }

            _lastTelemetryIngressDropTotal = totalDrops;
        }

        Publish(events);
    }

    public void ObserveRecording(TelemetryRecordingSnapshot snapshot, string? recordingSessionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var events = new List<DiagnosticEvent>();
        lock (_gate)
        {
            _correlationContext.ObserveRecordingSession(recordingSessionKey);
            var correlation = _correlationContext.Current;

            if (snapshot.DroppedPacketCount > 0 && (_lastRecordingDropCount < 0 || snapshot.DroppedPacketCount > _lastRecordingDropCount))
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "recording.drop",
                        DiagnosticSeverity.Warning,
                        "Recording",
                        "Recording dropped one or more packets.",
                        correlation.RecordingSessionId ?? correlation.AppSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["droppedPacketCount"] = snapshot.DroppedPacketCount.ToString(),
                            ["queueCapacityPackets"] = snapshot.QueueCapacityPackets?.ToString() ?? "unknown"
                        }));
            }

            _lastRecordingDropCount = snapshot.DroppedPacketCount;

            if (snapshot.RecordingIncomplete
                && (!_lastRecordingIncomplete
                    || !string.Equals(_lastRecordingErrorMessage, snapshot.IncompleteReason, StringComparison.Ordinal)))
            {
                events.Add(
                    CreateEvent(
                        _timeProvider.GetUtcNow(),
                        "recording.finalization-warning",
                        DiagnosticSeverity.Warning,
                        "Recording",
                        snapshot.IncompleteReason ?? "Recording was marked incomplete.",
                        correlation.RecordingSessionId ?? correlation.AppSessionId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["recordingIncomplete"] = snapshot.RecordingIncomplete.ToString(),
                            ["lastError"] = snapshot.LastErrorMessage ?? "none"
                        }));
            }

            _lastRecordingIncomplete = snapshot.RecordingIncomplete;
            _lastRecordingErrorMessage = snapshot.IncompleteReason;
        }

        Publish(events);
    }

    public void ObserveReplay(TelemetryReplaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DiagnosticEvent? diagnosticEvent = null;
        lock (_gate)
        {
            if (snapshot.SubscriberExceptionCount > 0
                && (_lastReplaySubscriberExceptionCount < 0
                    || snapshot.SubscriberExceptionCount > _lastReplaySubscriberExceptionCount))
            {
                var correlation = _correlationContext.Current;
                diagnosticEvent = CreateEvent(
                    _timeProvider.GetUtcNow(),
                    "replay.subscriber-failure",
                    DiagnosticSeverity.Warning,
                    "Replay",
                    snapshot.LastSubscriberErrorMessage ?? "Replay subscriber threw an exception.",
                    correlation.TelemetrySessionId ?? correlation.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["subscriberExceptionCount"] = snapshot.SubscriberExceptionCount.ToString(),
                        ["packetsReplayed"] = snapshot.PacketsReplayed.ToString()
                    });
            }

            _lastReplaySubscriberExceptionCount = snapshot.SubscriberExceptionCount;
        }

        Publish(diagnosticEvent);
    }

    private void Publish(IEnumerable<DiagnosticEvent> events)
    {
        foreach (var diagnosticEvent in events)
        {
            Publish(diagnosticEvent);
        }
    }

    private void Publish(DiagnosticEvent? diagnosticEvent)
    {
        if (diagnosticEvent is not null)
        {
            _diagnosticSink.Publish(diagnosticEvent);
        }
    }

    private static DiagnosticEvent CreateEvent(
        DateTimeOffset timestampUtc,
        string eventId,
        DiagnosticSeverity severity,
        string category,
        string message,
        string correlationId,
        IReadOnlyDictionary<string, string> properties)
    {
        return new DiagnosticEvent(
            timestampUtc,
            eventId,
            severity,
            category,
            message,
            properties,
            correlationId);
    }
}
