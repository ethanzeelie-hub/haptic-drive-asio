using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.App;

internal sealed record SupportBundleExportInputs(
    DateTimeOffset GeneratedAtUtc,
    string SelectedGameId,
    string SelectedGameDisplayName,
    DiagnosticsStatusPresentation DiagnosticsPresentation,
    SupportBundleStructuredDiagnostics StructuredDiagnostics,
    DiagnosticRedactionMode RedactionMode,
    string? SelectedRecordingFileName = null,
    string? SelectedRecordingDetailText = null);

internal sealed class SupportBundleExporter
{
    private const int CurrentFormatVersion = 2;

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

        var redactor = new SupportBundleDiagnosticRedactor(inputs.RedactionMode);
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
                WriteStringEntry(archive, "README.txt", BuildReadme(inputs.RedactionMode));
                WriteStringEntry(archive, "diagnostics-report.txt", NormalizeLineEndings(redactor.RedactText(inputs.DiagnosticsPresentation.ClipboardReportText)));
                WriteStringEntry(archive, "diagnostics-summary.json", BuildDiagnosticsSummaryJson(inputs, redactor));
                WriteStringEntry(archive, "diagnostic-events.json", SupportBundleJson.SerializeEvents(inputs.StructuredDiagnostics.Events, redactor, JsonOptions));
                if (!string.IsNullOrWhiteSpace(inputs.SelectedRecordingDetailText))
                {
                    WriteStringEntry(
                        archive,
                        "selected-recording-detail.txt",
                        NormalizeLineEndings(redactor.RedactText(inputs.SelectedRecordingDetailText)));
                }

                WriteStringEntry(archive, "manifest.json", BuildManifestJson(inputs, redactor));
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

    private static string BuildReadme(DiagnosticRedactionMode redactionMode)
    {
        return NormalizeLineEndings(
            $"""
            Haptic Drive ASIO support bundle

            Private local export. This bundle contains structured diagnostics, sanitized diagnostics text, and optional selected-recording detail text only.
            No hardware output is triggered by export.
            Redaction mode: {redactionMode}.
            Safe mode excludes private IPs, serials, raw USB payloads, and full local paths.
            Extended mode still redacts raw USB payloads, serials, hostnames, and process IDs.
            Do not commit raw captures, serial numbers, private device paths, or raw USB data.
            """);
    }

    private static string BuildDiagnosticsSummaryJson(SupportBundleExportInputs inputs, IDiagnosticRedactor redactor)
    {
        var payload = new
        {
            GeneratedAtUtc = inputs.GeneratedAtUtc,
            RedactionMode = inputs.RedactionMode.ToString(),
            SelectedRecordingFileName = redactor.RedactText(inputs.SelectedRecordingFileName ?? string.Empty),
            RoadRecorderStatusText = redactor.RedactText(inputs.DiagnosticsPresentation.RoadRecorderStatusText),
            SummaryText = redactor.RedactText(inputs.DiagnosticsPresentation.SummaryText),
            Items = inputs.DiagnosticsPresentation.Items.Select(redactor.RedactText).ToArray(),
            CorrelationIds = inputs.StructuredDiagnostics.CorrelationIds
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildManifestJson(SupportBundleExportInputs inputs, IDiagnosticRedactor redactor)
    {
        var payload = new
        {
            FormatVersion = CurrentFormatVersion,
            Application = "Haptic Drive ASIO",
            ApplicationVersion = ResolveApplicationVersion(),
            GeneratedAtUtc = inputs.GeneratedAtUtc,
            RedactionMode = inputs.RedactionMode.ToString(),
            SelectedGameId = inputs.SelectedGameId,
            SelectedGameDisplayName = inputs.SelectedGameDisplayName,
            ContainsSelectedRecordingDetail = !string.IsNullOrWhiteSpace(inputs.SelectedRecordingDetailText),
            ContainsRawCaptures = false,
            ContainsPrivateDevicePaths = false,
            ContainsPrivateIpAddresses = inputs.RedactionMode == DiagnosticRedactionMode.Extended,
            ContainsSerialNumbers = false,
            ContainsRawUsbPayloads = false,
            CorrelationIds = inputs.StructuredDiagnostics.CorrelationIds,
            EventCount = inputs.StructuredDiagnostics.Events.Count,
            SelectedRecordingFileName = redactor.RedactText(inputs.SelectedRecordingFileName ?? string.Empty),
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
            , "diagnostic-events.json"
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
