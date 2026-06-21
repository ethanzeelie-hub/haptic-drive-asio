using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Audio.Effects;

internal static class VehicleStateEffectGuards
{
    public static bool ShouldMuteForDrivingState(VehicleState vehicleState)
    {
        if (vehicleState.Session?.Value.GamePaused is > 0)
        {
            return true;
        }

        if (vehicleState.CarStatus?.Value.NetworkPaused is > 0)
        {
            return true;
        }

        if (vehicleState.Lap?.Value.DriverStatus == 0)
        {
            return true;
        }

        return vehicleState.Lap?.Value.ResultStatus is 0 or 1;
    }

    public static bool IsTelemetryFresh(
        VehicleState vehicleState,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateTelemetry(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    public static bool IsMotionFresh(
        VehicleState vehicleState,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateMotion(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    public static bool IsMotionExFresh(
        VehicleState vehicleState,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateMotionEx(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    public static bool IsLastEventFresh(
        VehicleState vehicleState,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateLastEvent(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    public static float SanitizeUnit(float value)
    {
        return HapticEffectMath.Clamp(value, 0f, 1f);
    }

    public static float SanitizeFiniteMagnitude(float value, float maximumMagnitude)
    {
        if (!float.IsFinite(value))
        {
            return 0f;
        }

        return HapticEffectMath.Clamp(Math.Abs(value), 0f, maximumMagnitude);
    }

    public static float CalculateWheelAverage<T>(
        VehicleWheelData<T> values,
        Func<T, float?> selector)
    {
        var sum = 0f;
        var count = 0;

        for (var index = 0; index < 4; index++)
        {
            var value = selector(values[index]);
            if (value is null || !float.IsFinite(value.Value))
            {
                continue;
            }

            sum += value.Value;
            count++;
        }

        return count == 0 ? 0f : sum / count;
    }

    public static float CalculateWheelMaximum<T>(
        VehicleWheelData<T> values,
        Func<T, float?> selector)
    {
        var maximum = 0f;

        for (var index = 0; index < 4; index++)
        {
            var value = selector(values[index]);
            if (value is null || !float.IsFinite(value.Value))
            {
                continue;
            }

            maximum = Math.Max(maximum, value.Value);
        }

        return maximum;
    }

    private static TelemetryFreshnessPolicy CreateFrameFreshnessPolicy(uint maximumFrameLag)
    {
        return new TelemetryFreshnessPolicy(
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            maximumFrameLag);
    }
}
