using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Safety;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class OutputSafetyParticipantIntegrationTests
{
    [Fact]
    public async Task AudioOutputParticipant_SilencesRunningOutputOnTrip()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create();
        Assert.True((await coordinator.StartAsync()).Succeeded);
        var participant = new AudioOutputSafetyParticipant(coordinator);

        await participant.SilenceAsync(
            new OutputInterlockSnapshot(
                IsLatched: true,
                Reason: OutputInterlockReason.UserEmergencyMute,
                Message: "test trip",
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: 1),
            CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.False(coordinator.OutputDevice.GetStatus().IsStreaming);
    }

    [Fact]
    public async Task ManualTestParticipant_SilencesBenchOnTrip()
    {
        await using var bench = new AudioTestBench();
        Assert.True((await bench.StartAsync()).Succeeded);
        var participant = new ManualAudioTestBenchSafetyParticipant(bench);

        await participant.SilenceAsync(
            new OutputInterlockSnapshot(
                IsLatched: true,
                Reason: OutputInterlockReason.UserEmergencyMute,
                Message: "test trip",
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: 1),
            CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.True(bench.GetSnapshot().EmergencyMute);
        Assert.False(bench.GetSnapshot().IsActive);
    }

    [Fact]
    public async Task ManualTestParticipant_DoesNotBlockResetWhenIdleAndSilent()
    {
        await using var bench = new AudioTestBench();
        var participant = new ManualAudioTestBenchSafetyParticipant(bench);

        var canReset = participant.CanReset(out var blocker);

        Assert.True(canReset);
        Assert.True(string.IsNullOrWhiteSpace(blocker));
        Assert.True(participant.Current.IsSilent);
    }

    [Fact]
    public async Task AudioOutputParticipant_DoesNotBlockResetWhenArmedButStoppedAndSilent()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = RuntimeTestPipelineFactory.Create(
            AudioOutputConfiguration.Default with
            {
                RequestedDeviceName = AsioAudioOutputDevice.PreferredDriverName,
                SelectedOutputChannel = 1,
                IsHardwareArmed = true
            },
            new AsioAudioOutputDevice(
                new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
                backend),
            options: HapticPipelineOptions.ManualRendering);
        var participant = new AudioOutputSafetyParticipant(coordinator);

        var canReset = participant.CanReset(out var blocker);

        Assert.True(canReset);
        Assert.True(string.IsNullOrWhiteSpace(blocker));
        Assert.True(participant.Current.IsSilent);
    }

    private sealed class FakeAsioDriverCatalog(IReadOnlyList<string> driverNames) : IAsioDriverCatalog
    {
        private readonly IReadOnlyList<string> _driverNames = driverNames;

        public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_driverNames);
        }
    }

    private sealed class FakeAsioOutputBackend(int outputChannelCount) : IAsioOutputBackend
    {
        public AsioOutputBackendSnapshot GetSnapshot()
        {
            return new AsioOutputBackendSnapshot(
                IsOpen: true,
                IsRunning: false,
                DriverName: AsioAudioOutputDevice.PreferredDriverName,
                SampleRate: AudioOutputConfiguration.Default.SampleRate,
                BufferSize: AudioOutputConfiguration.Default.BufferSize,
                OutputChannelCount: outputChannelCount,
                SubmittedBufferCount: 0,
                DroppedBufferCount: 0,
                CallbackCount: 0,
                UnderrunCount: 0,
                QueuedBufferCount: 0,
                LastCallbackJitter: null,
                MaximumCallbackJitter: null,
                LastError: null,
                QueueCapacityBuffers: 0);
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
                outputChannelCount));
        }

        public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Started fake ASIO backend."));
        }

        public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            return ValueTask.CompletedTask;
        }
    }
}
