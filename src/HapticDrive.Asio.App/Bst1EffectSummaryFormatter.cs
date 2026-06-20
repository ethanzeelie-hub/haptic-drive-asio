namespace HapticDrive.Asio.App;

internal sealed record Bst1EffectSummaryItem(
    string Key,
    string DisplayName,
    bool IsEnabled,
    bool IsActive);

internal sealed record Bst1EffectSummarySnapshot(
    IReadOnlyList<Bst1EffectSummaryItem> Items,
    bool OverallSlipLockEnabled,
    float PeakLevel);

internal static class Bst1EffectSummaryFormatter
{
    public static string FormatDiagnosticsText(Bst1EffectSummarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var orderedItems = OrderItems(snapshot.Items, Bst1EffectCatalog.DiagnosticsOrderKeys);
        var entries = orderedItems.Select(item => $"{item.Key} {item.IsEnabled}");
        return $"enabled {string.Join(", ", entries)}; overall slip/lock {snapshot.OverallSlipLockEnabled}; peak {snapshot.PeakLevel:0.000}.";
    }

    public static string FormatRoutingText(
        IReadOnlyList<Bst1EffectSummaryItem>? items,
        string fallback)
    {
        if (items is null || items.Count == 0)
        {
            return fallback;
        }

        var orderedItems = OrderItems(items, Bst1EffectCatalog.RoutingOrderKeys);
        return $"Effects: {string.Join("; ", orderedItems.Select(FormatRoutingEntry))}.";
    }

    private static IReadOnlyList<Bst1EffectSummaryItem> OrderItems(
        IReadOnlyList<Bst1EffectSummaryItem> items,
        IReadOnlyList<string> orderedKeys)
    {
        var map = items.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<Bst1EffectSummaryItem>(items.Count);

        foreach (var key in orderedKeys)
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

    private static string FormatRoutingEntry(Bst1EffectSummaryItem item)
    {
        if (!item.IsEnabled)
        {
            return $"{item.DisplayName} disabled";
        }

        return item.IsActive
            ? $"{item.DisplayName} enabled/active"
            : $"{item.DisplayName} enabled/idle";
    }
}
