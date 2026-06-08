namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed record PHprDetectedSoftwareProcess(
    string ProcessName,
    int? ProcessId = null,
    string? MainWindowTitle = null);
