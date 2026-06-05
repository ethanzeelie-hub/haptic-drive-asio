using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static partial class SimagicCaptureFilenameBuilder
{
    public static string Build(SimagicCaptureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var scenarioId = metadata.ScenarioId ?? SimagicCaptureScenarioId.BrakeTestVibration;
        var scenario = SimagicCaptureScenarios.TryGet(scenarioId, out var knownScenario)
            ? knownScenario
            : SimagicCaptureScenarios.Get(SimagicCaptureScenarioId.BrakeTestVibration);
        return Build(
            metadata.CaptureStartedAtUtc ?? DateTimeOffset.UtcNow,
            metadata.Software.SoftwareUnderTest ?? scenario.SoftwareUnderTest,
            scenario.DeviceName,
            scenarioId,
            metadata.Device.TargetModule,
            metadata.Action.SettingBefore,
            metadata.Action.SettingAfter);
    }

    public static string Build(
        DateTimeOffset captureStartedAtUtc,
        string software,
        string device,
        SimagicCaptureScenarioId scenarioId,
        SimagicCaptureTargetModule targetModule,
        SimagicCaptureSettingSnapshot? settingBefore = null,
        SimagicCaptureSettingSnapshot? settingAfter = null)
    {
        var scenario = SimagicCaptureScenarios.Get(scenarioId);
        var parts = new[]
        {
            captureStartedAtUtc.UtcDateTime.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture),
            Slugify(software),
            Slugify(device),
            Slugify(scenario.Slug),
            Slugify(targetModule.ToString()),
            FormatSettings(settingBefore, settingAfter)
        };

        var stem = string.Join("_", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return $"{stem}.pcapng";
    }

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var safe = SimagicCaptureSanitizer.SanitizeText(value) ?? "unknown";
        safe = UnsafeFilenameCharactersRegex().Replace(safe, "-");
        safe = NonSlugCharactersRegex().Replace(safe.ToLowerInvariant(), "-");
        safe = RepeatedDashRegex().Replace(safe, "-").Trim('-');
        return string.IsNullOrWhiteSpace(safe) || safe == "redacted" ? "unknown" : safe;
    }

    private static string FormatSettings(
        SimagicCaptureSettingSnapshot? settingBefore,
        SimagicCaptureSettingSnapshot? settingAfter)
    {
        var parts = new List<string>();
        AddRange(parts, settingBefore?.FrequencyHz, settingAfter?.FrequencyHz, "hz");
        AddRange(parts, settingBefore?.StrengthPercent, settingAfter?.StrengthPercent, "pct");
        AddRange(parts, settingBefore?.DurationMs, settingAfter?.DurationMs, "ms");
        return string.Join("_", parts);
    }

    private static void AddRange(List<string> parts, double? before, double? after, string unit)
    {
        if (after is null)
        {
            return;
        }

        var afterText = FormatNumber(after.Value);
        if (before is not null && Math.Abs(before.Value - after.Value) > 0.0001d)
        {
            parts.Add($"{FormatNumber(before.Value)}-to-{afterText}{unit}");
            return;
        }

        parts.Add($"{afterText}{unit}");
    }

    private static string FormatNumber(double value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"[<>:""/\\|?*\p{C}]")]
    private static partial Regex UnsafeFilenameCharactersRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugCharactersRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex RepeatedDashRegex();
}
