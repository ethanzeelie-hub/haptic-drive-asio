namespace HapticDrive.Input.Abstractions.Shift;

public sealed record ShiftIntentSourceSnapshot(
    bool IsAvailable,
    string? SelectedDeviceId,
    long EventCount,
    PaddleSide LastPaddleSide = PaddleSide.Unknown,
    DateTimeOffset? LastEventUtc = null,
    string? LastSuppressedReason = null);
