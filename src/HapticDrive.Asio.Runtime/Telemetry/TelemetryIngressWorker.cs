using System.Threading.Channels;
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
    string? LastErrorMessage);

public sealed class TelemetryIngressWorker : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly TelemetryIngressWorkerOptions _options;
    private readonly Func<UdpTelemetryPacket, HapticPipelinePacketResult> _processHapticPacket;
    private readonly Func<bool> _isRecordingEnabled;
    private readonly Func<UdpTelemetryPacket, TelemetryRecordingOperationResult> _recordPacket;
    private readonly Action<string> _markRecordingIncomplete;
    private readonly Func<bool> _isForwardingEnabled;
    private readonly Func<UdpTelemetryPacket, CancellationToken, ValueTask> _forwardPacketAsync;
    private readonly Channel<UdpTelemetryPacket> _hapticChannel;
    private readonly Channel<UdpTelemetryPacket> _recordingChannel;
    private readonly Channel<UdpTelemetryPacket> _forwardingChannel;
    private CancellationTokenSource? _stopCts;
    private Task? _hapticLoopTask;
    private Task? _recordingLoopTask;
    private Task? _forwardingLoopTask;
    private string? _lastErrorMessage;
    private long _receivedPacketCount;
    private long _queuedHapticPacketCount;
    private long _queuedRecordingPacketCount;
    private long _queuedForwardingPacketCount;
    private long _hapticDroppedPacketCount;
    private long _recordingDroppedPacketCount;
    private long _forwardingDroppedPacketCount;
    private long _recordingMarkedIncomplete;
    private bool _disposed;

    public TelemetryIngressWorker(
        Func<UdpTelemetryPacket, HapticPipelinePacketResult> processHapticPacket,
        Func<bool> isRecordingEnabled,
        Func<UdpTelemetryPacket, TelemetryRecordingOperationResult> recordPacket,
        Action<string> markRecordingIncomplete,
        Func<bool> isForwardingEnabled,
        Func<UdpTelemetryPacket, CancellationToken, ValueTask> forwardPacketAsync,
        TelemetryIngressWorkerOptions? options = null)
    {
        _processHapticPacket = processHapticPacket ?? throw new ArgumentNullException(nameof(processHapticPacket));
        _isRecordingEnabled = isRecordingEnabled ?? throw new ArgumentNullException(nameof(isRecordingEnabled));
        _recordPacket = recordPacket ?? throw new ArgumentNullException(nameof(recordPacket));
        _markRecordingIncomplete = markRecordingIncomplete ?? throw new ArgumentNullException(nameof(markRecordingIncomplete));
        _isForwardingEnabled = isForwardingEnabled ?? throw new ArgumentNullException(nameof(isForwardingEnabled));
        _forwardPacketAsync = forwardPacketAsync ?? throw new ArgumentNullException(nameof(forwardPacketAsync));
        _options = options ?? TelemetryIngressWorkerOptions.Default;
        _hapticChannel = CreateDropOldestChannel(_options.HapticChannelCapacity);
        _recordingChannel = Channel.CreateBounded<UdpTelemetryPacket>(
            new BoundedChannelOptions(_options.RecordingChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });
        _forwardingChannel = CreateDropOldestChannel(_options.ForwardingChannelCapacity);
    }

    public TelemetryIngressWorkerSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new TelemetryIngressWorkerSnapshot(
                IsRunning: _stopCts is { IsCancellationRequested: false },
                BackgroundWorkerCount: 3,
                ReceivedPacketCount: Interlocked.Read(ref _receivedPacketCount),
                HapticDroppedPacketCount: Interlocked.Read(ref _hapticDroppedPacketCount),
                RecordingDroppedPacketCount: Interlocked.Read(ref _recordingDroppedPacketCount),
                ForwardingDroppedPacketCount: Interlocked.Read(ref _forwardingDroppedPacketCount),
                RecordingMarkedIncomplete: Interlocked.Read(ref _recordingMarkedIncomplete) > 0,
                LastErrorMessage: _lastErrorMessage);
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

            var stopCts = new CancellationTokenSource();
            _stopCts = stopCts;
            _lastErrorMessage = null;
            _hapticLoopTask = Task.Run(() => RunHapticLoopAsync(stopCts.Token), CancellationToken.None);
            _recordingLoopTask = Task.Run(() => RunRecordingLoopAsync(stopCts.Token), CancellationToken.None);
            _forwardingLoopTask = Task.Run(() => RunForwardingLoopAsync(stopCts.Token), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    public bool Enqueue(UdpTelemetryPacket packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (_stopCts is not { IsCancellationRequested: false })
        {
            SetLastError("Telemetry ingress worker is not running.");
            return false;
        }

        Interlocked.Increment(ref _receivedPacketCount);

        var accepted = TryWriteDropOldest(
            _hapticChannel.Writer,
            packet,
            _options.HapticChannelCapacity,
            ref _queuedHapticPacketCount,
            ref _hapticDroppedPacketCount);

        if (_isRecordingEnabled())
        {
            if (!_recordingChannel.Writer.TryWrite(packet))
            {
                Interlocked.Increment(ref _recordingDroppedPacketCount);
                MarkRecordingIncomplete("Recording ingress channel dropped one or more packets.");
                SetLastError("Recording ingress channel dropped one or more packets.");
            }
            else
            {
                Interlocked.Increment(ref _queuedRecordingPacketCount);
            }
        }

        if (_isForwardingEnabled())
        {
            if (!TryWriteDropOldest(
                    _forwardingChannel.Writer,
                    packet,
                    _options.ForwardingChannelCapacity,
                    ref _queuedForwardingPacketCount,
                    ref _forwardingDroppedPacketCount))
            {
                SetLastError("Forwarding ingress channel is not accepting packets.");
            }
        }

        return accepted;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? stopCts;
        Task? hapticLoopTask;
        Task? recordingLoopTask;
        Task? forwardingLoopTask;

        lock (_gate)
        {
            stopCts = _stopCts;
            hapticLoopTask = _hapticLoopTask;
            recordingLoopTask = _recordingLoopTask;
            forwardingLoopTask = _forwardingLoopTask;
            _stopCts = null;
            _hapticLoopTask = null;
            _recordingLoopTask = null;
            _forwardingLoopTask = null;
        }

        if (stopCts is null)
        {
            return;
        }

        await stopCts.CancelAsync().ConfigureAwait(false);
        _hapticChannel.Writer.TryComplete();
        _recordingChannel.Writer.TryComplete();
        _forwardingChannel.Writer.TryComplete();

        await AwaitLoopAsync(hapticLoopTask).ConfigureAwait(false);
        await AwaitLoopAsync(recordingLoopTask).ConfigureAwait(false);
        await AwaitLoopAsync(forwardingLoopTask).ConfigureAwait(false);
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

    private async Task RunHapticLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var packet in _hapticChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queuedHapticPacketCount);

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

    private async Task RunRecordingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var packet in _recordingChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queuedRecordingPacketCount);
                var result = _recordPacket(packet);
                if (result.Status == TelemetryRecordingOperationStatus.Dropped)
                {
                    MarkRecordingIncomplete(result.Message);
                    SetLastError(result.Message);
                }
                else if (result.Status == TelemetryRecordingOperationStatus.Failure)
                {
                    MarkRecordingIncomplete(result.Message);
                    SetLastError(result.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkRecordingIncomplete($"Recording worker failed: {ex.Message}");
            SetLastError($"Recording worker failed: {ex.Message}");
        }
    }

    private async Task RunForwardingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var packet in _forwardingChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queuedForwardingPacketCount);

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
            _lastErrorMessage = message;
        }
    }

    private static Channel<UdpTelemetryPacket> CreateDropOldestChannel(int capacity)
    {
        return Channel.CreateBounded<UdpTelemetryPacket>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
    }

    private static bool TryWriteDropOldest(
        ChannelWriter<UdpTelemetryPacket> writer,
        UdpTelemetryPacket packet,
        int capacity,
        ref long queuedPacketCount,
        ref long droppedPacketCount)
    {
        var queuedCount = Interlocked.Increment(ref queuedPacketCount);
        if (!writer.TryWrite(packet))
        {
            Interlocked.Decrement(ref queuedPacketCount);
            return false;
        }

        if (queuedCount > capacity)
        {
            Interlocked.Decrement(ref queuedPacketCount);
            Interlocked.Increment(ref droppedPacketCount);
        }

        return true;
    }

    private static async Task AwaitLoopAsync(Task? loopTask)
    {
        if (loopTask is null)
        {
            return;
        }

        try
        {
            await loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
