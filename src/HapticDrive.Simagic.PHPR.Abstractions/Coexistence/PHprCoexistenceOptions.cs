namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed record PHprCoexistenceOptions
{
    public static PHprCoexistenceOptions Default { get; } = new();

    public IReadOnlyList<string> SimProProcessNamePatterns { get; init; } =
    [
        "SimProManager",
        "SimPro Manager",
        "SimProManagerV3",
        "SimPro"
    ];

    public IReadOnlyList<string> SimHubProcessNamePatterns { get; init; } =
    [
        "SimHub",
        "SimHubWPF"
    ];

    public PHprCoexistenceOptions Normalize()
    {
        return this with
        {
            SimProProcessNamePatterns = NormalizePatterns(SimProProcessNamePatterns, Default.SimProProcessNamePatterns),
            SimHubProcessNamePatterns = NormalizePatterns(SimHubProcessNamePatterns, Default.SimHubProcessNamePatterns)
        };
    }

    private static IReadOnlyList<string> NormalizePatterns(
        IReadOnlyList<string>? patterns,
        IReadOnlyList<string> defaults)
    {
        var normalized = (patterns ?? defaults)
            .Select(pattern => pattern?.Trim())
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? defaults : normalized!;
    }
}
