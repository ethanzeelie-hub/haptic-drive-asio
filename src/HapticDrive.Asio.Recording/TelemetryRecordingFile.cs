using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace HapticDrive.Asio.Recording;

public static class TelemetryRecordingFile
{
    public const int CurrentVersion = 2;
    public const int LegacyVersion = 1;
    public const int DefaultMaxPayloadLength = 65_535;

    private const int ReservedHeaderPaddingBytes = 512;
    private const string V2MagicText = "HDRVREC2";
    private const string V2RecordMagicText = "PKT2";
    private const string V2FooterMagicText = "END2";
    private const string V1MagicText = "HDREC001";

    internal const int MaxStringByteLength = 1_024;
    internal static readonly byte[] V2Magic = Encoding.ASCII.GetBytes(V2MagicText);
    internal static readonly byte[] V2RecordMagic = Encoding.ASCII.GetBytes(V2RecordMagicText);
    internal static readonly byte[] V2FooterMagic = Encoding.ASCII.GetBytes(V2FooterMagicText);
    internal static readonly byte[] V1Magic = Encoding.ASCII.GetBytes(V1MagicText);

    private static readonly JsonSerializerOptions HeaderJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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
            var openResult = await OpenReaderAsync(path, DefaultMaxPayloadLength, cancellationToken).ConfigureAwait(false);
            if (!openResult.Succeeded || openResult.Reader is null)
            {
                return new TelemetryRecordingSummaryLoadResult(openResult.Status, null, openResult.Message);
            }

            await using var reader = openResult.Reader;
            var summary = await BuildSummaryAsync(path, reader, cancellationToken).ConfigureAwait(false);
            return TelemetryRecordingSummaryLoadResult.Success(summary);
        }
        catch (Exception ex) when (TryMapLoadFailure(ex, "Recording summary load was cancelled.", "Recording summary could not be loaded", out var failure))
        {
            return new TelemetryRecordingSummaryLoadResult(failure.Status, null, failure.Message);
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
                FileShare.ReadWrite,
                bufferSize: 16 * 1024,
                useAsync: true);

            var magic = await ReadBytesAsync(stream, V2Magic.Length, cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);

            if (magic.SequenceEqual(V2Magic))
            {
                var descriptor = await ReadV2DescriptorAsync(stream, maxPayloadLength, cancellationToken).ConfigureAwait(false);
                var reader = new TelemetryRecordingFileReader(stream, descriptor.Metadata, descriptor.PacketIndices.Count, descriptor.PacketIndices, maxPayloadLength);
                stream = null;
                return TelemetryRecordingReaderOpenResult.Success(reader);
            }

            if (magic.SequenceEqual(V1Magic))
            {
                var header = await ReadV1HeaderAsync(stream, cancellationToken).ConfigureAwait(false);
                var reader = new TelemetryRecordingFileReader(stream, header.Metadata, header.PacketCount, null, maxPayloadLength);
                stream = null;
                return TelemetryRecordingReaderOpenResult.Success(reader);
            }

            throw new InvalidDataException("Recording header magic is invalid.");
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

    internal static RecordingHeaderReservation WriteHeader(Stream stream, TelemetryRecordingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);

        var headerDocument = CreateHeaderDocument(
            metadata with
            {
                EndedAtUtc = null,
                PacketCount = 0,
                RecordingComplete = false,
                DroppedPacketCount = 0
            });
        var reservedJson = SerializeReservedHeaderJson(headerDocument, out var reservedLength);

        stream.Write(V2Magic);
        WriteUInt32(stream, (uint)reservedLength);
        var jsonOffset = stream.Position;
        stream.Write(reservedJson);

        return new RecordingHeaderReservation(jsonOffset, reservedLength);
    }

    internal static void WritePacket(
        Stream stream,
        TelemetryRecordedPacket packet,
        int maxPayloadLength,
        Crc32? recordingCrc = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Payload.Length > maxPayloadLength)
        {
            throw new InvalidDataException($"Packet payload length {packet.Payload.Length} exceeds {maxPayloadLength} bytes.");
        }

        var payloadCrc = Crc32.Compute(packet.Payload);
        var receivedAtNanoseconds = ToUnixTimeNanoseconds(packet.ReceivedAtUtc);

        stream.Write(V2RecordMagic);
        WriteInt64(stream, packet.SequenceNumber);
        WriteInt64(stream, receivedAtNanoseconds);
        WriteInt32(stream, packet.Payload.Length);
        WriteUInt32(stream, payloadCrc);
        stream.Write(packet.Payload);

        if (recordingCrc is null)
        {
            return;
        }

        recordingCrc.Append(V2RecordMagic);
        AppendInt64(recordingCrc, packet.SequenceNumber);
        AppendInt64(recordingCrc, receivedAtNanoseconds);
        AppendInt32(recordingCrc, packet.Payload.Length);
        AppendUInt32(recordingCrc, payloadCrc);
        recordingCrc.Append(packet.Payload);
    }

    internal static void WriteFooter(
        Stream stream,
        long packetCount,
        DateTimeOffset endedAtUtc,
        uint recordingCrc32)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(V2FooterMagic);
        WriteInt64(stream, packetCount);
        WriteInt64(stream, ToUnixTimeNanoseconds(endedAtUtc));
        WriteUInt32(stream, recordingCrc32);
    }

    internal static bool TryUpdateHeader(
        Stream stream,
        RecordingHeaderReservation reservation,
        TelemetryRecordingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);

        var updatedBytes = JsonSerializer.SerializeToUtf8Bytes(CreateHeaderDocument(metadata), HeaderJsonOptions);
        if (updatedBytes.Length > reservation.ReservedHeaderLength)
        {
            return false;
        }

        var paddedBytes = new byte[reservation.ReservedHeaderLength];
        updatedBytes.CopyTo(paddedBytes, 0);
        for (var i = updatedBytes.Length; i < paddedBytes.Length; i++)
        {
            paddedBytes[i] = (byte)' ';
        }

        var returnPosition = stream.Position;
        stream.Seek(reservation.JsonOffset, SeekOrigin.Begin);
        stream.Write(paddedBytes);
        stream.Seek(returnPosition, SeekOrigin.Begin);
        return true;
    }

    private static async Task<TelemetryRecordingSummary> BuildSummaryAsync(
        string path,
        TelemetryRecordingFileReader reader,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);
        long payloadBytes = 0;
        long missingSequenceCount = 0;
        long largestSequenceGap = 0;
        long? firstSequenceNumber = null;
        long? lastSequenceNumber = null;
        long? previousSequenceNumber = null;
        DateTimeOffset? firstPacketAtUtc = null;
        DateTimeOffset? lastPacketAtUtc = null;

        await foreach (var packet in reader.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
        {
            payloadBytes += packet.Payload.Length;
            firstSequenceNumber ??= packet.SequenceNumber;
            lastSequenceNumber = packet.SequenceNumber;
            firstPacketAtUtc ??= packet.ReceivedAtUtc;
            lastPacketAtUtc = packet.ReceivedAtUtc;

            if (previousSequenceNumber.HasValue && packet.SequenceNumber > previousSequenceNumber.Value + 1)
            {
                var gap = packet.SequenceNumber - previousSequenceNumber.Value - 1;
                missingSequenceCount += gap;
                largestSequenceGap = Math.Max(largestSequenceGap, gap);
            }

            previousSequenceNumber = packet.SequenceNumber;
        }

        var duration = firstPacketAtUtc is null || lastPacketAtUtc is null
            ? TimeSpan.Zero
            : lastPacketAtUtc.Value - firstPacketAtUtc.Value;
        var approximatePacketRateHz = duration > TimeSpan.Zero
            ? reader.PacketCount / duration.TotalSeconds
            : 0d;

        return new TelemetryRecordingSummary(
            path,
            reader.Metadata,
            reader.PacketCount,
            fileInfo.Length,
            new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
            duration,
            payloadBytes,
            missingSequenceCount,
            largestSequenceGap,
            firstSequenceNumber,
            lastSequenceNumber,
            approximatePacketRateHz);
    }

    private static HeaderV2Document CreateHeaderDocument(TelemetryRecordingMetadata metadata)
    {
        return new HeaderV2Document(
            SchemaVersion: CurrentVersion,
            AppVersion: metadata.AppVersion,
            GameIntegrationId: metadata.GameIntegrationId,
            GameDisplayName: metadata.SourceGame,
            TelemetryProtocolName: metadata.TelemetryProtocolName,
            TelemetryProtocolVersion: metadata.TelemetryProtocolVersion,
            ProfileHash: metadata.ProfileHash,
            SourceProfile: metadata.SourceProfile,
            SourceEndpoint: metadata.SourceEndpoint,
            BindAddress: metadata.BindAddress,
            StartedAtUtc: metadata.CreatedAtUtc,
            EndedAtUtc: metadata.EndedAtUtc,
            PacketCount: metadata.PacketCount,
            RecordingComplete: metadata.RecordingComplete,
            DroppedPacketCount: metadata.DroppedPacketCount);
    }

    private static byte[] SerializeReservedHeaderJson(HeaderV2Document document, out int reservedLength)
    {
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, HeaderJsonOptions);
        reservedLength = jsonBytes.Length + ReservedHeaderPaddingBytes;

        var paddedBytes = new byte[reservedLength];
        jsonBytes.CopyTo(paddedBytes, 0);
        for (var i = jsonBytes.Length; i < paddedBytes.Length; i++)
        {
            paddedBytes[i] = (byte)' ';
        }

        return paddedBytes;
    }

    private static async ValueTask<HeaderV1> ReadV1HeaderAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        var magic = await ReadBytesAsync(stream, V1Magic.Length, cancellationToken).ConfigureAwait(false);
        if (!magic.SequenceEqual(V1Magic))
        {
            throw new InvalidDataException("Recording header magic is invalid.");
        }

        var version = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (version != LegacyVersion)
        {
            throw new UnsupportedVersionException($"Recording format version {version} is not supported.");
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

        return new HeaderV1(
            new TelemetryRecordingMetadata(
                createdAtUtc,
                sourceGame,
                sourceProfile,
                appVersion,
                PacketCount: packetCount,
                RecordingComplete: true),
            packetCount);
    }

    private static async ValueTask<V2Descriptor> ReadV2DescriptorAsync(
        FileStream stream,
        int maxPayloadLength,
        CancellationToken cancellationToken)
    {
        var magic = await ReadBytesAsync(stream, V2Magic.Length, cancellationToken).ConfigureAwait(false);
        if (!magic.SequenceEqual(V2Magic))
        {
            throw new InvalidDataException("Recording header magic is invalid.");
        }

        var headerLength = await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (headerLength == 0 || headerLength > 1024 * 1024)
        {
            throw new InvalidDataException("Recording header length is invalid.");
        }

        var headerBytes = await ReadBytesAsync(stream, checked((int)headerLength), cancellationToken).ConfigureAwait(false);
        var headerText = Encoding.UTF8.GetString(headerBytes).TrimEnd();
        var header = JsonSerializer.Deserialize<HeaderV2Document>(headerText, HeaderJsonOptions)
            ?? throw new InvalidDataException("Recording header JSON is invalid.");

        if (header.SchemaVersion != CurrentVersion)
        {
            throw new UnsupportedVersionException($"Recording format version {header.SchemaVersion} is not supported.");
        }

        var packetIndices = new List<V2PacketIndex>();
        var recordingCrc = new Crc32();
        var footerFound = false;
        DateTimeOffset? endedAtUtc = null;
        long footerPacketCount = 0;
        string? incompleteReason = null;

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < V2RecordMagic.Length)
            {
                incompleteReason = "Recording ended with a truncated marker.";
                break;
            }

            var marker = await ReadBytesAsync(stream, V2RecordMagic.Length, cancellationToken).ConfigureAwait(false);
            if (marker.SequenceEqual(V2FooterMagic))
            {
                if (stream.Length - stream.Position < sizeof(long) + sizeof(long) + sizeof(uint))
                {
                    incompleteReason = "Recording ended with a truncated footer.";
                    break;
                }

                footerPacketCount = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
                endedAtUtc = FromUnixTimeNanoseconds(await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false));
                var footerCrc = await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
                footerFound = true;

                if (footerPacketCount != packetIndices.Count)
                {
                    incompleteReason = "Recording footer packet count does not match recovered packet count.";
                }
                else if (footerCrc != recordingCrc.GetCurrentHashAsUInt32())
                {
                    incompleteReason = "Recording footer CRC is invalid.";
                }
                else if (stream.Position != stream.Length)
                {
                    incompleteReason = "Recording contains trailing bytes after the footer.";
                }

                break;
            }

            if (!marker.SequenceEqual(V2RecordMagic))
            {
                incompleteReason = "Recording contains invalid bytes where a packet record was expected.";
                break;
            }

            var packetReadResult = await TryReadV2PacketIndexAsync(
                    stream,
                    maxPayloadLength,
                    recordingCrc,
                    cancellationToken).ConfigureAwait(false);
            if (!packetReadResult.Succeeded)
            {
                incompleteReason = packetReadResult.FailureMessage;
                break;
            }

            packetIndices.Add(packetReadResult.PacketIndex!.Value);
        }

        var metadata = new TelemetryRecordingMetadata(
            header.StartedAtUtc,
            header.GameDisplayName,
            string.IsNullOrWhiteSpace(header.SourceProfile) ? "Default" : header.SourceProfile,
            header.AppVersion,
            header.GameIntegrationId,
            header.TelemetryProtocolName,
            header.TelemetryProtocolVersion,
            header.ProfileHash,
            string.IsNullOrWhiteSpace(header.SourceEndpoint) ? "unknown" : header.SourceEndpoint,
            string.IsNullOrWhiteSpace(header.BindAddress) ? "127.0.0.1" : header.BindAddress,
            endedAtUtc ?? header.EndedAtUtc,
            packetIndices.Count,
            IsRecordingComplete(header, footerFound && incompleteReason is null),
            header.DroppedPacketCount);

        return new V2Descriptor(metadata, packetIndices, incompleteReason);
    }

    private static async ValueTask<ReadPacketIndexResult> TryReadV2PacketIndexAsync(
        FileStream stream,
        int maxPayloadLength,
        Crc32 recordingCrc,
        CancellationToken cancellationToken)
    {
        var sequenceNumber = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
        var receivedAtNanoseconds = await ReadInt64Async(stream, cancellationToken).ConfigureAwait(false);
        var payloadLength = await ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (payloadLength < 0 || payloadLength > maxPayloadLength)
        {
            return ReadPacketIndexResult.Failure($"Recording packet payload length {payloadLength} is invalid.");
        }

        var payloadCrc32 = await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (stream.Length - stream.Position < payloadLength)
        {
            return ReadPacketIndexResult.Failure("Recording contains a truncated packet payload.");
        }

        var payloadOffset = stream.Position;
        var payloadCrc = new Crc32();
        recordingCrc.Append(V2RecordMagic);
        AppendInt64(recordingCrc, sequenceNumber);
        AppendInt64(recordingCrc, receivedAtNanoseconds);
        AppendInt32(recordingCrc, payloadLength);
        AppendUInt32(recordingCrc, payloadCrc32);

        var buffer = new byte[Math.Min(payloadLength, 16 * 1024)];
        var remaining = payloadLength;
        while (remaining > 0)
        {
            var readLength = Math.Min(buffer.Length, remaining);
            await stream.ReadExactlyAsync(buffer.AsMemory(0, readLength), cancellationToken).ConfigureAwait(false);
            payloadCrc.Append(buffer.AsSpan(0, readLength));
            recordingCrc.Append(buffer.AsSpan(0, readLength));
            remaining -= readLength;
        }

        if (payloadCrc.GetCurrentHashAsUInt32() != payloadCrc32)
        {
            return ReadPacketIndexResult.Failure("Recording packet CRC is invalid.");
        }

        return ReadPacketIndexResult.Success(
            new V2PacketIndex(
                sequenceNumber,
                FromUnixTimeNanoseconds(receivedAtNanoseconds),
                payloadOffset,
                payloadLength));
    }

    private static long ToUnixTimeNanoseconds(DateTimeOffset value)
    {
        return value.ToUnixTimeMilliseconds() * 1_000_000L + ((value.Ticks % TimeSpan.TicksPerMillisecond) * 100L);
    }

    private static bool IsRecordingComplete(HeaderV2Document header, bool footerValidated)
    {
        if (!footerValidated)
        {
            return false;
        }

        var headerWasFinalized = header.EndedAtUtc is not null
            || header.PacketCount > 0
            || header.DroppedPacketCount > 0
            || header.RecordingComplete;

        return !headerWasFinalized || header.RecordingComplete;
    }

    private static DateTimeOffset FromUnixTimeNanoseconds(long value)
    {
        var milliseconds = value / 1_000_000L;
        var nanosecondRemainder = value % 1_000_000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).AddTicks(nanosecondRemainder / 100L);
    }

    private static void AppendInt32(Crc32 crc, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        crc.Append(buffer);
    }

    private static void AppendUInt32(Crc32 crc, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        crc.Append(buffer);
    }

    private static void AppendInt64(Crc32 crc, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        crc.Append(buffer);
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

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
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

    private static async ValueTask<uint> ReadUInt32Async(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = await ReadBytesAsync(stream, sizeof(uint), cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
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
        private readonly IReadOnlyList<V2PacketIndex>? _packetIndices;
        private bool _packetsReadStarted;

        internal TelemetryRecordingFileReader(
            FileStream stream,
            TelemetryRecordingMetadata metadata,
            long packetCount,
            IReadOnlyList<V2PacketIndex>? packetIndices,
            int maxPayloadLength)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            PacketCount = packetCount;
            _packetIndices = packetIndices;
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

            if (_packetIndices is not null)
            {
                foreach (var packetIndex in _packetIndices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _stream.Seek(packetIndex.PayloadOffset, SeekOrigin.Begin);
                    var payload = await ReadBytesAsync(_stream, packetIndex.PayloadLength, cancellationToken).ConfigureAwait(false);
                    var relativeTime = packetIndex.ReceivedAtUtc - Metadata.CreatedAtUtc;
                    if (relativeTime < TimeSpan.Zero)
                    {
                        relativeTime = TimeSpan.Zero;
                    }

                    yield return new TelemetryRecordedPacket(
                        packetIndex.SequenceNumber,
                        packetIndex.ReceivedAtUtc,
                        relativeTime,
                        payload);
                }

                yield break;
            }

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
                var relativeTime = TimeSpan.FromTicks(relativeTicks);
                yield return new TelemetryRecordedPacket(
                    sequenceNumber,
                    Metadata.CreatedAtUtc + relativeTime,
                    relativeTime,
                    payload);
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

    internal readonly record struct RecordingHeaderReservation(long JsonOffset, int ReservedHeaderLength);

    private readonly record struct TelemetryRecordingLoadFailure(
        TelemetryRecordingLoadStatus Status,
        string Message);

    private sealed record HeaderV1(TelemetryRecordingMetadata Metadata, long PacketCount);

    private sealed record HeaderV2Document(
        int SchemaVersion,
        string AppVersion,
        string GameIntegrationId,
        string GameDisplayName,
        string TelemetryProtocolName,
        string TelemetryProtocolVersion,
        string ProfileHash,
        string? SourceProfile,
        string SourceEndpoint,
        string BindAddress,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? EndedAtUtc,
        long PacketCount,
        bool RecordingComplete,
        long DroppedPacketCount);

    private sealed record V2Descriptor(
        TelemetryRecordingMetadata Metadata,
        IReadOnlyList<V2PacketIndex> PacketIndices,
        string? IncompleteReason);

    internal readonly record struct V2PacketIndex(
        long SequenceNumber,
        DateTimeOffset ReceivedAtUtc,
        long PayloadOffset,
        int PayloadLength);

    private readonly record struct ReadPacketIndexResult(
        bool Succeeded,
        V2PacketIndex? PacketIndex,
        string? FailureMessage)
    {
        public static ReadPacketIndexResult Success(V2PacketIndex packetIndex)
        {
            return new(true, packetIndex, null);
        }

        public static ReadPacketIndexResult Failure(string message)
        {
            return new(false, null, message);
        }
    }

    private sealed class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException(string message)
            : base(message)
        {
        }
    }

    internal sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _current = 0xFFFF_FFFFu;

        public void Append(ReadOnlySpan<byte> bytes)
        {
            foreach (var value in bytes)
            {
                _current = (_current >> 8) ^ Table[(value ^ _current) & 0xFF];
            }
        }

        public uint GetCurrentHashAsUInt32()
        {
            return ~_current;
        }

        public static uint Compute(ReadOnlySpan<byte> bytes)
        {
            var crc = new Crc32();
            crc.Append(bytes);
            return crc.GetCurrentHashAsUInt32();
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) == 1
                        ? 0xEDB8_8320u ^ (value >> 1)
                        : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
