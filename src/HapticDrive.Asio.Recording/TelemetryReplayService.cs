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
    private string? _activeReplaySourceFilePath;
    private string _statusMessage = "Replay idle.";
    private long _packetsReplayed;

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
                _statusMessage);
        }
    }

    public async ValueTask<TelemetryReplayResult> ReplayFileAsync(
        string path,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var openResult = await TelemetryRecordingFile.OpenReaderAsync(path, _maxPayloadLength, cancellationToken).ConfigureAwait(false);
        if (!openResult.Succeeded || openResult.Reader is null)
        {
            return TelemetryReplayResult.Failure(0, openResult.Message);
        }

        await using var reader = openResult.Reader;
        return await ReplayPacketStreamAsync(
            reader.Metadata,
            ReadPacketsAsync(reader, cancellationToken),
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
        options ??= TelemetryReplayOptions.Fast;

        if (options.Speed <= 0 || double.IsNaN(options.Speed) || double.IsInfinity(options.Speed))
        {
            var failure = TelemetryReplayResult.Failure(0, "Replay speed must be a positive finite value.");
            SetStatus(failure);
            return failure;
        }

        using var replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
        }

        TelemetryReplayResult result;
        var previousRelativeTime = TimeSpan.Zero;

        try
        {
            await foreach (var recordedPacket in packets.ConfigureAwait(false))
            {
                replayCts.Token.ThrowIfCancellationRequested();

                if (options.PreserveTiming)
                {
                    var delay = ScaleDelay(recordedPacket.RelativeTime - previousRelativeTime, options.Speed);
                    if (delay > TimeSpan.Zero)
                    {
                        await DelayAsync(delay, replayCts.Token).ConfigureAwait(false);
                    }
                }

                var replayedPacket = new UdpTelemetryPacket(
                    recordedPacket.SequenceNumber,
                    recordedPacket.Payload.ToArray(),
                    new IPEndPoint(IPAddress.Loopback, 0),
                    metadata.CreatedAtUtc + recordedPacket.RelativeTime);

                PacketReplayed?.Invoke(
                    this,
                    new TelemetryReplayPacketEventArgs(replayedPacket, recordedPacket));
                Interlocked.Increment(ref _packetsReplayed);
                previousRelativeTime = recordedPacket.RelativeTime;
            }

            result = TelemetryReplayResult.Success(Interlocked.Read(ref _packetsReplayed));
        }
        catch (OperationCanceledException)
        {
            result = TelemetryReplayResult.Cancelled(Interlocked.Read(ref _packetsReplayed));
        }
        catch (EndOfStreamException ex)
        {
            result = TelemetryReplayResult.Failure(
                Interlocked.Read(ref _packetsReplayed),
                $"Replay failed: Recording is truncated: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            result = TelemetryReplayResult.Failure(
                Interlocked.Read(ref _packetsReplayed),
                $"Replay failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            result = TelemetryReplayResult.Failure(Interlocked.Read(ref _packetsReplayed), $"Replay failed: {ex.Message}");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeReplayCts, replayCts))
                {
                    _activeReplayCts = null;
                    _activeReplaySourceFilePath = null;
                }
            }
        }

        SetStatus(result);
        return result;
    }

    public async ValueTask StopAsync()
    {
        CancellationTokenSource? activeReplayCts;
        lock (_gate)
        {
            activeReplayCts = _activeReplayCts;
        }

        if (activeReplayCts is not null)
        {
            await activeReplayCts.CancelAsync().ConfigureAwait(false);
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

    private static IAsyncEnumerable<TelemetryRecordedPacket> ReadPacketsAsync(
        TelemetryRecordingFile.TelemetryRecordingFileReader reader,
        CancellationToken cancellationToken)
    {
        return reader.ReadPacketsAsync(cancellationToken);
    }
}
