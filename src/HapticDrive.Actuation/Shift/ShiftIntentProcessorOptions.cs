using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public sealed record ShiftIntentProcessorOptions
{
    public static ShiftIntentProcessorOptions Default { get; } = new();

    public bool IsEnabled { get; init; } = true;

    public ShiftIntentMode Mode { get; init; } = ShiftIntentMode.InstantPaddleOnly;

    public ShiftIntentProcessorOptions Normalize()
    {
        return Enum.IsDefined(Mode)
            ? this
            : this with { Mode = ShiftIntentMode.InstantPaddleOnly };
    }
}
