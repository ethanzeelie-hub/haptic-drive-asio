using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.App;

internal sealed record Bst1EffectCatalogItem(
    string Key,
    string SourceEffectKey,
    string DisplayName,
    int DiagnosticsOrder,
    int RoutingOrder,
    int EffectsPageOrder);

internal static class Bst1EffectCatalog
{
    public static IReadOnlyList<Bst1EffectCatalogItem> Items { get; } = CreateItems();

    public static IReadOnlyList<string> DiagnosticsOrderKeys { get; } = BuildOrderedKeys(item => item.DiagnosticsOrder);
    public static IReadOnlyList<string> RoutingOrderKeys { get; } = BuildOrderedKeys(item => item.RoutingOrder);
    public static IReadOnlyList<string> EffectsPageOrderKeys { get; } = BuildOrderedKeys(item => item.EffectsPageOrder);

    public static Bst1EffectCatalogItem GetRequired(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? Items.FirstOrDefault(item => string.Equals(item.SourceEffectKey, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown BST-1 effect key '{key}'.");
    }

    public static string GetDisplayLabelOrFallback(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return Items.FirstOrDefault(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.SourceEffectKey, key, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName
            ?? key;
    }

    private static IReadOnlyList<string> BuildOrderedKeys(Func<Bst1EffectCatalogItem, int> orderSelector)
    {
        return Items
            .OrderBy(orderSelector)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key)
            .ToArray();
    }

    private static IReadOnlyList<Bst1EffectCatalogItem> CreateItems()
    {
        var metadata = new Dictionary<string, (string Alias, string DisplayName, int DiagnosticsOrder, int RoutingOrder, int EffectsPageOrder)>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine-rpm"] = ("engine", "engine", 0, 2, 0),
            ["gear-shift"] = ("gear", "gear", 1, 0, 1),
            ["kerb"] = ("kerb", "kerb", 2, 3, 2),
            ["impact"] = ("impact", "impact", 3, 4, 3),
            ["road-texture"] = ("road", "road", 4, 1, 4),
            ["slip-lock"] = ("slip", "slip", 5, 5, 5)
        };

        var items = BuiltInHapticEffectRegistry.Instance.All
            .Where(descriptor => metadata.ContainsKey(descriptor.Key))
            .Select(descriptor =>
            {
                var entry = metadata[descriptor.Key];
                return new Bst1EffectCatalogItem(
                    entry.Alias,
                    descriptor.Key,
                    entry.DisplayName,
                    entry.DiagnosticsOrder,
                    entry.RoutingOrder,
                    entry.EffectsPageOrder);
            })
            .OrderBy(item => item.DiagnosticsOrder)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();

        items.Add(new Bst1EffectCatalogItem(
            "lock",
            "slip-lock",
            "lock",
            DiagnosticsOrder: 6,
            RoutingOrder: 6,
            EffectsPageOrder: int.MaxValue));

        return items;
    }
}
