using System.Buffers.Binary;
using System.IO;
using System.Net;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App.Tests;

public sealed class RecordingPacketHistogramAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsF125PacketHistogramForKnownPackets()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "histogram.hdrec");

        await CreateRecordingAsync(
            path,
            [
                new RecordedPacketTemplate(1, CreateDatagram(F125PacketKind.Motion), TimeSpan.Zero),
                new RecordedPacketTemplate(2, CreateDatagram(F125PacketKind.Motion), TimeSpan.FromMilliseconds(10)),
                new RecordedPacketTemplate(3, CreateDatagram(F125PacketKind.CarTelemetry), TimeSpan.FromMilliseconds(20))
            ]);

        var analysis = await RecordingPacketHistogramAnalyzer.AnalyzeAsync(path);

        Assert.Contains("Packet histogram:", analysis, StringComparison.Ordinal);
        Assert.Contains("Motion#0: 2", analysis, StringComparison.Ordinal);
        Assert.Contains("Car Telemetry#6: 1", analysis, StringComparison.Ordinal);
        Assert.Contains("Packet preview:", analysis, StringComparison.Ordinal);
        Assert.Contains("seq 1; 0 ms; Motion#0; 1,349 B", analysis, StringComparison.Ordinal);
        Assert.Contains("seq 3; 20 ms; Car Telemetry#6; 1,352 B", analysis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeDetailsAsync_ReturnsStructuredHistogramAndPreviewEntries()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "structured.hdrec");

        await CreateRecordingAsync(
            path,
            [
                new RecordedPacketTemplate(1, CreateDatagram(F125PacketKind.Motion), TimeSpan.Zero),
                new RecordedPacketTemplate(2, CreateDatagram(F125PacketKind.Motion), TimeSpan.FromMilliseconds(10)),
                new RecordedPacketTemplate(3, CreateDatagram(F125PacketKind.CarTelemetry), TimeSpan.FromMilliseconds(20))
            ]);

        var result = await RecordingPacketHistogramAnalyzer.AnalyzeDetailsAsync(path);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Analysis);
        Assert.Equal(3, result.Analysis.PacketCount);
        Assert.Equal("F1 25", result.Analysis.SourceGame);
        Assert.Collection(
            result.Analysis.HistogramEntries,
            entry =>
            {
                Assert.Equal("Motion", entry.Name);
                Assert.Equal((byte)0, entry.PacketId);
                Assert.Equal(2, entry.Count);
            },
            entry =>
            {
                Assert.Equal("Car Telemetry", entry.Name);
                Assert.Equal((byte)6, entry.PacketId);
                Assert.Equal(1, entry.Count);
            });
        Assert.Collection(
            result.Analysis.PreviewEntries,
            entry =>
            {
                Assert.Equal(1, entry.SequenceNumber);
                Assert.Equal(TimeSpan.Zero, entry.RelativeTime);
                Assert.Equal("Motion#0", entry.Label);
                Assert.Equal(1349, entry.PayloadSizeBytes);
            },
            entry =>
            {
                Assert.Equal(2, entry.SequenceNumber);
                Assert.Equal(TimeSpan.FromMilliseconds(10), entry.RelativeTime);
                Assert.Equal("Motion#0", entry.Label);
                Assert.Equal(1349, entry.PayloadSizeBytes);
            },
            entry =>
            {
                Assert.Equal(3, entry.SequenceNumber);
                Assert.Equal(TimeSpan.FromMilliseconds(20), entry.RelativeTime);
                Assert.Equal("Car Telemetry#6", entry.Label);
                Assert.Equal(1352, entry.PayloadSizeBytes);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsIgnoredAndInvalidPacketsSeparately()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "mixed.hdrec");

        await CreateRecordingAsync(
            path,
            [
                new RecordedPacketTemplate(1, CreateUnknownPacketIdDatagram(99), TimeSpan.Zero),
                new RecordedPacketTemplate(2, new byte[F125PacketDefinitions.HeaderSize - 1], TimeSpan.FromMilliseconds(10))
            ]);

        var analysis = await RecordingPacketHistogramAnalyzer.AnalyzeAsync(path);

        Assert.Contains("Ignored unknown packet IDs: 1", analysis, StringComparison.Ordinal);
        Assert.Contains("Invalid packet headers: 1", analysis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsUnsupportedSourceGameGracefully()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "unsupported.hdrec");

        await CreateRecordingAsync(
            path,
            [new RecordedPacketTemplate(1, [0x01, 0x02], TimeSpan.Zero)],
            metadata: new TelemetryRecordingMetadata(
                new DateTimeOffset(2026, 6, 12, 1, 0, 0, TimeSpan.Zero),
                "Other Game",
                "App Test",
                "stage-25u-test"));

        var analysis = await RecordingPacketHistogramAnalyzer.AnalyzeAsync(path);

        Assert.Contains("source game Other Game is not supported", analysis, StringComparison.Ordinal);
    }

    [Fact]
    public void InspectionFormatter_FormatsUnavailableResultsGracefully()
    {
        var text = RecordingPacketInspectionFormatter.Format(
            RecordingPacketInspectionResult.Unavailable("Packet histogram unavailable: recording path is missing."));

        Assert.Equal("Packet histogram unavailable: recording path is missing.", text);
    }

    [Fact]
    public void DetailFormatter_AppendsAnalysisTextWhenPresent()
    {
        var detail = RecordingLibraryDetailFormatter.BuildDetailText("Base detail.", "Packet histogram: Motion#0: 1.");

        Assert.Contains("Base detail.", detail, StringComparison.Ordinal);
        Assert.Contains("Packet histogram: Motion#0: 1.", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailFormatter_BuildsClipboardTextWithSummaryDetailAndAnalysis()
    {
        var clipboardText = RecordingLibraryDetailFormatter.BuildClipboardText(
            @"C:\Recordings\session.hdrec",
            "session.hdrec - 3 packet(s) - 20 ms - 4.0 KB",
            "Created today.",
            "Packet histogram: Motion#0: 2.");

        Assert.Contains("Recording: session.hdrec", clipboardText, StringComparison.Ordinal);
        Assert.Contains(@"Path: C:\Recordings\session.hdrec", clipboardText, StringComparison.Ordinal);
        Assert.Contains("Summary: session.hdrec - 3 packet(s) - 20 ms - 4.0 KB", clipboardText, StringComparison.Ordinal);
        Assert.Contains("Detail: Created today.", clipboardText, StringComparison.Ordinal);
        Assert.Contains("Packet histogram: Motion#0: 2.", clipboardText, StringComparison.Ordinal);
    }

    private static async Task CreateRecordingAsync(
        string path,
        IReadOnlyList<RecordedPacketTemplate> packets,
        TelemetryRecordingMetadata? metadata = null)
    {
        var createdAtUtc = new DateTimeOffset(2026, 6, 12, 1, 0, 0, TimeSpan.Zero);
        metadata ??= new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "App Test", "stage-25u-test");
        createdAtUtc = metadata.CreatedAtUtc;

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(path, metadata)).Succeeded);
        foreach (var packet in packets)
        {
            Assert.True(recorder.RecordPacket(new UdpTelemetryPacket(
                packet.SequenceNumber,
                packet.Payload,
                new IPEndPoint(IPAddress.Loopback, 20_778),
                createdAtUtc + packet.RelativeTime)).Succeeded);
        }

        Assert.True((await recorder.StopAsync()).Succeeded);
    }

    private static byte[] CreateDatagram(F125PacketKind kind)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == kind);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id);
        return datagram;
    }

    private static byte[] CreateUnknownPacketIdDatagram(byte packetId)
    {
        var datagram = new byte[F125PacketDefinitions.HeaderSize];
        WriteHeader(datagram, packetId);
        return datagram;
    }

    private static void WriteHeader(byte[] datagram, byte packetId)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), F125PacketDefinitions.PacketFormat);
        datagram[2] = F125PacketDefinitions.GameYear;
        datagram[3] = 1;
        datagram[4] = 0;
        datagram[5] = F125PacketDefinitions.PacketVersion;
        datagram[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), 123456789);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(12.25f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), 84);
        datagram[27] = 3;
        datagram[28] = 255;
    }

    private sealed record RecordedPacketTemplate(
        long SequenceNumber,
        byte[] Payload,
        TimeSpan RelativeTime);

    private sealed class TempRecordingDirectory : IDisposable
    {
        private TempRecordingDirectory(string parentPath, string path)
        {
            ParentPath = parentPath;
            Path = path;
        }

        public string Path { get; }

        public string ParentPath { get; }

        public static TempRecordingDirectory Create()
        {
            var parentPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "HapticDrive.Asio.App.Tests",
                Guid.NewGuid().ToString("N"));
            var path = System.IO.Path.Combine(parentPath, "Recordings");
            Directory.CreateDirectory(path);
            return new TempRecordingDirectory(parentPath, path);
        }

        public void Dispose()
        {
            if (Directory.Exists(ParentPath))
            {
                Directory.Delete(ParentPath, recursive: true);
            }
        }
    }
}
