using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AsioOutputReadinessTests
{
    [Fact]
    public async Task WindowsRegistryCatalog_IsSafeWhenNoDriversAreAvailable()
    {
        var catalog = new WindowsRegistryAsioDriverCatalog();

        var drivers = await catalog.GetDriverNamesAsync();

        Assert.NotNull(drivers);
    }

    [Fact]
    public async Task AsioReadinessDiagnostics_ReportUnavailableGracefully()
    {
        var diagnostics = new AsioReadinessDiagnostics(new FakeAsioDriverCatalog([]));
        await using var output = new NullAudioOutputDevice();

        var snapshot = await diagnostics.RefreshAsync(output.GetStatus());

        Assert.True(snapshot.Succeeded);
        Assert.False(snapshot.AsioAvailable);
        Assert.False(snapshot.MTrackDriverVisible);
        Assert.False(snapshot.WindowsSoundOutputVisibilityProvesAsio);
        Assert.Contains("No ASIO drivers", snapshot.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioReadinessDiagnostics_SurfaceFakeMTrackDriver()
    {
        var diagnostics = new AsioReadinessDiagnostics(
            new FakeAsioDriverCatalog(["Other ASIO", "M-Audio M-Track ASIO"]));
        await using var output = new NullAudioOutputDevice();

        var snapshot = await diagnostics.RefreshAsync(output.GetStatus());

        Assert.True(snapshot.AsioAvailable);
        Assert.True(snapshot.MTrackDriverVisible);
        Assert.Equal("M-Audio M-Track ASIO", snapshot.MatchedMTrackDriverName);
        Assert.False(snapshot.WindowsSoundOutputVisibilityProvesAsio);
    }

    [Fact]
    public async Task AsioOutputDevice_RequiresExplicitDriverSelection()
    {
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            new FakeAsioOutputBackend());

        var result = await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            SelectedOutputChannel = 0
        });

        Assert.False(result.Succeeded);
        Assert.Contains("Select an ASIO driver", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_InvalidDriverFailsWithoutFallback()
    {
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            new FakeAsioOutputBackend());

        var result = await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = "Other Missing Driver",
            SelectedOutputChannel = 0
        });

        Assert.False(result.Succeeded);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_InvalidOutputChannelFailsSafely()
    {
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            new FakeAsioOutputBackend(outputChannelCount: 2));

        var result = await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = 2
        });

        Assert.False(result.Succeeded);
        Assert.Equal(AudioOutputDeviceState.Faulted, result.Status.State);
        Assert.Contains("outside", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_CannotStartUntilArmed()
    {
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            new FakeAsioOutputBackend(outputChannelCount: 2));

        Assert.True((await device.OpenAsync(AudioOutputConfiguration.Default with
        {
            RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel = 0,
            IsHardwareArmed = false
        })).Succeeded);

        var startResult = await device.StartAsync();

        Assert.False(startResult.Succeeded);
        Assert.Contains("armed", startResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_StartStopLifecycleWorksWithFakeBackend()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 1))).Succeeded);
        Assert.True((await device.StartAsync()).Succeeded);
        Assert.True((await device.StopAsync()).Succeeded);

        Assert.False(backend.IsRunning);
        Assert.Equal(1, backend.StartCount);
        Assert.Equal(1, backend.StopCount);
    }

    [Fact]
    public async Task AsioOutputDevice_DisposeIsSafeAfterNormalStart()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 0))).Succeeded);
        Assert.True((await device.StartAsync()).Succeeded);
        await device.DisposeAsync();

        Assert.True(backend.Disposed);
        Assert.False(backend.IsRunning);
    }

    [Fact]
    public async Task AsioOutputDevice_DisposeIsSafeAfterFailedStart()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, failStart: true);
        var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 0))).Succeeded);
        Assert.False((await device.StartAsync()).Succeeded);
        await device.DisposeAsync();

        Assert.True(backend.Disposed);
        Assert.False(backend.IsRunning);
    }

    [Fact]
    public async Task AsioOutputDevice_RoutesMonoSafetyProcessedBufferToSelectedChannel()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);
        var source = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
        var output = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
        source.Samples[0] = 4f;
        var pipeline = new AudioRenderPipeline(AudioSampleFormat.FromConfiguration(AudioOutputConfiguration.Default))
        {
            SafetyOptions = AudioSafetyProcessorOptions.Default with
            {
                OutputGain = 1f,
                OutputGainCeiling = 0.5f,
                LimiterEnabled = true
            }
        };

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 1))).Succeeded);
        Assert.True((await device.StartAsync()).Succeeded);
        var submitResult = await pipeline.ProcessAndSubmitAsync([new AudioMixerInput(source)], output, device);

        Assert.True(submitResult.Succeeded, submitResult.Message);
        Assert.Equal(0f, backend.LastSubmittedSamples![0], precision: 6);
        Assert.Equal(0.5f, backend.LastSubmittedSamples[1], precision: 6);
        Assert.Equal(1, submitResult.Status.SubmittedBufferCount);
    }

    [Fact]
    public async Task EmergencyMuteForcesSilenceBeforeAsioOutput()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var device = new AsioAudioOutputDevice(
            new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
            backend);
        var source = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
        var output = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
        source.Samples[0] = 1f;
        var pipeline = new AudioRenderPipeline(AudioSampleFormat.FromConfiguration(AudioOutputConfiguration.Default))
        {
            SafetyOptions = AudioSafetyProcessorOptions.Default with { EmergencyMute = true }
        };

        Assert.True((await device.OpenAsync(ArmedConfiguration(channel: 0))).Succeeded);
        Assert.True((await device.StartAsync()).Succeeded);
        var submitResult = await pipeline.ProcessAndSubmitAsync([new AudioMixerInput(source)], output, device);

        Assert.True(submitResult.Succeeded, submitResult.Message);
        Assert.All(backend.LastSubmittedSamples!, sample => Assert.Equal(0f, sample, precision: 6));
    }

    [Fact(Skip = "Manual hardware test: requires local M-Audio ASIO driver validation and must not run in CI.")]
    public async Task Manual_MAudioAsioDriverDiscovery_IsVisibleWhenInstalledLocally()
    {
        var diagnostics = new AsioReadinessDiagnostics(new WindowsRegistryAsioDriverCatalog());
        await using var output = new NullAudioOutputDevice();

        var snapshot = await diagnostics.RefreshAsync(output.GetStatus());

        Assert.True(snapshot.MTrackDriverVisible);
    }

    [Fact(Skip = "Manual hardware test deferred: Dayton BST-1 has not arrived, so physical shaker output validation is skipped by default.")]
    public void Manual_DaytonBst1PhysicalOutput_IsDeferredUntilShakerArrives()
    {
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
        private readonly bool _failStart;

        public FakeAsioOutputBackend(int outputChannelCount = 2, bool failStart = false)
        {
            _outputChannelCount = outputChannelCount;
            _failStart = failStart;
        }

        public bool IsOpen { get; private set; }

        public bool IsRunning { get; private set; }

        public bool Disposed { get; private set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public float[]? LastSubmittedSamples { get; private set; }

        public AsioOutputBackendSnapshot GetSnapshot()
        {
            return new AsioOutputBackendSnapshot(
                IsOpen: IsOpen,
                IsRunning: IsRunning,
                DriverName: AsioAudioOutputDevice.PreferredDriverName,
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
            IsOpen = true;
            return ValueTask.FromResult(AsioOutputBackendOpenResult.Success(
                "Fake backend opened.",
                configuration.SampleRate,
                configuration.BufferSize,
                _outputChannelCount));
        }

        public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            if (_failStart)
            {
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure("Fake start failure."));
            }

            IsRunning = true;
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Fake backend started."));
        }

        public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            IsRunning = false;
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Fake backend stopped."));
        }

        public AsioOutputBackendOperationResult Submit(
            ReadOnlyMemory<float> interleavedSamples,
            int sampleRate,
            int frameCount,
            int outputChannelCount)
        {
            if (!IsRunning)
            {
                return AsioOutputBackendOperationResult.Failure("Fake backend stopped.");
            }

            LastSubmittedSamples = interleavedSamples.ToArray();
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
