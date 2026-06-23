using System.IO;
using System.Threading.Channels;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Recording;

public sealed class TelemetryRecordingService : IAsyncDisposable
{
    public const int DefaultQueueCapacityPackets = 8_192;

    private readonly object _sessionGate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
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
        lock (_sessionGate)
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

    public async ValueTask<TelemetryRecordingOperationResult> StartAsync(
        string path,
        TelemetryRecordingMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return TelemetryRecordingOperationResult.Cancelled("Recording start was cancelled.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return TelemetryRecordingOperationResult.Failure("Recording path is required.");
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            lock (_sessionGate)
            {
                if (_disposed)
                {
                    return TelemetryRecordingOperationResult.Failure("Recording service is disposed.");
                }

                if (_session is not null)
                {
                    return TelemetryRecordingOperationResult.AlreadyRecording("Recording is already running.");
                }
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            var createdAtUtc = _timeProvider.GetUtcNow();
            metadata ??= TelemetryRecordingMetadata.CreateDefault(createdAtUtc);
            var session = RecordingSession.CreateReserved(fullPath, metadata, _queueCapacityPackets);

            lock (_sessionGate)
            {
                _session = session;
                _lastErrorMessage = null;
            }

            Stream? stream = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                stream = _recordingStreamFactory(fullPath);
                var headerReservation = TelemetryRecordingFile.WriteHeader(stream, metadata);
                var recordingStream = stream;
                session.AttachWriterTask(Task.Run(
                    () => WriteLoopAsync(
                        recordingStream,
                        headerReservation,
                        session.Channel.Reader,
                        _maxPayloadLength,
                        session,
                        _timeProvider),
                    CancellationToken.None));
                stream = null;
                return TelemetryRecordingOperationResult.Success("Recording started.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidDataException)
            {
                stream?.Dispose();
                session.FailInitialization($"Recording could not start: {ex.Message}");
                lock (_sessionGate)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        _session = null;
                    }

                    _lastErrorMessage = ex.Message;
                }

                return TelemetryRecordingOperationResult.Failure($"Recording could not start: {ex.Message}");
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public TelemetryRecordingOperationResult RecordPacket(UdpTelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        RecordingSession? session;
        lock (_sessionGate)
        {
            if (_disposed)
            {
                return TelemetryRecordingOperationResult.Failure("Recording service is disposed.");
            }

            session = _session;
        }

        if (session is null || !session.IsAccepting)
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
            if (session.WriterTask.IsCompleted || !session.IsAccepting)
            {
                return SetSessionError(session, "Recording writer is not accepting packets.");
            }

            session.MarkPacketDropped();
            lock (_sessionGate)
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

        lock (_sessionGate)
        {
            _session?.MarkIncomplete(message);
            _lastErrorMessage = message;
        }
    }

    public async ValueTask<TelemetryRecordingDrainResult> WaitForQueueDrainAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Drain timeout cannot be negative.");
        }

        RecordingSession? session;
        lock (_sessionGate)
        {
            session = _session;
        }

        if (session is null)
        {
            return TelemetryRecordingDrainResult.Complete();
        }

        var deadlineUtc = _timeProvider.GetUtcNow() + timeout;
        while (session.QueuedPacketCount > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_timeProvider.GetUtcNow() >= deadlineUtc)
            {
                return TelemetryRecordingDrainResult.TimedOut(session.QueuedPacketCount);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }

        return TelemetryRecordingDrainResult.Complete();
    }

    public async ValueTask<TelemetryRecordingOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            RecordingSession? session;
            lock (_sessionGate)
            {
                session = _session;
                _session = null;
            }

            if (session is null)
            {
                return TelemetryRecordingOperationResult.NotRecording("Recording is not running.");
            }

            session.StopAccepting();
            session.Channel.Writer.TryComplete();

            RecordingWriterResult result;
            try
            {
                result = await session.WriterTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = RecordingWriterResult.Failure(0, "Recording stop was cancelled.");
            }

            lock (_sessionGate)
            {
                _lastErrorMessage = result.Succeeded ? result.WarningMessage : result.Message;
            }

            return result.Succeeded
                ? TelemetryRecordingOperationResult.Success(result.Message)
                : TelemetryRecordingOperationResult.Failure(result.Message);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        RecordingSession? session;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (_sessionGate)
            {
                session = _session;
                _session = null;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (session is not null)
        {
            session.StopAccepting();
            session.Channel.Writer.TryComplete();
            await session.WriterTask.ConfigureAwait(false);
        }

        _lifecycleGate.Dispose();
    }

    private TelemetryRecordingOperationResult SetSessionError(RecordingSession session, string message)
    {
        session.MarkIncomplete(message);
        session.StopAccepting();
        session.Channel.Writer.TryComplete(new InvalidDataException(message));

        lock (_sessionGate)
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

            try
            {
                if (!TelemetryRecordingFile.TryUpdateHeader(stream, headerReservation, finalMetadata))
                {
                    session.SetHeaderUpdateWarning("Recording header update could not be applied; footer remains authoritative.");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
            {
                session.SetHeaderUpdateWarning($"Recording header update failed safely: {ex.Message}");
            }

            FlushRecordingStream(stream);
            await stream.FlushAsync().ConfigureAwait(false);
            return RecordingWriterResult.Success(
                packetCount,
                BuildStopMessage(finalMetadata, packetCount, session.WarningMessage),
                session.WarningMessage);
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

    private static string BuildStopMessage(
        TelemetryRecordingMetadata metadata,
        long packetCount,
        string? warningMessage)
    {
        var message = metadata.RecordingComplete
            ? $"Recording stopped with {packetCount:N0} packets."
            : $"Recording stopped with {packetCount:N0} packets and was marked incomplete.";

        return string.IsNullOrWhiteSpace(warningMessage)
            ? message
            : $"{message} Warning: {warningMessage}";
    }

    private static void FlushRecordingStream(Stream stream)
    {
        if (stream is FileStream fileStream)
        {
            fileStream.Flush(flushToDisk: true);
            return;
        }

        stream.Flush();
    }

    private sealed class RecordingSession
    {
        private readonly TaskCompletionSource<RecordingWriterResult> _writerCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _packetCount;
        private long _enqueuedPacketCount;
        private long _dequeuedPacketCount;
        private long _droppedPacketCount;
        private int _recordingIncomplete;
        private int _accepting = 1;

        private RecordingSession(
            string filePath,
            DateTimeOffset startedAtUtc,
            TelemetryRecordingMetadata metadata,
            Channel<TelemetryRecordedPacket> channel,
            int queueCapacityPackets)
        {
            FilePath = filePath;
            StartedAtUtc = startedAtUtc;
            Metadata = metadata;
            Channel = channel;
            QueueCapacityPackets = queueCapacityPackets;
        }

        public string FilePath { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public TelemetryRecordingMetadata Metadata { get; }

        public Channel<TelemetryRecordedPacket> Channel { get; }

        public Task<RecordingWriterResult> WriterTask => _writerCompletion.Task;

        public int QueueCapacityPackets { get; }

        public long PacketCount => Interlocked.Read(ref _packetCount);

        public int QueuedPacketCount => Math.Max(0, (int)(Interlocked.Read(ref _enqueuedPacketCount) - Interlocked.Read(ref _dequeuedPacketCount)));

        public long DroppedPacketCount => Interlocked.Read(ref _droppedPacketCount);

        public bool RecordingIncomplete => Volatile.Read(ref _recordingIncomplete) > 0;

        public bool IsAccepting => Volatile.Read(ref _accepting) == 1;

        public string? IncompleteReason { get; private set; }

        public string? WarningMessage { get; private set; }

        public TimeSpan? LastPacketRelativeTime { get; private set; }

        public static RecordingSession CreateReserved(
            string filePath,
            TelemetryRecordingMetadata metadata,
            int queueCapacityPackets)
        {
            return new RecordingSession(
                filePath,
                metadata.CreatedAtUtc,
                metadata,
                System.Threading.Channels.Channel.CreateBounded<TelemetryRecordedPacket>(
                    new BoundedChannelOptions(queueCapacityPackets)
                    {
                        SingleReader = true,
                        SingleWriter = false,
                        FullMode = BoundedChannelFullMode.Wait
                    }),
                queueCapacityPackets);
        }

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

        public void SetHeaderUpdateWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                WarningMessage = message;
            }
        }

        public void StopAccepting()
        {
            Volatile.Write(ref _accepting, 0);
        }

        public void AttachWriterTask(Task<RecordingWriterResult> writerTask)
        {
            ArgumentNullException.ThrowIfNull(writerTask);
            _ = writerTask.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                    {
                        _writerCompletion.TrySetResult(RecordingWriterResult.Failure(0, "Recording writer was cancelled."));
                    }
                    else if (task.IsFaulted)
                    {
                        var message = task.Exception?.GetBaseException().Message ?? "Recording writer failed.";
                        _writerCompletion.TrySetResult(RecordingWriterResult.Failure(0, message));
                    }
                    else
                    {
                        _writerCompletion.TrySetResult(task.Result);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void FailInitialization(string message)
        {
            StopAccepting();
            Channel.Writer.TryComplete(new InvalidOperationException(message));
            _writerCompletion.TrySetResult(RecordingWriterResult.Failure(0, message));
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

    private sealed record RecordingWriterResult(
        bool Succeeded,
        long PacketCount,
        string Message,
        string? WarningMessage = null)
    {
        public static RecordingWriterResult Success(long packetCount, string message, string? warningMessage = null)
        {
            return new(true, packetCount, message, warningMessage);
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
