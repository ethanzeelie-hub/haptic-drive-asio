using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record WheelPaddleInputSnapshot(
    InputListenerStatus Status,
    InputDeviceSelection? SelectedDevice,
    WheelPaddleMapping Mapping,
    InputButtonState LeftPaddleState,
    InputButtonState RightPaddleState,
    int? LastChangedButtonId,
    InputButtonState LastChangedButtonState,
    WheelPaddleInputEvent? LastPaddleEvent,
    long PaddlePressCount,
    string? LastErrorMessage,
    DateTimeOffset StatusChangedAtUtc)
{
    public static WheelPaddleInputSnapshot NotConfigured { get; } = new(
        InputListenerStatus.NotConfigured,
        null,
        WheelPaddleMapping.Default,
        InputButtonState.Released,
        InputButtonState.Released,
        null,
        InputButtonState.Unknown,
        null,
        0,
        null,
        DateTimeOffset.UtcNow);

    public PaddleSide LastPaddleSide => LastPaddleEvent?.PaddleSide ?? PaddleSide.Unknown;

    public DateTimeOffset? LastPaddleEventUtc => LastPaddleEvent?.TimestampUtc;

    public string SafetyNote => "Stage 2E diagnostics only; mapped paddle presses do not trigger ShiftIntent routing or haptic output.";
}
