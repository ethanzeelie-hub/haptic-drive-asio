using System.Buffers.Binary;
using System.IO;
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

            var magic = await ReadBytesAsync(stream, Magic.Length, cancellationToken).ConfigureAwait(false);
            if (!magic.SequenceEqual(Magic))
            {
                return TelemetryRecordingSummaryLoadResult.Corrupt("Recording header magic is invalid.");
            }

            var version = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
            if (version != CurrentVersion)
            {
                return TelemetryRecordingSummaryLoadResult.UnsupportedVersion(
                    $"Recording format version {version} is not supported.");
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
                return TelemetryRecordingSummaryLoadResult.Corrupt("Recording packet count is invalid.");
            }

            return TelemetryRecordingSummaryLoadResult.Success(
                new TelemetryRecordingSummary(
                    path,
                    metadata,
                    packetCount,
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)));
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
        if (string.IsNullOrWhiteSpace(path))
        {
            return TelemetryRecordingLoadResult.Failure("Recording path is required.");
        }

        if (maxPayloadLength <= 0)
        {
            return TelemetryRecordingLoadResult.Failure("Maximum payload length must be positive.");
        }

        try
        {
            if (!File.Exists(path))
            {
                return TelemetryRecordingLoadResult.FileNotFound("Recording file does not exist.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);

            var magic = await ReadBytesAsync(stream, Magic.Length, cancellationToken).ConfigureAwait(false);
            if (!magic.SequenceEqual(Magic))
            {
                return TelemetryRecordingLoadResult.Corrupt("Recording header magic is invalid.");
            }

            var version = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
            if (version != CurrentVersion)
            {
                return TelemetryRecordingLoadResult.UnsupportedVersion(
                    $"Recording format version {version} is not supported.");
            }

            var createdAtUtcTicks = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
            var createdAtUtc = new DateTimeOffset(createdAtUtcTicks, TimeSpan.Zero);
            var sourceGame = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
            var sourceProfile = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
            var appVersion = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
            var packetCount = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);

            if (packetCount < 0)
            {
                return TelemetryRecordingLoadResult.Corrupt("Recording packet count is invalid.");
            }

            if (packetCount > int.MaxValue)
            {
                return TelemetryRecordingLoadResult.Corrupt("Recording packet count is too large to load safely.");
            }

            var packets = new List<TelemetryRecordedPacket>((int)packetCount);
            for (var i = 0L; i < packetCount; i++)
            {
                var sequenceNumber = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
                var relativeTicks = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
                if (relativeTicks < 0)
                {
                    return TelemetryRecordingLoadResult.Corrupt("Recording packet relative timestamp is invalid.");
                }

                var payloadLength = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
                if (payloadLength < 0 || payloadLength > maxPayloadLength)
                {
                    return TelemetryRecordingLoadResult.Corrupt(
                        $"Recording packet payload length {payloadLength} is invalid.");
                }

                var payload = await ReadBytesAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);
                packets.Add(new TelemetryRecordedPacket(sequenceNumber, TimeSpan.FromTicks(relativeTicks), payload));
            }

            if (stream.Position != stream.Length)
            {
                return TelemetryRecordingLoadResult.Corrupt("Recording contains trailing bytes after the final packet.");
            }

            return TelemetryRecordingLoadResult.Success(
                new TelemetryRecording(
                    new TelemetryRecordingMetadata(createdAtUtc, sourceGame, sourceProfile, appVersion),
                    packets));
        }
        catch (OperationCanceledException)
        {
            return TelemetryRecordingLoadResult.Cancelled("Recording load was cancelled.");
        }
        catch (EndOfStreamException ex)
        {
            return TelemetryRecordingLoadResult.Corrupt($"Recording is truncated: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return TelemetryRecordingLoadResult.Corrupt(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return TelemetryRecordingLoadResult.Corrupt($"Recording metadata is invalid: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return TelemetryRecordingLoadResult.Failure($"Recording could not be loaded: {ex.Message}");
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
}
