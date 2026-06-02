namespace HapticDrive.Asio.Core.Audio;

public sealed class AudioSampleBuffer
{
    private AudioSampleBuffer(AudioSampleFormat format, float[] samples)
    {
        Format = format;
        Samples = samples;
    }

    public AudioSampleFormat Format { get; }

    public float[] Samples { get; }

    public int SampleRate => Format.SampleRate;

    public int ChannelCount => Format.ChannelCount;

    public int FrameCount => Format.FrameCount;

    public int SampleCount => Format.SampleCount;

    public float this[int frame, int channel]
    {
        get => Samples[GetSampleIndex(frame, channel)];
        set => Samples[GetSampleIndex(frame, channel)] = value;
    }

    public static AudioSampleBuffer Allocate(AudioSampleFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return new AudioSampleBuffer(format, new float[format.SampleCount]);
    }

    public static AudioSampleBuffer Allocate(AudioOutputConfiguration configuration)
    {
        return Allocate(AudioSampleFormat.FromConfiguration(configuration));
    }

    public static AudioSampleBuffer FromInterleaved(AudioSampleFormat format, ReadOnlySpan<float> samples)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (samples.Length != format.SampleCount)
        {
            throw new ArgumentException(
                $"Expected {format.SampleCount} interleaved samples, but received {samples.Length}.",
                nameof(samples));
        }

        return new AudioSampleBuffer(format, samples.ToArray());
    }

    public static AudioSampleBuffer WrapInterleaved(AudioSampleFormat format, float[] samples)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length != format.SampleCount)
        {
            throw new ArgumentException(
                $"Expected {format.SampleCount} interleaved samples, but received {samples.Length}.",
                nameof(samples));
        }

        return new AudioSampleBuffer(format, samples);
    }

    public Span<float> AsSpan()
    {
        return Samples.AsSpan();
    }

    public ReadOnlySpan<float> AsReadOnlySpan()
    {
        return Samples.AsSpan();
    }

    public void Clear()
    {
        Array.Clear(Samples);
    }

    public void CopyFrom(AudioSampleBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureSameFormat(source.Format, Format);
        source.Samples.AsSpan().CopyTo(Samples);
    }

    public int GetSampleIndex(int frame, int channel)
    {
        if ((uint)frame >= (uint)FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        if ((uint)channel >= (uint)ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        return checked((frame * ChannelCount) + channel);
    }

    public static void EnsureSameFormat(AudioSampleFormat expected, AudioSampleFormat actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected != actual)
        {
            throw new ArgumentException(
                $"Audio buffer format mismatch. Expected {expected.SampleRate} Hz, {expected.ChannelCount} channel(s), {expected.FrameCount} frame(s); received {actual.SampleRate} Hz, {actual.ChannelCount} channel(s), {actual.FrameCount} frame(s).");
        }
    }
}
