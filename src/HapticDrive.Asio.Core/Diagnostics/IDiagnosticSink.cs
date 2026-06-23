namespace HapticDrive.Asio.Core.Diagnostics;

public interface IDiagnosticSink
{
    void Publish(DiagnosticEvent diagnosticEvent);

    IReadOnlyList<DiagnosticEvent> Snapshot();
}
