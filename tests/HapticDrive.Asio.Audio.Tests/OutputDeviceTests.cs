using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class OutputDeviceTests
{
    [Fact]
    public async Task NullOutputDevice_OpensStartsAndStopsWithoutHardware()
    {
        await using var device = new NullAudioOutputDevice();

        var openResult = await device.OpenAsync(AudioOutputConfiguration.Default);
        var startResult = await device.StartAsync();
        var stopResult = await device.StopAsync();

        Assert.True(openResult.Succeeded);
        Assert.True(startResult.Succeeded);
        Assert.True(stopResult.Succeeded);
        Assert.False(stopResult.Status.RequiresPhysicalHardware);
        Assert.False(stopResult.Status.IsManualDebugOnly);
        Assert.Equal(AudioOutputDeviceState.Stopped, stopResult.Status.State);
        Assert.Equal("NullAudioOutputDevice", stopResult.Status.DeviceName);
    }

    [Fact]
    public async Task NullOutputDevice_ConsumesSampleBuffersWithoutHardware()
    {
        await using var device = new NullAudioOutputDevice();
        var buffer = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
        buffer.Samples[0] = 0.25f;
        buffer.Samples[1] = -0.5f;

        Assert.True((await device.OpenAsync(AudioOutputConfiguration.Default)).Succeeded);
        Assert.True((await device.StartAsync()).Succeeded);
        var submitResult = await device.SubmitBufferAsync(buffer);
        var snapshot = device.GetSampleSinkSnapshot();

        Assert.True(submitResult.Succeeded, submitResult.Message);
        Assert.Equal(1, snapshot.SubmittedBufferCount);
        Assert.Equal(AudioOutputConfiguration.Default.BufferSize, snapshot.SubmittedFrameCount);
        Assert.Equal(AudioOutputConfiguration.Default.BufferSize, snapshot.SubmittedSampleCount);
        Assert.Equal(0.5f, snapshot.LastPeakLevel, precision: 6);
    }

    [Fact]
    public async Task WasapiDebugOutputDevice_IsManualDebugOnly()
    {
        await using var device = new WasapiDebugOutputDevice();

        var result = await device.OpenAsync(AudioOutputConfiguration.Default);

        Assert.True(result.Succeeded);
        Assert.True(result.Status.IsManualDebugOnly);
        Assert.False(result.Status.RequiresPhysicalHardware);
        Assert.Contains("manual debug", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_FailsGracefullyWhenDriverIsUnavailable()
    {
        await using var device = new AsioAudioOutputDevice(new FakeAsioDriverCatalog([]));

        var result = await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = 0
        });

        Assert.False(result.Succeeded);
        Assert.Equal(AudioOutputDeviceState.Faulted, result.Status.State);
        Assert.True(result.Status.RequiresPhysicalHardware);
        Assert.False(result.Status.IsAvailable);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_PrefersMAudioDriverWhenAvailable()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var device = new AsioAudioOutputDevice(new FakeAsioDriverCatalog(
        [
            "Other ASIO Driver",
            AsioAudioOutputDevice.PreferredDriverName
        ]), backend);

        var result = await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = 1
        });

        Assert.True(result.Succeeded);
        Assert.Equal(AudioOutputDeviceState.Open, result.Status.State);
        Assert.Equal(AsioAudioOutputDevice.PreferredDriverName, result.Status.DeviceName);
        Assert.Equal(2, result.Status.DeviceOutputChannelCount);
        Assert.Equal(1, result.Status.SelectedOutputChannel);
    }

    [Fact]
    public async Task AudioOutputDeviceFactory_CreatesRequestedDeviceWithoutFallback()
    {
        var factory = new AudioOutputDeviceFactory(new FakeAsioDriverCatalog([]));

        await using var asioDevice = factory.Create(AudioOutputDeviceKind.Asio);

        Assert.IsType<AsioAudioOutputDevice>(asioDevice);
        Assert.Equal(AudioOutputDeviceKind.Asio, asioDevice.Kind);
    }

    [Fact(Skip = "Manual hardware test: requires a real ASIO driver/interface and must not run in automated test suites.")]
    public async Task Manual_AsioOutputDevice_OpensRealDriverWhenHardwareIsAvailable()
    {
        await using var device = new AsioAudioOutputDevice();
        var result = await device.OpenAsync(AudioOutputConfiguration.Default);
        Assert.True(result.Succeeded);
    }

    private sealed class FakeAsioDriverCatalog : IAsioDriverCatalog
    {
        private readonly IReadOnlyList<string> _driverNames;

        public FakeAsioDriverCatalog(IReadOnlyList<string> driverNames)
        {
            _driverNames = driverNames;
        }

        public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_driverNames);
        }
    }

    private sealed class FakeAsioOutputBackend : IAsioOutputBackend
    {
        private readonly int _outputChannelCount;
        private bool _isOpen;

        public FakeAsioOutputBackend(int outputChannelCount)
        {
            _outputChannelCount = outputChannelCount;
        }

        public AsioOutputBackendSnapshot GetSnapshot()
        {
            return new AsioOutputBackendSnapshot(
                IsOpen: _isOpen,
                IsRunning: false,
                DriverName: null,
                SampleRate: AudioOutputConfiguration.Default.SampleRate,
                BufferSize: AudioOutputConfiguration.Default.BufferSize,
                OutputChannelCount: _outputChannelCount,
                SubmittedBufferCount: 0,
                DroppedBufferCount: 0,
                CallbackCount: 0,
                UnderrunCount: 0,
                QueuedBufferCount: 0,
                LastCallbackJitter: null,
                MaximumCallbackJitter: null,
                LastError: null);
        }

        public ValueTask<AsioOutputBackendOpenResult> OpenAsync(
            string driverName,
            AudioOutputConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isOpen = true;
            return ValueTask.FromResult(AsioOutputBackendOpenResult.Success(
                "Opened fake ASIO backend.",
                configuration.SampleRate,
                configuration.BufferSize,
                _outputChannelCount));
        }

        public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Started."));
        }

        public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Stopped."));
        }

        public AsioOutputBackendOperationResult Submit(
            ReadOnlyMemory<float> interleavedSamples,
            int sampleRate,
            int frameCount,
            int outputChannelCount)
        {
            return AsioOutputBackendOperationResult.Success("Submitted.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
