using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

internal static class HapticFrameEffectGuards
{
    public static bool ShouldMuteForDrivingState(HapticEffectInput input)
    {
        return input.Frame.Context.IsPaused || !input.Frame.Context.IsPlayerControlled || !input.Frame.Context.AllowsDrivingOutput;
    }

    public static bool IsTelemetryFresh(HapticEffectInput input)
    {
        return input.Freshness.Telemetry.IsFresh;
    }

    public static bool IsMotionFresh(HapticEffectInput input)
    {
        return input.Freshness.Motion.IsFresh;
    }

    public static bool IsMotionExFresh(HapticEffectInput input)
    {
        return input.Freshness.MotionEx.IsFresh;
    }

    public static bool IsLastEventFresh(HapticEffectInput input)
    {
        return input.Freshness.Event.IsFresh;
    }

    public static bool IsCarStatusFresh(HapticEffectInput input)
    {
        return input.Freshness.CarStatus.IsFresh;
    }
}
