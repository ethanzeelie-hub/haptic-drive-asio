using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.TestBench;

public sealed class SilenceTestSignalGenerator : IAudioTestSignalGenerator
{
    public SilenceTestSignalGenerator(AudioTestSignalDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AudioTestSignalDefinition Definition { get; }

    public void Reset()
    {
    }

    public void Generate(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();
    }
}

public sealed class SineToneTestSignalGenerator : IAudioTestSignalGenerator
{
    private const double TwoPi = Math.PI * 2.0;
    private long _frameCursor;

    public SineToneTestSignalGenerator(AudioTestSignalDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AudioTestSignalDefinition Definition { get; }

    public void Reset()
    {
        _frameCursor = 0;
    }

    public void Generate(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var timeSeconds = (double)_frameCursor / destination.SampleRate;
            var sample = (float)(Definition.Amplitude * Math.Sin(TwoPi * Definition.FrequencyHz * timeSeconds));
            AudioTestSignalBufferWriter.WriteMonoFrame(destination, frame, sample);
            _frameCursor++;
        }
    }
}

public sealed class FrequencySweepTestSignalGenerator : IAudioTestSignalGenerator
{
    private const double TwoPi = Math.PI * 2.0;
    private long _frameCursor;

    public FrequencySweepTestSignalGenerator(AudioTestSignalDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AudioTestSignalDefinition Definition { get; }

    public void Reset()
    {
        _frameCursor = 0;
    }

    public void Generate(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var sweepFrameCount = Math.Max(1L, (long)Math.Round(Definition.SweepDurationSeconds * destination.SampleRate));
        var sweepDurationSeconds = (double)sweepFrameCount / destination.SampleRate;
        var frequencySlope = (Definition.SweepEndFrequencyHz - Definition.SweepStartFrequencyHz) / sweepDurationSeconds;

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var localFrame = _frameCursor % sweepFrameCount;
            var timeSeconds = (double)localFrame / destination.SampleRate;
            var phaseCycles = (Definition.SweepStartFrequencyHz * timeSeconds)
                + (0.5 * frequencySlope * timeSeconds * timeSeconds);
            var sample = (float)(Definition.Amplitude * Math.Sin(TwoPi * phaseCycles));
            AudioTestSignalBufferWriter.WriteMonoFrame(destination, frame, sample);
            _frameCursor++;
        }
    }
}

public sealed class PulseTestSignalGenerator : IAudioTestSignalGenerator
{
    private long _frameCursor;

    public PulseTestSignalGenerator(AudioTestSignalDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AudioTestSignalDefinition Definition { get; }

    public void Reset()
    {
        _frameCursor = 0;
    }

    public void Generate(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var localFrame = _frameCursor % Definition.PulseIntervalFrames;
            var sample = localFrame < Definition.PulseWidthFrames ? Definition.Amplitude : 0f;
            AudioTestSignalBufferWriter.WriteMonoFrame(destination, frame, sample);
            _frameCursor++;
        }
    }
}

public sealed class ConstantTestSignalGenerator : IAudioTestSignalGenerator
{
    public ConstantTestSignalGenerator(AudioTestSignalDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AudioTestSignalDefinition Definition { get; }

    public void Reset()
    {
    }

    public void Generate(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            AudioTestSignalBufferWriter.WriteMonoFrame(destination, frame, Definition.ConstantValue);
        }
    }
}

internal static class AudioTestSignalBufferWriter
{
    public static void WriteMonoFrame(AudioSampleBuffer destination, int frame, float sample)
    {
        for (var channel = 0; channel < destination.ChannelCount; channel++)
        {
            destination[frame, channel] = sample;
        }
    }
}
