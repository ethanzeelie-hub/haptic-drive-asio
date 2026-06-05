namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed record SimagicDeviceInventoryExport
{
    public string Stage { get; init; } = "Stage 2G";

    public string Purpose { get; init; } = "Read-only Simagic P700 / P-HPR device inventory.";

    public string HardwareStatus { get; init; } = "Not evaluated.";

    public IReadOnlyList<string> SafetyStatements { get; init; } =
    [
        "Read-only inventory only.",
        "No output reports.",
        "No feature writes.",
        "No vibration commands.",
        "No P-HPR commands.",
        "No SimPro Manager or SimHub control."
    ];

    public SimagicDeviceInventorySnapshot Snapshot { get; init; } = SimagicDeviceInventorySnapshot.NotRun;

    public static SimagicDeviceInventoryExport FromSnapshot(SimagicDeviceInventorySnapshot snapshot)
    {
        var sanitized = SimagicDeviceInventorySanitizer.SanitizeSnapshot(snapshot);
        var specificCandidateObserved = sanitized.Items.Any(item =>
            item.CandidateKind is SimagicDeviceCandidateKind.P700PedalController
                or SimagicDeviceCandidateKind.PHprModuleOrController
                or SimagicDeviceCandidateKind.AlphaEvoWheelbase
                or SimagicDeviceCandidateKind.GtNeoWheelInput
                or SimagicDeviceCandidateKind.SimagicUnknown);

        return new SimagicDeviceInventoryExport
        {
            HardwareStatus = specificCandidateObserved
                ? "Sanitized local read-only candidate inventory collected. Treat as unvalidated until Ethan confirms against Device Manager / USBView."
                : "No Simagic-specific local inventory observed by this tool run. Real P700/P-HPR inventory is awaiting user-provided data.",
            Snapshot = sanitized
        };
    }
}
