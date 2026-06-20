using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace HapticDrive.Asio.Recording;

public static class TelemetryRecordingFile
{
    public const int CurrentVersion = 1;
    public const int DefaultMaxPayloadLength = 65_535;

    internal const int MaxStringByteLength = 1_024;
    internal static readonly byte[] Magic = Encoding.ASCII.GetBytes("HDREC001");

    public static async Task<TelemetryRecordingSummaryLoadResult> LoadSummaryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TelemetryRecordingSummaryLoadResult.Failure("Recording path is required.");
        }

        try
        {
            if (!File.Exists(path))
            {
                return TelemetryRecordingSummaryLoadResult.FileNotFound("Recording file does not exist.");
            }

            var fileInfo = new FileInfo(path);
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4 * 1024,
                useAsync: true);
            var header = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            var packetSummary = await SummarizePacketsAsync(
                    stream,
                    header.PacketCount,
                    DefaultMaxPayloadLength,
                    cancellationToken)
                .ConfigureAwait(false);

            return TelemetryRecordingSummaryLoadResult.Success(
                new TelemetryRecordingSummary(
                    path,
                    header.Metadata,
                    header.PacketCount,
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
                    packetSummary.Duration,
                    packetSummary.PayloadBytes,
                    packetSummary.MissingSequenceCount,
                    packetSummary.LargestSequenceGap,
                    packetSummary.FirstSequenceNumber,
                    packetSummary.LastSequenceNumber,
                    packetSummary.ApproximatePacketRateHz));
        }
        catch (OperationCanceledException)
        {
            return TelemetryRecordingSummaryLoadResult.Cancelled("Recording summary load was cancelled.");
        }
        catch (EndOfStreamException ex)
        {
            return TelemetryRecordingSummaryLoadResult.Corrupt($"Recording is truncated: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return TelemetryRecordingSummaryLoadResult.Corrupt(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return TelemetryRecordingSummaryLoadResult.Corrupt($"Recording metadata is invalid: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return TelemetryRecordingSummaryLoadResult.Failure($"Recording summary could not be loaded: {ex.Message}");
        }
    }

    public static async Task<TelemetryRecordingLoadResult> LoadAsync(
        string path,
        int maxPayloadLength = DefaultMaxPayloadLength,
        CancellationToken cancellationToken = default)
    {
        var openResult = await OpenReaderAsync(path, maxPayloadLength, cancellationToken).ConfigureAwait(false);
        if (!openResult.Succeeded || openResult.Reader is null)
        {
            return new TelemetryRecordingLoadResult(openResult.Status, null, openResult.Message);
        }

        await using var reader = openResult.Reader;

        try
        {
            if (reader.PacketCount > int.MaxValue)
            {
                return TelemetryRecordingLoadResult.Corrupt("Recording packet count is too large to load safely.");
            }

            var packets = new List<TelemetryRecordedPacket>((int)reader.PacketCount);
            await foreach (var packet in reader.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
            {
                packets.Add(packet);
            }

            return TelemetryRecordingLoadResult.Success(new TelemetryRecording(reader.Metadata, packets));
        }
        catch (Exception ex) when (TryMapLoadFailure(ex, "Recording load was cancelled.", "Recording could not be loaded", out var failure))
        {
            return new TelemetryRecordingLoadResult(failure.Status, null, failure.Message);
        }
    }

    internal static async Task<TelemetryRecordingReaderOpenResult> OpenReaderAsync(
        string path,
        int maxPayloadLength = DefaultMaxPayloadLength,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TelemetryRecordingReaderOpenResult.Failure("Recording path is required.");
        }

        if (maxPayloadLength <= 0)
        {
            return TelemetryRecordingReaderOpenResult.Failure("Maximum payload length must be positive.");
        }

        FileStream? stream = null;

        try
        {
            if (!File.Exists(path))
            {
                return TelemetryRecordingReaderOpenResult.FileNotFound("Recording file does not exist.");
            }

            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);

            var header = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            var reader = new TelemetryRecordingFileReader(stream, header.Metadata, header.PacketCount, maxPayloadLength);
            stream = null;
            return TelemetryRecordingReaderOpenResult.Success(reader);
        }
        catch (Exception ex) when (TryMapLoadFailure(ex, "Recording reader open was cancelled.", "Recording could not be opened", out var failure))
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            return new TelemetryRecordingReaderOpenResult(failure.Status, null, failure.Message);
        }
    }

    internal static long WriteHeader(Stream stream, TelemetryRecordingMetadata metadata)
    {
        stream.Write(Magic);
        WriteInt32(stream, CurrentVersion);
        WriteInt64(stream, metadata.CreatedAtUtc.UtcTicks);
        WriteString(stream, metadata.SourceGame);
        WriteString(stream, metadata.SourceProfile);
        WriteString(stream, metadata.AppVersion);

        var packetCountOffset = stream.Position;
        WriteInt64(stream, 0);

        return packetCountOffset;
    }

    internal static void WritePacket(Stream stream, TelemetryRecordedPacket packet, int maxPayloadLength)
    {
        if (packet.Payload.Length > maxPayloadLength)
        {
            throw new InvalidDataException($"Packet payload length {packet.Payload.Length} exceeds {maxPayloadLength} bytes.");
        }

        if (packet.RelativeTime.Ticks < 0)
        {
            throw new InvalidDataException("Packet relative timestamp cannot be negative.");
        }

        WriteInt64(stream, packet.SequenceNumber);
        WriteInt64(stream, packet.RelativeTime.Ticks);
        WriteInt32(stream, packet.Payload.Length);
        stream.Write(packet.Payload);
    }

    internal static void UpdatePacketCount(Stream stream, long packetCountOffset, long packetCount)
    {
        stream.Seek(packetCountOffset, SeekOrigin.Begin);
        WriteInt64(stream, packetCount);
        stream.Seek(0, SeekOrigin.End);
    }

    private static async ValueTask<TelemetryRecordingFileHeader> ReadHeaderAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        var magic = await ReadBytesAsync(stream, Magic.Length, cancellationToken).ConfigureAwait(false);
        if (!magic.SequenceEqual(Magic))
        {
            throw new InvalidDataException("Recording header magic is invalid.");
        }

        var version = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (version != CurrentVersion)
        {
            throw new UnsupportedVersionException($"Recording format version {version} is not supported.");
        }

        var createdAtUtcTicks = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
        var createdAtUtc = new DateTimeOffset(createdAtUtcTicks, TimeSpan.Zero);
        var metadata = new TelemetryRecordingMetadata(
            createdAtUtc,
            await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false),
            await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false),
            await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false));
        var packetCount = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);

        if (packetCount < 0)
        {
            throw new InvalidDataException("Recording packet count is invalid.");
        }

        return new TelemetryRecordingFileHeader(metadata, packetCount);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxStringByteLength)
        {
            throw new InvalidDataException($"Recording string metadata exceeds {MaxStringByteLength} bytes.");
        }

        WriteInt32(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static async ValueTask<string> ReadStringAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var byteLength = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (byteLength < 0 || byteLength > MaxStringByteLength)
        {
            throw new InvalidDataException($"Recording string metadata length {byteLength} is invalid.");
        }

        var bytes = await ReadBytesAsync(stream, byteLength, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }

    private static async ValueTask<int> ReadInt32Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(int), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private static async ValueTask<long> ReadInt64Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(long), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private static async ValueTask<byte[]> ReadBytesAsync(
        FileStream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer;
    }

    private static async ValueTask<TelemetryRecordingPacketSummary> SummarizePacketsAsync(
        FileStream stream,
        long packetCount,
        int maxPayloadLength,
        CancellationToken cancellationToken)
    {
        var duration = TimeSpan.Zero;
        long payloadBytes = 0;
        long missingSequenceCount = 0;
        long largestSequenceGap = 0;
        long? firstSequenceNumber = null;
        long? lastSequenceNumber = null;
        long? previousSequenceNumber = null;

        for (var i = 0L; i < packetCount; i++)
        {
            var sequenceNumber = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
            var relativeTicks = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
            if (relativeTicks < 0)
            {
                throw new InvalidDataException("Recording packet relative timestamp is invalid.");
            }

            duration = TimeSpan.FromTicks(Math.Max(duration.Ticks, relativeTicks));

            var payloadLength = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
            if (payloadLength < 0 || payloadLength > maxPayloadLength)
            {
                throw new InvalidDataException($"Recording packet payload length {payloadLength} is invalid.");
            }

            payloadBytes += payloadLength;
            await ReadBytesAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);

            firstSequenceNumber ??= sequenceNumber;
            lastSequenceNumber = sequenceNumber;

            if (previousSequenceNumber.HasValue && sequenceNumber > previousSequenceNumber.Value + 1)
            {
                var gap = sequenceNumber - previousSequenceNumber.Value - 1;
                missingSequenceCount += gap;
                largestSequenceGap = Math.Max(largestSequenceGap, gap);
            }

            previousSequenceNumber = sequenceNumber;
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Recording contains trailing bytes after the final packet.");
        }

        var approximatePacketRateHz = duration > TimeSpan.Zero
            ? packetCount / duration.TotalSeconds
            : 0d;

        return new TelemetryRecordingPacketSummary(
            duration,
            payloadBytes,
            missingSequenceCount,
            largestSequenceGap,
            firstSequenceNumber,
            lastSequenceNumber,
            approximatePacketRateHz);
    }

    private static bool TryMapLoadFailure(
        Exception ex,
        string cancelledMessage,
        string failurePrefix,
        out TelemetryRecordingLoadFailure failure)
    {
        switch (ex)
        {
            case OperationCanceledException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.Cancelled,
                    cancelledMessage);
                return true;
            case EndOfStreamException endOfStreamException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.Corrupt,
                    $"Recording is truncated: {endOfStreamException.Message}");
                return true;
            case UnsupportedVersionException unsupportedVersionException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.UnsupportedVersion,
                    unsupportedVersionException.Message);
                return true;
            case InvalidDataException invalidDataException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.Corrupt,
                    invalidDataException.Message);
                return true;
            case ArgumentOutOfRangeException argumentOutOfRangeException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.Corrupt,
                    $"Recording metadata is invalid: {argumentOutOfRangeException.Message}");
                return true;
            case IOException or UnauthorizedAccessException or NotSupportedException:
                failure = new TelemetryRecordingLoadFailure(
                    TelemetryRecordingLoadStatus.Failure,
                    $"{failurePrefix}: {ex.Message}");
                return true;
            default:
                failure = default;
                return false;
        }
    }

    internal sealed class TelemetryRecordingFileReader : IAsyncDisposable
    {
        private readonly FileStream _stream;
        private readonly int _maxPayloadLength;
        private bool _packetsReadStarted;

        public TelemetryRecordingFileReader(
            FileStream stream,
            TelemetryRecordingMetadata metadata,
            long packetCount,
            int maxPayloadLength)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            PacketCount = packetCount;
            _maxPayloadLength = maxPayloadLength;
        }

        public TelemetryRecordingMetadata Metadata { get; }

        public long PacketCount { get; }

        public async IAsyncEnumerable<TelemetryRecordedPacket> ReadPacketsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_packetsReadStarted)
            {
                throw new InvalidOperationException("Recording packets can only be read once per open reader.");
            }

            _packetsReadStarted = true;

            for (var i = 0L; i < PacketCount; i++)
            {
                var sequenceNumber = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                var relativeTicks = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                if (relativeTicks < 0)
                {
                    throw new InvalidDataException("Recording packet relative timestamp is invalid.");
                }

                var payloadLength = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
                if (payloadLength < 0 || payloadLength > _maxPayloadLength)
                {
                    throw new InvalidDataException($"Recording packet payload length {payloadLength} is invalid.");
                }

                var payload = await ReadBytesAsync(_stream, payloadLength, cancellationToken).ConfigureAwait(false);
                yield return new TelemetryRecordedPacket(sequenceNumber, TimeSpan.FromTicks(relativeTicks), payload);
            }

            if (_stream.Position != _stream.Length)
            {
                throw new InvalidDataException("Recording contains trailing bytes after the final packet.");
            }
        }

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }
    }

    internal sealed record TelemetryRecordingReaderOpenResult(
        TelemetryRecordingLoadStatus Status,
        TelemetryRecordingFileReader? Reader,
        string Message)
    {
        public bool Succeeded => Status == TelemetryRecordingLoadStatus.Success;

        public static TelemetryRecordingReaderOpenResult Success(TelemetryRecordingFileReader reader)
        {
            return new(TelemetryRecordingLoadStatus.Success, reader, "Recording reader opened.");
        }

        public static TelemetryRecordingReaderOpenResult FileNotFound(string message)
        {
            return new(TelemetryRecordingLoadStatus.FileNotFound, null, message);
        }

        public static TelemetryRecordingReaderOpenResult Failure(string message)
        {
            return new(TelemetryRecordingLoadStatus.Failure, null, message);
        }
    }

    private readonly record struct TelemetryRecordingLoadFailure(
        TelemetryRecordingLoadStatus Status,
        string Message);

    private sealed record TelemetryRecordingFileHeader(
        TelemetryRecordingMetadata Metadata,
        long PacketCount);

    private readonly record struct TelemetryRecordingPacketSummary(
        TimeSpan Duration,
        long PayloadBytes,
        long MissingSequenceCount,
        long LargestSequenceGap,
        long? FirstSequenceNumber,
        long? LastSequenceNumber,
        double ApproximatePacketRateHz);

    private sealed class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException(string message)
            : base(message)
        {
        }
    }
}
