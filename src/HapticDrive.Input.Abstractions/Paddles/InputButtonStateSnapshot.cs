namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record InputButtonStateSnapshot(
    InputListenerStatus Status,
    IReadOnlyDictionary<int, InputButtonState> Buttons,
    string? ErrorMessage = null)
{
    public static InputButtonStateSnapshot Stopped { get; } = new(
        InputListenerStatus.Stopped,
        new Dictionary<int, InputButtonState>());
}
