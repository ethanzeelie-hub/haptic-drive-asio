using System.Buffers.Binary;
using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research;
using HapticDrive.Simagic.PHPR.Research.Capture;
using HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicCaptureAnalysisTests
{
    [Fact]
    public async Task CsvAnalysis_CreatesPayloadObservationsAndSanitizedSummaries()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var csvPath = Path.Combine(tempDirectory, "simhub_synthetic.csv");
            await File.WriteAllTextAsync(csvPath, """
                file,kind,frame,time,payload_spaced
                002_brake_50hz_10pct_1000ms_tes_usb_packets.csv,active_start,3927,3.264321000,F1 EC 01 01 32 0A 00 00 00 00
                002_brake_50hz_10pct_1000ms_tes_usb_packets.csv,stop_or_idle,4947,4.280591000,F1 EC 01 00 0A 00 00 00 00 00
                """);

            var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(csvPath);
            var json = JsonSerializer.Serialize(report, SimagicCaptureJson.Options);

            Assert.Equal(1, report.SourceFileCount);
            Assert.Equal(2, report.PayloadObservationCount);
            Assert.Equal(2, report.UniquePayloadCount);
            Assert.Contains(report.FileSummaries, summary => summary.SourceKind == SimagicCaptureAnalysisSourceKind.WiresharkCsv);
            Assert.Contains(report.TopPayloads, payload => payload.SourceColumns.Contains("payload_spaced"));
            Assert.DoesNotContain(nameof(SimagicUsbPayloadObservation.PayloadBytes), json, StringComparison.Ordinal);
            Assert.DoesNotContain("F1 EC 01 01 32 0A 00 00 00 00", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task TextSummaryAnalysis_ReadsSetReportCountsAndPayloads()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var textPath = Path.Combine(tempDirectory, "phpr_setreport_summary.txt");
            await File.WriteAllTextAsync(textPath, """
                ====================================================================================================
                FILE: 002_brake_50hz_50pct_250ms_tes_usb_packets.csv
                ====================================================================================================
                All payload records: 5065
                SET_REPORT candidate records: 112
                Top SET_REPORT candidate payloads:
                  count=   3 len=64 time=3.190115000 frame=3942 payload=80 1E 89 02 02 03 00 0F 01 01 08 01 1A 0B 08 02 10 41 18 32 20 F0 2E 30 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
                """);

            var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(textPath);

            Assert.Equal(3, report.PayloadObservationCount);
            var summary = Assert.Single(report.FileSummaries);
            Assert.Equal(5065, summary.DeclaredPayloadRecordCount);
            Assert.Equal(112, summary.DeclaredSetReportCandidateCount);
            Assert.Equal(3, summary.PayloadRecordCount);
            Assert.Contains(report.TopPayloads, payload => payload.PayloadLength == 64);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CompareTextAnalysis_CapturesByteDiffObservationsWithoutFieldClaims()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var textPath = Path.Combine(tempDirectory, "compare.txt");
            await File.WriteAllTextAsync(textPath, """
                COMPARE: left.csv VS right.csv
                changed bytes: 1
                left.csv: 80 1E 89 02
                right.csv: 80 1E 89 03
                  byte 03 / 0x03: left.csv=02, right.csv=03
                """);

            var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(textPath);
            var diff = Assert.Single(report.DiffObservations);

            Assert.Equal(1, diff.ChangedByteCount);
            Assert.Equal(3, Assert.Single(diff.Differences).Offset);
            Assert.Contains(report.SafetyBoundary, item => item.Contains("No protocol hypotheses", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureDiff_FindsClosestPayloadPairs()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var leftPath = Path.Combine(tempDirectory, "left.csv");
            var rightPath = Path.Combine(tempDirectory, "right.csv");
            await File.WriteAllTextAsync(leftPath, """
                frame,time,payload_spaced
                1,1.0,80 1E 89 02 10 41
                """);
            await File.WriteAllTextAsync(rightPath, """
                frame,time,payload_spaced
                1,1.0,80 1E 89 03 10 41
                """);

            var report = await new SimagicCaptureAnalysisReader().AnalyzeDiffAsync(leftPath, rightPath);
            var diff = Assert.Single(report.DiffObservations);

            Assert.Equal(1, diff.ChangedByteCount);
            Assert.Equal("0x03", Assert.Single(diff.Differences).HexOffset);
            Assert.Equal("02", Assert.Single(diff.Differences).LeftValueHex);
            Assert.Equal("03", Assert.Single(diff.Differences).RightValueHex);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PcapNgAnalysis_ParsesSyntheticUsbPcapContainerSummary()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var pcapPath = Path.Combine(tempDirectory, "synthetic.pcapng");
            await File.WriteAllBytesAsync(pcapPath, CreateSyntheticPcapNg());

            var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(pcapPath);
            var summary = Assert.Single(report.PcapSummaries);

            Assert.True(summary.Parsed);
            Assert.Equal(SimagicCaptureAnalysisSourceKind.PcapNg, summary.SourceKind);
            Assert.Equal(1, summary.SectionCount);
            Assert.Equal(1, summary.InterfaceCount);
            Assert.Equal(1, summary.PacketCount);
            Assert.Equal(4, summary.TotalCapturedBytes);
            Assert.Equal(249, Assert.Single(summary.Interfaces).LinkType);
            Assert.Contains(report.Warnings, warning => warning.Message.Contains("USBPcap link type", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Exporter_WritesSanitizedAnalysisJson()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var csvPath = Path.Combine(tempDirectory, "capture.csv");
            var outputDirectory = Path.Combine(tempDirectory, "generated");
            await File.WriteAllTextAsync(csvPath, """
                frame,time,payload_spaced
                1,1.0,F1 EC 01 01 32 0A 00 00 00 00
                """);
            var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(csvPath);

            var path = await new SimagicCaptureAnalysisExporter().ExportJsonAsync(report, outputDirectory);
            var json = await File.ReadAllTextAsync(path);

            Assert.Contains("Stage 2I", json, StringComparison.Ordinal);
            Assert.Contains("PayloadPreviewHex", json, StringComparison.Ordinal);
            Assert.DoesNotContain("payloadBytes", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CliHelp_ListsCaptureAnalysisCommands()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("capture-analysis", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("capture-diff", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"haptic-drive-stage-2i-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] CreateSyntheticPcapNg()
    {
        using var stream = new MemoryStream();
        WriteUInt32(stream, 0x0A0D0D0A);
        WriteUInt32(stream, 28);
        WriteUInt32(stream, 0x1A2B3C4D);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt64(stream, ulong.MaxValue);
        WriteUInt32(stream, 28);

        WriteUInt32(stream, 1);
        WriteUInt32(stream, 20);
        WriteUInt16(stream, 249);
        WriteUInt16(stream, 0);
        WriteUInt32(stream, 65535);
        WriteUInt32(stream, 20);

        WriteUInt32(stream, 6);
        WriteUInt32(stream, 36);
        WriteUInt32(stream, 0);
        WriteUInt32(stream, 0);
        WriteUInt32(stream, 0);
        WriteUInt32(stream, 4);
        WriteUInt32(stream, 4);
        stream.Write([0x01, 0x02, 0x03, 0x04]);
        WriteUInt32(stream, 36);
        return stream.ToArray();
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }
}
