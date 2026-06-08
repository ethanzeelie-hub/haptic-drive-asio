namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed record PHprSoftwareProcessSnapshot(
    IReadOnlyList<PHprDetectedSoftwareProcess> Processes,
    DateTimeOffset ScannedAtUtc,
    bool IsSupported,
    string? ErrorMessage = null)
{
    public static PHprSoftwareProcessSnapshot Unsupported(string message, DateTimeOffset? scannedAtUtc = null)
    {
        return new PHprSoftwareProcessSnapshot(
            [],
            scannedAtUtc ?? DateTimeOffset.UtcNow,
            IsSupported: false,
            string.IsNullOrWhiteSpace(message) ? "Process snapshot is unsupported on this platform." : message.Trim());
    }
}
