using System.Text.Json;
using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.App;

internal sealed record SupportBundleCorrelationIds(
    string AppSessionId,
    string? TelemetrySessionId,
    string? RecordingSessionId,
    string OutputSessionId,
    long PHprAuthorizationGeneration);

internal sealed record SupportBundleStructuredDiagnostics(
    SupportBundleCorrelationIds CorrelationIds,
    IReadOnlyList<DiagnosticEvent> Events);

internal sealed record StructuredDiagnosticsBuildInputs(
    DateTimeOffset GeneratedAtUtc,
    string SelectedGameId,
    string SelectedGameDisplayName,
    string SelectedOutputId,
    string ActiveProfileName,
    IReadOnlyList<DiagnosticEvent> BufferedEvents,
    SupportBundleCorrelationIds CorrelationIds);

internal static class StructuredDiagnosticsBuilder
{
    public static SupportBundleStructuredDiagnostics Build(StructuredDiagnosticsBuildInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var events = new List<DiagnosticEvent>(inputs.BufferedEvents.Count + 1);
        events.AddRange(inputs.BufferedEvents.OrderBy(item => item.TimestampUtc));
        events.Add(
            new DiagnosticEvent(
                inputs.GeneratedAtUtc,
                "app.diagnostics.snapshot",
                DiagnosticSeverity.Information,
                "SupportBundle",
                "Structured diagnostics snapshot captured for support export.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gameId"] = inputs.SelectedGameId,
                    ["gameDisplayName"] = inputs.SelectedGameDisplayName,
                    ["outputId"] = inputs.SelectedOutputId,
                    ["profileName"] = inputs.ActiveProfileName,
                    ["authorizationGeneration"] = inputs.CorrelationIds.PHprAuthorizationGeneration.ToString()
                },
                inputs.CorrelationIds.AppSessionId));

        return new SupportBundleStructuredDiagnostics(inputs.CorrelationIds, events);
    }
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
