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

public interface IDiagnosticRedactor
{
    string RedactText(string value);

    IReadOnlyDictionary<string, string> RedactProperties(IReadOnlyDictionary<string, string> properties);
}
