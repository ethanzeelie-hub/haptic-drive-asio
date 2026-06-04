using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public sealed class InMemoryShiftIntentSink : IShiftIntentSink
{
    private readonly object _gate = new();
    private readonly List<ShiftIntentEvent> _events = [];

    public long AcceptedCount
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public void OnShiftIntentAccepted(ShiftIntentEvent shiftIntentEvent)
    {
        ArgumentNullException.ThrowIfNull(shiftIntentEvent);

        lock (_gate)
        {
            _events.Add(shiftIntentEvent);
        }
    }

    public IReadOnlyList<ShiftIntentEvent> GetAcceptedEvents()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }
}
