using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Asio.App;

internal sealed record LocalGearTestReadiness(
    bool IsEnabled,
    bool IsReady,
    bool CanStartListener,
    string Message)
{
    public static LocalGearTestReadiness Evaluate(
        bool isEnabled,
        bool autoStartListener,
        WheelPaddleInputSnapshot paddleSnapshot,
        string? selectionBlocker,
        bool hasLeftMapping,
        bool hasRightMapping,
        bool phprDirectReady,
        bool bst1Enabled,
        bool bst1AsioReady)
    {
        if (!isEnabled)
        {
            return new LocalGearTestReadiness(
                false,
                false,
                selectionBlocker is null,
                "Local gear test disabled.");
        }

        if (selectionBlocker is not null)
        {
            return new LocalGearTestReadiness(
                true,
                false,
                false,
                $"Blocked: {selectionBlocker}");
        }

        if (!hasLeftMapping || !hasRightMapping)
        {
            return new LocalGearTestReadiness(
                true,
                false,
                true,
                "Blocked: map both left and right paddles.");
        }

        if (paddleSnapshot.Status is not InputListenerStatus.Listening)
        {
            return new LocalGearTestReadiness(
                true,
                false,
                true,
                autoStartListener
                    ? "Blocked: paddle listener stopped; auto-start is available."
                    : "Blocked: paddle listener stopped.");
        }

        if (bst1Enabled && !bst1AsioReady)
        {
            return new LocalGearTestReadiness(
                true,
                false,
                false,
                "Blocked: BST-1 ASIO not ready.");
        }

        if (!phprDirectReady && !bst1Enabled)
        {
            return new LocalGearTestReadiness(
                true,
                false,
                false,
                "Blocked: P-HPR direct not ready and BST-1 gear pulse disabled.");
        }

        return new LocalGearTestReadiness(
            true,
            true,
            false,
            "Local gear test ready; Start Haptics and F1 telemetry are not required.");
    }
}
