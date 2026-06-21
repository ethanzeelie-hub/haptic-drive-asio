using System.Threading.Channels;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Recording;

public sealed class TelemetryRecordingService : IAsyncDisposable
{
    public const int DefaultQueueCapacityPackets = 4_096;

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxPayloadLength;
    private readonly int _queueCapacityPackets;
    private readonly Func<string, Stream> _recordingStreamFactory;
    private RecordingSession? _session;
    private string? _lastErrorMessage;
    private bool _disposed;

    public TelemetryRecordingService(
        TimeProvider? timeProvider = null,
        int maxPayloadLength = TelemetryRecordingFile.DefaultMaxPayloadLength,
        int queueCapacityPackets = DefaultQueueCapacityPackets,
        Func<string, Stream>? recordingStreamFactory = null)
    {
        if (maxPayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadLength), "Maximum payload length must be positive.");
        }

        if (queueCapacityPackets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacityPackets), "Recording queue capacity must be positive.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxPayloadLength = maxPayloadLength;
        _queueCapacityPackets = queueCapacityPackets;
        _recordingStreamFactory = recordingStreamFactory ?? CreateFileStream;
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
                _lastErrorMessage,
                _session?.QueueCapacityPackets,
                _session?.QueuedPacketCount ?? 0,
                _session?.DroppedPacketCount ?? 0,
                _session?.RecordingIncomplete ?? false,
                _session?.IncompleteReason);
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

        Stream? stream = null;

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

            stream = _recordingStreamFactory(fullPath);

            var headerReservation = TelemetryRecordingFile.WriteHeader(stream, metadata);
            var recordingStream = stream;
            stream = null;
            var channel = Channel.CreateBounded<TelemetryRecordedPacket>(
                new BoundedChannelOptions(_queueCapacityPackets)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });
            Task<RecordingWriterResult>? writerTask = null;
            var session = new RecordingSession(fullPath, metadata.CreatedAtUtc, metadata, headerReservation, channel, writerTask: null!, _queueCapacityPackets);
            writerTask = Task.Run(
                () => WriteLoopAsync(recordingStream, headerReservation, channel.Reader, _maxPayloadLength, session, _timeProvider),
                CancellationToken.None);
            session.AttachWriterTask(writerTask);

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

        var recordedPacket = new TelemetryRecordedPacket(
            packet.SequenceNumber,
            packet.ReceivedAtUtc,
            relativeTime,
            packet.Payload);
        if (!session.Channel.Writer.TryWrite(recordedPacket))
        {
            if (session.Channel.Reader.Completion.IsCompleted || session.WriterTask.IsCompleted)
            {
                return SetSessionError(session, "Recording writer is not accepting packets.");
            }

            session.MarkPacketDropped();
            lock (_gate)
            {
                _lastErrorMessage = $"Recording queue full at capacity {_queueCapacityPackets:N0}; packet dropped.";
            }

            return TelemetryRecordingOperationResult.Dropped(_lastErrorMessage);
        }

        session.MarkPacketRecorded(relativeTime);
        return TelemetryRecordingOperationResult.Success("Packet queued for recording.");
    }

    public void MarkIncomplete(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Recording is incomplete.";
        }

        lock (_gate)
        {
            _session?.MarkIncomplete(message);
            _lastErrorMessage = message;
        }
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
        Stream stream,
        TelemetryRecordingFile.RecordingHeaderReservation headerReservation,
        ChannelReader<TelemetryRecordedPacket> reader,
        int maxPayloadLength,
        RecordingSession session,
        TimeProvider timeProvider)
    {
        var packetCount = 0L;
        var recordingCrc = new TelemetryRecordingFile.Crc32();

        try
        {
            await foreach (var packet in reader.ReadAllAsync().ConfigureAwait(false))
            {
                session.MarkPacketDequeued();
                TelemetryRecordingFile.WritePacket(
                    stream,
                    packet,
                    maxPayloadLength,
                    recordingCrc);
                packetCount++;
            }

            var endedAtUtc = timeProvider.GetUtcNow();
            var finalMetadata = session.BuildFinalMetadata(packetCount, endedAtUtc);
            TelemetryRecordingFile.WriteFooter(
                stream,
                packetCount,
                endedAtUtc,
                recordingCrc.GetCurrentHashAsUInt32());
            TelemetryRecordingFile.TryUpdateHeader(stream, headerReservation, finalMetadata);
            await stream.FlushAsync().ConfigureAwait(false);
            return RecordingWriterResult.Success(
                packetCount,
                finalMetadata.RecordingComplete
                    ? $"Recording stopped with {packetCount:N0} packets."
                    : $"Recording stopped with {packetCount:N0} packets and was marked incomplete.");
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
            TelemetryRecordingMetadata metadata,
            TelemetryRecordingFile.RecordingHeaderReservation headerReservation,
            Channel<TelemetryRecordedPacket> channel,
            Task<RecordingWriterResult> writerTask,
            int queueCapacityPackets)
        {
            FilePath = filePath;
            StartedAtUtc = startedAtUtc;
            Metadata = metadata;
            HeaderReservation = headerReservation;
            Channel = channel;
            WriterTask = writerTask;
            QueueCapacityPackets = queueCapacityPackets;
        }

        public string FilePath { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public TelemetryRecordingMetadata Metadata { get; }

        public TelemetryRecordingFile.RecordingHeaderReservation HeaderReservation { get; }

        public Channel<TelemetryRecordedPacket> Channel { get; }

        public Task<RecordingWriterResult> WriterTask { get; private set; }

        public int QueueCapacityPackets { get; }

        public long PacketCount => Interlocked.Read(ref _packetCount);

        public int QueuedPacketCount => Math.Max(0, (int)(Interlocked.Read(ref _enqueuedPacketCount) - Interlocked.Read(ref _dequeuedPacketCount)));

        public long DroppedPacketCount => Interlocked.Read(ref _droppedPacketCount);

        public bool RecordingIncomplete => Volatile.Read(ref _recordingIncomplete) > 0;

        public string? IncompleteReason { get; private set; }

        public TimeSpan? LastPacketRelativeTime { get; private set; }

        private long _packetCount;
        private long _enqueuedPacketCount;
        private long _dequeuedPacketCount;
        private long _droppedPacketCount;
        private int _recordingIncomplete;

        public void MarkPacketRecorded(TimeSpan relativeTime)
        {
            Interlocked.Increment(ref _packetCount);
            Interlocked.Increment(ref _enqueuedPacketCount);
            LastPacketRelativeTime = relativeTime;
        }

        public void MarkPacketDequeued()
        {
            Interlocked.Increment(ref _dequeuedPacketCount);
        }

        public void MarkPacketDropped()
        {
            Interlocked.Increment(ref _droppedPacketCount);
            MarkIncomplete("Recording queue overflowed; capture is incomplete.");
        }

        public void MarkIncomplete(string message)
        {
            Interlocked.Exchange(ref _recordingIncomplete, 1);
            IncompleteReason = message;
        }

        public void AttachWriterTask(Task<RecordingWriterResult> writerTask)
        {
            WriterTask = writerTask ?? throw new ArgumentNullException(nameof(writerTask));
        }

        public TelemetryRecordingMetadata BuildFinalMetadata(long packetCount, DateTimeOffset endedAtUtc)
        {
            return Metadata with
            {
                EndedAtUtc = endedAtUtc,
                PacketCount = packetCount,
                RecordingComplete = !RecordingIncomplete && DroppedPacketCount == 0,
                DroppedPacketCount = DroppedPacketCount
            };
        }
    }

    private sealed record RecordingWriterResult(bool Succeeded, long PacketCount, string Message)
    {
        public static RecordingWriterResult Success(long packetCount, string message)
        {
            return new(true, packetCount, message);
        }

        public static RecordingWriterResult Failure(long packetCount, string message)
        {
            return new(false, packetCount, message);
        }
    }

    private static Stream CreateFileStream(string fullPath)
    {
        return new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 16 * 1024,
            useAsync: true);
    }
}
