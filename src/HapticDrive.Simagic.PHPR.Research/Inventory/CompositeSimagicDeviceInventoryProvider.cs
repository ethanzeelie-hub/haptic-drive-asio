using HapticDrive.Input.Windows;

namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed class CompositeSimagicDeviceInventoryProvider : ISimagicDeviceInventoryProvider
{
    private readonly IReadOnlyList<ISimagicDeviceInventorySource> _sources;
    private readonly SimagicDeviceCandidateClassifier _classifier;

    public CompositeSimagicDeviceInventoryProvider(
        IEnumerable<ISimagicDeviceInventorySource> sources,
        SimagicDeviceCandidateClassifier? classifier = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = sources.ToArray();
        _classifier = classifier ?? new SimagicDeviceCandidateClassifier();
    }

    public static CompositeSimagicDeviceInventoryProvider CreateDefault()
    {
        return new CompositeSimagicDeviceInventoryProvider(
            [
                new InputDiscoverySimagicDeviceInventorySource(new WindowsInputDeviceDiscovery()),
                new WindowsRegistrySimagicDeviceInventorySource(
                    @"SYSTEM\CurrentControlSet\Enum\HID",
                    SimagicDeviceInventoryMethod.WindowsRegistryHid,
                    "Windows HID registry"),
                new WindowsRegistrySimagicDeviceInventorySource(
                    @"SYSTEM\CurrentControlSet\Enum\USB",
                    SimagicDeviceInventoryMethod.WindowsRegistryUsb,
                    "Windows USB registry")
            ]);
    }

    public async ValueTask<SimagicDeviceInventorySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var discoveredAtUtc = DateTimeOffset.UtcNow;
        var methods = new List<SimagicDeviceInventoryMethod>();
        var errors = new List<SimagicDeviceInventoryError>();
        var items = new List<SimagicDeviceInventoryItem>();

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            methods.Add(source.Method);

            try
            {
                var result = await source.EnumerateAsync(discoveredAtUtc, cancellationToken);
                errors.AddRange(result.Errors);
                methods.AddRange(result.Items.Select(item => item.DiscoveryMethod));
                items.AddRange(result.Items.Select(_classifier.ScoreItem));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(new SimagicDeviceInventoryError(source.Method, ex.Message));
            }
        }

        return new SimagicDeviceInventorySnapshot
        {
            DiscoveredAtUtc = discoveredAtUtc,
            Items = items
                .OrderByDescending(item => item.CandidateScore)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Methods = methods.Distinct().ToArray(),
            Errors = errors.ToArray()
        };
    }
}
