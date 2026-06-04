namespace HapticDrive.Input.Abstractions.Devices;

public sealed record InputControlInfo(
    string ControlId,
    string DisplayName,
    InputControlKind Kind,
    int? Index = null,
    string? Metadata = null);
