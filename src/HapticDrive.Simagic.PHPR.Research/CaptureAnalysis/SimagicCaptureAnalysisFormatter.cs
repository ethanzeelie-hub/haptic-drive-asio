using System.Text;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public static class SimagicCaptureAnalysisFormatter
{
    public const string SafetyBanner = """
        STAGE 2I CAPTURE ANALYSIS SAFETY
        This tool reads local captures or sanitized Wireshark exports and writes sanitized summaries only.
        It performs no USB writes, no output reports, no feature reports, no vibration commands,
        no P-HPR commands, no protocol hypotheses, and no SimPro Manager / SimHub control.
        """;

    public static string FormatReport(SimagicCaptureAnalysisReport report, string? exportPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine("Sanitized capture analysis generated.");
        builder.AppendLine($"Sources: {report.SourceFileCount:N0}");
        builder.AppendLine($"Payload observations: {report.PayloadObservationCount:N0}");
        builder.AppendLine($"Unique payloads: {report.UniquePayloadCount:N0}");
        builder.AppendLine($"PCAP summaries: {report.PcapSummaries.Count:N0}");
        builder.AppendLine($"Warnings: {report.Warnings.Count:N0}");
        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            builder.AppendLine($"Sanitized analysis: {exportPath}");
        }

        builder.AppendLine("Exports contain fingerprints, short previews, counts, and byte-diff observations only.");
        builder.AppendLine("Stage 2J documents protocol hypotheses separately.");
        return builder.ToString();
    }

    public static string FormatDiff(SimagicCaptureAnalysisReport report, string? exportPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine("Sanitized capture payload diff generated.");
        builder.AppendLine($"Diff observations: {report.DiffObservations.Count:N0}");
        foreach (var diff in report.DiffObservations.Take(8))
        {
            builder.AppendLine(
                $"- {diff.ChangedByteCount:N0} changed byte(s), length {diff.PayloadLength:N0}, left {diff.LeftFingerprint}, right {diff.RightFingerprint}");
        }

        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            builder.AppendLine($"Sanitized diff analysis: {exportPath}");
        }

        builder.AppendLine("Diffs are byte observations only, not protocol field claims.");
        return builder.ToString();
    }
}
