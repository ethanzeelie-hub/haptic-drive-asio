using System.IO;
using System.Text;

namespace HapticDrive.Asio.App;

internal sealed record SelectedRecordingDetailExportInputs(
    DateTimeOffset GeneratedAtUtc,
    string RecordingPath,
    string DetailText);

internal sealed class SelectedRecordingDetailExporter
{
    public string ExportText(SelectedRecordingDetailExportInputs inputs, string directory)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Export directory is required.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(inputs.RecordingPath))
        {
            throw new ArgumentException("Recording path is required.", nameof(inputs));
        }

        if (string.IsNullOrWhiteSpace(inputs.DetailText))
        {
            throw new ArgumentException("Recording detail text is required.", nameof(inputs));
        }

        var exportDirectory = Path.Combine(directory, "recording-inspections");
        Directory.CreateDirectory(exportDirectory);

        var safeFileStem = SanitizeFileName(Path.GetFileNameWithoutExtension(inputs.RecordingPath));
        if (string.IsNullOrWhiteSpace(safeFileStem))
        {
            safeFileStem = "recording";
        }

        var fileStem = $"selected-recording-detail-{inputs.GeneratedAtUtc:yyyyMMdd-HHmmss}-{safeFileStem}";
        var finalPath = Path.Combine(exportDirectory, $"{fileStem}.txt");
        var tempPath = Path.Combine(exportDirectory, $"{fileStem}.tmp");

        var content = BuildExportText(inputs);
        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(tempPath, finalPath);
        return finalPath;
    }

    private static string BuildExportText(SelectedRecordingDetailExportInputs inputs)
    {
        return NormalizeLineEndings(
            $"Haptic Drive ASIO selected recording detail{Environment.NewLine}{Environment.NewLine}"
            + $"Exported: {inputs.GeneratedAtUtc:O}{Environment.NewLine}"
            + $"Recording path: {inputs.RecordingPath}{Environment.NewLine}{Environment.NewLine}"
            + inputs.DetailText);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new char[fileName.Length];
        for (var index = 0; index < fileName.Length; index++)
        {
            sanitized[index] = invalid.Contains(fileName[index]) ? '-' : fileName[index];
        }

        return new string(sanitized).Trim().TrimEnd('.');
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
