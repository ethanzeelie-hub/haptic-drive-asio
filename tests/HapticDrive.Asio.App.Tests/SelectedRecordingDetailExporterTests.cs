using System.IO;
using System.Text;

namespace HapticDrive.Asio.App.Tests;

public sealed class SelectedRecordingDetailExporterTests
{
    [Fact]
    public void ExportText_WritesDeterministicInspectionArtifact()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new SelectedRecordingDetailExporter();
        var generatedAt = new DateTimeOffset(2026, 6, 20, 6, 7, 8, TimeSpan.Zero);

        var path = exporter.ExportText(
            new SelectedRecordingDetailExportInputs(
                generatedAt,
                @"C:\Recordings\session-one.hdrec",
                """
                Recording: session-one.hdrec
                Summary: session-one.hdrec - 3 packet(s)
                Detail: Packet histogram: Motion#0: 2.
                """),
            directory.Path);

        Assert.True(File.Exists(path));
        Assert.Contains(Path.Combine("recording-inspections", "selected-recording-detail-20260620-060708-session-one.txt"), path, StringComparison.OrdinalIgnoreCase);

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Haptic Drive ASIO selected recording detail", content, StringComparison.Ordinal);
        Assert.Contains("Exported: 2026-06-20T06:07:08.0000000+00:00", content, StringComparison.Ordinal);
        Assert.Contains(@"Recording path: C:\Recordings\session-one.hdrec", content, StringComparison.Ordinal);
        Assert.Contains("Packet histogram: Motion#0: 2.", content, StringComparison.Ordinal);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"selected-recording-export-test-{Guid.NewGuid():N}");
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
