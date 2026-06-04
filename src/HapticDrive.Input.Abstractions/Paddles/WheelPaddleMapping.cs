using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record WheelPaddleMapping
{
    public static TimeSpan DefaultDebounceDuration { get; } = TimeSpan.FromMilliseconds(20);

    public static WheelPaddleMapping Default { get; } = new();

    public string? SelectedDeviceId { get; init; }

    public InputDiscoveryMethod SelectedMethod { get; init; } = InputDiscoveryMethod.WindowsGameController;

    public int? LeftPaddleButtonId { get; init; }

    public int? RightPaddleButtonId { get; init; }

    public TimeSpan DebounceDuration { get; init; } = DefaultDebounceDuration;

    public WheelPaddleMapping Normalize()
    {
        return this with
        {
            SelectedDeviceId = string.IsNullOrWhiteSpace(SelectedDeviceId)
                ? null
                : SelectedDeviceId.Trim(),
            LeftPaddleButtonId = NormalizeButtonId(LeftPaddleButtonId),
            RightPaddleButtonId = NormalizeButtonId(RightPaddleButtonId),
            DebounceDuration = NormalizeDebounce(DebounceDuration)
        };
    }

    public PaddleSide ResolvePaddleSide(int buttonId)
    {
        if (buttonId <= 0)
        {
            return PaddleSide.Unknown;
        }

        if (LeftPaddleButtonId == buttonId)
        {
            return PaddleSide.Left;
        }

        return RightPaddleButtonId == buttonId
            ? PaddleSide.Right
            : PaddleSide.Unknown;
    }

    private static int? NormalizeButtonId(int? buttonId)
    {
        return buttonId is > 0 and <= 128 ? buttonId : null;
    }

    private static TimeSpan NormalizeDebounce(TimeSpan debounce)
    {
        if (debounce < TimeSpan.Zero)
        {
            return DefaultDebounceDuration;
        }

        return debounce > TimeSpan.FromMilliseconds(250)
            ? TimeSpan.FromMilliseconds(250)
            : debounce;
    }
}
