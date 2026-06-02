using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioTestSignalGeneratorTests
{
    [Fact]
    public void SilenceGenerator_OutputsSilence()
    {
        var format = Format(frameCount: 4);
        var buffer = AudioSampleBuffer.FromInterleaved(format, [1f, -1f, 0.5f, -0.5f]);
        var generator = new SilenceTestSignalGenerator(AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Silence));

        generator.Generate(buffer);

        AssertSamples(buffer, [0f, 0f, 0f, 0f]);
    }

    [Fact]
    public void SineToneGenerator_OutputsDeterministicExpectedSamples()
    {
        var buffer = AudioSampleBuffer.Allocate(Format(frameCount: 4));
        var generator = new SineToneTestSignalGenerator(
            new AudioTestSignalDefinition(
                AudioTestSignalKind.SineTone,
                amplitude: 1f,
                frequencyHz: 12_000f));

        generator.Generate(buffer);

        AssertSamples(buffer, [0f, 1f, 0f, -1f]);
    }

    [Fact]
    public void SineToneGenerator_ResetRepeatsOutput()
    {
        var format = Format(frameCount: 4);
        var first = AudioSampleBuffer.Allocate(format);
        var second = AudioSampleBuffer.Allocate(format);
        var generator = new SineToneTestSignalGenerator(
            new AudioTestSignalDefinition(
                AudioTestSignalKind.SineTone,
                amplitude: 0.5f,
                frequencyHz: 12_000f));

        generator.Generate(first);
        generator.Reset();
        generator.Generate(second);

        AssertSamples(second, first.Samples);
    }

    [Fact]
    public void FrequencySweepGenerator_IsDeterministic()
    {
        var format = Format(frameCount: 8);
        var first = AudioSampleBuffer.Allocate(format);
        var second = AudioSampleBuffer.Allocate(format);
        var definition = new AudioTestSignalDefinition(
            AudioTestSignalKind.FrequencySweep,
            amplitude: 0.75f,
            sweepStartFrequencyHz: 20f,
            sweepEndFrequencyHz: 120f,
            sweepDurationSeconds: 0.25);

        new FrequencySweepTestSignalGenerator(definition).Generate(first);
        new FrequencySweepTestSignalGenerator(definition).Generate(second);

        AssertSamples(second, first.Samples);
    }

    [Fact]
    public void PulseGenerator_OutputsDeterministicTransient()
    {
        var buffer = AudioSampleBuffer.Allocate(Format(frameCount: 8));
        var generator = new PulseTestSignalGenerator(
            new AudioTestSignalDefinition(
                AudioTestSignalKind.Pulse,
                amplitude: 1f,
                pulseIntervalFrames: 4,
                pulseWidthFrames: 1));

        generator.Generate(buffer);

        AssertSamples(buffer, [1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f]);
    }

    [Fact]
    public void ConstantGenerator_OutputsConstantValue()
    {
        var buffer = AudioSampleBuffer.Allocate(Format(frameCount: 4));
        var generator = new ConstantTestSignalGenerator(
            new AudioTestSignalDefinition(
                AudioTestSignalKind.Constant,
                constantValue: -0.25f));

        generator.Generate(buffer);

        AssertSamples(buffer, [-0.25f, -0.25f, -0.25f, -0.25f]);
    }

    private static AudioSampleFormat Format(int frameCount)
    {
        return new AudioSampleFormat(48_000, 1, frameCount);
    }

    private static void AssertSamples(AudioSampleBuffer buffer, IReadOnlyList<float> expected)
    {
        Assert.Equal(expected.Count, buffer.SampleCount);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], buffer.Samples[i], precision: 6);
        }
    }
}
