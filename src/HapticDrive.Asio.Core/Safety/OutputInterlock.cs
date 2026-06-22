using System.Threading;

namespace HapticDrive.Asio.Core.Safety;

public sealed class OutputInterlock : IOutputInterlock
{
    private readonly object _gate = new();
    private readonly List<Func<(bool CanReset, string Blocker)>> _resetGuards = [];
    private OutputInterlockSnapshot _current = OutputInterlockSnapshot.StartupSafeDefault();
    private long _observerFailureCount;

    public OutputInterlockSnapshot Current => Volatile.Read(ref _current);

    public bool AllowsOutput => !Current.IsLatched;

    public long ObserverFailureCount => Interlocked.Read(ref _observerFailureCount);

    public event EventHandler<OutputInterlockSnapshot>? Changed;

    public IDisposable RegisterResetGuard(Func<(bool CanReset, string Blocker)> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        lock (_gate)
        {
            _resetGuards.Add(guard);
        }

        return new ResetGuardRegistration(this, guard);
    }

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

        PublishChanged(snapshot);
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

            if (!CanResetLocked())
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

        PublishChanged(snapshot);
        return true;
    }

    private void PublishChanged(OutputInterlockSnapshot snapshot)
    {
        var subscribers = Changed;
        if (subscribers is null)
        {
            return;
        }

        foreach (EventHandler<OutputInterlockSnapshot> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, snapshot);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _observerFailureCount);
            }
        }
    }

    private bool CanResetLocked()
    {
        foreach (var guard in _resetGuards.ToArray())
        {
            try
            {
                if (!guard().CanReset)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

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

    private sealed class ResetGuardRegistration : IDisposable
    {
        private readonly OutputInterlock _owner;
        private Func<(bool CanReset, string Blocker)>? _guard;

        public ResetGuardRegistration(OutputInterlock owner, Func<(bool CanReset, string Blocker)> guard)
        {
            _owner = owner;
            _guard = guard;
        }

        public void Dispose()
        {
            var guard = Interlocked.Exchange(ref _guard, null);
            if (guard is null)
            {
                return;
            }

            lock (_owner._gate)
            {
                _owner._resetGuards.Remove(guard);
            }
        }
    }
}
