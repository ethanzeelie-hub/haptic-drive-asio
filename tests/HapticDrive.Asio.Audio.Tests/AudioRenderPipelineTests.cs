using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioRenderPipelineTests
{
    [Fact]
    public async Task Pipeline_MixesAppliesSafetyAndSubmitsToNullOutputWithoutHardware()
    {
        var configuration = AudioOutputConfiguration.Default;
        var format = AudioSampleFormat.FromConfiguration(configuration);
        var pipeline = new AudioRenderPipeline(format)
        {
            SafetyOptions = new AudioSafetyProcessorOptions(
                OutputGain: 1f,
                OutputGainCeiling: 1f,
                LimiterEnabled: true,
                EmergencyMute: false)
        };
        var outputBuffer = AudioSampleBuffer.Allocate(format);
        var source = AudioSampleBuffer.Allocate(format);
        source.Samples[0] = 0.5f;
        source.Samples[1] = 0.5f;

        await using var outputDevice = new NullAudioOutputDevice();
        Assert.True((await outputDevice.OpenAsync(configuration)).Succeeded);
        Assert.True((await outputDevice.StartAsync()).Succeeded);

        var result = await pipeline.ProcessAndSubmitAsync(
            [new AudioMixerInput(source, gain: 2f)],
            outputBuffer,
            outputDevice);
        var snapshot = outputDevice.GetSampleSinkSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, snapshot.SubmittedBufferCount);
        Assert.Equal(configuration.BufferSize, snapshot.SubmittedFrameCount);
        Assert.Equal(1f, snapshot.LastPeakLevel, precision: 6);
    }
}
