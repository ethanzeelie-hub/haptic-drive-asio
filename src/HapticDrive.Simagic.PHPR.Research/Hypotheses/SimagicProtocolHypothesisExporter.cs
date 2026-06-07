using System.Text;
using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research.Capture;

namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public sealed class SimagicProtocolHypothesisExporter
{
    public async ValueTask<string> ExportJsonAsync(
        SimagicProtocolHypothesisSet hypothesisSet,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hypothesisSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);
        var json = JsonSerializer.Serialize(hypothesisSet, SimagicCaptureJson.Options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }

    public async ValueTask<string> ExportMarkdownAsync(
        SimagicProtocolHypothesisSet hypothesisSet,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hypothesisSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);
        await File.WriteAllTextAsync(path, ToMarkdown(hypothesisSet), cancellationToken);
        return path;
    }

    public string ToMarkdown(SimagicProtocolHypothesisSet hypothesisSet)
    {
        ArgumentNullException.ThrowIfNull(hypothesisSet);

        var builder = new StringBuilder();
        builder.AppendLine("# Simagic Protocol Hypotheses Export");
        builder.AppendLine();
        builder.AppendLine($"Stage: {hypothesisSet.Stage}");
        builder.AppendLine();
        builder.AppendLine(hypothesisSet.Purpose);
        builder.AppendLine();
        builder.AppendLine("## Safety Boundary");
        foreach (var boundary in hypothesisSet.SafetyBoundary)
        {
            builder.AppendLine($"- {boundary}");
        }

        builder.AppendLine();
        builder.AppendLine("## Hypotheses");
        foreach (var hypothesis in hypothesisSet.Hypotheses)
        {
            builder.AppendLine();
            builder.AppendLine($"### {hypothesis.Id}: {hypothesis.Title}");
            builder.AppendLine();
            builder.AppendLine($"- Family: {hypothesis.ProtocolFamily}");
            builder.AppendLine($"- Source: {hypothesis.SoftwareSource}");
            builder.AppendLine($"- Status: {hypothesis.Status}");
            builder.AppendLine($"- Real write status: {hypothesis.RealWriteStatus}");
            builder.AppendLine($"- Confidence: {hypothesis.Confidence}");
            builder.AppendLine($"- Mock only: {hypothesis.MockOnly}");
            builder.AppendLine($"- Output command: {hypothesis.IsOutputCommand}");
            builder.AppendLine($"- Summary: {hypothesis.Summary}");
            builder.AppendLine($"- Safety: {hypothesis.NoWriteSafetyNote}");
        }

        builder.AppendLine();
        builder.AppendLine("## Real Write Blockers");
        foreach (var blocker in hypothesisSet.RealWriteBlockers)
        {
            builder.AppendLine($"- {blocker}");
        }

        return builder.ToString();
    }
}
