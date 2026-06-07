using System.Text;

namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public static class SimagicProtocolHypothesisFormatter
{
    public const string SafetyBanner = """
        STAGE 2J PROTOCOL HYPOTHESES SAFETY
        This tool exports sanitized hypothesis records only.
        It performs no USB writes, no output reports, no feature reports, no vibration commands,
        no P-HPR commands, no protocol encoder/decoder for live hardware, no output-device calls,
        and no SimPro Manager / SimHub control.
        """;

    public static string FormatSummary(SimagicProtocolHypothesisSet hypothesisSet, string? exportPath = null)
    {
        ArgumentNullException.ThrowIfNull(hypothesisSet);

        var builder = new StringBuilder();
        builder.AppendLine("Sanitized protocol hypotheses loaded.");
        builder.AppendLine($"Stage: {hypothesisSet.Stage}");
        builder.AppendLine($"Hypotheses: {hypothesisSet.Hypotheses.Count:N0}");
        builder.AppendLine($"Unknowns: {hypothesisSet.Unknowns.Count:N0}");
        builder.AppendLine($"Real write blockers: {hypothesisSet.RealWriteBlockers.Count:N0}");
        foreach (var hypothesis in hypothesisSet.Hypotheses)
        {
            builder.AppendLine(
                $"- {hypothesis.Id}: {hypothesis.Status}, {hypothesis.Confidence}, real writes {hypothesis.RealWriteStatus}");
        }

        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            builder.AppendLine($"Sanitized hypothesis export: {exportPath}");
        }

        builder.AppendLine("Nothing in these hypotheses authorises real USB writes.");
        return builder.ToString();
    }
}
