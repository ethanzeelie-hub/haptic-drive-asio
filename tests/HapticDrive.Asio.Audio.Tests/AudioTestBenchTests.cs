using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioTestBenchTests
{
    [Fact]
    public async Task DefaultTestBench_UsesNullOutputWithoutHardware()
    {
        var testBench = new AudioTestBench();

        try
        {
            var snapshot = testBench.GetSnapshot();

            Assert.Equal(AudioOutputDeviceKind.Null, snapshot.OutputKind);
            Assert.False(snapshot.OutputRequiresPhysicalHardware);
            Assert.False(snapshot.OutputIsManualDebugOnly);
        }
        finally
        {
            await testBench.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartStop_UpdatesActiveState()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice);

        var startResult = await testBench.StartAsync();
        var stopResult = await testBench.StopAsync();

        Assert.True(startResult.Succeeded, startResult.Message);
        Assert.True(startResult.Snapshot.IsActive);
        Assert.True(stopResult.Succeeded, stopResult.Message);
        Assert.False(stopResult.Snapshot.IsActive);
        Assert.Equal(AudioOutputDeviceState.Stopped, stopResult.Snapshot.OutputState);
    }

    [Fact]
    public async Task RenderBeforeStart_FailsWithoutSubmittingBuffer()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice);

        var result = await testBench.RenderNextBufferAsync();
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.False(result.Succeeded);
        Assert.Equal(0, sink.SubmittedBufferCount);
    }

    [Fact]
    public async Task RenderNextBuffer_FlowsThroughMixerSafetyAndNullOutput()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice)
        {
            SafetyOptions = UnitySafetyOptions
        };
        testBench.SelectSignal(new AudioTestSignalDefinition(
            AudioTestSignalKind.SineTone,
            amplitude: 1f,
            frequencyHz: 12_000f));

        Assert.True((await testBench.StartAsync()).Succeeded);
        var renderResult = await testBench.RenderNextBufferAsync();
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(1, sink.SubmittedBufferCount);
        Assert.Equal(4, sink.SubmittedFrameCount);
        Assert.Equal(1f, sink.LastPeakLevel, precision: 6);
        Assert.Equal(1f, renderResult.Snapshot.OutputPeakLevel, precision: 6);
    }

    [Fact]
    public async Task MasterMute_OutputsSilence()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice)
        {
            IsMuted = true,
            SafetyOptions = UnitySafetyOptions
        };
        testBench.SelectSignal(new AudioTestSignalDefinition(
            AudioTestSignalKind.Constant,
            constantValue: 1f));

        Assert.True((await testBench.StartAsync()).Succeeded);
        var renderResult = await testBench.RenderNextBufferAsync();
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.True(renderResult.Snapshot.IsMuted);
        Assert.Equal(0f, renderResult.Snapshot.OutputPeakLevel);
        Assert.Equal(0f, sink.LastPeakLevel);
    }

    [Fact]
    public async Task EmergencyMute_OutputsSilenceRegardlessOfSelectedSignal()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice)
        {
            EmergencyMute = true,
            SafetyOptions = UnitySafetyOptions
        };
        testBench.SelectSignal(new AudioTestSignalDefinition(
            AudioTestSignalKind.Constant,
            constantValue: 1f));

        Assert.True((await testBench.StartAsync()).Succeeded);
        var renderResult = await testBench.RenderNextBufferAsync();
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.True(renderResult.Snapshot.EmergencyMute);
        Assert.Equal(0f, renderResult.Snapshot.OutputPeakLevel);
        Assert.Equal(0f, sink.LastPeakLevel);
    }

    [Fact]
    public async Task OverRangeSignal_IsLimitedBySafetyChain()
    {
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(Configuration(), outputDevice);
        testBench.SelectSignal(new AudioTestSignalDefinition(
            AudioTestSignalKind.Constant,
            constantValue: 8f));

        Assert.True((await testBench.StartAsync()).Succeeded);
        var renderResult = await testBench.RenderNextBufferAsync();
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, renderResult.Snapshot.OutputPeakLevel, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, sink.LastPeakLevel, precision: 6);
        Assert.Equal(4, renderResult.Snapshot.LimitedSampleCount);
    }

    private static AudioOutputConfiguration Configuration()
    {
        return new AudioOutputConfiguration(
            SampleRate: 48_000,
            ChannelCount: 1,
            BufferSize: 4);
    }

    private static AudioSafetyProcessorOptions UnitySafetyOptions { get; } = new(
        OutputGain: 1f,
        OutputGainCeiling: 1f,
        LimiterEnabled: true,
        EmergencyMute: false);
}
