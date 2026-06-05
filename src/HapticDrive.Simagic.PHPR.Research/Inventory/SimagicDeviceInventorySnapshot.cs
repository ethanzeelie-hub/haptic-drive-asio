namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed record SimagicDeviceInventorySnapshot
{
    public DateTimeOffset DiscoveredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<SimagicDeviceInventoryItem> Items { get; init; } = [];

    public IReadOnlyList<SimagicDeviceInventoryMethod> Methods { get; init; } = [];

    public IReadOnlyList<SimagicDeviceInventoryError> Errors { get; init; } = [];

    public bool HasRun { get; init; } = true;

    public static SimagicDeviceInventorySnapshot NotRun { get; } = new()
    {
        DiscoveredAtUtc = DateTimeOffset.MinValue,
        HasRun = false
    };

    public int DeviceCount => Items.Count;

    public bool ReadOnlyDiscoverySucceeded =>
        HasRun
        && Errors.Count == 0
        && Items.All(item => item.ReadOnlyDiscoverySucceeded);

    public IReadOnlyList<SimagicDeviceInventoryItem> SpecificSimagicCandidates =>
        Items
            .Where(item => item.CandidateKind is SimagicDeviceCandidateKind.P700PedalController
                or SimagicDeviceCandidateKind.PHprModuleOrController
                or SimagicDeviceCandidateKind.AlphaEvoWheelbase
                or SimagicDeviceCandidateKind.GtNeoWheelInput
                or SimagicDeviceCandidateKind.SimagicUnknown)
            .OrderByDescending(item => item.CandidateScore)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<SimagicDeviceInventoryItem> P700Candidates =>
        Items
            .Where(item => item.CandidateKind == SimagicDeviceCandidateKind.P700PedalController)
            .OrderByDescending(item => item.CandidateScore)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<SimagicDeviceInventoryItem> PHprCandidates =>
        Items
            .Where(item => item.CandidateKind == SimagicDeviceCandidateKind.PHprModuleOrController)
            .OrderByDescending(item => item.CandidateScore)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<SimagicDeviceInventoryItem> GenericHidOrUsbCandidates =>
        Items
            .Where(item => item.CandidateKind is SimagicDeviceCandidateKind.GenericHid or SimagicDeviceCandidateKind.GenericUsbInput)
            .OrderByDescending(item => item.CandidateScore)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
