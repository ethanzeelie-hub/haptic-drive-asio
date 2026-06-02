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

    public static double AdvancePhase(double phase, double frequencyHz, int sampleRate)
    {
        if (sampleRate <= 0 || double.IsNaN(frequencyHz) || double.IsInfinity(frequencyHz) || frequencyHz <= 0.0)
        {
            return phase;
        }

        phase += TwoPi * frequencyHz / sampleRate;
        if (phase >= TwoPi)
        {
            phase %= TwoPi;
        }

        return phase;
    }

    public static float SpeedScale(float speedKph, float minimumSpeedKph, float fullSpeedKph)
    {
        if (!float.IsFinite(speedKph) || speedKph <= minimumSpeedKph)
        {
            return 0f;
        }

        if (!float.IsFinite(fullSpeedKph) || fullSpeedKph <= minimumSpeedKph)
        {
            return 1f;
        }

        return Clamp((speedKph - minimumSpeedKph) / (fullSpeedKph - minimumSpeedKph), 0f, 1f);
    }

    public static float SmoothingCoefficient(int sampleRate, TimeSpan smoothingTime)
    {
        if (sampleRate <= 0 || smoothingTime <= TimeSpan.Zero)
        {
            return 1f;
        }

        var coefficient = 1.0 - Math.Exp(-1.0 / (smoothingTime.TotalSeconds * sampleRate));
        return (float)Clamp(coefficient, 0.0, 1.0);
    }

    public static double DeterministicSignedUnitNoise(long index, int seed = 0)
    {
        unchecked
        {
            var value = (ulong)index + 0x9E3779B97F4A7C15UL + ((ulong)seed * 0xBF58476D1CE4E5B9UL);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return ((value >> 11) * (1.0 / (1UL << 53)) * 2.0) - 1.0;
        }
    }
}
