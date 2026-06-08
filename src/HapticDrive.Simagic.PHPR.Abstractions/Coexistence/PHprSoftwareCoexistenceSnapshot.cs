using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed record PHprSoftwareCoexistenceSnapshot(
    PHprSoftwareConflictStatus Status,
    bool SimProRunning,
    bool SimHubRunning,
    IReadOnlyList<PHprDetectedSoftwareProcess> SimProProcesses,
    IReadOnlyList<PHprDetectedSoftwareProcess> SimHubProcesses,
    DateTimeOffset LastScanAtUtc,
    bool IsSupported,
    string Message,
    string? ErrorMessage = null)
{
    public static PHprSoftwareCoexistenceSnapshot NotScanned { get; } = new(
        PHprSoftwareConflictStatus.Unknown,
        SimProRunning: false,
        SimHubRunning: false,
        [],
        [],
        DateTimeOffset.MinValue,
        IsSupported: true,
        "SimPro/SimHub coexistence detection has not run yet.");
}
