using System.Buffers.Binary;
using System.Net;
using System.Text;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Recording.Tests;

public sealed class TelemetryRecordingServiceTests
{
    [Fact]
    public async Task TelemetryRecordingV2_WritesHeaderRecordsAndFooter()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 8, 0, 0, TimeSpan.Zero);

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "V2 Header Test", "stage-09-test"))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(1, [0x01, 0x02], createdAtUtc.AddMilliseconds(5))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(2, [0x03, 0x04, 0x05], createdAtUtc.AddMilliseconds(15))).Succeeded);
        Assert.True((await recorder.StopAsync()).Succeeded);

        var bytes = await File.ReadAllBytesAsync(path);
        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.Equal("HDRVREC2", Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Contains("PKT2", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal("END2", Encoding.ASCII.GetString(bytes, bytes.Length - 24, 4));
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.True(loadResult.Recording.Metadata.RecordingComplete);
        Assert.Equal(2, loadResult.Recording.Metadata.PacketCount);
        Assert.NotNull(loadResult.Recording.Metadata.EndedAtUtc);
    }

    [Fact]
    public async Task TelemetryRecordingV2_PreservesRawPayloadBytes()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 8, 15, 0, TimeSpan.Zero);
        var firstPayload = new byte[] { 0x10, 0x20, 0x30 };
        var secondPayload = new byte[] { 0xAA, 0x00, 0xFF, 0x55 };

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Payload Test", "stage-09-test"))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(10, firstPayload, createdAtUtc.AddMilliseconds(10))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(11, secondPayload, createdAtUtc.AddMilliseconds(25))).Succeeded);
        firstPayload[0] = 0x99;
        secondPayload[0] = 0x11;
        Assert.True((await recorder.StopAsync()).Succeeded);

        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Equal([0x10, 0x20, 0x30], loadResult.Recording.Packets[0].Payload);
        Assert.Equal([0xAA, 0x00, 0xFF, 0x55], loadResult.Recording.Packets[1].Payload);
        Assert.Equal(TimeSpan.FromMilliseconds(10), loadResult.Recording.Packets[0].RelativeTime);
        Assert.Equal(TimeSpan.FromMilliseconds(25), loadResult.Recording.Packets[1].RelativeTime);
    }

    [Fact]
    public async Task TelemetryRecordingV2_ReaderRecoversMissingFooter()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 8, 30, 0, TimeSpan.Zero);

        await WriteRecordingAsync(path, createdAtUtc, [0x01, 0x02], [0x03, 0x04, 0x05]);

        var bytes = await File.ReadAllBytesAsync(path);
        await File.WriteAllBytesAsync(path, bytes[..^24]);

        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Equal(2, loadResult.Recording.Packets.Count);
        Assert.False(loadResult.Recording.Metadata.RecordingComplete);
        Assert.Equal(2, loadResult.Recording.Metadata.PacketCount);
    }

    [Fact]
    public async Task TelemetryRecordingV2_ReaderStopsBeforeCorruptCrcRecord()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 8, 45, 0, TimeSpan.Zero);
        await WriteRecordingAsync(path, createdAtUtc, [0x10, 0x11], [0x21, 0x22, 0x23]);

        var bytes = await File.ReadAllBytesAsync(path);
        var payloadIndex = FindLastSequence(bytes, [0x21, 0x22, 0x23]);
        Assert.True(payloadIndex >= 0);
        bytes[payloadIndex] ^= 0x7F;
        await File.WriteAllBytesAsync(path, bytes);

        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Single(loadResult.Recording.Packets);
        Assert.Equal(1, loadResult.Recording.Packets[0].SequenceNumber);
        Assert.False(loadResult.Recording.Metadata.RecordingComplete);
    }

    [Fact]
    public async Task TelemetryRecordingV2_MetadataUsesSelectedGameAndProfileHash()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 9, 0, 0, TimeSpan.Zero);
        var metadata = TelemetryRecordingMetadata.CreateDefault(
            createdAtUtc,
            sourceGame: "F1 25",
            sourceProfile: "Wet Setup",
            gameIntegrationId: "f1-25",
            telemetryProtocolName: "F1 25 UDP",
            telemetryProtocolVersion: "v3",
            profileHash: "abc123",
            sourceEndpoint: "F1 25|127.0.0.1",
            bindAddress: "127.0.0.1");

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(path, metadata)).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(1, [0x01], createdAtUtc)).Succeeded);
        Assert.True((await recorder.StopAsync()).Succeeded);

        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Equal("F1 25", loadResult.Recording.Metadata.SourceGame);
        Assert.Equal("Wet Setup", loadResult.Recording.Metadata.SourceProfile);
        Assert.Equal("abc123", loadResult.Recording.Metadata.ProfileHash);
        Assert.Equal("f1-25", loadResult.Recording.Metadata.GameIntegrationId);
        Assert.Equal("F1 25 UDP", loadResult.Recording.Metadata.TelemetryProtocolName);
        Assert.Equal("v3", loadResult.Recording.Metadata.TelemetryProtocolVersion);
        Assert.Equal("F1 25|127.0.0.1", loadResult.Recording.Metadata.SourceEndpoint);
    }

    [Fact]
    public async Task TelemetryRecordingCompatibility_ReadsExistingV1Recordings()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 9, 15, 0, TimeSpan.Zero);
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            WriteLegacyHeader(stream, createdAtUtc, packetCount: 2);
            WriteLegacyPacket(stream, 10, TimeSpan.FromMilliseconds(5), [0x01, 0x02]);
            WriteLegacyPacket(stream, 11, TimeSpan.FromMilliseconds(20), [0x03, 0x04, 0x05]);
            await stream.FlushAsync();
        }

        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Equal(2, loadResult.Recording.Packets.Count);
        Assert.True(loadResult.Recording.Metadata.RecordingComplete);
        Assert.Equal(TimeSpan.FromMilliseconds(5), loadResult.Recording.Packets[0].RelativeTime);
        Assert.Equal(TimeSpan.FromMilliseconds(20), loadResult.Recording.Packets[1].RelativeTime);
    }

    [Fact]
    public async Task RecordingSummary_LoadsMetadataAndSequenceHealth()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 9, 30, 0, TimeSpan.Zero);

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Summary Test", "stage-09-test"))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(1, [0x01], createdAtUtc)).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(4, [0x02, 0x03], createdAtUtc.AddMilliseconds(20))).Succeeded);
        Assert.True((await recorder.StopAsync()).Succeeded);

        var result = await TelemetryRecordingFile.LoadSummaryAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Summary);
        Assert.Equal("Summary Test", result.Summary.Metadata.SourceProfile);
        Assert.Equal(2, result.Summary.PacketCount);
        Assert.Equal(TimeSpan.FromMilliseconds(20), result.Summary.Duration);
        Assert.Equal(3, result.Summary.PayloadBytes);
        Assert.Equal(2, result.Summary.MissingSequenceCount);
        Assert.Equal(2, result.Summary.LargestSequenceGap);
    }

    [Fact]
    public async Task Recording_InvalidPathFailsSafely()
    {
        await using var recorder = new TelemetryRecordingService();

        var result = await recorder.StartAsync("   ");

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingOperationStatus.Failure, result.Status);
        Assert.Contains("path is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recording_RejectsUnreasonablePayloadLengthSafely()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 9, 45, 0, TimeSpan.Zero);
        await using var recorder = new TelemetryRecordingService(maxPayloadLength: 3);

        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Length Test", "stage-09-test"))).Succeeded);
        var recordResult = recorder.RecordPacket(CreatePacket(1, [0x01, 0x02, 0x03, 0x04], createdAtUtc));
        var stopResult = await recorder.StopAsync();

        Assert.False(recordResult.Succeeded);
        Assert.Contains("exceeds 3 bytes", recordResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(stopResult.Succeeded);
    }

    [Fact]
    public async Task TelemetryIngressRecording_QueueDropMarksRecordingIncomplete()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero);
        var blockingStream = new BlockingPacketWriteStream();

        await using var recorder = new TelemetryRecordingService(
            queueCapacityPackets: 1,
            recordingStreamFactory: _ => blockingStream);
        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Backpressure Test", "stage-09-test"))).Succeeded);

        blockingStream.BlockPacketWrites = true;
        Assert.True(recorder.RecordPacket(CreatePacket(1, [0x01], createdAtUtc)).Succeeded);
        Assert.True(blockingStream.WaitForBlockedPacketWrite(TimeSpan.FromSeconds(3)));
        Assert.True(recorder.RecordPacket(CreatePacket(2, [0x02], createdAtUtc.AddMilliseconds(5))).Succeeded);

        var droppedResult = recorder.RecordPacket(CreatePacket(3, [0x03], createdAtUtc.AddMilliseconds(10)));
        var snapshot = recorder.GetSnapshot();

        Assert.Equal(TelemetryRecordingOperationStatus.Dropped, droppedResult.Status);
        Assert.True(snapshot.RecordingIncomplete);
        Assert.Equal(1, snapshot.DroppedPacketCount);
        Assert.Contains("queue", snapshot.IncompleteReason, StringComparison.OrdinalIgnoreCase);

        blockingStream.ReleaseBlockedWrites();
        Assert.True((await recorder.StopAsync()).Succeeded);
    }

    [Fact]
    public async Task Loading_CorruptHeaderFailsSafely()
    {
        var path = CreateTempRecordingPath();
        await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("not-a-recording"));

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.Corrupt, result.Status);
    }

    [Fact]
    public async Task Loading_UnsupportedVersionFailsSafely()
    {
        var path = CreateTempRecordingPath();
        await File.WriteAllBytesAsync(path, CreateLegacyHeaderBytes(version: 99, packetCount: 0));

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.UnsupportedVersion, result.Status);
    }

    [Fact]
    public async Task Loading_TruncatedPacketRecordFailsSafely()
    {
        var path = CreateTempRecordingPath();
        await File.WriteAllBytesAsync(path, CreateLegacyHeaderBytes(version: 1, packetCount: 1));

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.Corrupt, result.Status);
        Assert.Contains("truncated", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Replay_UsesAbsoluteDeadlinesWithoutAccumulatingProcessingDelay()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 21, 11, 0, 0, TimeSpan.Zero));
        var scheduler = new AdvancingDelayScheduler(timeProvider, TimeSpan.FromMilliseconds(5));
        var replay = new TelemetryReplayService(timeProvider, delayScheduler: scheduler);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero)),
            [
                new TelemetryRecordedPacket(1, new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero), TimeSpan.Zero, [0x01]),
                new TelemetryRecordedPacket(2, new DateTimeOffset(2026, 6, 20, 0, 0, 0, 10, TimeSpan.Zero), TimeSpan.FromMilliseconds(10), [0x02]),
                new TelemetryRecordedPacket(3, new DateTimeOffset(2026, 6, 20, 0, 0, 0, 20, TimeSpan.Zero), TimeSpan.FromMilliseconds(20), [0x03])
            ]);

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.TimePreserving);
        var snapshot = replay.GetSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(
            [TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(5)],
            scheduler.Delays);
        Assert.Equal(TimeSpan.FromMilliseconds(10), snapshot.TotalReplayDrift);
        Assert.Equal(TimeSpan.FromMilliseconds(5), snapshot.MaxLatePacket);
        Assert.Equal(0, snapshot.SkippedSleepCount);
    }

    [Fact]
    public async Task ReplayPacketsCarryFreshReceiveTimestamps()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 21, 11, 15, 0, TimeSpan.Zero));
        var replay = new TelemetryReplayService(timeProvider);
        var recordedAtUtc = new DateTimeOffset(2026, 6, 20, 11, 15, 0, TimeSpan.Zero);
        var replayed = new List<UdpTelemetryPacket>();
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(recordedAtUtc),
            [new TelemetryRecordedPacket(1, recordedAtUtc, TimeSpan.Zero, [0x01, 0x02])]);
        replay.PacketReplayed += (_, args) => replayed.Add(args.Packet);

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.Fast);

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(replayed);
        Assert.Equal(timeProvider.GetUtcNow(), replayed[0].ReceivedAtUtc);
        Assert.NotEqual(recordedAtUtc, replayed[0].ReceivedAtUtc);
        Assert.Equal(timeProvider.GetTimestamp(), replayed[0].ReceivedAtTimestamp);
    }

    [Fact]
    public async Task Replay_ValidPacketPassesThroughF125ParserAndVehicleStateAdapter()
    {
        var datagram = CreateF125Datagram(F125PacketKind.CarTelemetry, playerCarIndex: 4);
        var telemetryOffset = 4 * 60;
        WriteUInt16(datagram, telemetryOffset, 289);
        datagram[HeaderOffset + telemetryOffset + 15] = 6;

        var replay = new TelemetryReplayService();
        var adapter = new F125VehicleStateAdapter();
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 21, 11, 30, 0, TimeSpan.Zero)),
            [new TelemetryRecordedPacket(1, TimeSpan.Zero, datagram)]);
        replay.PacketReplayed += (_, args) =>
        {
            var parseResult = F125PacketParser.Parse(args.Packet.Payload);
            Assert.True(parseResult.Succeeded, parseResult.Message);
            var update = adapter.Apply(parseResult);
            Assert.True(update.WasApplied, update.Message);
        };

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.Fast);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(289, adapter.Current.Telemetry!.Value.SpeedKph);
        Assert.Equal(6, adapter.Current.Telemetry.Value.Gear);
    }

    [Fact]
    public async Task Replay_MalformedPacketDoesNotCrashParserOrVehicleStateAdapter()
    {
        var replay = new TelemetryReplayService();
        var adapter = new F125VehicleStateAdapter();
        var parserFailedSafely = false;
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 21, 11, 45, 0, TimeSpan.Zero)),
            [new TelemetryRecordedPacket(1, TimeSpan.Zero, [0x01, 0x02, 0x03])]);
        replay.PacketReplayed += (_, args) =>
        {
            var parseResult = F125PacketParser.Parse(args.Packet.Payload);
            var update = adapter.Apply(parseResult);
            parserFailedSafely = parseResult.Failed && update.WasIgnored;
        };

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.Fast);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(parserFailedSafely);
        Assert.Null(adapter.Current.Telemetry);
    }

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    private static async Task WriteRecordingAsync(
        string path,
        DateTimeOffset createdAtUtc,
        byte[] firstPayload,
        byte[] secondPayload)
    {
        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Replay Test", "stage-09-test"))).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(1, firstPayload, createdAtUtc)).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(2, secondPayload, createdAtUtc.AddMilliseconds(8))).Succeeded);
        Assert.True((await recorder.StopAsync()).Succeeded);
    }

    private static UdpTelemetryPacket CreatePacket(long sequenceNumber, byte[] payload, DateTimeOffset receivedAtUtc)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            receivedAtUtc,
            TimeProvider.System.GetTimestamp());
    }

    private static string CreateTempRecordingPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Recording.Tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdrec");
    }

    private static void WriteLegacyHeader(Stream stream, DateTimeOffset createdAtUtc, long packetCount)
    {
        stream.Write(CreateLegacyHeaderBytes(version: 1, packetCount: packetCount, createdAtUtc));
    }

    private static byte[] CreateLegacyHeaderBytes(
        int version,
        long packetCount,
        DateTimeOffset? createdAtUtc = null)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("HDREC001"));
        WriteInt32(stream, version);
        WriteInt64(stream, (createdAtUtc ?? new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero)).UtcTicks);
        WriteString(stream, "F1 25");
        WriteString(stream, "Legacy Test");
        WriteString(stream, "stage-09-test");
        WriteInt64(stream, packetCount);
        return stream.ToArray();
    }

    private static void WriteLegacyPacket(Stream stream, long sequenceNumber, TimeSpan relativeTime, byte[] payload)
    {
        WriteInt64(stream, sequenceNumber);
        WriteInt64(stream, relativeTime.Ticks);
        WriteInt32(stream, payload.Length);
        stream.Write(payload);
    }

    private static int FindLastSequence(byte[] haystack, byte[] needle)
    {
        for (var i = haystack.Length - needle.Length; i >= 0; i--)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[] CreateF125Datagram(F125PacketKind kind, byte playerCarIndex)
    {
        var definition = F125PacketDefinitions.All.Single(packet => packet.Kind == kind);
        var datagram = new byte[definition.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), F125PacketDefinitions.PacketFormat);
        datagram[2] = F125PacketDefinitions.GameYear;
        datagram[3] = 1;
        datagram[4] = 0;
        datagram[5] = F125PacketDefinitions.PacketVersion;
        datagram[6] = definition.Id;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), 123_456_789);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(12.25f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), 84);
        datagram[27] = playerCarIndex;
        datagram[28] = 255;
        return datagram;
    }

    private static void WriteUInt16(byte[] datagram, int bodyOffset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(ushort)), value);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
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

    private sealed class AdvancingDelayScheduler : ITelemetryReplayDelayScheduler
    {
        private readonly ManualTimeProvider _timeProvider;
        private readonly TimeSpan _delayOverhead;

        public AdvancingDelayScheduler(ManualTimeProvider timeProvider, TimeSpan delayOverhead)
        {
            _timeProvider = timeProvider;
            _delayOverhead = delayOverhead;
        }

        public List<TimeSpan> Delays { get; } = [];

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            _timeProvider.Advance(delay + _delayOverhead);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _originUtc;
        private long _timestamp;

        public ManualTimeProvider(DateTimeOffset originUtc)
        {
            _originUtc = originUtc;
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref _timestamp);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _originUtc.AddTicks(GetTimestamp());
        }

        public void Advance(TimeSpan amount)
        {
            Interlocked.Add(ref _timestamp, amount.Ticks);
        }
    }

    private sealed class BlockingPacketWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly ManualResetEventSlim _packetWriteBlocked = new(false);
        private readonly ManualResetEventSlim _releasePacketWrites = new(false);

        public bool BlockPacketWrites { get; set; }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public bool WaitForBlockedPacketWrite(TimeSpan timeout)
        {
            return _packetWriteBlocked.Wait(timeout);
        }

        public void ReleaseBlockedWrites()
        {
            _releasePacketWrites.Set();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MaybeBlockPacketWrite();
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            MaybeBlockPacketWrite();
            _inner.Write(buffer);
        }

        public override ValueTask DisposeAsync()
        {
            _packetWriteBlocked.Dispose();
            _releasePacketWrites.Dispose();
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _packetWriteBlocked.Dispose();
                _releasePacketWrites.Dispose();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void MaybeBlockPacketWrite()
        {
            if (!BlockPacketWrites)
            {
                return;
            }

            _packetWriteBlocked.Set();
            _releasePacketWrites.Wait(TimeSpan.FromSeconds(3));
        }
    }
}
