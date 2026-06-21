using System.Text.Json;
using System.Text.RegularExpressions;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.App;

internal sealed record SupportBundleCorrelationIds(
    string AppSessionId,
    string? TelemetrySessionId,
    string? RecordingSessionId,
    string? OutputDeviceSessionId);

internal sealed record SupportBundleStructuredDiagnostics(
    SupportBundleCorrelationIds CorrelationIds,
    IReadOnlyList<DiagnosticEvent> Events);

internal sealed record StructuredDiagnosticsBuildInputs(
    DateTimeOffset GeneratedAtUtc,
    string SelectedGameId,
    string SelectedGameDisplayName,
    string SelectedOutputId,
    string ActiveProfileName,
    string? SettingsError,
    HapticPipelineSnapshot PipelineSnapshot,
    UdpTelemetryReceiverSnapshot ReceiverSnapshot,
    TelemetryIngressWorkerSnapshot IngressSnapshot,
    AudioRuntimeDiagnosticsSnapshot AudioDiagnostics,
    SupportBundleCorrelationIds CorrelationIds);

internal static class StructuredDiagnosticsBuilder
{
    public static SupportBundleStructuredDiagnostics Build(StructuredDiagnosticsBuildInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var events = new List<DiagnosticEvent>
        {
            CreateEvent(
                inputs.GeneratedAtUtc,
                "app.diagnostics.snapshot",
                DiagnosticSeverity.Information,
                "SupportBundle",
                "Structured diagnostics snapshot captured for support export.",
                inputs.CorrelationIds.AppSessionId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gameId"] = inputs.SelectedGameId,
                    ["gameDisplayName"] = inputs.SelectedGameDisplayName,
                    ["outputId"] = inputs.SelectedOutputId,
                    ["profileName"] = inputs.ActiveProfileName,
                    ["pipelineRunning"] = inputs.PipelineSnapshot.IsRunning.ToString(),
                    ["recordingActive"] = inputs.PipelineSnapshot.Recording.IsRecording.ToString(),
                    ["replayActive"] = inputs.PipelineSnapshot.Replay.IsReplaying.ToString()
                })
        };

        if (inputs.PipelineSnapshot.OutputInterlock.IsLatched)
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "safety.output-interlock",
                    inputs.PipelineSnapshot.OutputInterlock.Reason is HapticDrive.Asio.Core.Safety.OutputInterlockReason.StartupSafeDefault
                        or HapticDrive.Asio.Core.Safety.OutputInterlockReason.UserEmergencyMute
                        or HapticDrive.Asio.Core.Safety.OutputInterlockReason.Shutdown
                        ? DiagnosticSeverity.Warning
                        : DiagnosticSeverity.Error,
                    "Safety",
                    $"Global output interlock is latched: {inputs.PipelineSnapshot.OutputInterlock.Reason}.",
                    inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reason"] = inputs.PipelineSnapshot.OutputInterlock.Reason.ToString(),
                        ["allowsOutput"] = inputs.PipelineSnapshot.OutputInterlock.AllowsOutput.ToString(),
                        ["generation"] = inputs.PipelineSnapshot.OutputInterlock.Generation.ToString()
                    }));
        }

        if (inputs.PipelineSnapshot.TelemetryTimedOutMuted
            || (inputs.PipelineSnapshot.VehicleStateUpdateCount > 0 && !inputs.PipelineSnapshot.TelemetryFreshness.IsFresh))
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "telemetry.stale",
                    DiagnosticSeverity.Warning,
                    "Telemetry",
                    "Telemetry freshness is stale for output-driving signals.",
                    inputs.CorrelationIds.TelemetrySessionId ?? inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["telemetryAgeMs"] = inputs.PipelineSnapshot.TelemetryAge?.TotalMilliseconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) ?? "none",
                        ["maxTelemetryAgeMs"] = TelemetryFreshnessPolicy.Default.MaxTelemetryAge.TotalMilliseconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                        ["telemetryFresh"] = inputs.PipelineSnapshot.TelemetryFreshness.IsFresh.ToString(),
                        ["sessionFresh"] = inputs.PipelineSnapshot.SessionFreshness.IsFresh.ToString()
                    }));
        }

        if (inputs.IngressSnapshot.HapticDroppedPacketCount > 0
            || inputs.IngressSnapshot.ForwardingDroppedPacketCount > 0
            || inputs.ReceiverSnapshot.IgnoredRemotePacketCount > 0
            || inputs.ReceiverSnapshot.OversizedDatagramCount > 0)
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "telemetry.ingress-backpressure",
                    DiagnosticSeverity.Warning,
                    "Telemetry",
                    "Telemetry ingress dropped, ignored, or rejected one or more packets.",
                    inputs.CorrelationIds.TelemetrySessionId ?? inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["hapticDroppedPackets"] = inputs.IngressSnapshot.HapticDroppedPacketCount.ToString(),
                        ["forwardingDroppedPackets"] = inputs.IngressSnapshot.ForwardingDroppedPacketCount.ToString(),
                        ["ignoredRemotePackets"] = inputs.ReceiverSnapshot.IgnoredRemotePacketCount.ToString(),
                        ["oversizedDatagrams"] = inputs.ReceiverSnapshot.OversizedDatagramCount.ToString()
                    }));
        }

        if (inputs.PipelineSnapshot.Output.UnderrunCount > 0
            || inputs.PipelineSnapshot.Output.DroppedBufferCount > 0
            || inputs.PipelineSnapshot.RenderOverrunCount > 0)
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "audio.output-health",
                    DiagnosticSeverity.Warning,
                    "Audio",
                    "Audio output reported underruns, dropped buffers, or render overruns.",
                    inputs.CorrelationIds.OutputDeviceSessionId ?? inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["underrunCount"] = inputs.PipelineSnapshot.Output.UnderrunCount.ToString(),
                        ["droppedBufferCount"] = inputs.PipelineSnapshot.Output.DroppedBufferCount.ToString(),
                        ["renderOverrunCount"] = inputs.PipelineSnapshot.RenderOverrunCount.ToString(),
                        ["interlockSilenceCount"] = inputs.PipelineSnapshot.InterlockSilenceCount.ToString(),
                        ["staleFrameSilenceCount"] = inputs.PipelineSnapshot.StaleFrameSilenceCount.ToString()
                    }));
        }

        if (inputs.PipelineSnapshot.Recording.DroppedPacketCount > 0
            || inputs.IngressSnapshot.RecordingDroppedPacketCount > 0
            || inputs.PipelineSnapshot.Recording.RecordingIncomplete
            || inputs.IngressSnapshot.RecordingMarkedIncomplete)
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "recording.capture-integrity",
                    DiagnosticSeverity.Warning,
                    "Recording",
                    "Recording capture dropped packets or was marked incomplete.",
                    inputs.CorrelationIds.RecordingSessionId ?? inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ingressDroppedPackets"] = inputs.IngressSnapshot.RecordingDroppedPacketCount.ToString(),
                        ["writerDroppedPackets"] = inputs.PipelineSnapshot.Recording.DroppedPacketCount.ToString(),
                        ["recordingIncomplete"] = inputs.PipelineSnapshot.Recording.RecordingIncomplete.ToString(),
                        ["ingressMarkedIncomplete"] = inputs.IngressSnapshot.RecordingMarkedIncomplete.ToString()
                    }));
        }

        if (!string.IsNullOrWhiteSpace(inputs.SettingsError))
        {
            events.Add(
                CreateEvent(
                    inputs.GeneratedAtUtc,
                    "persistence.settings-warning",
                    DiagnosticSeverity.Warning,
                    "Persistence",
                    "Settings persistence reported a warning or recovery condition.",
                    inputs.CorrelationIds.AppSessionId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["settingsError"] = inputs.SettingsError.Trim()
                    }));
        }

        return new SupportBundleStructuredDiagnostics(inputs.CorrelationIds, events);
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

internal sealed partial class SupportBundleDiagnosticRedactor : IDiagnosticRedactor
{
    private readonly DiagnosticRedactionMode _mode;

    public SupportBundleDiagnosticRedactor(DiagnosticRedactionMode mode)
    {
        _mode = mode;
    }

    public string RedactText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = value.Trim();
        redacted = UserProfilePathRegex().Replace(
            redacted,
            match => _mode == DiagnosticRedactionMode.Safe
                ? "%USERPROFILE%\\<redacted-path>"
                : $"%USERPROFILE%{match.Groups[2].Value}");
        redacted = AbsoluteWindowsPathRegex().Replace(
            redacted,
            match => _mode == DiagnosticRedactionMode.Safe
                ? "<redacted-path>"
                : $"{match.Groups[1].Value}<redacted-path>");
        redacted = HidPathRegex().Replace(redacted, "$1<redacted>");
        redacted = RawUsbPayloadRegex().Replace(redacted, "$1<redacted>");
        redacted = SerialLabelRegex().Replace(redacted, "$1<redacted>");
        redacted = LongSerialTokenRegex().Replace(redacted, "<redacted>");
        redacted = ProcessIdRegex().Replace(redacted, "$1<redacted>");
        redacted = HostNameRegex().Replace(redacted, "$1<redacted-host>");

        if (_mode == DiagnosticRedactionMode.Safe)
        {
            redacted = PrivateIpRegex().Replace(redacted, "<private-ip>");
        }

        return redacted;
    }

    public IReadOnlyDictionary<string, string> RedactProperties(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.ToDictionary(
            pair => pair.Key,
            pair => RedactText(pair.Value),
            StringComparer.Ordinal);
    }

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)[^\\]+(\\[^\r\n;,""]*)?")]
    private static partial Regex UserProfilePathRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\)(?!Users\\<redacted>|<redacted-path>)[^\r\n;,""]+")]
    private static partial Regex AbsoluteWindowsPathRegex();

    [GeneratedRegex(@"(?i)((?:\\\\\?\\)?(?:hid|usb)[#\\][^#\\\s]*(?:vid_[0-9a-f]{4}|pid_[0-9a-f]{4})[^#\\\s]*[#\\])[^#\\\s]+")]
    private static partial Regex HidPathRegex();

    [GeneratedRegex(@"(?i)\b((?:raw usb|raw hid|usb payload|raw bytes?|payload bytes?|transfer bytes?)\s*[:=]\s*)(?:[0-9a-f]{2}\s+){2,}[0-9a-f]{2}\b")]
    private static partial Regex RawUsbPayloadRegex();

    [GeneratedRegex(@"(?i)\b(serial|sn|s/n)\s*[:=_-]?\s*[a-z0-9][a-z0-9_-]{5,}\b")]
    private static partial Regex SerialLabelRegex();

    [GeneratedRegex(@"(?i)\b(?=[a-z0-9_-]{10,}\b)(?=.*[a-z])(?=.*[0-9])[a-z0-9_-]{10,}\b")]
    private static partial Regex LongSerialTokenRegex();

    [GeneratedRegex(@"(?i)\b(pid|process id)\s*[:=#]?\s*\d+\b")]
    private static partial Regex ProcessIdRegex();

    [GeneratedRegex(@"(?i)\b(hostname|host)\s*[:=]\s*[a-z0-9][a-z0-9.-]*\b")]
    private static partial Regex HostNameRegex();

    [GeneratedRegex(@"\b(?:(?:10|127)\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[0-1])\.\d{1,3}\.\d{1,3}|169\.254\.\d{1,3}\.\d{1,3})\b")]
    private static partial Regex PrivateIpRegex();
}

internal static class SupportBundleJson
{
    public static string SerializeEvents(
        IReadOnlyList<DiagnosticEvent> events,
        IDiagnosticRedactor redactor,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);

        var payload = events.Select(item => new
        {
            item.TimestampUtc,
            item.EventId,
            Severity = item.Severity.ToString(),
            item.Category,
            Message = redactor.RedactText(item.Message),
            Properties = redactor.RedactProperties(item.Properties),
            item.CorrelationId
        });

        return JsonSerializer.Serialize(payload, options);
    }
}
