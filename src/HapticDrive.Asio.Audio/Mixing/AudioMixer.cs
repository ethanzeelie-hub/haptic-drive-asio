using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Mixing;

public sealed class AudioMixer
{
    public AudioMixerSnapshot Mix(
        IReadOnlyList<AudioMixerInput>? inputs,
        AudioSampleBuffer destination,
        AudioMixerSettings? settings = null)
    {
        if (inputs is null)
        {
            return Mix(ReadOnlySpan<AudioMixerInput>.Empty, destination, settings);
        }

        if (inputs is AudioMixerInput[] array)
        {
            return Mix(array.AsSpan(0, inputs.Count), destination, settings);
        }

        if (inputs is ArraySegment<AudioMixerInput> segment)
        {
            return Mix(segment.AsSpan(), destination, settings);
        }

        var copiedInputs = new AudioMixerInput[inputs.Count];
        for (var i = 0; i < inputs.Count; i++)
        {
            copiedInputs[i] = inputs[i];
        }

        return Mix(copiedInputs.AsSpan(), destination, settings);
    }

    public AudioMixerSnapshot Mix(
        ReadOnlySpan<AudioMixerInput> inputs,
        AudioSampleBuffer destination,
        AudioMixerSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var effectiveSettings = settings ?? AudioMixerSettings.Default;

        destination.Clear();

        var inputSourceCount = inputs.Length;
        var masterGain = SanitizeGain(effectiveSettings.MasterGain);

        if (effectiveSettings.IsMuted || effectiveSettings.EmergencyMute || inputs.Length == 0)
        {
            return new AudioMixerSnapshot(
                IsRunning: false,
                effectiveSettings.IsMuted,
                effectiveSettings.EmergencyMute,
                inputSourceCount,
                ActiveSourceCount: 0,
                masterGain,
                PeakLevel: 0f);
        }

        var activeSourceCount = 0;
        for (var i = 0; i < inputs.Length; i++)
        {
            var input = inputs[i];
            if (input.IsMuted)
            {
                continue;
            }

            AudioSampleBuffer.EnsureSameFormat(input.Buffer.Format, destination.Format);
            var sourceGain = SanitizeGain(input.Gain);
            if (sourceGain == 0f || masterGain == 0f)
            {
                continue;
            }

            activeSourceCount++;
            MixSource(input.Buffer, destination, sourceGain * masterGain);
        }

        return new AudioMixerSnapshot(
            IsRunning: activeSourceCount > 0,
            effectiveSettings.IsMuted,
            effectiveSettings.EmergencyMute,
            inputSourceCount,
            activeSourceCount,
            masterGain,
            CalculatePeak(destination));
    }

    private static void MixSource(AudioSampleBuffer source, AudioSampleBuffer destination, float gain)
    {
        var sourceSamples = source.Samples;
        var destinationSamples = destination.Samples;

        for (var i = 0; i < destinationSamples.Length; i++)
        {
            var sample = SanitizeSample(sourceSamples[i]);
            var mixed = destinationSamples[i] + ((double)sample * gain);
            destinationSamples[i] = ToFiniteFloat(mixed);
        }
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

    private static float SanitizeGain(float gain)
    {
        return float.IsFinite(gain) ? gain : 0f;
    }

    private static float SanitizeSample(float sample)
    {
        return float.IsFinite(sample) ? sample : 0f;
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
