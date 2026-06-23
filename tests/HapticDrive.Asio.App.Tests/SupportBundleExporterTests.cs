using System.IO;
using System.IO.Compression;
using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.App.Tests;

public sealed class SupportBundleExporterTests
{
    [Fact]
    public void ExportZip_WritesSanitizedDiagnosticsBundle()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new SupportBundleExporter();
        var generatedAt = new DateTimeOffset(2026, 6, 20, 4, 5, 6, TimeSpan.Zero);
        var inputs = new SupportBundleExportInputs(
            generatedAt,
            SelectedGameId: "f1-25",
            SelectedGameDisplayName: "F1 25",
            new DiagnosticsStatusPresentation(
                RoadRecorderStatusText: "Road recorder: disabled; path disabled; last fallback none.",
                SummaryText: "UDP 0 packet(s), parser 0 valid / 0 failed, effects 0, output peak 0.000, callbacks 0.",
                Items:
                [
                    "Pipeline: stopped",
                    "P-HPR real direct control: enabled; selected True; private path held in memory only."
                ],
                ClipboardReportText:
                """
                Haptic Drive ASIO diagnostics
                Generated: 6/20/2026 4:05 AM
                UDP 0 packet(s), parser 0 valid / 0 failed, effects 0, output peak 0.000, callbacks 0.
                Pipeline: stopped
                P-HPR real direct control: enabled; selected True; private path held in memory only.
                """),
            CreateStructuredDiagnostics(
                generatedAt,
                new SupportBundleCorrelationIds("app-1", "telemetry-1", null, "output-1", 3)),
            DiagnosticRedactionMode.Safe);

        var path = exporter.ExportZip(inputs, directory.Path);

        Assert.True(File.Exists(path));
        Assert.Contains(Path.Combine("support-bundles", "support-bundle-20260620-040506.zip"), path, StringComparison.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(path);
        Assert.Equal(
            ["README.txt", "diagnostic-events.json", "diagnostics-report.txt", "diagnostics-summary.json", "manifest.json"],
            archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var readme = ReadEntryText(archive, "README.txt");
        var eventsJson = ReadEntryText(archive, "diagnostic-events.json");
        var report = ReadEntryText(archive, "diagnostics-report.txt");
        var summaryJson = ReadEntryText(archive, "diagnostics-summary.json");
        var manifestJson = ReadEntryText(archive, "manifest.json");

        Assert.Contains("structured diagnostics", readme, StringComparison.Ordinal);
        Assert.Contains("Redaction mode: Safe.", readme, StringComparison.Ordinal);
        Assert.Contains("No hardware output is triggered by export.", readme, StringComparison.Ordinal);
        Assert.Contains(@"""EventId"": ""app.diagnostics.snapshot""", eventsJson, StringComparison.Ordinal);
        Assert.Contains("Haptic Drive ASIO diagnostics", report, StringComparison.Ordinal);
        Assert.Contains("private path held in memory only", report, StringComparison.Ordinal);
        Assert.Contains(@"""SelectedGameId"": ""f1-25""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""SelectedGameDisplayName"": ""F1 25""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""RedactionMode"": ""Safe""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""ContainsRawCaptures"": false", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""ContainsPrivateDevicePaths"": false", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""RoadRecorderStatusText"": ""Road recorder: disabled; path disabled; last fallback none.""", summaryJson, StringComparison.Ordinal);
        Assert.Contains(@"""Items"": [", summaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportZip_IncludesSelectedRecordingDetailWhenProvided()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new SupportBundleExporter();
        var generatedAt = new DateTimeOffset(2026, 6, 20, 5, 6, 7, TimeSpan.Zero);
        var inputs = new SupportBundleExportInputs(
            generatedAt,
            SelectedGameId: "f1-25",
            SelectedGameDisplayName: "F1 25",
            new DiagnosticsStatusPresentation(
                RoadRecorderStatusText: "Road recorder: disabled.",
                SummaryText: "Diagnostics summary.",
                Items: ["Pipeline: stopped"],
                ClipboardReportText: "Haptic Drive ASIO diagnostics"),
            CreateStructuredDiagnostics(
                generatedAt,
                new SupportBundleCorrelationIds("app-2", null, "recording-2", "output-2", 2)),
            DiagnosticRedactionMode.Safe,
            SelectedRecordingFileName: "session.hdrec",
            SelectedRecordingDetailText:
            """
            Recording: session.hdrec
            Summary: session.hdrec - 3 packet(s)
            Detail: Packet histogram: Motion#0: 2.
            """);

        var path = exporter.ExportZip(inputs, directory.Path);

        using var archive = ZipFile.OpenRead(path);
        Assert.Contains("selected-recording-detail.txt", archive.Entries.Select(entry => entry.FullName));

        var selectedRecordingDetail = ReadEntryText(archive, "selected-recording-detail.txt");
        var summaryJson = ReadEntryText(archive, "diagnostics-summary.json");
        var manifestJson = ReadEntryText(archive, "manifest.json");

        Assert.Contains("Recording: session.hdrec", selectedRecordingDetail, StringComparison.Ordinal);
        Assert.Contains(@"""SelectedRecordingFileName"": ""session.hdrec""", summaryJson, StringComparison.Ordinal);
        Assert.Contains(@"""ContainsSelectedRecordingDetail"": true", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""selected-recording-detail.txt""", manifestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportBundle_IncludesRedactionManifest()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new SupportBundleExporter();
        var generatedAt = new DateTimeOffset(2026, 6, 20, 7, 8, 9, TimeSpan.Zero);
        var inputs = new SupportBundleExportInputs(
            generatedAt,
            SelectedGameId: "f1-25",
            SelectedGameDisplayName: "F1 25",
            new DiagnosticsStatusPresentation(
                RoadRecorderStatusText: "disabled",
                SummaryText: "summary",
                Items: ["item"],
                ClipboardReportText: "report"),
            CreateStructuredDiagnostics(
                generatedAt,
                new SupportBundleCorrelationIds("app-4", null, null, "output-4", 5)),
            DiagnosticRedactionMode.Extended);

        var path = exporter.ExportZip(inputs, directory.Path);

        using var archive = ZipFile.OpenRead(path);
        var manifestJson = ReadEntryText(archive, "manifest.json");

        Assert.Contains(@"""RedactionMode"": ""Extended""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""PrivateIpInclusionRequested"": true", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""RawUsbDataExcluded"": true", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""RedactionCategoriesApplied"": [", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""CorrelationIdsIncluded"": [", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""EventCount"": 2", manifestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportFailure_LeavesNoPartialFinalZip()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new SupportBundleExporter((archive, _, _) =>
        {
            var entry = archive.CreateEntry("README.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("partial");
            throw new InvalidOperationException("boom");
        });

        var inputs = new SupportBundleExportInputs(
            new DateTimeOffset(2026, 6, 20, 8, 9, 10, TimeSpan.Zero),
            SelectedGameId: "f1-25",
            SelectedGameDisplayName: "F1 25",
            new DiagnosticsStatusPresentation("disabled", "summary", ["item"], "report"),
            CreateStructuredDiagnostics(
                DateTimeOffset.UtcNow,
                new SupportBundleCorrelationIds("app-5", null, null, "output-5", 1)),
            DiagnosticRedactionMode.Safe);

        Assert.Throws<InvalidOperationException>(() => exporter.ExportZip(inputs, directory.Path));

        var supportBundlesDirectory = Path.Combine(directory.Path, "support-bundles");
        Assert.False(Directory.EnumerateFiles(supportBundlesDirectory, "*.zip", SearchOption.TopDirectoryOnly).Any());
        Assert.False(Directory.EnumerateFiles(supportBundlesDirectory, "*.tmp", SearchOption.TopDirectoryOnly).Any());
    }

    private static SupportBundleStructuredDiagnostics CreateStructuredDiagnostics(
        DateTimeOffset generatedAt,
        SupportBundleCorrelationIds correlationIds)
    {
        return StructuredDiagnosticsBuilder.Build(
            new StructuredDiagnosticsBuildInputs(
                generatedAt,
                "f1-25",
                "F1 25",
                "null",
                "Default",
                [
                    new DiagnosticEvent(
                        generatedAt.AddSeconds(-1),
                        "telemetry.stale",
                        DiagnosticSeverity.Warning,
                        "Telemetry",
                        "Telemetry freshness is stale for output-driving signals.",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["telemetryFresh"] = "False"
                        },
                        correlationIds.TelemetrySessionId ?? correlationIds.AppSessionId)
                ],
                correlationIds));
    }

    private static string ReadEntryText(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Entry '{entryName}' was not found.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"support-bundle-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
