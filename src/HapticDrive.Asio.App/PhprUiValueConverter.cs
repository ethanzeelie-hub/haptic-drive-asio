using System.Globalization;

namespace HapticDrive.Asio.App;

internal static class PhprUiValueConverter
{
    public const double MinimumStrengthPercent = 0d;
    public const double MaximumStrengthPercent = 100d;
    public const double DefaultTestStrengthPercent = 10d;
    public const double MinimumFrequencyHz = 1d;
    public const double MaximumFrequencyHz = 50d;
    public const int DefaultTestFrequencyHz = 50;
    public const int MinimumDurationMs = 10;
    public const int MaximumDurationMs = 1_000;
    public const int DefaultTestDurationMs = 50;

    public static double PercentToRatio(double percent)
    {
        return Math.Clamp(percent, MinimumStrengthPercent, MaximumStrengthPercent) / 100d;
    }

    public static double RatioToPercent(double ratio)
    {
        return Math.Clamp(ratio, 0d, 1d) * 100d;
    }

    public static double ClampFrequencyHz(double frequencyHz)
    {
        return Math.Clamp(frequencyHz, MinimumFrequencyHz, MaximumFrequencyHz);
    }

    public static int ClampDurationMs(int durationMs)
    {
        return Math.Clamp(durationMs, MinimumDurationMs, MaximumDurationMs);
    }

    public static string FormatPercent(double ratio)
    {
        return RatioToPercent(ratio).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string FormatFrequency(double frequencyHz)
    {
        return ClampFrequencyHz(frequencyHz).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static bool TryParseStrengthPercent(
        string text,
        string label,
        out double ratio,
        out string message)
    {
        ratio = 0d;
        var valueText = NormalizePercentText(text);
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            || !double.IsFinite(percent)
            || percent < MinimumStrengthPercent
            || percent > MaximumStrengthPercent)
        {
            message = $"{label} strength must be a number from 0 to 100%.";
            return false;
        }

        ratio = PercentToRatio(percent);
        message = $"{label} strength ready.";
        return true;
    }

    public static bool TryParseFrequencyHz(
        string text,
        string label,
        out double frequencyHz,
        out string message)
    {
        frequencyHz = 0d;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < MinimumFrequencyHz
            || parsed > MaximumFrequencyHz)
        {
            message = $"{label} frequency must be a number from 1 to 50 Hz.";
            return false;
        }

        frequencyHz = parsed;
        message = $"{label} frequency ready.";
        return true;
    }

    public static bool TryParseDurationMs(
        string text,
        string label,
        out int durationMs,
        out string message)
    {
        durationMs = 0;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < MinimumDurationMs
            || parsed > MaximumDurationMs)
        {
            message = $"{label} duration must be between 10 and 1000 ms.";
            return false;
        }

        durationMs = parsed;
        message = $"{label} duration ready.";
        return true;
    }

    private static string NormalizePercentText(string text)
    {
        var trimmed = text.Trim();
        return trimmed.EndsWith("%", StringComparison.Ordinal)
            ? trimmed[..^1].Trim()
            : trimmed;
    }
}
