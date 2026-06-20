using System.IO;
using System.IO.Compression;

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
                """));

        var path = exporter.ExportZip(inputs, directory.Path);

        Assert.True(File.Exists(path));
        Assert.Contains(Path.Combine("support-bundles", "support-bundle-20260620-040506.zip"), path, StringComparison.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(path);
        Assert.Equal(
            ["README.txt", "diagnostics-report.txt", "diagnostics-summary.json", "manifest.json"],
            archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var readme = ReadEntryText(archive, "README.txt");
        var report = ReadEntryText(archive, "diagnostics-report.txt");
        var summaryJson = ReadEntryText(archive, "diagnostics-summary.json");
        var manifestJson = ReadEntryText(archive, "manifest.json");

        Assert.Contains("sanitized diagnostics text only", readme, StringComparison.Ordinal);
        Assert.Contains("No hardware output is triggered by export.", readme, StringComparison.Ordinal);
        Assert.Contains("Haptic Drive ASIO diagnostics", report, StringComparison.Ordinal);
        Assert.Contains("private path held in memory only", report, StringComparison.Ordinal);
        Assert.Contains(@"""SelectedGameId"": ""f1-25""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""SelectedGameDisplayName"": ""F1 25""", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""ContainsRawCaptures"": false", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""ContainsPrivateDevicePaths"": false", manifestJson, StringComparison.Ordinal);
        Assert.Contains(@"""RoadRecorderStatusText"": ""Road recorder: disabled; path disabled; last fallback none.""", summaryJson, StringComparison.Ordinal);
        Assert.Contains(@"""Items"": [", summaryJson, StringComparison.Ordinal);
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
