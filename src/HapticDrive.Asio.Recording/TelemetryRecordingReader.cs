using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace HapticDrive.Asio.Recording;

public sealed class TelemetryRecordingReader : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly int _maxPayloadLength;
    private readonly RecordingFormatKind _format;
    private readonly long _legacyPacketCount;
    private bool _packetsReadStarted;

    private TelemetryRecordingReader(
        FileStream stream,
        TelemetryRecordingMetadata metadata,
        RecordingFormatKind format,
        int maxPayloadLength,
        long legacyPacketCount)
    {
        _stream = stream;
        Metadata = metadata;
        _format = format;
        _maxPayloadLength = maxPayloadLength;
        _legacyPacketCount = legacyPacketCount;
    }

    public TelemetryRecordingMetadata Metadata { get; }

    public static async Task<TelemetryRecordingReader> OpenAsync(
        string path,
        int maxPayloadLength = TelemetryRecordingFile.DefaultMaxPayloadLength,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recording path is required.", nameof(path));
        }

        if (maxPayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadLength), "Maximum payload length must be positive.");
        }

        var fullPath = Path.GetFullPath(path);
        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 16 * 1024,
            useAsync: true);

        try
        {
            var magic = await ReadBytesAsync(stream, TelemetryRecordingFile.V2Magic.Length, cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);

            if (magic.SequenceEqual(TelemetryRecordingFile.V2Magic))
            {
                var metadata = await ReadV2HeaderAsync(stream, cancellationToken).ConfigureAwait(false);
                return new TelemetryRecordingReader(stream, metadata, RecordingFormatKind.Version2, maxPayloadLength, 0);
            }

            if (magic.SequenceEqual(TelemetryRecordingFile.V1Magic))
            {
                var header = await ReadV1HeaderAsync(stream, cancellationToken).ConfigureAwait(false);
                return new TelemetryRecordingReader(stream, header.Metadata, RecordingFormatKind.Version1, maxPayloadLength, header.PacketCount);
            }

            throw new InvalidDataException("Recording header magic is invalid.");
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static async IAsyncEnumerable<TelemetryRecordedPacket> ReadPacketsAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var reader = await OpenAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        await foreach (var packet in reader.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return packet;
        }
    }

    public async IAsyncEnumerable<TelemetryRecordedPacket> ReadPacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_packetsReadStarted)
        {
            throw new InvalidOperationException("Recording packets can only be read once per open reader.");
        }

        _packetsReadStarted = true;

        if (_format == RecordingFormatKind.Version1)
        {
            for (var i = 0L; i < _legacyPacketCount; i++)
            {
                if (_stream.Length - _stream.Position < sizeof(long) + sizeof(long) + sizeof(int))
                {
                    yield break;
                }

                var sequenceNumber = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                var relativeTicks = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                if (relativeTicks < 0)
                {
                    yield break;
                }

                var payloadLength = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
                if (payloadLength < 0 || payloadLength > _maxPayloadLength || _stream.Length - _stream.Position < payloadLength)
                {
                    yield break;
                }

                var payload = await ReadBytesAsync(_stream, payloadLength, cancellationToken).ConfigureAwait(false);
                var relativeTime = TimeSpan.FromTicks(relativeTicks);
                yield return new TelemetryRecordedPacket(
                    sequenceNumber,
                    Metadata.CreatedAtUtc + relativeTime,
                    relativeTime,
                    payload);
            }

            yield break;
        }

        var packetCount = 0L;
        var recordingCrc = new TelemetryRecordingFile.Crc32();
        while (_stream.Position < _stream.Length)
        {
            if (_stream.Length - _stream.Position < TelemetryRecordingFile.V2RecordMagic.Length)
            {
                yield break;
            }

            var marker = await ReadBytesAsync(_stream, TelemetryRecordingFile.V2RecordMagic.Length, cancellationToken).ConfigureAwait(false);
            if (marker.SequenceEqual(TelemetryRecordingFile.V2FooterMagic))
            {
                if (_stream.Length - _stream.Position < sizeof(long) + sizeof(long) + sizeof(uint))
                {
                    yield break;
                }

                var footerPacketCount = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                _ = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
                var footerCrc = await ReadUInt32Async(_stream, cancellationToken).ConfigureAwait(false);
                if (footerPacketCount != packetCount
                    || footerCrc != recordingCrc.GetCurrentHashAsUInt32()
                    || _stream.Position != _stream.Length)
                {
                    yield break;
                }

                yield break;
            }

            if (!marker.SequenceEqual(TelemetryRecordingFile.V2RecordMagic))
            {
                yield break;
            }

            if (_stream.Length - _stream.Position < sizeof(long) + sizeof(long) + sizeof(int) + sizeof(uint))
            {
                yield break;
            }

            var sequenceNumber = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
            var receivedAtNanoseconds = await ReadInt64Async(_stream, cancellationToken).ConfigureAwait(false);
            var payloadLength = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
            if (payloadLength < 0 || payloadLength > _maxPayloadLength || _stream.Length - _stream.Position < payloadLength + sizeof(uint))
            {
                yield break;
            }

            var payloadCrc32 = await ReadUInt32Async(_stream, cancellationToken).ConfigureAwait(false);
            var payload = await ReadBytesAsync(_stream, payloadLength, cancellationToken).ConfigureAwait(false);
            if (TelemetryRecordingFile.Crc32.Compute(payload) != payloadCrc32)
            {
                yield break;
            }

            recordingCrc.Append(TelemetryRecordingFile.V2RecordMagic);
            AppendInt64(recordingCrc, sequenceNumber);
            AppendInt64(recordingCrc, receivedAtNanoseconds);
            AppendInt32(recordingCrc, payloadLength);
            AppendUInt32(recordingCrc, payloadCrc32);
            recordingCrc.Append(payload);

            packetCount++;
            var receivedAtUtc = FromUnixTimeNanoseconds(receivedAtNanoseconds);
            var relativeTime = receivedAtUtc - Metadata.CreatedAtUtc;
            if (relativeTime < TimeSpan.Zero)
            {
                relativeTime = TimeSpan.Zero;
            }

            yield return new TelemetryRecordedPacket(
                sequenceNumber,
                receivedAtUtc,
                relativeTime,
                payload);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _stream.DisposeAsync();
    }

    private static async Task<TelemetryRecordingMetadata> ReadV2HeaderAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        var magic = await ReadBytesAsync(stream, TelemetryRecordingFile.V2Magic.Length, cancellationToken).ConfigureAwait(false);
        if (!magic.SequenceEqual(TelemetryRecordingFile.V2Magic))
        {
            throw new InvalidDataException("Recording header magic is invalid.");
        }

        var headerLength = await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (headerLength == 0 || headerLength > 1024 * 1024)
        {
            throw new InvalidDataException("Recording header length is invalid.");
        }

        var headerBytes = await ReadBytesAsync(stream, checked((int)headerLength), cancellationToken).ConfigureAwait(false);
        var json = JsonDocument.Parse(Encoding.UTF8.GetString(headerBytes).TrimEnd());
        var root = json.RootElement;

        return new TelemetryRecordingMetadata(
            CreatedAtUtc: root.GetProperty("startedAtUtc").GetDateTimeOffset(),
            SourceGame: root.GetProperty("gameDisplayName").GetString() ?? "unknown",
            SourceProfile: root.TryGetProperty("sourceProfile", out var sourceProfile) && sourceProfile.ValueKind == JsonValueKind.String
                ? sourceProfile.GetString() ?? "Default"
                : "Default",
            AppVersion: root.GetProperty("appVersion").GetString() ?? "unknown",
            GameIntegrationId: root.GetProperty("gameIntegrationId").GetString() ?? "f1-25",
            TelemetryProtocolName: root.GetProperty("telemetryProtocolName").GetString() ?? "F1 25 UDP",
            TelemetryProtocolVersion: root.GetProperty("telemetryProtocolVersion").GetString() ?? "v3",
            ProfileHash: root.GetProperty("profileHash").GetString() ?? string.Empty,
            SourceEndpoint: root.GetProperty("sourceEndpoint").GetString() ?? "unknown",
            BindAddress: root.GetProperty("bindAddress").GetString() ?? "127.0.0.1",
            EndedAtUtc: root.TryGetProperty("endedAtUtc", out var endedAtUtc) && endedAtUtc.ValueKind != JsonValueKind.Null
                ? endedAtUtc.GetDateTimeOffset()
                : null,
            PacketCount: root.TryGetProperty("packetCount", out var packetCount) ? packetCount.GetInt64() : 0,
            RecordingComplete: root.TryGetProperty("recordingComplete", out var recordingComplete) && recordingComplete.GetBoolean(),
            DroppedPacketCount: root.TryGetProperty("droppedPacketCount", out var droppedPacketCount) ? droppedPacketCount.GetInt64() : 0);
    }

    private static async Task<LegacyHeader> ReadV1HeaderAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var magic = await ReadBytesAsync(stream, TelemetryRecordingFile.V1Magic.Length, cancellationToken).ConfigureAwait(false);
        if (!magic.SequenceEqual(TelemetryRecordingFile.V1Magic))
        {
            throw new InvalidDataException("Recording header magic is invalid.");
        }

        var version = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (version != TelemetryRecordingFile.LegacyVersion)
        {
            throw new InvalidDataException($"Recording format version {version} is not supported.");
        }

        var createdAtUtcTicks = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
        var createdAtUtc = new DateTimeOffset(createdAtUtcTicks, TimeSpan.Zero);
        var sourceGame = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
        var sourceProfile = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
        var appVersion = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
        var packetCount = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
        if (packetCount < 0)
        {
            throw new InvalidDataException("Recording packet count is invalid.");
        }

        return new LegacyHeader(
            new TelemetryRecordingMetadata(
                createdAtUtc,
                sourceGame,
                sourceProfile,
                appVersion,
                PacketCount: packetCount,
                RecordingComplete: true),
            packetCount);
    }

    private static long ToUnixTimeNanoseconds(DateTimeOffset value)
    {
        return value.ToUnixTimeMilliseconds() * 1_000_000L + ((value.Ticks % TimeSpan.TicksPerMillisecond) * 100L);
    }

    private static DateTimeOffset FromUnixTimeNanoseconds(long value)
    {
        var milliseconds = value / 1_000_000L;
        var nanosecondRemainder = value % 1_000_000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).AddTicks(nanosecondRemainder / 100L);
    }

    private static void AppendInt32(TelemetryRecordingFile.Crc32 crc, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        crc.Append(buffer);
    }

    private static void AppendUInt32(TelemetryRecordingFile.Crc32 crc, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        crc.Append(buffer);
    }

    private static void AppendInt64(TelemetryRecordingFile.Crc32 crc, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        crc.Append(buffer);
    }

    private static async Task<string> ReadStringAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var byteLength = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (byteLength < 0 || byteLength > TelemetryRecordingFile.MaxStringByteLength)
        {
            throw new InvalidDataException($"Recording string metadata length {byteLength} is invalid.");
        }

        var bytes = await ReadBytesAsync(stream, byteLength, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task<int> ReadInt32Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(int), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private static async Task<uint> ReadUInt32Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(uint), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    private static async Task<long> ReadInt64Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(long), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private static async Task<byte[]> ReadBytesAsync(FileStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer;
    }

    private enum RecordingFormatKind
    {
        Version1 = 1,
        Version2 = 2
    }

    private sealed record LegacyHeader(TelemetryRecordingMetadata Metadata, long PacketCount);
}
