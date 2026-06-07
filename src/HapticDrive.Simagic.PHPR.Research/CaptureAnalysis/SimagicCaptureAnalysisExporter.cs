using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research.Capture;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed class SimagicCaptureAnalysisExporter
{
    public async ValueTask<string> ExportJsonAsync(
        SimagicCaptureAnalysisReport report,
        string outputDirectory,
        string fileName = "simagic-capture-analysis-sanitized.json",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, fileName);
        var json = JsonSerializer.Serialize(report, SimagicCaptureJson.Options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }
}
