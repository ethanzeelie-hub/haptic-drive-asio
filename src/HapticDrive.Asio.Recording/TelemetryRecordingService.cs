using System.Threading.Channels;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Recording;

public sealed class TelemetryRecordingService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxPayloadLength;
    private RecordingSession? _session;
    private string? _lastErrorMessage;
    private bool _disposed;

    public TelemetryRecordingService(
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

    public TelemetryRecordingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new TelemetryRecordingSnapshot(
                _session is not null,
                _session?.FilePath,
                _session?.PacketCount ?? 0,
                _session?.LastPacketRelativeTime,
                _lastErrorMessage);
        }
    }

    public ValueTask<TelemetryRecordingOperationResult> StartAsync(
        string path,
        TelemetryRecordingMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(TelemetryRecordingOperationResult.Cancelled("Recording start was cancelled."));
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.FromResult(TelemetryRecordingOperationResult.Failure("Recording service is disposed."));
            }

            if (_session is not null)
            {
                return ValueTask.FromResult(TelemetryRecordingOperationResult.AlreadyRecording("Recording is already running."));
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.FromResult(TelemetryRecordingOperationResult.Failure("Recording path is required."));
        }

        FileStream? stream = null;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var createdAtUtc = _timeProvider.GetUtcNow();
            metadata ??= TelemetryRecordingMetadata.CreateDefault(createdAtUtc);

            stream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);

            var packetCountOffset = TelemetryRecordingFile.WriteHeader(stream, metadata);
            var recordingStream = stream;
            stream = null;
            var channel = Channel.CreateUnbounded<TelemetryRecordedPacket>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
            var writerTask = Task.Run(
                () => WriteLoopAsync(recordingStream, packetCountOffset, channel.Reader, _maxPayloadLength),
                CancellationToken.None);
            var session = new RecordingSession(fullPath, metadata.CreatedAtUtc, channel, writerTask);

            lock (_gate)
            {
                if (_session is not null)
                {
                    channel.Writer.TryComplete();
                    return ValueTask.FromResult(TelemetryRecordingOperationResult.AlreadyRecording("Recording is already running."));
                }

                _session = session;
                _lastErrorMessage = null;
            }

            return ValueTask.FromResult(TelemetryRecordingOperationResult.Success("Recording started."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidDataException)
        {
            stream?.Dispose();

            lock (_gate)
            {
                _lastErrorMessage = ex.Message;
            }

            return ValueTask.FromResult(TelemetryRecordingOperationResult.Failure($"Recording could not start: {ex.Message}"));
        }
    }

    public TelemetryRecordingOperationResult RecordPacket(UdpTelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        RecordingSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return TelemetryRecordingOperationResult.Failure("Recording service is disposed.");
            }

            session = _session;
        }

        if (session is null)
        {
            return TelemetryRecordingOperationResult.NotRecording("Recording is not running.");
        }

        if (packet.Payload.Length > _maxPayloadLength)
        {
            return SetSessionError(
                session,
                $"Packet payload length {packet.Payload.Length} exceeds {_maxPayloadLength} bytes.");
        }

        var relativeTime = packet.ReceivedAtUtc - session.StartedAtUtc;
        if (relativeTime < TimeSpan.Zero)
        {
            relativeTime = TimeSpan.Zero;
        }

        var recordedPacket = new TelemetryRecordedPacket(packet.SequenceNumber, relativeTime, packet.Payload);
        if (!session.Channel.Writer.TryWrite(recordedPacket))
        {
            return SetSessionError(session, "Recording writer is not accepting packets.");
        }

        session.MarkPacketRecorded(relativeTime);
        return TelemetryRecordingOperationResult.Success("Packet queued for recording.");
    }

    public async ValueTask<TelemetryRecordingOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordingSession? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }

        if (session is null)
        {
            return TelemetryRecordingOperationResult.NotRecording("Recording is not running.");
        }

        session.Channel.Writer.TryComplete();

        RecordingWriterResult result;
        try
        {
            result = await session.WriterTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = RecordingWriterResult.Failure(0, "Recording stop was cancelled.");
        }

        lock (_gate)
        {
            _lastErrorMessage = result.Succeeded ? null : result.Message;
        }

        return result.Succeeded
            ? TelemetryRecordingOperationResult.Success($"Recording stopped with {result.PacketCount:N0} packets.")
            : TelemetryRecordingOperationResult.Failure(result.Message);
    }

    public async ValueTask DisposeAsync()
    {
        RecordingSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        if (session is not null)
        {
            session.Channel.Writer.TryComplete();
            await session.WriterTask.ConfigureAwait(false);
        }
    }

    private TelemetryRecordingOperationResult SetSessionError(RecordingSession session, string message)
    {
        session.Channel.Writer.TryComplete(new InvalidDataException(message));

        lock (_gate)
        {
            _lastErrorMessage = message;
        }

        return TelemetryRecordingOperationResult.Failure(message);
    }

    private static async Task<RecordingWriterResult> WriteLoopAsync(
        FileStream stream,
        long packetCountOffset,
        ChannelReader<TelemetryRecordedPacket> reader,
        int maxPayloadLength)
    {
        var packetCount = 0L;

        try
        {
            await foreach (var packet in reader.ReadAllAsync().ConfigureAwait(false))
            {
                TelemetryRecordingFile.WritePacket(
                    stream,
                    packet,
                    maxPayloadLength);
                packetCount++;
            }

            TelemetryRecordingFile.UpdatePacketCount(stream, packetCountOffset, packetCount);
            await stream.FlushAsync().ConfigureAwait(false);
            return RecordingWriterResult.Success(packetCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RecordingWriterResult.Failure(packetCount, $"Recording writer failed: {ex.Message}");
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class RecordingSession
    {
        public RecordingSession(
            string filePath,
            DateTimeOffset startedAtUtc,
            Channel<TelemetryRecordedPacket> channel,
            Task<RecordingWriterResult> writerTask)
        {
            FilePath = filePath;
            StartedAtUtc = startedAtUtc;
            Channel = channel;
            WriterTask = writerTask;
        }

        public string FilePath { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public Channel<TelemetryRecordedPacket> Channel { get; }

        public Task<RecordingWriterResult> WriterTask { get; }

        public long PacketCount => Interlocked.Read(ref _packetCount);

        public TimeSpan? LastPacketRelativeTime { get; private set; }

        private long _packetCount;

        public void MarkPacketRecorded(TimeSpan relativeTime)
        {
            Interlocked.Increment(ref _packetCount);
            LastPacketRelativeTime = relativeTime;
        }
    }

    private sealed record RecordingWriterResult(bool Succeeded, long PacketCount, string Message)
    {
        public static RecordingWriterResult Success(long packetCount)
        {
            return new(true, packetCount, "Recording writer completed.");
        }

        public static RecordingWriterResult Failure(long packetCount, string message)
        {
            return new(false, packetCount, message);
        }
    }
}
