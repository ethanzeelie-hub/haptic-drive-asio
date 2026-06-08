using System.Text;
using System.Text.Json;

namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed class PHprManualValidationResultExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ExportMarkdown(PHprManualValidationResult result, string directory)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Export directory is required.", nameof(directory));
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"phpr-validation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, FormatMarkdown(result), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string FormatMarkdown(PHprManualValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var evaluation = result.Evaluate();
        var builder = new StringBuilder();
        builder.AppendLine("# P-HPR Manual Validation Result");
        builder.AppendLine();
        builder.AppendLine("Private local result. Do not commit raw captures, serial numbers, private device paths, or unsanitized hardware data.");
        builder.AppendLine();
        builder.AppendLine($"Created UTC: {result.CreatedAtUtc:O}");
        builder.AppendLine($"Status: {(evaluation.CanMarkPass ? "Pass-ready" : evaluation.PassRequested ? "Pass blocked" : "Draft or non-pass")}");
        builder.AppendLine($"Issues: {(evaluation.Issues.Count == 0 ? "none" : string.Join("; ", evaluation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")))}");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(JsonSerializer.Serialize(result, JsonOptions));
        builder.AppendLine("```");
        return builder.ToString();
    }
}
