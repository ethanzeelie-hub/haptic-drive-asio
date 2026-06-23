using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public abstract class BufferedHapticEffectRuntime : IHapticEffectRuntime
{
    private AudioSampleBuffer? _buffer;

    protected BufferedHapticEffectRuntime(string key, string displayName)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    public string Key { get; }

    public string DisplayName { get; }

    public void Reset()
    {
        ResetCore();
    }

    public void ApplySettings(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ApplySettingsCore(parameters);
    }

    public void Render(in HapticRenderFrame frame, Span<float> left, Span<float> right, int sampleRate, int frameCount)
    {
        if (frameCount <= 0)
        {
            return;
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (left.Length < frameCount)
        {
            throw new ArgumentException("Left channel span is shorter than the requested frame count.", nameof(left));
        }

        if (!right.IsEmpty && right.Length < frameCount)
        {
            throw new ArgumentException("Right channel span is shorter than the requested frame count.", nameof(right));
        }

        left = left[..frameCount];
        right = right.IsEmpty ? right : right[..frameCount];
        left.Clear();
        if (!right.IsEmpty)
        {
            right.Clear();
        }

        var channelCount = right.IsEmpty ? 1 : 2;
        EnsureBuffer(sampleRate, frameCount, channelCount);
        var buffer = _buffer!;
        RenderCore(frame, buffer);
        CopyToChannels(buffer, left, right);
    }

    protected abstract void ResetCore();

    protected abstract void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters);

    protected abstract void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer);

    private void EnsureBuffer(int sampleRate, int frameCount, int channelCount)
    {
        if (_buffer is not null
            && _buffer.SampleRate == sampleRate
            && _buffer.FrameCount == frameCount
            && _buffer.ChannelCount == channelCount)
        {
            return;
        }

        _buffer = AudioSampleBuffer.Allocate(new AudioSampleFormat(sampleRate, channelCount, frameCount));
    }

    private static void CopyToChannels(AudioSampleBuffer source, Span<float> left, Span<float> right)
    {
        if (source.ChannelCount == 1)
        {
            for (var frame = 0; frame < source.FrameCount; frame++)
            {
                var sample = source[frame, 0];
                left[frame] = sample;
                if (!right.IsEmpty)
                {
                    right[frame] = sample;
                }
            }

            return;
        }

        for (var frame = 0; frame < source.FrameCount; frame++)
        {
            left[frame] = source[frame, 0];
            if (!right.IsEmpty)
            {
                right[frame] = source[frame, 1];
            }
        }
    }
}
