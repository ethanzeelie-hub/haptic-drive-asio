using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Runtime;

public sealed record RuntimeLifecycleOperationResult(
    long Generation,
    bool Applied);

public sealed class RuntimeLifecycleCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HapticRuntimeControlSnapshot _currentSnapshot = HapticRuntimeControlSnapshot.Default;
    private long _generation;

    public long CurrentGeneration => Interlocked.Read(ref _generation);

    public HapticRuntimeControlSnapshot CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

    public bool ShouldApply(long generation)
    {
        return generation == CurrentGeneration;
    }

    public void PublishSnapshot(HapticRuntimeControlSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _currentSnapshot, snapshot);
    }

    public long AdvanceGeneration()
    {
        return Interlocked.Increment(ref _generation);
    }

    public async ValueTask<RuntimeLifecycleOperationResult> RunSerializedAsync(
        Func<long, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var generation = AdvanceGeneration();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await operation(generation, cancellationToken).ConfigureAwait(false);
            return new RuntimeLifecycleOperationResult(generation, ShouldApply(generation));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<RuntimeLifecycleOperationResult> RunShutdownAsync(
        IOutputInterlock outputInterlock,
        Func<long, CancellationToken, Task> shutdownOperation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputInterlock);
        ArgumentNullException.ThrowIfNull(shutdownOperation);

        outputInterlock.Trip(OutputInterlockReason.Shutdown, "Runtime shutdown requested.");
        return await RunSerializedAsync(shutdownOperation, cancellationToken).ConfigureAwait(false);
    }
}
