using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Effects;

internal static class HapticEffectMath
{
    public const double TwoPi = Math.PI * 2.0;

    public static void WriteMonoFrame(AudioSampleBuffer destination, int frame, float sample)
    {
        for (var channel = 0; channel < destination.ChannelCount; channel++)
        {
            destination[frame, channel] = sample;
        }
    }

    public static float CalculatePeak(AudioSampleBuffer buffer)
    {
        var peak = 0f;
        foreach (var sample in buffer.Samples)
        {
            if (!float.IsFinite(sample))
            {
                continue;
            }

            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak;
    }

    public static float Clamp(float value, float minimum, float maximum)
    {
        if (!float.IsFinite(value))
        {
            return minimum;
        }

        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return minimum;
        }

        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    public static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }
}
