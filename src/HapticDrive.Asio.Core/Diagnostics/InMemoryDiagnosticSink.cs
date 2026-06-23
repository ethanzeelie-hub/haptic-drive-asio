namespace HapticDrive.Asio.Core.Diagnostics;

public sealed class InMemoryDiagnosticSink : IDiagnosticSink
{
    public const int DefaultCapacity = 4_096;

    private readonly object _gate = new();
    private readonly DiagnosticEvent[] _buffer;
    private int _head;
    private int _count;

    public InMemoryDiagnosticSink(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Diagnostic sink capacity must be positive.");
        }

        _buffer = new DiagnosticEvent[capacity];
    }

    public void Publish(DiagnosticEvent diagnosticEvent)
    {
        if (diagnosticEvent is null)
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                var writeIndex = (_head + _count) % _buffer.Length;
                if (_count == _buffer.Length)
                {
                    _buffer[_head] = diagnosticEvent;
                    _head = (_head + 1) % _buffer.Length;
                    return;
                }

                _buffer[writeIndex] = diagnosticEvent;
                _count++;
            }
        }
        catch
        {
        }
    }

    public IReadOnlyList<DiagnosticEvent> Snapshot()
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                return Array.Empty<DiagnosticEvent>();
            }

            var copy = new DiagnosticEvent[_count];
            for (var index = 0; index < _count; index++)
            {
                copy[index] = _buffer[(_head + index) % _buffer.Length];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
