using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class OutputStreamingTests
{
    [Fact]
    public async Task NullOutput_OutputOwnedStreamingReportsCallbackCadence()
    {
        await using var device = new NullAudioOutputDevice();
        Assert.True((await device.OpenAsync(AudioOutputConfiguration.Default)).Succeeded);

        var callbackCount = 0;
        var result = await device.StartStreamingAsync((buffer, context) =>
        {
            buffer.Clear();
            if (Interlocked.Increment(ref callbackCount) >= 4)
            {
                buffer.Samples[0] = 0.125f;
            }

            return AudioOutputRenderCallbackResult.Success("Rendered test buffer.");
        });

        Assert.True(result.Succeeded, result.Message);
        await WaitUntilAsync(() => Volatile.Read(ref callbackCount) >= 4);
        Assert.True((await device.StopAsync()).Succeeded);

        var status = device.GetStatus();
        Assert.True(status.RenderCallbackCount >= 4);
        Assert.True(status.SubmittedBufferCount >= 4);
        Assert.NotNull(status.LastRenderDuration);
        Assert.NotNull(status.LastCallbackJitter);
        Assert.False(status.IsStreaming);
    }

    [Fact]
    public async Task AsioOutput_OutputOwnedStreamingRoutesMonoToSelectedChannel()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 1))).Succeeded);
        Assert.True((await device.StartStreamingAsync((buffer, context) =>
        {
            buffer.Clear();
            buffer.Samples[0] = 0.25f;
            return AudioOutputRenderCallbackResult.Success("Rendered routed buffer.");
        })).Succeeded);

        await WaitUntilAsync(() => backend.LastSubmittedSamples is not null);
        Assert.True((await device.StopAsync()).Succeeded);

        Assert.Equal(0f, backend.LastSubmittedSamples![0], precision: 6);
        Assert.Equal(0.25f, backend.LastSubmittedSamples[1], precision: 6);
        Assert.True(device.GetStatus().RenderCallbackCount > 0);
        Assert.True(device.GetStatus().SubmittedBufferCount > 0);
    }

    [Fact]
    public async Task AsioOutput_DroppedBackendBuffersSurfaceDiagnostics()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, failSubmit: true);
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 0))).Succeeded);
        Assert.True((await device.StartStreamingAsync((buffer, context) =>
        {
            buffer.Clear();
            buffer.Samples[0] = 0.2f;
            return AudioOutputRenderCallbackResult.Success("Rendered dropped buffer.");
        })).Succeeded);

        await WaitUntilAsync(() => device.GetStatus().DroppedBufferCount > 0);
        Assert.True((await device.StopAsync()).Succeeded);

        var status = device.GetStatus();
        Assert.True(status.DroppedBufferCount > 0);
        Assert.Contains("drop", status.LastError ?? status.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutput_StopAndDisposeStopOutputOwnedCallbacks()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 0))).Succeeded);
        Assert.True((await device.StartStreamingAsync((buffer, context) =>
        {
            buffer.Clear();
            return AudioOutputRenderCallbackResult.Success("Rendered disposable buffer.");
        })).Succeeded);

        await WaitUntilAsync(() => device.GetStatus().RenderCallbackCount >= 2);
        await device.DisposeAsync();
        var callbacksAfterDispose = device.GetStatus().RenderCallbackCount;
        await Task.Delay(25);

        Assert.True(backend.Disposed);
        Assert.False(backend.IsRunning);
        Assert.Equal(callbacksAfterDispose, device.GetStatus().RenderCallbackCount);
    }

    private static AudioOutputConfiguration ArmedConfiguration(int channel)
    {
        return AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = channel,
            IsHardwareArmed = true
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
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
        private readonly bool _failSubmit;
        private long _submittedBufferCount;
        private long _droppedBufferCount;

        public FakeAsioOutputBackend(int outputChannelCount, bool failSubmit = false)
        {
            _outputChannelCount = outputChannelCount;
            _failSubmit = failSubmit;
        }

        public bool IsRunning { get; private set; }

        public bool Disposed { get; private set; }

        public float[]? LastSubmittedSamples { get; private set; }

        public AsioOutputBackendSnapshot GetSnapshot()
        {
            return new AsioOutputBackendSnapshot(
                IsOpen: true,
                IsRunning: IsRunning,
                DriverName: AsioAudioOutputDevice.PreferredDriverName,
                SampleRate: AudioOutputConfiguration.Default.SampleRate,
                BufferSize: AudioOutputConfiguration.Default.BufferSize,
                OutputChannelCount: _outputChannelCount,
                SubmittedBufferCount: Interlocked.Read(ref _submittedBufferCount),
                DroppedBufferCount: Interlocked.Read(ref _droppedBufferCount),
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
                _outputChannelCount));
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
            IsRunning = false;
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Stopped fake ASIO backend."));
        }

        public AsioOutputBackendOperationResult Submit(
            ReadOnlyMemory<float> interleavedSamples,
            int sampleRate,
            int frameCount,
            int outputChannelCount)
        {
            if (!IsRunning)
            {
                Interlocked.Increment(ref _droppedBufferCount);
                return AsioOutputBackendOperationResult.Failure("Fake backend stopped.");
            }

            if (_failSubmit)
            {
                Interlocked.Increment(ref _droppedBufferCount);
                return AsioOutputBackendOperationResult.Failure("Fake backend dropped buffer.");
            }

            LastSubmittedSamples = interleavedSamples.ToArray();
            Interlocked.Increment(ref _submittedBufferCount);
            return AsioOutputBackendOperationResult.Success("Fake backend accepted buffer.");
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }
}
