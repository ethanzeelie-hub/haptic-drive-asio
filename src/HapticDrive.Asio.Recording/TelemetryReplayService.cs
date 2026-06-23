using System.Net;
using System.Runtime.CompilerServices;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Recording;

public sealed record TelemetryReplayOptions(bool PreserveTiming, double Speed)
{
    public static TelemetryReplayOptions Fast { get; } = new(false, 1);

    public static TelemetryReplayOptions TimePreserving { get; } = new(true, 1);
}

public interface ITelemetryReplayDelayScheduler
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class TelemetryReplayPacketEventArgs : EventArgs
{
    public TelemetryReplayPacketEventArgs(UdpTelemetryPacket packet, TelemetryRecordedPacket recordedPacket)
    {
        Packet = packet;
        RecordedPacket = recordedPacket;
    }

    public UdpTelemetryPacket Packet { get; }

    public TelemetryRecordedPacket RecordedPacket { get; }
}

public interface ITelemetryReplayService
{
    event EventHandler<TelemetryReplayPacketEventArgs>? PacketReplayed;

    TelemetryReplaySnapshot GetSnapshot();

    ValueTask<TelemetryReplayResult> ReplayAsync(
        TelemetryRecording recording,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<TelemetryReplayResult> ReplayFileAsync(
        string path,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync();
}

public sealed class TelemetryReplayService : ITelemetryReplayService
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly ITelemetryReplayDelayScheduler? _delayScheduler;
    private readonly int _maxPayloadLength;
    private CancellationTokenSource? _activeReplayCts;
    private Task<TelemetryReplayResult>? _activeReplayTask;
    private string? _activeReplaySourceFilePath;
    private string _statusMessage = "Replay idle.";
    private long _packetsReplayed;
    private long _totalReplayDriftTicks;
    private long _maxLatePacketTicks;
    private long _skippedSleepCount;
    private long _subscriberExceptionCount;
    private string? _lastSubscriberErrorMessage;

    public TelemetryReplayService(
        TimeProvider? timeProvider = null,
        int maxPayloadLength = TelemetryRecordingFile.DefaultMaxPayloadLength,
        ITelemetryReplayDelayScheduler? delayScheduler = null)
    {
        if (maxPayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadLength), "Maximum payload length must be positive.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _delayScheduler = delayScheduler;
        _maxPayloadLength = maxPayloadLength;
    }

    public event EventHandler<TelemetryReplayPacketEventArgs>? PacketReplayed;

    public TelemetryReplaySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new TelemetryReplaySnapshot(
                _activeReplayCts is not null,
                _activeReplaySourceFilePath,
                Interlocked.Read(ref _packetsReplayed),
                _statusMessage,
                TimeSpan.FromTicks(Interlocked.Read(ref _totalReplayDriftTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maxLatePacketTicks)),
                Interlocked.Read(ref _skippedSleepCount),
                Interlocked.Read(ref _subscriberExceptionCount),
                _lastSubscriberErrorMessage);
        }
    }

    public async ValueTask<TelemetryReplayResult> ReplayFileAsync(
        string path,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        TelemetryRecordingReader reader;
        try
        {
            reader = await TelemetryRecordingReader.OpenAsync(path, _maxPayloadLength, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or ArgumentException)
        {
            return TelemetryReplayResult.Failure(0, $"Replay failed: {ex.Message}");
        }

        await using var replayReader = reader;
        return await ReplayPacketStreamAsync(
            replayReader.Metadata,
            replayReader.ReadPacketsAsync(cancellationToken),
            options,
            fullPath: path,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TelemetryReplayResult> ReplayAsync(
        TelemetryRecording recording,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        return await ReplayPacketStreamAsync(
            recording.Metadata,
            ReadPacketsAsync(recording.Packets, cancellationToken),
            options,
            fullPath: null,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TelemetryReplayResult> ReplayPacketStreamAsync(
        TelemetryRecordingMetadata metadata,
        IAsyncEnumerable<TelemetryRecordedPacket> packets,
        TelemetryReplayOptions? options,
        string? fullPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(packets);
        options ??= TelemetryReplayOptions.TimePreserving;

        if (options.Speed <= 0 || double.IsNaN(options.Speed) || double.IsInfinity(options.Speed))
        {
            var failure = TelemetryReplayResult.Failure(0, "Replay speed must be a positive finite value.");
            SetStatus(failure);
            return failure;
        }

        var replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<TelemetryReplayResult>? replayTask;

        lock (_gate)
        {
            if (_activeReplayCts is not null)
            {
                return TelemetryReplayResult.Failure(0, "Replay is already running.");
            }

            _activeReplayCts = replayCts;
            _activeReplaySourceFilePath = fullPath;
            _statusMessage = "Replay active.";
            Interlocked.Exchange(ref _packetsReplayed, 0);
            Interlocked.Exchange(ref _totalReplayDriftTicks, 0);
            Interlocked.Exchange(ref _maxLatePacketTicks, 0);
            Interlocked.Exchange(ref _skippedSleepCount, 0);
            Interlocked.Exchange(ref _subscriberExceptionCount, 0);
            _lastSubscriberErrorMessage = null;
            replayTask = RunReplayLoopAsync(packets, options, replayCts.Token);
            _activeReplayTask = replayTask;
        }

        try
        {
            var result = await replayTask.ConfigureAwait(false);
            SetStatus(result);
            return result;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeReplayCts, replayCts) || ReferenceEquals(_activeReplayTask, replayTask))
                {
                    _activeReplayCts = null;
                    _activeReplayTask = null;
                    _activeReplaySourceFilePath = null;
                }
            }

            replayCts.Dispose();
        }
    }

    public async ValueTask StopAsync()
    {
        CancellationTokenSource? activeReplayCts;
        Task<TelemetryReplayResult>? activeReplayTask;
        lock (_gate)
        {
            activeReplayCts = _activeReplayCts;
            activeReplayTask = _activeReplayTask;
        }

        if (activeReplayCts is not null)
        {
            await activeReplayCts.CancelAsync().ConfigureAwait(false);
        }

        if (activeReplayTask is not null)
        {
            await activeReplayTask.ConfigureAwait(false);
        }
    }

    private async Task<TelemetryReplayResult> RunReplayLoopAsync(
        IAsyncEnumerable<TelemetryRecordedPacket> packets,
        TelemetryReplayOptions options,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? firstRecordedPacketAtUtc = null;
        long replayStartTimestamp = 0;

        try
        {
            await foreach (var recordedPacket in packets.ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (firstRecordedPacketAtUtc is null)
                {
                    firstRecordedPacketAtUtc = recordedPacket.ReceivedAtUtc;
                    replayStartTimestamp = _timeProvider.GetTimestamp();
                }

                var targetOffset = TimeSpan.Zero;
                if (options.PreserveTiming)
                {
                    targetOffset = ScaleDelay(recordedPacket.ReceivedAtUtc - firstRecordedPacketAtUtc.Value, options.Speed);
                    var elapsed = _timeProvider.GetElapsedTime(replayStartTimestamp, _timeProvider.GetTimestamp());
                    var remainingDelay = targetOffset - elapsed;
                    if (remainingDelay > TimeSpan.Zero)
                    {
                        await DelayAsync(remainingDelay, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    else if (targetOffset > TimeSpan.Zero)
                    {
                        Interlocked.Increment(ref _skippedSleepCount);
                    }
                }

                var actualElapsed = options.PreserveTiming
                    ? _timeProvider.GetElapsedTime(replayStartTimestamp, _timeProvider.GetTimestamp())
                    : TimeSpan.Zero;
                var lateBy = actualElapsed - targetOffset;
                if (lateBy > TimeSpan.Zero)
                {
                    Interlocked.Add(ref _totalReplayDriftTicks, lateBy.Ticks);
                    UpdateMaxLatePacketTicks(lateBy.Ticks);
                }

                var replayedPacket = new UdpTelemetryPacket(
                    recordedPacket.SequenceNumber,
                    recordedPacket.Payload,
                    new IPEndPoint(IPAddress.Loopback, 0),
                    _timeProvider.GetUtcNow(),
                    _timeProvider.GetTimestamp());

                PublishPacketReplayed(replayedPacket, recordedPacket);
                Interlocked.Increment(ref _packetsReplayed);
            }

            return TelemetryReplayResult.Success(Interlocked.Read(ref _packetsReplayed));
        }
        catch (OperationCanceledException)
        {
            return TelemetryReplayResult.Cancelled(Interlocked.Read(ref _packetsReplayed));
        }
        catch (EndOfStreamException ex)
        {
            return TelemetryReplayResult.Failure(
                Interlocked.Read(ref _packetsReplayed),
                $"Replay failed: Recording is truncated: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return TelemetryReplayResult.Failure(
                Interlocked.Read(ref _packetsReplayed),
                $"Replay failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return TelemetryReplayResult.Failure(Interlocked.Read(ref _packetsReplayed), $"Replay failed: {ex.Message}");
        }
    }

    private static TimeSpan ScaleDelay(TimeSpan delay, double speed)
    {
        if (delay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var scaledTicks = (long)Math.Max(0, Math.Round(delay.Ticks / speed));
        return TimeSpan.FromTicks(scaledTicks);
    }

    private ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return _delayScheduler is not null
            ? _delayScheduler.DelayAsync(delay, cancellationToken)
            : new ValueTask(Task.Delay(delay, _timeProvider, cancellationToken));
    }

    private void SetStatus(TelemetryReplayResult result)
    {
        lock (_gate)
        {
            _statusMessage = result.Message;
            Interlocked.Exchange(ref _packetsReplayed, result.PacketsReplayed);
        }
    }

    private void UpdateMaxLatePacketTicks(long latePacketTicks)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _maxLatePacketTicks);
            if (latePacketTicks <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxLatePacketTicks, latePacketTicks, current) == current)
            {
                return;
            }
        }
    }

    private void PublishPacketReplayed(UdpTelemetryPacket packet, TelemetryRecordedPacket recordedPacket)
    {
        var subscribers = PacketReplayed;
        if (subscribers is null)
        {
            return;
        }

        var args = new TelemetryReplayPacketEventArgs(packet, recordedPacket);
        foreach (EventHandler<TelemetryReplayPacketEventArgs> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _lastSubscriberErrorMessage = ex.Message;
                }

                Interlocked.Increment(ref _subscriberExceptionCount);
            }
        }
    }

    private static async IAsyncEnumerable<TelemetryRecordedPacket> ReadPacketsAsync(
        IReadOnlyList<TelemetryRecordedPacket> packets,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var packet in packets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return packet;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

}
