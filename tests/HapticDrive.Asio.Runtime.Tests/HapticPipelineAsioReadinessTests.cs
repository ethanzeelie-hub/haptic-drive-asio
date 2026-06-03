using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelineAsioReadinessTests
{
    [Fact]
    public async Task DefaultPipelineOutputMode_RemainsNull()
    {
        await using var coordinator = new HapticPipelineCoordinator();

        var snapshot = coordinator.GetSnapshot();

        Assert.Equal(AudioOutputDeviceKind.Null, snapshot.Output.Kind);
        Assert.False(snapshot.Output.RequiresPhysicalHardware);
    }

    [Fact]
    public async Task StopHaptics_StopsFakeAsioOutput()
    {
        var backend = new FakeAsioOutputBackend();
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True(backend.IsRunning);

        var stopResult = await coordinator.StopAsync();

        Assert.True(stopResult.Succeeded, stopResult.Message);
        Assert.False(backend.IsRunning);
        Assert.Equal(1, backend.StopCount);
    }

    [Fact]
    public async Task SwitchingAwayFromAsio_StopsFakeAsioOutputBeforeNullPipelineStarts()
    {
        var backend = new FakeAsioOutputBackend();
        await using var asioCoordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend));

        Assert.True((await asioCoordinator.StartAsync()).Succeeded);
        Assert.True((await asioCoordinator.StopAsync()).Succeeded);

        await using var nullCoordinator = new HapticPipelineCoordinator();

        Assert.False(backend.IsRunning);
        Assert.Equal(AudioOutputDeviceKind.Null, nullCoordinator.GetSnapshot().Output.Kind);
    }

    private static AudioOutputConfiguration ArmedConfiguration()
    {
        return AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = 0,
            IsHardwareArmed = true
        };
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
        public bool IsRunning { get; private set; }

        public int StopCount { get; private set; }

        public AsioOutputBackendSnapshot GetSnapshot()
        {
            return new AsioOutputBackendSnapshot(
                IsOpen: true,
                IsRunning: IsRunning,
                DriverName: AsioAudioOutputDevice.PreferredDriverName,
                SampleRate: AudioOutputConfiguration.Default.SampleRate,
                BufferSize: AudioOutputConfiguration.Default.BufferSize,
                OutputChannelCount: 2,
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
            return ValueTask.FromResult(AsioOutputBackendOpenResult.Success(
                "Opened fake ASIO backend.",
                configuration.SampleRate,
                configuration.BufferSize,
                outputChannelCount: 2));
        }

        public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = true;
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Started fake ASIO backend."));
        }

        public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            IsRunning = false;
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Stopped fake ASIO backend."));
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
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }
}
