namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed record SimagicDeviceInventoryItem
{
    public string DeviceId { get; init; } = "unknown";

    public string DisplayName { get; init; } = "Unknown device";

    public string? Manufacturer { get; init; }

    public string? ProductName { get; init; }

    public string? ServiceName { get; init; }

    public string? DriverProvider { get; init; }

    public string? DriverVersion { get; init; }

    public string? DeviceClass { get; init; }

    public string? ClassGuid { get; init; }

    public ushort? VendorId { get; init; }

    public ushort? ProductId { get; init; }

    public string? InterfaceNumber { get; init; }

    public string? CollectionNumber { get; init; }

    public ushort? HidUsagePage { get; init; }

    public ushort? HidUsage { get; init; }

    public int? InputReportByteLength { get; init; }

    public int? OutputReportByteLength { get; init; }

    public int? FeatureReportByteLength { get; init; }

    public IReadOnlyList<string> EndpointSummaries { get; init; } = [];

    public string? SafeInstanceId { get; init; }

    public string? SafeDevicePath { get; init; }

    public SimagicDeviceCandidateKind CandidateKind { get; init; } = SimagicDeviceCandidateKind.Unknown;

    public int CandidateScore { get; init; }

    public string CandidateReason { get; init; } = "No Simagic, HID, or USB input signals found.";

    public SimagicDeviceInventoryMethod DiscoveryMethod { get; init; } = SimagicDeviceInventoryMethod.Unknown;

    public bool ReadOnlyDiscoverySucceeded { get; init; } = true;

    public string? ErrorMessage { get; init; }

    public DateTimeOffset DiscoveredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? VendorProductText => VendorId is null || ProductId is null
        ? null
        : $"VID_{VendorId:X4}/PID_{ProductId:X4}";
}
