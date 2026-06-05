using System.Text;

namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static class SimagicCaptureToolFormatter
{
    public const string SafetyBanner = """
        STAGE 2H CAPTURE METADATA SAFETY
        This tool creates templates, validates metadata, and exports sanitized manifests only.
        It performs no USB capture analysis, no USB writes, no output reports, no feature reports,
        no vibration commands, no P-HPR commands, and no SimPro Manager / SimHub control.
        """;

    public static string FormatScenarios()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Stage 2H capture scenarios");
        builder.AppendLine();
        builder.AppendLine("| Scenario ID | Target | Software | Settings | Description |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var scenario in SimagicCaptureScenarios.RequiredScenarios)
        {
            builder.Append("| ");
            builder.Append(scenario.Id);
            builder.Append(" | ");
            builder.Append(scenario.RecommendedTarget);
            builder.Append(" | ");
            builder.Append(scenario.SoftwareUnderTest);
            builder.Append(" | ");
            builder.Append(FormatRequiredSettings(scenario));
            builder.Append(" | ");
            builder.Append(Escape(scenario.Description));
            builder.AppendLine(" |");
        }

        return builder.ToString();
    }

    public static string FormatValidation(SimagicCaptureValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.AppendLine(result.IsValid
            ? "Capture metadata validation passed."
            : "Capture metadata validation failed.");
        builder.AppendLine($"Errors: {result.ErrorCount:N0}");
        builder.AppendLine($"Warnings: {result.WarningCount:N0}");

        foreach (var message in result.Messages)
        {
            builder.AppendLine($"- {message.Severity}: {message.Field}: {message.Message}");
        }

        return builder.ToString();
    }

    public static string FormatManifestSummary(SimagicCaptureManifest manifest, string? exportPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var builder = new StringBuilder();
        builder.AppendLine("Sanitized capture manifest generated.");
        builder.AppendLine($"Metadata files: {manifest.SourceMetadataCount:N0}");
        builder.AppendLine($"Entries: {manifest.Entries.Count:N0}");
        builder.AppendLine($"Validation warnings: {manifest.Entries.Sum(entry => entry.Validation.WarningCount):N0}");
        builder.AppendLine($"Validation errors: {manifest.Entries.Sum(entry => entry.Validation.ErrorCount):N0}");
        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            builder.AppendLine($"Sanitized manifest: {exportPath}");
        }

        builder.AppendLine("Manifest entries contain sanitized metadata only, not raw .pcap/.pcapng bytes.");
        return builder.ToString();
    }

    private static string FormatRequiredSettings(SimagicCaptureScenario scenario)
    {
        var settings = new List<string>();
        if (scenario.RequiresStrength)
        {
            settings.Add("strength");
        }

        if (scenario.RequiresFrequency)
        {
            settings.Add("frequency");
        }

        if (scenario.RequiresDuration)
        {
            settings.Add("duration");
        }

        return settings.Count == 0 ? "none" : string.Join(", ", settings);
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
