namespace HapticDrive.Asio.Core.Diagnostics;

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string EventId,
    DiagnosticSeverity Severity,
    string Category,
    string Message,
    IReadOnlyDictionary<string, string> Properties,
    string? CorrelationId);
