using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioMixerTests
{
    private static readonly AudioSampleFormat Format = new(48_000, 1, 4);

    [Fact]
    public void EmptyMixerOutput_IsSilence()
    {
        var mixer = new AudioMixer();
        var output = Buffer([0.1f, 0.2f, 0.3f, 0.4f]);

        var snapshot = mixer.Mix([], output);

        AssertSamples(output, [0f, 0f, 0f, 0f]);
        Assert.False(snapshot.IsRunning);
        Assert.Equal(0, snapshot.ActiveSourceCount);
        Assert.Equal(0f, snapshot.PeakLevel);
    }

    [Fact]
    public void SingleSource_IsPassedThrough()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var source = Buffer([0.1f, -0.2f, 0.3f, -0.4f]);

        var snapshot = mixer.Mix([new AudioMixerInput(source)], output);

        AssertSamples(output, [0.1f, -0.2f, 0.3f, -0.4f]);
        Assert.True(snapshot.IsRunning);
        Assert.Equal(1, snapshot.ActiveSourceCount);
        Assert.Equal(0.4f, snapshot.PeakLevel, precision: 6);
    }

    [Fact]
    public void MultipleSources_AreSummedDeterministically()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var first = Buffer([0.1f, 0.2f, 0.3f, 0.4f]);
        var second = Buffer([0.5f, -0.1f, -0.2f, 0.0f]);

        var snapshot = mixer.Mix(
            [
                new AudioMixerInput(first),
                new AudioMixerInput(second)
            ],
            output);

        AssertSamples(output, [0.6f, 0.1f, 0.1f, 0.4f]);
        Assert.Equal(2, snapshot.ActiveSourceCount);
    }

    [Fact]
    public void MasterGain_IsApplied()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var source = Buffer([0.2f, -0.4f, 0.6f, -0.8f]);

        mixer.Mix(
            [new AudioMixerInput(source)],
            output,
            new AudioMixerSettings(MasterGain: 0.5f, IsMuted: false, EmergencyMute: false));

        AssertSamples(output, [0.1f, -0.2f, 0.3f, -0.4f]);
    }

    [Fact]
    public void PerSourceGain_IsApplied()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var first = Buffer([0.1f, 0.2f, 0.3f, 0.4f]);
        var second = Buffer([0.4f, 0.4f, 0.4f, 0.4f]);

        mixer.Mix(
            [
                new AudioMixerInput(first, gain: 2f),
                new AudioMixerInput(second, gain: 0.5f)
            ],
            output);

        AssertSamples(output, [0.4f, 0.6f, 0.8f, 1.0f]);
    }

    [Fact]
    public void NormalMute_OutputsSilence()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var source = Buffer([1f, 1f, 1f, 1f]);

        var snapshot = mixer.Mix(
            [new AudioMixerInput(source)],
            output,
            new AudioMixerSettings(MasterGain: 1f, IsMuted: true, EmergencyMute: false));

        AssertSamples(output, [0f, 0f, 0f, 0f]);
        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.IsMuted);
    }

    [Fact]
    public void EmergencyMute_OutputsSilence()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var source = Buffer([1f, 1f, 1f, 1f]);

        var snapshot = mixer.Mix(
            [new AudioMixerInput(source)],
            output,
            new AudioMixerSettings(MasterGain: 1f, IsMuted: false, EmergencyMute: true));

        AssertSamples(output, [0f, 0f, 0f, 0f]);
        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.EmergencyMute);
    }

    [Fact]
    public void InvalidSamplesAndGains_AreSanitized()
    {
        var mixer = new AudioMixer();
        var output = AudioSampleBuffer.Allocate(Format);
        var invalidSamples = Buffer([float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.25f]);
        var ignoredSource = Buffer([1f, 1f, 1f, 1f]);

        mixer.Mix(
            [
                new AudioMixerInput(invalidSamples),
                new AudioMixerInput(ignoredSource, gain: float.NaN)
            ],
            output);

        AssertSamples(output, [0f, 0f, 0f, 0.25f]);
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
