using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticsSupportBundleTests
{
    [Fact]
    public void EventsIncludeCorrelationIds()
    {
        var generatedAtUtc = new DateTimeOffset(2026, 6, 24, 1, 2, 3, TimeSpan.Zero);
        var result = StructuredDiagnosticsBuilder.Build(
            new StructuredDiagnosticsBuildInputs(
                generatedAtUtc,
                SelectedGameId: "f1-25",
                SelectedGameDisplayName: "F1 25",
                SelectedOutputId: "null",
                ActiveProfileName: "Default",
                BufferedEvents:
                [
                    new DiagnosticEvent(
                        generatedAtUtc.AddSeconds(-5),
                        "telemetry.stale",
                        DiagnosticSeverity.Warning,
                        "Telemetry",
                        "Telemetry freshness is stale for output-driving signals.",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["telemetryFresh"] = "False"
                        },
                        "telemetry-session-1")
                ],
                CorrelationIds: new SupportBundleCorrelationIds(
                    AppSessionId: "app-session-1",
                    TelemetrySessionId: "telemetry-session-1",
                    RecordingSessionId: "recording-session-1",
                    OutputSessionId: "output-session-1",
                    PHprAuthorizationGeneration: 4)));

        Assert.Equal("app-session-1", result.CorrelationIds.AppSessionId);
        Assert.Equal("telemetry-session-1", result.CorrelationIds.TelemetrySessionId);
        Assert.Equal("recording-session-1", result.CorrelationIds.RecordingSessionId);
        Assert.Equal("output-session-1", result.CorrelationIds.OutputSessionId);
        Assert.Equal(4, result.CorrelationIds.PHprAuthorizationGeneration);
        Assert.Equal(2, result.Events.Count);
        Assert.Contains(result.Events, item => item.EventId == "telemetry.stale" && item.CorrelationId == "telemetry-session-1");
        Assert.Contains(result.Events, item => item.EventId == "app.diagnostics.snapshot" && item.CorrelationId == "app-session-1");
    }
}
