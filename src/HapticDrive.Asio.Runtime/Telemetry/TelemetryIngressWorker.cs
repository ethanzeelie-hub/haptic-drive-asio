using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.Runtime.Telemetry;

public sealed record TelemetryIngressWorkerOptions(
    int HapticChannelCapacity = 512,
    int ForwardingChannelCapacity = 2_048,
    int RecordingChannelCapacity = 8_192)
{
    public static TelemetryIngressWorkerOptions Default { get; } = new();
}

public sealed record TelemetryIngressWorkerSnapshot(
    bool IsRunning,
    int BackgroundWorkerCount,
    long ReceivedPacketCount,
    long HapticDroppedPacketCount,
    long RecordingDroppedPacketCount,
    long ForwardingDroppedPacketCount,
    bool RecordingMarkedIncomplete,
    string? LastErrorMessage,
    int RemainingHapticPacketCount = 0,
    int RemainingForwardingPacketCount = 0,
    int RemainingRecordingPacketCount = 0);

public sealed class TelemetryIngressWorker : IAsyncDisposable
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly TelemetryIngressWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Func<UdpTelemetryPacket, HapticPipelinePacketResult> _processHapticPacket;
    private readonly Func<bool> _isRecordingEnabled;
    private readonly Func<UdpTelemetryPacket, TelemetryRecordingOperationResult> _enqueueForRecording;
    private readonly Func<TimeSpan, CancellationToken, ValueTask<TelemetryRecordingDrainResult>> _waitForRecordingDrainAsync;
    private readonly Action<string> _markRecordingIncomplete;
    private readonly Func<bool> _isForwardingEnabled;
    private readonly Func<UdpTelemetryPacket, CancellationToken, ValueTask> _forwardPacketAsync;
    private TelemetryIngressDropOldestQueue? _hapticQueue;
    private TelemetryIngressDropOldestQueue? _forwardingQueue;
    private CancellationTokenSource? _stopCts;
    private Task? _hapticLoopTask;
    private Task? _forwardingLoopTask;
    private string? _lastErrorMessage;
    private long _receivedPacketCount;
    private long _recordingDroppedPacketCount;
    private long _recordingMarkedIncomplete;
    private int _remainingRecordingPacketCount;
    private int _acceptingPackets;
    private bool _disposed;

    public TelemetryIngressWorker(
        Func<UdpTelemetryPacket, HapticPipelinePacketResult> processHapticPacket,
        Func<bool> isRecordingEnabled,
        Func<UdpTelemetryPacket, TelemetryRecordingOperationResult> enqueueForRecording,
        Func<TimeSpan, CancellationToken, ValueTask<TelemetryRecordingDrainResult>> waitForRecordingDrainAsync,
        Action<string> markRecordingIncomplete,
        Func<bool> isForwardingEnabled,
        Func<UdpTelemetryPacket, CancellationToken, ValueTask> forwardPacketAsync,
        TelemetryIngressWorkerOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _processHapticPacket = processHapticPacket ?? throw new ArgumentNullException(nameof(processHapticPacket));
        _isRecordingEnabled = isRecordingEnabled ?? throw new ArgumentNullException(nameof(isRecordingEnabled));
        _enqueueForRecording = enqueueForRecording ?? throw new ArgumentNullException(nameof(enqueueForRecording));
        _waitForRecordingDrainAsync = waitForRecordingDrainAsync ?? throw new ArgumentNullException(nameof(waitForRecordingDrainAsync));
        _markRecordingIncomplete = markRecordingIncomplete ?? throw new ArgumentNullException(nameof(markRecordingIncomplete));
        _isForwardingEnabled = isForwardingEnabled ?? throw new ArgumentNullException(nameof(isForwardingEnabled));
        _forwardPacketAsync = forwardPacketAsync ?? throw new ArgumentNullException(nameof(forwardPacketAsync));
        _options = options ?? TelemetryIngressWorkerOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TelemetryIngressWorkerSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new TelemetryIngressWorkerSnapshot(
                IsRunning: _stopCts is { IsCancellationRequested: false } && Volatile.Read(ref _acceptingPackets) == 1,
                BackgroundWorkerCount: 2,
                ReceivedPacketCount: Interlocked.Read(ref _receivedPacketCount),
                HapticDroppedPacketCount: _hapticQueue?.DroppedItemCount ?? 0,
                RecordingDroppedPacketCount: Interlocked.Read(ref _recordingDroppedPacketCount),
                ForwardingDroppedPacketCount: _forwardingQueue?.DroppedItemCount ?? 0,
                RecordingMarkedIncomplete: Interlocked.Read(ref _recordingMarkedIncomplete) > 0,
                LastErrorMessage: _lastErrorMessage,
                RemainingHapticPacketCount: _hapticQueue?.Count ?? 0,
                RemainingForwardingPacketCount: _forwardingQueue?.Count ?? 0,
                RemainingRecordingPacketCount: Volatile.Read(ref _remainingRecordingPacketCount));
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_stopCts is { IsCancellationRequested: false })
            {
                return ValueTask.CompletedTask;
            }

            _hapticQueue = new TelemetryIngressDropOldestQueue(_options.HapticChannelCapacity);
            _forwardingQueue = new TelemetryIngressDropOldestQueue(_options.ForwardingChannelCapacity);
            _stopCts = new CancellationTokenSource();
            _lastErrorMessage = null;
            Interlocked.Exchange(ref _receivedPacketCount, 0);
            Interlocked.Exchange(ref _recordingDroppedPacketCount, 0);
            Interlocked.Exchange(ref _recordingMarkedIncomplete, 0);
            Volatile.Write(ref _remainingRecordingPacketCount, 0);
            Volatile.Write(ref _acceptingPackets, 1);
            var hapticQueue = _hapticQueue;
            var forwardingQueue = _forwardingQueue;
            var stopToken = _stopCts.Token;
            _hapticLoopTask = Task.Run(() => RunHapticLoopAsync(hapticQueue, stopToken), CancellationToken.None);
            _forwardingLoopTask = Task.Run(() => RunForwardingLoopAsync(forwardingQueue, stopToken), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    public bool ProcessTelemetryPacket(UdpTelemetryPacket packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        TelemetryIngressDropOldestQueue? hapticQueue;
        lock (_gate)
        {
            hapticQueue = _hapticQueue;
        }

        if (Volatile.Read(ref _acceptingPackets) == 0 || hapticQueue is null)
        {
            SetLastError("Telemetry ingress worker is not accepting packets.");
            return false;
        }

        Interlocked.Increment(ref _receivedPacketCount);
        return hapticQueue.TryEnqueue(packet);
    }

    public void EnqueueForRecording(UdpTelemetryPacket packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (Volatile.Read(ref _acceptingPackets) == 0)
        {
            SetLastError("Telemetry ingress worker rejected a recording enqueue after accepting was disabled.");
            return;
        }

        if (!_isRecordingEnabled())
        {
            return;
        }

        var result = _enqueueForRecording(packet);
        if (result.Status is TelemetryRecordingOperationStatus.Dropped or TelemetryRecordingOperationStatus.Failure)
        {
            Interlocked.Increment(ref _recordingDroppedPacketCount);
            MarkRecordingIncomplete(result.Message);
            SetLastError(result.Message);
        }
    }

    public bool EnqueueForForwarding(UdpTelemetryPacket packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (!_isForwardingEnabled())
        {
            return false;
        }

        TelemetryIngressDropOldestQueue? forwardingQueue;
        lock (_gate)
        {
            forwardingQueue = _forwardingQueue;
        }

        if (Volatile.Read(ref _acceptingPackets) == 0 || forwardingQueue is null)
        {
            SetLastError("Telemetry ingress worker rejected a forwarding enqueue after accepting was disabled.");
            return false;
        }

        return forwardingQueue.TryEnqueue(packet);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? stopCts;
        Task? hapticLoopTask;
        Task? forwardingLoopTask;
        TelemetryIngressDropOldestQueue? hapticQueue;
        TelemetryIngressDropOldestQueue? forwardingQueue;

        lock (_gate)
        {
            stopCts = _stopCts;
            hapticLoopTask = _hapticLoopTask;
            forwardingLoopTask = _forwardingLoopTask;
            hapticQueue = _hapticQueue;
            forwardingQueue = _forwardingQueue;
            _stopCts = null;
            _hapticLoopTask = null;
            _forwardingLoopTask = null;
            Volatile.Write(ref _acceptingPackets, 0);
        }

        if (stopCts is null)
        {
            return;
        }

        hapticQueue?.Complete();
        forwardingQueue?.Complete();

        await AwaitLoopAsync(hapticLoopTask, stopCts, cancellationToken).ConfigureAwait(false);
        await AwaitLoopAsync(forwardingLoopTask, stopCts, cancellationToken).ConfigureAwait(false);

        var recordingDrain = await _waitForRecordingDrainAsync(DrainTimeout, cancellationToken).ConfigureAwait(false);
        if (!recordingDrain.Drained)
        {
            Interlocked.Exchange(ref _recordingMarkedIncomplete, 1);
            Volatile.Write(ref _remainingRecordingPacketCount, recordingDrain.RemainingQueuedPacketCount);
            _markRecordingIncomplete(recordingDrain.Message);
            lock (_gate)
            {
                _lastErrorMessage = $"{_timeProvider.GetUtcNow():O} {recordingDrain.Message}";
                _hapticQueue = hapticQueue;
                _forwardingQueue = forwardingQueue;
            }
        }
        else
        {
            Volatile.Write(ref _remainingRecordingPacketCount, 0);
        }

        stopCts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunHapticLoopAsync(
        TelemetryIngressDropOldestQueue queue,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var packet = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                if (packet is null)
                {
                    return;
                }

                try
                {
                    _processHapticPacket(packet);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    SetLastError($"Haptic telemetry processing failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunForwardingLoopAsync(
        TelemetryIngressDropOldestQueue queue,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var packet = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                if (packet is null)
                {
                    return;
                }

                try
                {
                    await _forwardPacketAsync(packet, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    SetLastError($"UDP forwarding failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void MarkRecordingIncomplete(string message)
    {
        Interlocked.Exchange(ref _recordingMarkedIncomplete, 1);
        _markRecordingIncomplete(message);
    }

    private void SetLastError(string message)
    {
        lock (_gate)
        {
            _lastErrorMessage = $"{_timeProvider.GetUtcNow():O} {message}";
        }
    }

    private static async Task AwaitLoopAsync(
        Task? loopTask,
        CancellationTokenSource stopCts,
        CancellationToken cancellationToken)
    {
        if (loopTask is null)
        {
            return;
        }

        try
        {
            await loopTask.WaitAsync(DrainTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        catch (TimeoutException)
        {
            await stopCts.CancelAsync().ConfigureAwait(false);
        }
    }
}
