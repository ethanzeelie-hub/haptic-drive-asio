namespace HapticDrive.Asio.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public enum DiagnosticRedactionMode
{
    Safe,
    Extended
}

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string EventId,
    DiagnosticSeverity Severity,
    string Category,
    string Message,
    IReadOnlyDictionary<string, string> Properties,
    string? CorrelationId);

public interface IDiagnosticRedactor
{
    string RedactText(string value);

    IReadOnlyDictionary<string, string> RedactProperties(IReadOnlyDictionary<string, string> properties);
}
