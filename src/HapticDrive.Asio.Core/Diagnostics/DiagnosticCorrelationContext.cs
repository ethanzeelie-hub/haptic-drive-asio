namespace HapticDrive.Asio.Core.Diagnostics;

public sealed record DiagnosticCorrelationSnapshot(
    string AppSessionId,
    string? TelemetrySessionId,
    string? RecordingSessionId,
    string OutputSessionId,
    long PHprAuthorizationGeneration);

public sealed class DiagnosticCorrelationContext
{
    private readonly object _gate = new();
    private readonly string _appSessionId = Guid.NewGuid().ToString("N");
    private string? _telemetrySessionId;
    private string? _telemetrySessionKey;
    private string? _recordingSessionId;
    private string? _recordingSessionKey;
    private string _outputSessionId = Guid.NewGuid().ToString("N");
    private string? _outputSessionKey;
    private long _phprAuthorizationGeneration;

    public DiagnosticCorrelationSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return new DiagnosticCorrelationSnapshot(
                    _appSessionId,
                    _telemetrySessionId,
                    _recordingSessionId,
                    _outputSessionId,
                    _phprAuthorizationGeneration);
            }
        }
    }

    public void ObserveTelemetrySession(string? sessionKey)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                return;
            }

            if (string.Equals(_telemetrySessionKey, sessionKey, StringComparison.Ordinal))
            {
                return;
            }

            _telemetrySessionKey = sessionKey;
            _telemetrySessionId = Guid.NewGuid().ToString("N");
        }
    }

    public void ObserveRecordingSession(string? sessionKey)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                _recordingSessionKey = null;
                _recordingSessionId = null;
                return;
            }

            if (string.Equals(_recordingSessionKey, sessionKey, StringComparison.Ordinal))
            {
                return;
            }

            _recordingSessionKey = sessionKey;
            _recordingSessionId = Guid.NewGuid().ToString("N");
        }
    }

    public void ObserveOutputSession(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new ArgumentException("Output session key is required.", nameof(sessionKey));
        }

        lock (_gate)
        {
            if (string.Equals(_outputSessionKey, sessionKey, StringComparison.Ordinal))
            {
                return;
            }

            _outputSessionKey = sessionKey;
            _outputSessionId = Guid.NewGuid().ToString("N");
        }
    }

    public void ObservePhprAuthorizationGeneration(long generation)
    {
        lock (_gate)
        {
            _phprAuthorizationGeneration = generation;
        }
    }
}
