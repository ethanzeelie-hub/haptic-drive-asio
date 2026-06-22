using System.Threading.Channels;
using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Runtime.Safety;

public sealed class OutputInterlockSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromMilliseconds(500);
    private readonly object _gate = new();
    private readonly IOutputInterlock _interlock;
    private readonly IReadOnlyList<IOutputSafetyParticipant> _participants;
    private readonly Channel<OutputInterlockSnapshot> _channel;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IDisposable? _resetGuardRegistration;
    private readonly Task _worker;
    private OutputInterlockSupervisorSnapshot _current;
    private bool _disposed;

    public OutputInterlockSupervisor(
        IOutputInterlock interlock,
        IEnumerable<IOutputSafetyParticipant> participants)
    {
        _interlock = interlock ?? throw new ArgumentNullException(nameof(interlock));
        _participants = (participants ?? throw new ArgumentNullException(nameof(participants))).ToArray();
        _channel = Channel.CreateBounded<OutputInterlockSnapshot>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _current = BuildSnapshot(_interlock.Current, 0, 0, null, null);
        _interlock.Changed += OnInterlockChanged;
        _resetGuardRegistration = (_interlock as OutputInterlock)?.RegisterResetGuard(GetResetReadiness);
        _worker = Task.Run(ProcessSnapshotsAsync);
    }

    public OutputInterlockSupervisorSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool CanReset(out string blocker)
    {
        foreach (var participant in _participants)
        {
            var snapshot = participant.Current;
            if (snapshot.HasFault)
            {
                blocker = $"{participant.Name}: {snapshot.Message}";
                return false;
            }

            if (!participant.CanReset(out var participantBlocker))
            {
                blocker = string.IsNullOrWhiteSpace(participantBlocker)
                    ? $"{participant.Name} is not ready to reset."
                    : $"{participant.Name}: {participantBlocker}";
                return false;
            }
        }

        blocker = string.Empty;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _interlock.Changed -= OnInterlockChanged;
        _resetGuardRegistration?.Dispose();
        _channel.Writer.TryComplete();
        await _disposeCts.CancelAsync().ConfigureAwait(false);

        var waitTask = Task.WhenAny(_worker, Task.Delay(DisposalTimeout));
        await waitTask.ConfigureAwait(false);
        _disposeCts.Dispose();
    }

    private void OnInterlockChanged(object? sender, OutputInterlockSnapshot snapshot)
    {
        _channel.Writer.TryWrite(snapshot);
    }

    private (bool CanReset, string Blocker) GetResetReadiness()
    {
        return CanReset(out var blocker)
            ? (true, string.Empty)
            : (false, blocker);
    }

    private async Task ProcessSnapshotsAsync()
    {
        try
        {
            await foreach (var snapshot in _channel.Reader.ReadAllAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                await ProcessSnapshotAsync(snapshot, _disposeCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask ProcessSnapshotAsync(OutputInterlockSnapshot snapshot, CancellationToken cancellationToken)
    {
        long participantFailures = Current.ParticipantFailureCount;
        string? lastFailure = Current.LastFailure;

        if (snapshot.IsLatched)
        {
            foreach (var participant in _participants)
            {
                try
                {
                    using var participantTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    participantTimeout.CancelAfter(ParticipantTimeout);
                    await participant.SilenceAsync(snapshot, participantTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    participantFailures++;
                    lastFailure = $"{participant.Name} timed out while silencing for output interlock generation {snapshot.Generation}.";
                }
                catch (Exception ex)
                {
                    participantFailures++;
                    lastFailure = $"{participant.Name} failed while silencing for output interlock generation {snapshot.Generation}: {ex.Message}";
                }
            }
        }
        else
        {
            foreach (var participant in _participants)
            {
                try
                {
                    participant.OnInterlockReset(snapshot);
                }
                catch (Exception ex)
                {
                    participantFailures++;
                    lastFailure = $"{participant.Name} failed while observing output interlock reset generation {snapshot.Generation}: {ex.Message}";
                }
            }
        }

        lock (_gate)
        {
            _current = BuildSnapshot(
                snapshot,
                _current.ProcessedSnapshotCount + 1,
                participantFailures,
                lastFailure,
                DateTimeOffset.UtcNow);
        }
    }

    private OutputInterlockSupervisorSnapshot BuildSnapshot(
        OutputInterlockSnapshot interlock,
        long processedSnapshotCount,
        long participantFailureCount,
        string? lastFailure,
        DateTimeOffset? lastProcessedAtUtc)
    {
        return new OutputInterlockSupervisorSnapshot(
            interlock,
            _participants.Select(participant => participant.Current).ToArray(),
            processedSnapshotCount,
            participantFailureCount,
            lastFailure,
            lastProcessedAtUtc);
    }
}
