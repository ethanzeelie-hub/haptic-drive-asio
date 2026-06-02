namespace HapticDrive.Asio.Core.Audio;

public sealed record AudioSampleFormat
{
    public AudioSampleFormat(int sampleRate, int channelCount, int frameCount)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        }

        if (channelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be positive.");
        }

        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
        }

        SampleRate = sampleRate;
        ChannelCount = channelCount;
        FrameCount = frameCount;
        SampleCount = checked(channelCount * frameCount);
    }

    public int SampleRate { get; }

    public int ChannelCount { get; }

    public int FrameCount { get; }

    public int SampleCount { get; }

    public static AudioSampleFormat FromConfiguration(AudioOutputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AudioSampleFormat(
            configuration.SampleRate,
            configuration.ChannelCount,
            configuration.BufferSize);
    }
}
