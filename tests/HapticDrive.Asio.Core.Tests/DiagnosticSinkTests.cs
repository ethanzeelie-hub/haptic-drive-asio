using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.Core.Tests;

public sealed class DiagnosticSinkTests
{
    [Fact]
    public void EventRing_EvictsOldestAtCapacity()
    {
        var sink = new InMemoryDiagnosticSink(capacity: 2);

        sink.Publish(CreateEvent("event-1", "first"));
        sink.Publish(CreateEvent("event-2", "second"));
        sink.Publish(CreateEvent("event-3", "third"));

        var snapshot = sink.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("event-2", snapshot[0].EventId);
        Assert.Equal("event-3", snapshot[1].EventId);
    }

    [Fact]
    public void Publish_DoesNotThrowWhenSinkFull()
    {
        var sink = new InMemoryDiagnosticSink(capacity: 1);

        var exception = Record.Exception(() =>
        {
            sink.Publish(CreateEvent("event-1", "first"));
            sink.Publish(CreateEvent("event-2", "second"));
            sink.Publish(CreateEvent("event-3", "third"));
        });

        Assert.Null(exception);
        Assert.Single(sink.Snapshot());
    }

    [Fact]
    public void CorrelationIds_StablePerSessionAndRotateOnSessionChange()
    {
        var context = new DiagnosticCorrelationContext();
        var initial = context.Current;

        context.ObserveTelemetrySession("telemetry-a");
        context.ObserveOutputSession("output-a");
        context.ObserveRecordingSession("recording-a");
        context.ObservePhprAuthorizationGeneration(3);
        var afterFirstObserve = context.Current;

        context.ObserveTelemetrySession("telemetry-a");
        context.ObserveOutputSession("output-a");
        context.ObserveRecordingSession("recording-a");
        context.ObservePhprAuthorizationGeneration(3);
        var stable = context.Current;

        context.ObserveTelemetrySession("telemetry-b");
        context.ObserveOutputSession("output-b");
        context.ObserveRecordingSession("recording-b");
        context.ObservePhprAuthorizationGeneration(4);
        var rotated = context.Current;

        Assert.Equal(initial.AppSessionId, afterFirstObserve.AppSessionId);
        Assert.Equal(afterFirstObserve.AppSessionId, stable.AppSessionId);
        Assert.Equal(afterFirstObserve.TelemetrySessionId, stable.TelemetrySessionId);
        Assert.Equal(afterFirstObserve.OutputSessionId, stable.OutputSessionId);
        Assert.Equal(afterFirstObserve.RecordingSessionId, stable.RecordingSessionId);
        Assert.NotEqual(afterFirstObserve.TelemetrySessionId, rotated.TelemetrySessionId);
        Assert.NotEqual(afterFirstObserve.OutputSessionId, rotated.OutputSessionId);
        Assert.NotEqual(afterFirstObserve.RecordingSessionId, rotated.RecordingSessionId);
        Assert.Equal(4, rotated.PHprAuthorizationGeneration);
    }

    private static DiagnosticEvent CreateEvent(string eventId, string message)
    {
        return new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            eventId,
            DiagnosticSeverity.Information,
            "Test",
            message,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "app-session");
    }
}
