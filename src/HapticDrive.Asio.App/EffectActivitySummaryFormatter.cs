using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal static class EffectActivitySummaryFormatter
{
    public static string Format(
        IReadOnlyList<HapticEffectActivityItem>? items,
        string separator,
        string fallback)
    {
        if (items is null || items.Count == 0)
        {
            return fallback;
        }

        return string.Join(
            separator,
            items.Select(item => $"{Bst1EffectCatalog.GetDisplayLabelOrFallback(item.Label)} {item.StatusText.ToLowerInvariant()}"));
    }
}
