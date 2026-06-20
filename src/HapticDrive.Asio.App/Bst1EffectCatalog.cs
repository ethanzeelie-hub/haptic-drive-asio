namespace HapticDrive.Asio.App;

internal sealed record Bst1EffectCatalogItem(
    string Key,
    string DisplayName,
    int DiagnosticsOrder,
    int RoutingOrder,
    int EffectsPageOrder);

internal static class Bst1EffectCatalog
{
    public static IReadOnlyList<Bst1EffectCatalogItem> Items { get; } =
    [
        new Bst1EffectCatalogItem("engine", "engine", DiagnosticsOrder: 0, RoutingOrder: 2, EffectsPageOrder: 0),
        new Bst1EffectCatalogItem("gear", "gear", DiagnosticsOrder: 1, RoutingOrder: 0, EffectsPageOrder: 1),
        new Bst1EffectCatalogItem("kerb", "kerb", DiagnosticsOrder: 2, RoutingOrder: 3, EffectsPageOrder: 2),
        new Bst1EffectCatalogItem("impact", "impact", DiagnosticsOrder: 3, RoutingOrder: 4, EffectsPageOrder: 3),
        new Bst1EffectCatalogItem("road", "road", DiagnosticsOrder: 4, RoutingOrder: 1, EffectsPageOrder: 4),
        new Bst1EffectCatalogItem("slip", "slip", DiagnosticsOrder: 5, RoutingOrder: 5, EffectsPageOrder: 5),
        new Bst1EffectCatalogItem("lock", "lock", DiagnosticsOrder: 6, RoutingOrder: 6, EffectsPageOrder: int.MaxValue)
    ];

    public static IReadOnlyList<string> DiagnosticsOrderKeys { get; } = BuildOrderedKeys(item => item.DiagnosticsOrder);
    public static IReadOnlyList<string> RoutingOrderKeys { get; } = BuildOrderedKeys(item => item.RoutingOrder);
    public static IReadOnlyList<string> EffectsPageOrderKeys { get; } = BuildOrderedKeys(item => item.EffectsPageOrder);

    public static Bst1EffectCatalogItem GetRequired(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown BST-1 effect key '{key}'.");
    }

    private static IReadOnlyList<string> BuildOrderedKeys(Func<Bst1EffectCatalogItem, int> orderSelector)
    {
        return Items
            .OrderBy(orderSelector)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key)
            .ToArray();
    }
}
