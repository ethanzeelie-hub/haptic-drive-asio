using System.Threading;

namespace HapticDrive.Asio.Core.Safety;

public sealed class OutputInterlock : IOutputInterlock
{
    private readonly object _gate = new();
    private OutputInterlockSnapshot _current = OutputInterlockSnapshot.StartupSafeDefault();

    public OutputInterlockSnapshot Current => Volatile.Read(ref _current);

    public bool AllowsOutput => !Current.IsLatched;

    public event EventHandler<OutputInterlockSnapshot>? Changed;

    public void Trip(OutputInterlockReason reason, string message)
    {
        OutputInterlockSnapshot snapshot;

        lock (_gate)
        {
            var current = _current;
            snapshot = new OutputInterlockSnapshot(
                IsLatched: true,
                Reason: reason,
                Message: NormalizeMessage(message, reason, latched: true),
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: current.Generation + 1);
            Volatile.Write(ref _current, snapshot);
        }

        Changed?.Invoke(this, snapshot);
    }

    public bool Reset(string message)
    {
        OutputInterlockSnapshot snapshot;

        lock (_gate)
        {
            var current = _current;
            if (!current.IsLatched)
            {
                return false;
            }

            snapshot = new OutputInterlockSnapshot(
                IsLatched: false,
                Reason: current.Reason,
                Message: NormalizeMessage(message, current.Reason, latched: false),
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: current.Generation + 1);
            Volatile.Write(ref _current, snapshot);
        }

        Changed?.Invoke(this, snapshot);
        return true;
    }

    private static string NormalizeMessage(string? message, OutputInterlockReason reason, bool latched)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message.Trim();
        }

        return latched
            ? $"Output interlock latched: {reason}."
            : "Output interlock reset; output may resume when the runtime requests it.";
    }
}
