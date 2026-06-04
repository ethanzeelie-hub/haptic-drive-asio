namespace HapticDrive.Input.Abstractions.Devices;

public sealed record InputDeviceInfo
{
    public string DeviceId { get; init; } = "unknown";

    public string DisplayName { get; init; } = "Unknown input device";

    public string? Manufacturer { get; init; }

    public string? ProductName { get; init; }

    public ushort? VendorId { get; init; }

    public ushort? ProductId { get; init; }

    public string? InstanceId { get; init; }

    public string? DevicePath { get; init; }

    public string? DeviceClass { get; init; }

    public InputDeviceKind Kind { get; init; } = InputDeviceKind.Unknown;

    public InputDiscoveryMethod DiscoveryMethod { get; init; } = InputDiscoveryMethod.Unknown;

    public IReadOnlyList<InputControlInfo> Controls { get; init; } = [];

    public int? ButtonCount { get; init; }

    public int? AxisCount { get; init; }

    public int? NativeDeviceIndex { get; init; }

    public ushort? HidUsagePage { get; init; }

    public ushort? HidUsage { get; init; }

    public int? InputReportByteLength { get; init; }

    public bool ReadOnlyDiscoverySucceeded { get; init; } = true;

    public string? ErrorMessage { get; init; }

    public DateTimeOffset DiscoveredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool LooksLikeSimagic { get; init; }

    public bool LooksLikeAlphaOrWheelbase { get; init; }

    public bool LooksLikeGtNeoOrWheelInput { get; init; }

    public bool LooksLikeP700Pedals { get; init; }

    public InputDeviceCandidateKind CandidateKind { get; init; } = InputDeviceCandidateKind.Unknown;

    public int CandidateScore { get; init; }

    public string CandidateReason { get; init; } = "No Simagic-specific signals found.";
}
