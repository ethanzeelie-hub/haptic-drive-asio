namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class WindowsPhprDirectOutputCandidateProvider : IPHprDirectOutputCandidateProvider
{
    private readonly IReadOnlyList<IPHprDirectOutputCandidateProvider> _providers;

    public WindowsPhprDirectOutputCandidateProvider()
        : this(
            [
                new WindowsHidDeviceInterfacePhprDirectOutputCandidateProvider(),
                new WindowsRegistryHidPhprDirectOutputCandidateProvider(),
                new WindowsRawInputPhprDirectOutputCandidateProvider()
            ])
    {
    }

    public WindowsPhprDirectOutputCandidateProvider(IEnumerable<IPHprDirectOutputCandidateProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToArray();
    }

    public IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null)
    {
        var candidates = _providers
            .SelectMany(provider => provider.DiscoverCandidates(discoveredAtUtc))
            .GroupBy(candidate => DeduplicateKey(candidate), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.HasOpenableHidPath)
                .ThenByDescending(candidate => candidate.HasKnownOutputReportCapability)
                .ThenByDescending(candidate => candidate.HasOutputOrFeatureReportCapability)
                .ThenByDescending(candidate => candidate.HasF1EcFeatureReportId)
                .ThenByDescending(candidate => candidate.Confidence)
                .First())
            .OrderByDescending(candidate => candidate.HasOpenableHidPath)
            .ThenByDescending(candidate => candidate.HasKnownOutputReportCapability)
            .ThenByDescending(candidate => candidate.HasOutputOrFeatureReportCapability)
            .ThenByDescending(candidate => candidate.HasF1EcFeatureReportId)
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.IsRawInputOnly)
            .ThenBy(candidate => candidate.VendorProductText, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.SafeDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates;
    }

    private static string DeduplicateKey(PHprDirectOutputCandidate candidate)
    {
        if (candidate.HasOpenableHidPath)
        {
            return $"path:{candidate.DevicePath}";
        }

        return candidate.VendorId is null || candidate.ProductId is null
            ? candidate.CandidateId
            : $"metadata:{candidate.SourceMethod}:{candidate.VendorId:X4}:{candidate.ProductId:X4}:{candidate.HidUsagePage?.ToString("X4") ?? "none"}:{candidate.HidUsage?.ToString("X4") ?? "none"}:{candidate.InterfaceNumber ?? "none"}:{candidate.CollectionNumber ?? "none"}:{candidate.CandidateId}";
    }
}
