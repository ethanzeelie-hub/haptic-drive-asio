using System.Buffers.Binary;
using System.Net;
using System.Text;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Recording.Tests;

public sealed class TelemetryRecordingServiceTests
{
    [Fact]
    public async Task Recording_WritesPacketsInOrderWithExactPayloadCopiesAndRelativeTiming()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 2, 8, 30, 0, TimeSpan.Zero);
        var firstPayload = new byte[] { 0x01, 0x02, 0x03 };
        var secondPayload = new byte[] { 0xAA, 0x00, 0xFF, 0x10 };

        await using var recorder = new TelemetryRecordingService();
        var startResult = await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Unit Test", "stage-09-test"));

        Assert.True(startResult.Succeeded, startResult.Message);

        var firstRecordResult = recorder.RecordPacket(CreatePacket(10, firstPayload, createdAtUtc.AddMilliseconds(10)));
        firstPayload[0] = 0x99;
        var secondRecordResult = recorder.RecordPacket(CreatePacket(11, secondPayload, createdAtUtc.AddMilliseconds(25)));

        var snapshot = recorder.GetSnapshot();
        var stopResult = await recorder.StopAsync();
        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(firstRecordResult.Succeeded, firstRecordResult.Message);
        Assert.True(secondRecordResult.Succeeded, secondRecordResult.Message);
        Assert.Equal(2, snapshot.PacketCount);
        Assert.Equal(TimeSpan.FromMilliseconds(25), snapshot.LastPacketRelativeTime);
        Assert.True(stopResult.Succeeded, stopResult.Message);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Equal("F1 25", loadResult.Recording.Metadata.SourceGame);
        Assert.Equal("Unit Test", loadResult.Recording.Metadata.SourceProfile);
        Assert.Equal(2, loadResult.Recording.Packets.Count);
        Assert.Equal(10, loadResult.Recording.Packets[0].SequenceNumber);
        Assert.Equal(11, loadResult.Recording.Packets[1].SequenceNumber);
        Assert.Equal(TimeSpan.FromMilliseconds(10), loadResult.Recording.Packets[0].RelativeTime);
        Assert.Equal(TimeSpan.FromMilliseconds(25), loadResult.Recording.Packets[1].RelativeTime);
        Assert.Equal([0x01, 0x02, 0x03], loadResult.Recording.Packets[0].Payload);
        Assert.Equal(secondPayload, loadResult.Recording.Packets[1].Payload);
    }

    [Fact]
    public async Task Recording_ZeroPacketSessionStopsToReadableFile()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

        await using var recorder = new TelemetryRecordingService();

        var startResult = await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Empty", "stage-09-test"));
        var stopResult = await recorder.StopAsync();
        var loadResult = await TelemetryRecordingFile.LoadAsync(path);

        Assert.True(startResult.Succeeded, startResult.Message);
        Assert.True(stopResult.Succeeded, stopResult.Message);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Recording);
        Assert.Empty(loadResult.Recording.Packets);
        Assert.Equal(createdAtUtc, loadResult.Recording.Metadata.CreatedAtUtc);
    }

    [Fact]
    public async Task Recording_InvalidPathFailsSafely()
    {
        await using var recorder = new TelemetryRecordingService();

        var result = await recorder.StartAsync("   ");

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingOperationStatus.Failure, result.Status);
        Assert.Contains("path is required", result.Message);
    }

    [Fact]
    public async Task Recording_RejectsUnreasonablePayloadLengthSafely()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        await using var recorder = new TelemetryRecordingService(maxPayloadLength: 3);

        var startResult = await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Length Test", "stage-09-test"));
        var recordResult = recorder.RecordPacket(CreatePacket(1, [0x01, 0x02, 0x03, 0x04], createdAtUtc));
        var stopResult = await recorder.StopAsync();

        Assert.True(startResult.Succeeded, startResult.Message);
        Assert.False(recordResult.Succeeded);
        Assert.Contains("exceeds 3 bytes", recordResult.Message);
        Assert.False(stopResult.Succeeded);
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
        await File.WriteAllBytesAsync(path, CreateHeaderBytes(version: 99, packetCount: 0));

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.UnsupportedVersion, result.Status);
    }

    [Fact]
    public async Task Loading_TruncatedPacketRecordFailsSafely()
    {
        var path = CreateTempRecordingPath();
        await File.WriteAllBytesAsync(path, CreateHeaderBytes(version: 1, packetCount: 1));

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.Corrupt, result.Status);
        Assert.Contains("truncated", result.Message);
    }

    [Fact]
    public async Task Loading_InvalidPayloadLengthFailsSafely()
    {
        var path = CreateTempRecordingPath();
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var header = CreateHeaderBytes(version: 1, packetCount: 1);
            await stream.WriteAsync(header);
            WriteInt64(stream, 1);
            WriteInt64(stream, 0);
            WriteInt32(stream, TelemetryRecordingFile.DefaultMaxPayloadLength + 1);
        }

        var result = await TelemetryRecordingFile.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(TelemetryRecordingLoadStatus.Corrupt, result.Status);
        Assert.Contains("payload length", result.Message);
    }

    [Fact]
    public async Task ReplayFile_EmitsPacketsInRecordedOrderAndPreservesRawBytes()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero);
        var firstPayload = new byte[] { 0x10, 0x20 };
        var secondPayload = new byte[] { 0x30, 0x40, 0x50 };
        await WriteRecordingAsync(path, createdAtUtc, firstPayload, secondPayload);
        var replay = new TelemetryReplayService();
        var replayed = new List<UdpTelemetryPacket>();
        replay.PacketReplayed += (_, args) => replayed.Add(args.Packet);

        var result = await replay.ReplayFileAsync(path, TelemetryReplayOptions.Fast);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.PacketsReplayed);
        Assert.Equal(2, replayed.Count);
        Assert.Equal(1, replayed[0].SequenceNumber);
        Assert.Equal(2, replayed[1].SequenceNumber);
        Assert.Equal(firstPayload, replayed[0].Payload);
        Assert.Equal(secondPayload, replayed[1].Payload);
        Assert.Equal(createdAtUtc, replayed[0].ReceivedAtUtc);
        Assert.Equal(createdAtUtc.AddMilliseconds(8), replayed[1].ReceivedAtUtc);
    }

    [Fact]
    public async Task Replay_StopCancelsSafely()
    {
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)),
            [
                new TelemetryRecordedPacket(1, TimeSpan.Zero, [0x01]),
                new TelemetryRecordedPacket(2, TimeSpan.FromSeconds(10), [0x02])
            ]);
        var replay = new TelemetryReplayService();
        var replayedCount = 0;
        var firstPacketReplayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        replay.PacketReplayed += (_, _) =>
        {
            replayedCount++;
            firstPacketReplayed.TrySetResult();
        };

        var replayTask = replay.ReplayAsync(recording, TelemetryReplayOptions.TimePreserving).AsTask();
        await firstPacketReplayed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await replay.StopAsync();
        var result = await replayTask;

        Assert.Equal(TelemetryReplayStatus.Cancelled, result.Status);
        Assert.Equal(1, result.PacketsReplayed);
        Assert.Equal(1, replayedCount);
    }

    [Fact]
    public async Task Replay_ValidPacketPassesThroughF125ParserAndVehicleStateAdapter()
    {
        var datagram = CreateF125Datagram(F125PacketKind.CarTelemetry, playerCarIndex: 4);
        var telemetryOffset = 4 * 60;
        WriteUInt16(datagram, telemetryOffset, 289);
        datagram[HeaderOffset + telemetryOffset + 15] = 6;
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.Zero)),
            [new TelemetryRecordedPacket(1, TimeSpan.Zero, datagram)]);
        var replay = new TelemetryReplayService();
        var adapter = new F125VehicleStateAdapter();
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
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(new DateTimeOffset(2026, 6, 2, 14, 0, 0, TimeSpan.Zero)),
            [new TelemetryRecordedPacket(1, TimeSpan.Zero, [0x01, 0x02, 0x03])]);
        var replay = new TelemetryReplayService();
        var adapter = new F125VehicleStateAdapter();
        var parserFailedSafely = false;
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
        var startResult = await recorder.StartAsync(
            path,
            new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "Replay Test", "stage-09-test"));
        Assert.True(startResult.Succeeded, startResult.Message);
        Assert.True(recorder.RecordPacket(CreatePacket(1, firstPayload, createdAtUtc)).Succeeded);
        Assert.True(recorder.RecordPacket(CreatePacket(2, secondPayload, createdAtUtc.AddMilliseconds(8))).Succeeded);
        var stopResult = await recorder.StopAsync();
        Assert.True(stopResult.Succeeded, stopResult.Message);
    }

    private static UdpTelemetryPacket CreatePacket(long sequenceNumber, byte[] payload, DateTimeOffset receivedAtUtc)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            receivedAtUtc);
    }

    private static string CreateTempRecordingPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Recording.Tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdrec");
    }

    private static byte[] CreateHeaderBytes(int version, long packetCount)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("HDREC001"));
        WriteInt32(stream, version);
        WriteInt64(stream, new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero).UtcTicks);
        WriteString(stream, "F1 25");
        WriteString(stream, "Corrupt Test");
        WriteString(stream, "stage-09-test");
        WriteInt64(stream, packetCount);
        return stream.ToArray();
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
}
