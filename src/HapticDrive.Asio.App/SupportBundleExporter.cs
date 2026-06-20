using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace HapticDrive.Asio.App;

internal sealed record SupportBundleExportInputs(
    DateTimeOffset GeneratedAtUtc,
    string SelectedGameId,
    string SelectedGameDisplayName,
    DiagnosticsStatusPresentation DiagnosticsPresentation,
    string? SelectedRecordingFileName = null,
    string? SelectedRecordingDetailText = null);

internal sealed class SupportBundleExporter
{
    private const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ExportZip(SupportBundleExportInputs inputs, string directory)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Export directory is required.", nameof(directory));
        }

        var supportBundlesDirectory = Path.Combine(directory, "support-bundles");
        Directory.CreateDirectory(supportBundlesDirectory);

        var fileStem = $"support-bundle-{inputs.GeneratedAtUtc:yyyyMMdd-HHmmss}";
        var finalPath = Path.Combine(supportBundlesDirectory, $"{fileStem}.zip");
        var tempPath = Path.Combine(supportBundlesDirectory, $"{fileStem}.tmp");

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                WriteStringEntry(archive, "README.txt", BuildReadme());
                WriteStringEntry(archive, "diagnostics-report.txt", NormalizeLineEndings(inputs.DiagnosticsPresentation.ClipboardReportText));
                WriteStringEntry(archive, "diagnostics-summary.json", BuildDiagnosticsSummaryJson(inputs));
                if (!string.IsNullOrWhiteSpace(inputs.SelectedRecordingDetailText))
                {
                    WriteStringEntry(
                        archive,
                        "selected-recording-detail.txt",
                        NormalizeLineEndings(inputs.SelectedRecordingDetailText));
                }

                WriteStringEntry(archive, "manifest.json", BuildManifestJson(inputs));
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static string BuildReadme()
    {
        return NormalizeLineEndings(
            """
            Haptic Drive ASIO support bundle

            Private local export. This bundle contains sanitized diagnostics text plus optional selected-recording detail text only.
            No hardware output is triggered by export.
            Do not commit raw captures, serial numbers, or private device paths.
            """);
    }

    private static string BuildDiagnosticsSummaryJson(SupportBundleExportInputs inputs)
    {
        var payload = new
        {
            GeneratedAtUtc = inputs.GeneratedAtUtc,
            inputs.SelectedRecordingFileName,
            inputs.DiagnosticsPresentation.RoadRecorderStatusText,
            inputs.DiagnosticsPresentation.SummaryText,
            Items = inputs.DiagnosticsPresentation.Items
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildManifestJson(SupportBundleExportInputs inputs)
    {
        var payload = new
        {
            FormatVersion = CurrentFormatVersion,
            Application = "Haptic Drive ASIO",
            ApplicationVersion = ResolveApplicationVersion(),
            GeneratedAtUtc = inputs.GeneratedAtUtc,
            inputs.SelectedGameId,
            inputs.SelectedGameDisplayName,
            ContainsSelectedRecordingDetail = !string.IsNullOrWhiteSpace(inputs.SelectedRecordingDetailText),
            ContainsRawCaptures = false,
            ContainsPrivateDevicePaths = false,
            Files = BuildManifestFiles(inputs)
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static IReadOnlyList<string> BuildManifestFiles(SupportBundleExportInputs inputs)
    {
        var files = new List<string>
        {
            "README.txt",
            "diagnostics-report.txt",
            "diagnostics-summary.json"
        };

        if (!string.IsNullOrWhiteSpace(inputs.SelectedRecordingDetailText))
        {
            files.Add("selected-recording-detail.txt");
        }

        files.Add("manifest.json");
        return files;
    }

    private static string ResolveApplicationVersion()
    {
        var assembly = typeof(SupportBundleExporter).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static void WriteStringEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
