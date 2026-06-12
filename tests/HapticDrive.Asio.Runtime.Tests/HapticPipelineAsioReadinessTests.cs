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

    [Fact]
    public async Task ManualAsioHardwareTest_DefaultPipelineBlocksOnNullOutput()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);

        var result = coordinator.StartManualAsioHardwareTest(new ManualAsioHardwareTestRequest(
            40f,
            TimeSpan.FromMilliseconds(250)));

        Assert.False(result.Succeeded);
        Assert.Contains("ASIO Output", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AudioOutputDeviceKind.Null, coordinator.GetSnapshot().Output.Kind);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_BlocksUnlessAsioIsArmed()
    {
        await using var unarmed = new HapticPipelineCoordinator(
            ArmedConfiguration() with { IsHardwareArmed = false },
            new AsioAudioOutputDevice(
                new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
                new FakeAsioOutputBackend()),
            options: HapticPipelineOptions.ManualRendering);
        var unarmedStart = await unarmed.StartAsync();
        Assert.False(unarmedStart.Succeeded);
        var unarmedResult = unarmed.StartManualAsioHardwareTest(new ManualAsioHardwareTestRequest(
            40f,
            TimeSpan.FromMilliseconds(250)));
        Assert.False(unarmedResult.Succeeded);
        Assert.Contains("armed", unarmedResult.Message, StringComparison.OrdinalIgnoreCase);

        await using var stopped = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new AsioAudioOutputDevice(
                new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]),
                new FakeAsioOutputBackend()),
            options: HapticPipelineOptions.ManualRendering);

        var stoppedResult = await stopped.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45)));

        Assert.True(stoppedResult.Succeeded, stoppedResult.Message);
        Assert.False(stopped.GetSnapshot().IsRunning);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_BlockedByEmergencyMute()
    {
        var backend = new FakeAsioOutputBackend();
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.SetEmergencyMuteAsync(true)).Succeeded);
        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            40f,
            TimeSpan.FromMilliseconds(250)));

        Assert.False(result.Succeeded);
        Assert.Contains("Emergency mute", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_BlockedByInvalidChannelDiagnostics()
    {
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new FakeStartedAsioOutputDevice(selectedChannel: 2, outputChannelCount: 2),
            options: HapticPipelineOptions.ManualRendering);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var result = coordinator.StartManualAsioHardwareTest(new ManualAsioHardwareTestRequest(
            40f,
            TimeSpan.FromMilliseconds(250)));

        Assert.False(result.Succeeded);
        Assert.Contains("outside", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(50f)]
    public async Task ManualAsioHardwareTest_RendersSafetyProcessedBuffersToFakeAsio(float frequencyHz)
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var start = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            frequencyHz,
            TimeSpan.FromMilliseconds(45)));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();

        Assert.True(start.Succeeded, start.Message);
        Assert.NotNull(backend.LastSubmittedSamples);
        Assert.Contains(
            backend.LastSubmittedSamples!.Where((_, index) => index % 2 == 1),
            sample => Math.Abs(sample) > 0f);
        Assert.All(backend.LastSubmittedSamples.Where((_, index) => index % 2 == 0), sample => Assert.Equal(0f, sample, precision: 6));
        Assert.True(snapshot.OutputPeakLevel > 0f);
        Assert.True(snapshot.FramesRendered > 0);
        Assert.False(snapshot.HapticsRunning);
        Assert.True(snapshot.LastPulseUsedAsio);
        Assert.True(snapshot.LastManualPulseUsedAsio);
        Assert.False(snapshot.LastGearPulseUsedAsio);
        Assert.Equal(0, backend.SubmitBeforeStartCount);
        Assert.Equal(1, backend.StartCount);
        Assert.Equal(1, backend.StopCount);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_UsesRequestedStrengthFrequencyDurationAndClampsRange()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var start = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            500f,
            TimeSpan.FromMilliseconds(-5),
            2f));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();

        Assert.True(start.Succeeded, start.Message);
        Assert.Equal(80f, snapshot.LastFrequencyHz);
        Assert.Equal(100f, snapshot.LastStrengthPercent);
        Assert.Equal(200f, snapshot.LastOutputTrimPercent);
        Assert.Equal(45, snapshot.LastDurationMs);
        Assert.Equal(1, snapshot.SelectedOutputChannel);
        Assert.True(snapshot.SubmittedFrameCount > 0);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_OutputTrimScalesBeforeLimiterAndKeepsLimiterActive()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var start = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45),
            1.0f,
            OutputTrim: 4.0f));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();

        Assert.True(start.Succeeded, start.Message);
        Assert.Equal(100f, snapshot.LastStrengthPercent);
        Assert.Equal(400f, snapshot.LastOutputTrimPercent);
        Assert.Equal(4.0f, snapshot.LastEffectivePreLimiterAmplitude);
        Assert.True(snapshot.LimiterApplied);
        Assert.True(snapshot.LastEffectivePostLimiterAmplitude is > 0f and <= 0.75f);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_BlockedUnarmedDoesNotEmitPartialOutput()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1) with { IsHardwareArmed = false },
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45)));

        Assert.False(result.Succeeded);
        Assert.Equal(0, backend.StartCount);
        Assert.Equal(0, backend.SubmitCount);
        Assert.Equal(0, backend.SubmitBeforeStartCount);
        Assert.Null(backend.LastSubmittedSamples);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_PaddleGearSourceRecordsLastGearPulseUsedAsio()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var start = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45),
            0.5f,
            Source: "paddle gear bench"));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();

        Assert.True(start.Succeeded, start.Message);
        Assert.True(snapshot.LastPulseUsedAsio);
        Assert.False(snapshot.LastManualPulseUsedAsio);
        Assert.True(snapshot.LastGearPulseUsedAsio);
    }

    [Fact]
    public async Task ManualAsioHardwareTest_WritesFlightRecorderForAcceptedAndBlockedPulses()
    {
        using var directory = new TempDirectory();
        var recorder = new FileManualAsioHardwareTestFlightRecorder(directory.Path);
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);
        coordinator.SetManualAsioHardwareTestFlightRecorder(recorder);

        var accepted = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45),
            0.5f,
            Source: "paddle gear bench",
            AcceptedPaddleEventSequence: 42,
            PaddleSide: "Left",
            PaddleButtonId: 14));
        await using var nullCoordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        nullCoordinator.SetManualAsioHardwareTestFlightRecorder(recorder);
        var blocked = await nullCoordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(45)));

        var jsonl = File.ReadAllLines(recorder.LogPath);
        Assert.True(accepted.Succeeded, accepted.Message);
        Assert.False(blocked.Succeeded);
        Assert.Contains(jsonl, line => line.Contains("pulse-accepted", StringComparison.Ordinal));
        Assert.Contains(jsonl, line => line.Contains("pulse-completed", StringComparison.Ordinal));
        Assert.Contains(jsonl, line => line.Contains("pulse-blocked", StringComparison.Ordinal));
        Assert.Contains(jsonl, line => line.Contains("paddle gear bench", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ManualAsioHardwareTest_CompletesWithoutLeavingHapticsRunning()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        Assert.True((await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            40f,
            TimeSpan.FromMilliseconds(45)))).Succeeded);

        Assert.False(coordinator.GetManualAsioHardwareTestSnapshot().IsActive);
        Assert.False(coordinator.GetSnapshot().IsRunning);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(300)]
    public async Task StandaloneManualBst1Pulse_RendersAllRequiredFramesWithoutOverfillingBoundedQueue(int durationMs)
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, queueCapacity: 3);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            45f,
            TimeSpan.FromMilliseconds(durationMs),
            0.5f,
            Source: "manual test"));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();
        var expectedFrames = (long)Math.Ceiling(durationMs / 1000d * AudioOutputConfiguration.Default.SampleRate);
        var expectedBuffers = (int)Math.Ceiling((double)expectedFrames / AudioOutputConfiguration.Default.BufferSize);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(expectedFrames, snapshot.FramesRendered);
        Assert.Equal(expectedBuffers, backend.SubmitCount);
        Assert.Equal(0, backend.SubmitBeforeStartCount);
        Assert.True(backend.FirstSubmitSawCallbackActive);
        Assert.True(backend.MaxQueuedBufferCount <= 3);
        Assert.Equal(0, snapshot.DroppedFrameCount);
        Assert.False(snapshot.HapticsRunning);
    }

    [Fact]
    public async Task StandaloneManualBst1Pulse_BlocksWithoutPartialOutputWhenAsioCannotStart()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, failStart: true);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            45f,
            TimeSpan.FromMilliseconds(100)));

        Assert.False(result.Succeeded);
        Assert.Equal(0, backend.SubmitCount);
        Assert.Null(backend.LastSubmittedSamples);
    }

    [Fact]
    public async Task StandaloneManualBst1Pulse_LogsQueueFullWithoutSubmittingPastCapacity()
    {
        using var directory = new TempDirectory();
        var recorder = new FileManualAsioHardwareTestFlightRecorder(directory.Path);
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, queueCapacity: 3, initialQueuedBufferCount: 3);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);
        coordinator.SetManualAsioHardwareTestFlightRecorder(recorder);

        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            50f,
            TimeSpan.FromMilliseconds(100)));
        var jsonl = File.ReadAllLines(recorder.LogPath);

        Assert.False(result.Succeeded);
        Assert.Equal(0, backend.SubmitCount);
        Assert.Contains(jsonl, line => line.Contains("queue-full-before-submit", StringComparison.Ordinal));
        Assert.Contains(jsonl, line => line.Contains("\"QueueCapacityBuffers\":3", StringComparison.Ordinal));
        Assert.Contains(jsonl, line => line.Contains("\"QueueCountBeforeSubmit\":3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PaddleGearBst1Pulse_UsesStandaloneAsioPathWithoutHapticsOrTelemetry()
    {
        var backend = new FakeAsioOutputBackend(outputChannelCount: 2, queueCapacity: 3);
        await using var coordinator = new HapticPipelineCoordinator(
            ArmedConfiguration(channel: 1),
            new AsioAudioOutputDevice(new FakeAsioDriverCatalog([AsioAudioOutputDevice.PreferredDriverName]), backend),
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            55f,
            TimeSpan.FromMilliseconds(100),
            0.6f,
            Source: "paddle gear bench",
            AcceptedPaddleEventSequence: 12,
            PaddleSide: "Right",
            PaddleButtonId: 7));
        var snapshot = coordinator.GetManualAsioHardwareTestSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.True(snapshot.LastGearPulseUsedAsio);
        Assert.False(snapshot.LastManualPulseUsedAsio);
        Assert.False(snapshot.HapticsRunning);
        Assert.True(backend.FirstSubmitSawCallbackActive);
        Assert.Equal(0, backend.SubmitBeforeStartCount);
    }

    private static AudioOutputConfiguration ArmedConfiguration(int channel = 0)
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
        private readonly int _queueCapacity;
        private readonly bool _autoDrainSubmittedBuffers;
        private int _queuedBufferCount;

        public FakeAsioOutputBackend(
            int outputChannelCount = 2,
            bool failStart = false,
            int queueCapacity = 0,
            int initialQueuedBufferCount = 0,
            bool autoDrainSubmittedBuffers = true)
        {
            _outputChannelCount = outputChannelCount;
            _failStart = failStart;
            _queueCapacity = queueCapacity;
            _queuedBufferCount = initialQueuedBufferCount;
            _autoDrainSubmittedBuffers = autoDrainSubmittedBuffers;
        }

        public bool IsRunning { get; private set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int SubmitCount { get; private set; }

        public int SubmitBeforeStartCount { get; private set; }

        public int MaxQueuedBufferCount { get; private set; }

        public bool FirstSubmitSawCallbackActive { get; private set; }

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
                SubmittedBufferCount: SubmitCount,
                DroppedBufferCount: 0,
                CallbackCount: IsRunning ? StartCount : 0,
                UnderrunCount: 0,
                QueuedBufferCount: _queuedBufferCount,
                LastCallbackJitter: null,
                MaximumCallbackJitter: null,
                LastError: null,
                QueueCapacityBuffers: _queueCapacity);
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
            StartCount++;
            if (_failStart)
            {
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure("Fake start failure."));
            }

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
            SubmitCount++;
            if (!IsRunning)
            {
                SubmitBeforeStartCount++;
            }

            if (IsRunning && StartCount > 0)
            {
                FirstSubmitSawCallbackActive = true;
            }

            if (_queueCapacity > 0 && _queuedBufferCount >= _queueCapacity)
            {
                return AsioOutputBackendOperationResult.Failure("Native ASIO backend queue is full; buffer dropped.");
            }

            if (_queueCapacity > 0)
            {
                _queuedBufferCount++;
                MaxQueuedBufferCount = Math.Max(MaxQueuedBufferCount, _queuedBufferCount);
            }

            LastSubmittedSamples = interleavedSamples.ToArray();
            if (_autoDrainSubmittedBuffers && _queuedBufferCount > 0)
            {
                _queuedBufferCount--;
            }

            return AsioOutputBackendOperationResult.Success("Submitted.");
        }

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStartedAsioOutputDevice : IAudioOutputDevice
    {
        private readonly int _selectedChannel;
        private readonly int _outputChannelCount;
        private AudioOutputDeviceState _state = AudioOutputDeviceState.Created;

        public FakeStartedAsioOutputDevice(int selectedChannel, int outputChannelCount)
        {
            _selectedChannel = selectedChannel;
            _outputChannelCount = outputChannelCount;
        }

        public AudioOutputDeviceKind Kind => AudioOutputDeviceKind.Asio;

        public string DisplayName => "ASIO Output";

        public bool RequiresPhysicalHardware => true;

        public bool IsManualDebugOnly => false;

        public AudioOutputStatus GetStatus()
        {
            return new AudioOutputStatus(
                AudioOutputDeviceKind.Asio,
                _state,
                "ASIO Output",
                "Fake ASIO output.",
                AsioAudioOutputDevice.PreferredDriverName,
                AudioOutputConfiguration.Default.SampleRate,
                AudioOutputConfiguration.Default.ChannelCount,
                AudioOutputConfiguration.Default.BufferSize,
                RequiresPhysicalHardware: true,
                IsManualDebugOnly: false,
                IsAvailable: true,
                IsHardwareArmed: true,
                SelectedOutputChannel: _selectedChannel,
                DeviceOutputChannelCount: _outputChannelCount);
        }

        public ValueTask<AudioOutputDeviceResult> OpenAsync(
            AudioOutputConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = AudioOutputDeviceState.Open;
            return ValueTask.FromResult(AudioOutputDeviceResult.Success("Opened.", GetStatus()));
        }

        public ValueTask<AudioOutputDeviceResult> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = AudioOutputDeviceState.Started;
            return ValueTask.FromResult(AudioOutputDeviceResult.Success("Started.", GetStatus()));
        }

        public ValueTask<AudioOutputDeviceResult> StartStreamingAsync(
            AudioOutputRenderCallback renderCallback,
            CancellationToken cancellationToken = default)
        {
            return StartAsync(cancellationToken);
        }

        public ValueTask<AudioOutputDeviceResult> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = AudioOutputDeviceState.Stopped;
            return ValueTask.FromResult(AudioOutputDeviceResult.Success("Stopped.", GetStatus()));
        }

        public ValueTask<AudioOutputDeviceResult> SubmitBufferAsync(
            AudioSampleBuffer buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AudioOutputDeviceResult.Success("Submitted.", GetStatus()));
        }

        public ValueTask DisposeAsync()
        {
            _state = AudioOutputDeviceState.Disposed;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HapticDrive.Asio.Runtime.Tests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
