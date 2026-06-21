using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Audio.Effects;

internal static class HapticFrameEffectGuards
{
    public static bool ShouldMuteForDrivingState(HapticFrame frame)
    {
        return frame.Context.IsPaused || !frame.Context.IsPlayerControlled || !frame.Context.AllowsDrivingOutput;
    }

    public static bool IsTelemetryFresh(HapticFrame frame)
    {
        return TryGetFreshness(frame, HapticFrameSignalNames.Telemetry).IsFresh;
    }

    public static bool IsMotionFresh(HapticFrame frame)
    {
        return TryGetFreshness(frame, HapticFrameSignalNames.Motion).IsFresh;
    }

    public static bool IsMotionExFresh(HapticFrame frame)
    {
        return TryGetFreshness(frame, HapticFrameSignalNames.MotionEx).IsFresh;
    }

    public static bool IsLastEventFresh(HapticFrame frame)
    {
        return TryGetFreshness(frame, HapticFrameSignalNames.Event).IsFresh;
    }

    public static bool IsCarStatusFresh(HapticFrame frame)
    {
        return TryGetFreshness(frame, HapticFrameSignalNames.CarStatus).IsFresh;
    }

    private static VehicleSignalFreshness TryGetFreshness(HapticFrame frame, string key)
    {
        if (frame.Freshness.TryGetValue(key, out var freshness))
        {
            return freshness;
        }

        return new VehicleSignalFreshness(false, false, false, false, false, null, null);
    }
}
