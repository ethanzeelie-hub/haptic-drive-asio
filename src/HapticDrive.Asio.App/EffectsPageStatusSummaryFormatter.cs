namespace HapticDrive.Asio.App;

internal sealed record EffectStatusSummaryItem(
    string Key,
    string SummaryText);

internal static class EffectsPageStatusSummaryFormatter
{
    public static string Format(
        IReadOnlyList<EffectStatusSummaryItem>? items,
        string fallback)
    {
        if (items is null || items.Count == 0)
        {
            return fallback;
        }

        var orderedItems = OrderItems(items);
        return string.Join(", ", orderedItems.Select(item => item.SummaryText));
    }

    private static IReadOnlyList<EffectStatusSummaryItem> OrderItems(IReadOnlyList<EffectStatusSummaryItem> items)
    {
        var map = items.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<EffectStatusSummaryItem>(items.Count);

        foreach (var key in Bst1EffectCatalog.EffectsPageOrderKeys)
        {
            if (map.TryGetValue(key, out var item))
            {
                ordered.Add(item);
            }
        }

        foreach (var item in items)
        {
            if (!ordered.Any(existing => string.Equals(existing.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }
}
