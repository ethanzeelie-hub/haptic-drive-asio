using System.Net;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Recording;

public sealed record TelemetryReplayOptions(bool PreserveTiming, double Speed)
{
    public static TelemetryReplayOptions Fast { get; } = new(false, 1);

    public static TelemetryReplayOptions TimePreserving { get; } = new(true, 1);
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
    private readonly int _maxPayloadLength;
    private CancellationTokenSource? _activeReplayCts;

    public TelemetryReplayService(
        TimeProvider? timeProvider = null,
        int maxPayloadLength = TelemetryRecordingFile.DefaultMaxPayloadLength)
    {
        if (maxPayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadLength), "Maximum payload length must be positive.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxPayloadLength = maxPayloadLength;
    }

    public event EventHandler<TelemetryReplayPacketEventArgs>? PacketReplayed;

    public async ValueTask<TelemetryReplayResult> ReplayFileAsync(
        string path,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await TelemetryRecordingFile.LoadAsync(path, _maxPayloadLength, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Succeeded || loadResult.Recording is null)
        {
            return TelemetryReplayResult.Failure(0, loadResult.Message);
        }

        return await ReplayAsync(loadResult.Recording, options, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TelemetryReplayResult> ReplayAsync(
        TelemetryRecording recording,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        options ??= TelemetryReplayOptions.Fast;

        if (options.Speed <= 0 || double.IsNaN(options.Speed) || double.IsInfinity(options.Speed))
        {
            return TelemetryReplayResult.Failure(0, "Replay speed must be a positive finite value.");
        }

        using var replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_gate)
        {
            if (_activeReplayCts is not null)
            {
                return TelemetryReplayResult.Failure(0, "Replay is already running.");
            }

            _activeReplayCts = replayCts;
        }

        var packetsReplayed = 0L;
        var previousRelativeTime = TimeSpan.Zero;

        try
        {
            foreach (var recordedPacket in recording.Packets)
            {
                replayCts.Token.ThrowIfCancellationRequested();

                if (options.PreserveTiming)
                {
                    var delay = ScaleDelay(recordedPacket.RelativeTime - previousRelativeTime, options.Speed);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, _timeProvider, replayCts.Token).ConfigureAwait(false);
                    }
                }

                var replayedPacket = new UdpTelemetryPacket(
                    recordedPacket.SequenceNumber,
                    recordedPacket.Payload.ToArray(),
                    new IPEndPoint(IPAddress.Loopback, 0),
                    recording.Metadata.CreatedAtUtc + recordedPacket.RelativeTime);

                PacketReplayed?.Invoke(
                    this,
                    new TelemetryReplayPacketEventArgs(replayedPacket, recordedPacket));
                packetsReplayed++;
                previousRelativeTime = recordedPacket.RelativeTime;
            }

            return TelemetryReplayResult.Success(packetsReplayed);
        }
        catch (OperationCanceledException)
        {
            return TelemetryReplayResult.Cancelled(packetsReplayed);
        }
        catch (Exception ex)
        {
            return TelemetryReplayResult.Failure(packetsReplayed, $"Replay failed: {ex.Message}");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeReplayCts, replayCts))
                {
                    _activeReplayCts = null;
                }
            }
        }
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
}
