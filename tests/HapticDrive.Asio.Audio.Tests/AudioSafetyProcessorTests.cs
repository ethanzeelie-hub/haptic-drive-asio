using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioSafetyProcessorTests
{
    private static readonly AudioSampleFormat Format = new(48_000, 1, 4);

    [Fact]
    public void EmergencyMute_ForcesSilenceRegardlessOfInput()
    {
        var processor = new AudioSafetyProcessor();
        var source = Buffer([1f, -1f, float.PositiveInfinity, float.NaN]);
        var output = AudioSampleBuffer.Allocate(Format);

        var snapshot = processor.Process(
            source,
            output,
            new AudioSafetyProcessorOptions(
                OutputGain: 1f,
                OutputGainCeiling: 1f,
                LimiterEnabled: true,
                EmergencyMute: true));

        AssertSamples(output, [0f, 0f, 0f, 0f]);
        Assert.True(snapshot.EmergencyMute);
        Assert.Equal(0f, snapshot.OutputPeakLevel);
    }

    [Fact]
    public void NaNAndInfinityInputs_AreSanitized()
    {
        var processor = new AudioSafetyProcessor();
        var source = Buffer([float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.5f]);
        var output = AudioSampleBuffer.Allocate(Format);

        var snapshot = processor.Process(
            source,
            output,
            new AudioSafetyProcessorOptions(
                OutputGain: 1f,
                OutputGainCeiling: 1f,
                LimiterEnabled: true,
                EmergencyMute: false));

        AssertSamples(output, [0f, 0f, 0f, 0.5f]);
        Assert.Equal(3, snapshot.SanitizedSampleCount);
        Assert.Equal(0.5f, snapshot.InputPeakLevel, precision: 6);
        Assert.Equal(0.5f, snapshot.OutputPeakLevel, precision: 6);
    }

    [Fact]
    public void OverRangeSamples_ArePeakLimited()
    {
        var processor = new AudioSafetyProcessor();
        var source = Buffer([2f, -4f, 0.5f, 0f]);
        var output = AudioSampleBuffer.Allocate(Format);

        var snapshot = processor.Process(
            source,
            output,
            new AudioSafetyProcessorOptions(
                OutputGain: 1f,
                OutputGainCeiling: 1f,
                LimiterEnabled: true,
                EmergencyMute: false));

        AssertSamples(output, [0.5f, -1f, 0.125f, 0f]);
        Assert.Equal(2, snapshot.LimitedSampleCount);
        Assert.Equal(0, snapshot.ClippedSampleCount);
        Assert.Equal(1f, snapshot.OutputPeakLevel, precision: 6);
    }

    [Fact]
    public void OverRangeSamples_AreHardClippedWhenLimiterIsDisabled()
    {
        var processor = new AudioSafetyProcessor();
        var source = Buffer([2f, -3f, 0.5f, 0f]);
        var output = AudioSampleBuffer.Allocate(Format);

        var snapshot = processor.Process(
            source,
            output,
            new AudioSafetyProcessorOptions(
                OutputGain: 1f,
                OutputGainCeiling: 1f,
                LimiterEnabled: false,
                EmergencyMute: false));

        AssertSamples(output, [1f, -1f, 0.5f, 0f]);
        Assert.Equal(0, snapshot.LimitedSampleCount);
        Assert.Equal(2, snapshot.ClippedSampleCount);
    }

    [Fact]
    public void Stage18rBDefaults_ApplyFullOutputGainAndInternalCeiling()
    {
        var processor = new AudioSafetyProcessor();
        var source = Buffer([4f, -4f, 1f, 0f]);
        var output = AudioSampleBuffer.Allocate(Format);

        var snapshot = processor.Process(source, output);

        AssertSamples(output, [1f, -1f, 0.25f, 0f]);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGain, snapshot.OutputGain);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, snapshot.OutputGainCeiling);
        Assert.Equal(2, snapshot.LimitedSampleCount);
    }

    private static AudioSampleBuffer Buffer(float[] samples)
    {
        return AudioSampleBuffer.FromInterleaved(Format, samples);
    }

    private static void AssertSamples(AudioSampleBuffer buffer, float[] expected)
    {
        Assert.Equal(expected.Length, buffer.SampleCount);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], buffer.Samples[i], precision: 6);
        }
    }
}
