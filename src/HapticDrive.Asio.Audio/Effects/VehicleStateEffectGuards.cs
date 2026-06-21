using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

internal static class VehicleStateEffectGuards
{
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

}
