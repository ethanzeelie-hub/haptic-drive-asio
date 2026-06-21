using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Safety;

public sealed class AudioSafetyProcessor
{
    public AudioSafetyProcessorSnapshot Process(
        AudioSampleBuffer source,
        AudioSampleBuffer destination,
        AudioSafetyProcessorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        AudioSampleBuffer.EnsureSameFormat(source.Format, destination.Format);

        var effectiveOptions = options ?? AudioSafetyProcessorOptions.Default;
        var outputGain = SanitizeOutputGain(effectiveOptions.OutputGain);
        var outputCeiling = SanitizeCeiling(effectiveOptions.OutputGainCeiling);
        var limiterEnabled = effectiveOptions.LimiterEnabled;

        if (effectiveOptions.EmergencyMute)
        {
            destination.Clear();
            return new AudioSafetyProcessorSnapshot(
                EmergencyMute: true,
                limiterEnabled,
                outputGain,
                outputCeiling,
                InputPeakLevel: CalculatePeak(source),
                OutputPeakLevel: 0f,
                SanitizedSampleCount: 0,
                LimitedSampleCount: 0,
                ClippedSampleCount: 0);
        }

        var sanitizedSampleCount = 0;
        var inputPeakLevel = 0f;
        var preLimitPeakLevel = 0f;

        for (var i = 0; i < source.SampleCount; i++)
        {
            var inputSample = source.Samples[i];
            if (!float.IsFinite(inputSample))
            {
                inputSample = 0f;
                sanitizedSampleCount++;
            }

            inputPeakLevel = Math.Max(inputPeakLevel, Math.Abs(inputSample));

            var gainedSample = (double)inputSample * outputGain;
            var finiteGainedSample = ToFiniteFloat(gainedSample);
            if (!float.IsFinite(finiteGainedSample))
            {
                finiteGainedSample = 0f;
                sanitizedSampleCount++;
            }

            destination.Samples[i] = finiteGainedSample;
            preLimitPeakLevel = Math.Max(preLimitPeakLevel, Math.Abs(finiteGainedSample));
        }

        var limiterGain = limiterEnabled && preLimitPeakLevel > outputCeiling
            ? outputCeiling / preLimitPeakLevel
            : 1f;
        var limitedSampleCount = 0;
        var clippedSampleCount = 0;
        var outputPeakLevel = 0f;

        for (var i = 0; i < destination.SampleCount; i++)
        {
            var beforeLimit = destination.Samples[i];
            if (limiterGain < 1f && Math.Abs(beforeLimit) > outputCeiling)
            {
                limitedSampleCount++;
            }

            var sample = beforeLimit * limiterGain;
            if (sample > outputCeiling)
            {
                sample = outputCeiling;
                clippedSampleCount++;
            }
            else if (sample < -outputCeiling)
            {
                sample = -outputCeiling;
                clippedSampleCount++;
            }

            destination.Samples[i] = sample;
            outputPeakLevel = Math.Max(outputPeakLevel, Math.Abs(sample));
        }

        return new AudioSafetyProcessorSnapshot(
            EmergencyMute: false,
            limiterEnabled,
            outputGain,
            outputCeiling,
            inputPeakLevel,
            outputPeakLevel,
            sanitizedSampleCount,
            limitedSampleCount,
            clippedSampleCount);
    }

    private static float CalculatePeak(AudioSampleBuffer buffer)
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

    private static float SanitizeOutputGain(float outputGain)
    {
        return float.IsFinite(outputGain) ? outputGain : 0f;
    }

    private static float SanitizeCeiling(float outputCeiling)
    {
        return float.IsFinite(outputCeiling) && outputCeiling > 0f
            ? outputCeiling
            : AudioSafetyProcessorOptions.DefaultOutputGainCeiling;
    }

    private static float ToFiniteFloat(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0f;
        }

        if (value > float.MaxValue)
        {
            return float.MaxValue;
        }

        if (value < -float.MaxValue)
        {
            return -float.MaxValue;
        }

        return (float)value;
    }
}
