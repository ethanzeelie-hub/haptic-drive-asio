using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed class HapticPipelineCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan StandaloneManualAsioCallbackActivationTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ManualAsioQueueRoomTimeout = TimeSpan.FromMilliseconds(250);

    private readonly object _diagnosticsGate = new();
    private readonly object _renderCallbackGate = new();
    private readonly object _manualAsioHardwareTestGate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly AudioSampleBuffer _outputBuffer;
    private readonly HapticPipelineOptions _options;
    private readonly string _manualAsioHardwareSessionId = Guid.NewGuid().ToString("N");
    private readonly string _localBst1PulseRendererInstanceId = $"local-bst1-renderer-{Guid.NewGuid():N}";
    private readonly IGameTelemetryAdapter _telemetryGameAdapter;
    private readonly long[] _packetIdCounts;
    private readonly DateTimeOffset?[] _packetIdLastObservedAtUtc;
    private readonly bool _ownsForwarder;
    private readonly bool _ownsRecordingService;
    private readonly bool _ownsReplayService;

    private AudioRenderPipelineSnapshot? _lastAudioSnapshot;
    private HapticEffectEngineSnapshot _lastEffectSnapshot;
    private HapticDriveProfile _currentProfile;
    private HapticPipelineInputSource _inputSource = HapticPipelineInputSource.None;
    private DateTimeOffset? _lastPacketAtUtc;
    private DateTimeOffset? _lastVehicleStateUpdateAtUtc;
    private DateTimeOffset? _lastVehicleStateWallClockAtUtc;
    private TimeSpan? _lastRenderTelemetryAge;
    private string _lastPacketMessage;
    private string _lastVehicleStateMessage;
    private string? _lastPipelineError;
    private string? _lastManualAsioHardwareTestBlockedReason;
    private string? _lastManualAsioHardwareTestError;
    private ManualAsioHardwareTestRequest? _lastManualAsioHardwareTestRequest;
    private ManualAsioHardwareTestRun? _manualAsioHardwareTestRun;
    private IManualAsioHardwareTestFlightRecorder? _manualAsioHardwareFlightRecorder;
    private long _manualAsioHardwareTestRenderedFrameCount;
    private long _manualAsioHardwareTestPulseGeneration;
    private long _manualAsioHardwareTestStaleStopIgnoredCount;
    private long _manualAsioHardwareDroppedPulseCount;
    private bool _lastManualAsioHardwareTestUsedAsio;
    private bool _lastManualBst1PulseUsedAsio;
    private bool _lastGearBst1PulseUsedAsio;
    private bool _lastManualAsioHardwareTestBlocked;
    private bool _lastManualAsioHardwareTestLimiterApplied;
    private float _lastManualAsioHardwareTestPeak;
    private long _lastManualAsioHardwareSubmittedFrames;
    private long _lastManualAsioHardwareDroppedFrames;
    private bool _disposed;
    private bool _isRunning;
    private bool _outputOpened;
    private bool _localAsioPulseStreaming;
    private bool _normalMuted;
    private bool _emergencyMuted;
    private bool _telemetryTimedOutMuted;
    private long _packetsObserved;
    private long _packetParseSuccessCount;
    private long _packetParseIgnoredCount;
    private long _packetParseFailureCount;
    private long _vehicleStateUpdateCount;
    private long _renderedBufferCount;

    public HapticPipelineCoordinator(
        AudioOutputConfiguration? configuration = null,
        IAudioOutputDevice? outputDevice = null,
        IUdpTelemetryForwarder? telemetryForwarder = null,
        TelemetryRecordingService? recordingService = null,
        ITelemetryReplayService? replayService = null,
        HapticDriveProfile? profile = null,
        HapticPipelineOptions? options = null,
        IEnumerable<UdpTelemetryForwardingDestination>? forwardingDestinations = null,
        IGameTelemetryAdapter? telemetryGameAdapter = null)
    {
        _telemetryGameAdapter = telemetryGameAdapter ?? new F125GameTelemetryAdapter();
        Configuration = configuration ?? AudioOutputConfiguration.Default;
        _options = options ?? HapticPipelineOptions.Default;
        Format = AudioSampleFormat.FromConfiguration(Configuration);
        OutputDevice = outputDevice ?? new NullAudioOutputDevice();
        TelemetryForwarder = telemetryForwarder ?? new UdpTelemetryForwarder(forwardingDestinations);
        RecordingService = recordingService ?? new TelemetryRecordingService();
        ReplayService = replayService ?? new TelemetryReplayService();
        AudioPipeline = new AudioRenderPipeline(Format);
        EffectEngine = new HapticEffectEngine(Format);
        _outputBuffer = AudioSampleBuffer.Allocate(Format);
        _ownsForwarder = telemetryForwarder is null;
        _ownsRecordingService = recordingService is null;
        _ownsReplayService = replayService is null;
        _currentProfile = profile ?? HapticDriveProfile.Default;
        var packetIndexLength = Math.Max(
            16,
            _telemetryGameAdapter.PacketDescriptors.Count == 0
                ? 0
                : _telemetryGameAdapter.PacketDescriptors.Max(descriptor => descriptor.PacketId + 1));
        _packetIdCounts = new long[packetIndexLength];
        _packetIdLastObservedAtUtc = new DateTimeOffset?[packetIndexLength];
        _lastPacketMessage = $"Waiting for {_telemetryGameAdapter.GameName} packets.";
        _lastVehicleStateMessage = $"Waiting for parsed {_telemetryGameAdapter.GameName} packets.";
        ApplyProfile(_currentProfile);
        _lastEffectSnapshot = EffectEngine.GetSnapshot();
        ReplayService.PacketReplayed += ReplayService_PacketReplayed;
    }

    public AudioOutputConfiguration Configuration { get; }

    public AudioSampleFormat Format { get; }

    public IAudioOutputDevice OutputDevice { get; }

    public IUdpTelemetryForwarder TelemetryForwarder { get; }

    public TelemetryRecordingService RecordingService { get; }

    public ITelemetryReplayService ReplayService { get; }

    public AudioRenderPipeline AudioPipeline { get; }

    public HapticEffectEngine EffectEngine { get; }

    public HapticDriveProfile CurrentProfile => _currentProfile;

    public void SetManualAsioHardwareTestFlightRecorder(IManualAsioHardwareTestFlightRecorder? flightRecorder)
    {
        _manualAsioHardwareFlightRecorder = flightRecorder;
    }

    public async ValueTask<AudioOutputDeviceResult> HydrateOutputReadinessAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_outputOpened)
        {
            return AudioOutputDeviceResult.Success("Output readiness already hydrated.", OutputDevice.GetStatus());
        }

        var openResult = await OutputDevice.OpenAsync(Configuration, cancellationToken).ConfigureAwait(false);
        if (openResult.Succeeded)
        {
            _outputOpened = true;
        }

        return openResult;
    }

    public async ValueTask<HapticPipelineOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_isRunning)
            {
                return HapticPipelineOperationResult.Success("Haptic pipeline is already running.");
            }

            if (!_outputOpened)
            {
                var openResult = await OutputDevice.OpenAsync(Configuration, cancellationToken).ConfigureAwait(false);
                if (!openResult.Succeeded)
                {
                    SetPipelineError(openResult.Message);
                    return HapticPipelineOperationResult.Failure(openResult.Message, openResult);
                }

                _outputOpened = true;
            }

            _isRunning = true;
            var startResult = _options.UseOutputOwnedRendering
                ? await OutputDevice.StartStreamingAsync(RenderOutputBuffer, cancellationToken).ConfigureAwait(false)
                : await OutputDevice.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!startResult.Succeeded)
            {
                _isRunning = false;
                SetPipelineError(startResult.Message);
                return HapticPipelineOperationResult.Failure(startResult.Message, startResult);
            }

            SetPipelineError(null);
            return HapticPipelineOperationResult.Success(
                "Haptic pipeline started with the selected output device.",
                startResult);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<HapticPipelineOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ReplayService.StopAsync().ConfigureAwait(false);
            _isRunning = false;
            StopManualAsioHardwareTest("Manual ASIO Hardware Test stopped because haptics stopped.");

            var status = OutputDevice.GetStatus();
            if (status.State is AudioOutputDeviceState.Started or AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
            {
                var stopResult = await OutputDevice.StopAsync(cancellationToken).ConfigureAwait(false);
                _localAsioPulseStreaming = false;
                if (!stopResult.Succeeded)
                {
                    SetPipelineError(stopResult.Message);
                    return HapticPipelineOperationResult.Failure(stopResult.Message, stopResult);
                }

                SetPipelineError(null);
                return HapticPipelineOperationResult.Success("Haptic pipeline stopped.", stopResult);
            }

            SetPipelineError(null);
            return HapticPipelineOperationResult.Success("Haptic pipeline stopped.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public void ApplyProfile(HapticDriveProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var validation = HapticProfileValidator.Validate(profile);
        _currentProfile = validation.Profile;
        _normalMuted = _currentProfile.Mixer.IsMuted;
        EffectEngine.UpdateOptions(_currentProfile.ToEffectOptions());
        AudioPipeline.MixerSettings = _currentProfile.ToMixerSettings(_emergencyMuted);
        AudioPipeline.SafetyOptions = _currentProfile.ToSafetyOptions(_emergencyMuted);
        _lastEffectSnapshot = EffectEngine.GetSnapshot();
    }

    public void SetMuted(bool isMuted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _normalMuted = isMuted;
        AudioPipeline.MixerSettings = AudioPipeline.MixerSettings with { IsMuted = isMuted };
    }

    public void NotifyLocalGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EffectEngine.NotifyRoadTextureGearPulseAccepted(timestampUtc);
        _lastEffectSnapshot = EffectEngine.GetSnapshot();
    }

    public async ValueTask<HapticPipelineOperationResult> SetEmergencyMuteAsync(
        bool emergencyMuted,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _emergencyMuted = emergencyMuted;
        AudioPipeline.MixerSettings = AudioPipeline.MixerSettings with { EmergencyMute = emergencyMuted };
        AudioPipeline.SafetyOptions = AudioPipeline.SafetyOptions with { EmergencyMute = emergencyMuted };

        if (!_isRunning)
        {
            return HapticPipelineOperationResult.Success(
                emergencyMuted
                    ? "Emergency mute enabled while the haptic pipeline is stopped."
                    : "Emergency mute cleared while the haptic pipeline is stopped.");
        }

        if (_options.UseOutputOwnedRendering)
        {
            return HapticPipelineOperationResult.Success(
                emergencyMuted
                    ? "Emergency mute enabled for the output-owned render path."
                    : "Emergency mute cleared for the output-owned render path.");
        }

        return await RenderNextBufferAsync(cancellationToken).ConfigureAwait(false);
    }

    public ManualAsioHardwareTestResult StartManualAsioHardwareTest(
        ManualAsioHardwareTestRequest request)
    {
        return StartManualAsioHardwareTestAsync(request).AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask<ManualAsioHardwareTestResult> StartManualAsioHardwareTestAsync(
        ManualAsioHardwareTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = request.Normalize();
        if (!normalized.IsSupportedFrequency)
        {
            return StoreBlockedManualAsioHardwareTest(
                $"Manual BST-1 pulse frequency must be between {ManualAsioHardwareTestRequest.MinimumFrequencyHz:0.#} Hz and {ManualAsioHardwareTestRequest.MaximumFrequencyHz:0.#} Hz.");
        }

        if (normalized.Duration <= TimeSpan.Zero || normalized.Duration > ManualAsioHardwareTestRequest.MaximumDuration)
        {
            return StoreBlockedManualAsioHardwareTest(
                "Manual BST-1 pulse duration must be greater than zero and no more than 1 second.");
        }

        var outputStatus = OutputDevice.GetStatus();
        var blockedReason = GetManualAsioHardwareTestBlockedReason(outputStatus);
        if (blockedReason is not null)
        {
            return StoreBlockedManualAsioHardwareTest(blockedReason, normalized, outputStatus);
        }

        var wasPipelineRunning = _isRunning;
        var wasOutputStarted = outputStatus.State == AudioOutputDeviceState.Started;
        var wasOutputOpened = _outputOpened
            || outputStatus.State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped;

        if (!wasOutputOpened)
        {
            var openResult = await OutputDevice.OpenAsync(Configuration, cancellationToken).ConfigureAwait(false);
            if (!openResult.Succeeded)
            {
                return StoreBlockedManualAsioHardwareTest(openResult.Message, normalized, OutputDevice.GetStatus());
            }

            _outputOpened = true;
            outputStatus = OutputDevice.GetStatus();
            blockedReason = GetManualAsioHardwareTestBlockedReason(outputStatus);
            if (blockedReason is not null)
            {
                return StoreBlockedManualAsioHardwareTest(blockedReason, normalized, outputStatus);
            }
        }

        if (outputStatus.Kind != AudioOutputDeviceKind.Asio)
        {
            return StoreBlockedManualAsioHardwareTest("Blocked: selected output is Null.", normalized, outputStatus);
        }

        var frameCount = Math.Max(1L, (long)Math.Ceiling(normalized.Duration.TotalSeconds * Configuration.SampleRate));
        var signal = new AudioTestSignalDefinition(
            AudioTestSignalKind.SineTone,
            normalized.EffectivePreLimiterAmplitude,
            normalized.FrequencyHz);
        var generation = Interlocked.Increment(ref _manualAsioHardwareTestPulseGeneration);
        var run = new ManualAsioHardwareTestRun(
            AudioTestSignalGeneratorFactory.Create(signal),
            AudioSampleBuffer.Allocate(Format),
            normalized,
            frameCount,
            generation,
            DateTimeOffset.UtcNow);

        ManualAsioHardwareTestRun? supersededRun;
        lock (_manualAsioHardwareTestGate)
        {
            supersededRun = _manualAsioHardwareTestRun;
            if (supersededRun is not null && supersededRun.FramesRemaining > 0)
            {
                supersededRun.MarkSuperseded();
                Interlocked.Increment(ref _manualAsioHardwareDroppedPulseCount);
            }

            _manualAsioHardwareTestRun = run;
            _lastManualAsioHardwareTestRequest = normalized;
            _lastManualAsioHardwareTestBlockedReason = null;
            _lastManualAsioHardwareTestError = null;
            _manualAsioHardwareTestRenderedFrameCount = 0;
            _lastManualAsioHardwareTestUsedAsio = true;
            if (IsManualAsioGearPulseSource(normalized.Source))
            {
                _lastGearBst1PulseUsedAsio = true;
                EffectEngine.NotifyRoadTextureGearPulseAccepted(run.StartedAtUtc);
                _lastEffectSnapshot = EffectEngine.GetSnapshot();
            }
            else
            {
                _lastManualBst1PulseUsedAsio = true;
            }
            _lastManualAsioHardwareTestBlocked = false;
            _lastManualAsioHardwareTestLimiterApplied = false;
            _lastManualAsioHardwareTestPeak = 0f;
            _lastManualAsioHardwareSubmittedFrames = 0;
            _lastManualAsioHardwareDroppedFrames = 0;
        }

        if (supersededRun is not null && supersededRun.FramesRemaining > 0)
        {
            RecordManualAsioHardwareFlight(
                "pulse-superseded",
                supersededRun.Request,
                outputStatus,
                supersededRun.GenerationId,
                run: supersededRun,
                startTimestamp: supersededRun.StartedAtUtc,
                stopTimestamp: DateTimeOffset.UtcNow,
                expectedFrameCount: supersededRun.TotalFrameCount,
                acceptedFrameCount: supersededRun.PulseOwnedFramesConsumed,
                renderedFrameCount: supersededRun.PulseOwnedFramesConsumed,
                completionReason: "superseded",
                replacedByLatestPressWins: true);
        }

        RecordManualAsioHardwareFlight("pulse-accepted", normalized, outputStatus, generation, startTimestamp: run.StartedAtUtc);

        if (_options.UseOutputOwnedRendering)
        {
            try
            {
                var callbackCountBeforePulse = GetCombinedAsioCallbackCount(OutputDevice.GetStatus());
                var streamResult = await EnsureLocalAsioPulseStreamingAsync(normalized, generation, run.StartedAtUtc, cancellationToken)
                    .ConfigureAwait(false);
                if (!streamResult.Succeeded)
                {
                    return StoreBlockedManualAsioHardwareTest(streamResult.Message, normalized, OutputDevice.GetStatus());
                }

                await WaitForManualAsioHardwarePulseRenderedByCallbackAsync(
                    run,
                    cancellationToken,
                    callbackCountBeforePulse: callbackCountBeforePulse).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (_manualAsioHardwareTestGate)
                {
                    if (ReferenceEquals(_manualAsioHardwareTestRun, run))
                    {
                        _manualAsioHardwareTestRun = null;
                    }

                    _lastManualAsioHardwareTestError = $"Manual BST-1 pulse failed safely: {ex.Message}";
                }

                RecordManualAsioHardwareFlight(
                    "pulse-failed",
                    normalized,
                    OutputDevice.GetStatus(),
                    generation,
                    exception: ex,
                    startTimestamp: run.StartedAtUtc,
                    stopTimestamp: DateTimeOffset.UtcNow,
                    expectedFrameCount: run.TotalFrameCount,
                    renderedFrameCount: run.PulseOwnedFramesConsumed,
                    completionReason: "failed");
                return ManualAsioHardwareTestResult.Blocked(
                    $"Manual BST-1 pulse failed safely: {ex.Message}",
                    GetManualAsioHardwareTestSnapshot());
            }
        }
        else if (!wasPipelineRunning && !wasOutputStarted)
        {
            try
            {
                var callbackCountBeforePulse = GetCombinedAsioCallbackCount(OutputDevice.GetStatus());
                RecordManualAsioHardwareFlight(
                    "stream-start-requested",
                    normalized,
                    OutputDevice.GetStatus(),
                    generation,
                    streamStartRequested: true,
                    callbackCountBeforePulse: callbackCountBeforePulse,
                    startTimestamp: run.StartedAtUtc);
                var startResult = await OutputDevice.StartAsync(cancellationToken).ConfigureAwait(false);
                if (!startResult.Succeeded)
                {
                    return StoreBlockedManualAsioHardwareTest(startResult.Message, normalized, OutputDevice.GetStatus());
                }

                var callbackActiveAtUtc = await WaitForStandaloneManualAsioCallbackActiveAsync(
                    callbackCountBeforePulse,
                    normalized,
                    generation,
                    run.StartedAtUtc,
                    cancellationToken).ConfigureAwait(false);
                if (callbackActiveAtUtc is null)
                {
                    return StoreBlockedManualAsioHardwareTest(
                        "ASIO callback did not become active before the standalone BST-1 pulse deadline; no pulse buffers were submitted.",
                        normalized,
                        OutputDevice.GetStatus());
                }

                await RenderManualAsioHardwarePulseAsync(
                    run,
                    cancellationToken,
                    paceSubmissions: true,
                    respectQueueCapacity: true,
                    callbackCountBeforePulse: callbackCountBeforePulse,
                    callbackActiveAtUtc: callbackActiveAtUtc).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (_manualAsioHardwareTestGate)
                {
                    if (ReferenceEquals(_manualAsioHardwareTestRun, run))
                    {
                        _manualAsioHardwareTestRun = null;
                    }

                    _lastManualAsioHardwareTestError = $"Manual BST-1 pulse failed safely: {ex.Message}";
                }

                RecordManualAsioHardwareFlight(
                    "pulse-failed",
                    normalized,
                    OutputDevice.GetStatus(),
                    generation,
                    exception: ex,
                    startTimestamp: run.StartedAtUtc,
                    stopTimestamp: DateTimeOffset.UtcNow);
                return ManualAsioHardwareTestResult.Blocked(
                    $"Manual BST-1 pulse failed safely: {ex.Message}",
                    GetManualAsioHardwareTestSnapshot());
            }
            finally
            {
                await DelayForStandaloneManualAsioDrainAsync(normalized, cancellationToken).ConfigureAwait(false);
                var stopResult = await OutputDevice.StopAsync(cancellationToken).ConfigureAwait(false);
                if (!stopResult.Succeeded)
                {
                    lock (_manualAsioHardwareTestGate)
                    {
                        _lastManualAsioHardwareTestError = stopResult.Message;
                    }
                }
            }
        }
        else
        {
            if (outputStatus.State != AudioOutputDeviceState.Started)
            {
                var startResult = await OutputDevice.StartAsync(cancellationToken).ConfigureAwait(false);
                if (!startResult.Succeeded)
                {
                    return StoreBlockedManualAsioHardwareTest(startResult.Message, normalized, OutputDevice.GetStatus());
                }

                outputStatus = OutputDevice.GetStatus();
            }

            try
            {
                await RenderManualAsioHardwarePulseAsync(
                    run,
                    cancellationToken,
                    respectQueueCapacity: true,
                    callbackCountBeforePulse: GetCombinedAsioCallbackCount(OutputDevice.GetStatus())).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (_manualAsioHardwareTestGate)
                {
                    if (ReferenceEquals(_manualAsioHardwareTestRun, run))
                    {
                        _manualAsioHardwareTestRun = null;
                    }

                    _lastManualAsioHardwareTestError = $"Manual BST-1 pulse failed safely: {ex.Message}";
                }

                RecordManualAsioHardwareFlight(
                    "pulse-failed",
                    normalized,
                    OutputDevice.GetStatus(),
                    generation,
                    exception: ex,
                    startTimestamp: run.StartedAtUtc,
                    stopTimestamp: DateTimeOffset.UtcNow);
                return ManualAsioHardwareTestResult.Blocked(
                    $"Manual BST-1 pulse failed safely: {ex.Message}",
                    GetManualAsioHardwareTestSnapshot());
            }
        }

        return ManualAsioHardwareTestResult.Success(
            $"Manual BST-1 pulse sent through ASIO channel {outputStatus.SelectedOutputChannel}; {normalized.SignalName}, {normalized.StrengthPercent:0}% strength, {normalized.Duration.TotalMilliseconds:0} ms.",
            GetManualAsioHardwareTestSnapshot());
    }

    public void StopManualAsioHardwareTest(string? reason = null)
    {
        lock (_manualAsioHardwareTestGate)
        {
            _manualAsioHardwareTestRun = null;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _lastManualAsioHardwareTestError = reason.Trim();
            }
        }
    }

    public ManualAsioHardwareTestSnapshot GetManualAsioHardwareTestSnapshot()
    {
        var outputStatus = OutputDevice.GetStatus();
        ManualAsioHardwareTestRequest? lastRequest;
        ManualAsioHardwareTestRun? activeRun;
        long renderedFrames;
        string? blockedReason;
        string? lastError;
        lock (_manualAsioHardwareTestGate)
        {
            lastRequest = _lastManualAsioHardwareTestRequest;
            activeRun = _manualAsioHardwareTestRun;
            renderedFrames = _manualAsioHardwareTestRenderedFrameCount;
            blockedReason = _lastManualAsioHardwareTestBlockedReason;
            lastError = _lastManualAsioHardwareTestError;
        }

        return new ManualAsioHardwareTestSnapshot(
            IsActive: activeRun is not null,
            TestMode: outputStatus.Kind == AudioOutputDeviceKind.Asio ? "ASIO Hardware" : outputStatus.Kind.ToString(),
            OutputMode: outputStatus.Kind.ToString(),
            SelectedAsioDriver: outputStatus.DeviceName ?? outputStatus.DisplayName,
            SelectedOutputChannel: outputStatus.SelectedOutputChannel,
            AsioRunning: outputStatus.Kind == AudioOutputDeviceKind.Asio
                && outputStatus.State == AudioOutputDeviceState.Started,
            AsioArmed: outputStatus.IsHardwareArmed,
            AsioCallbackActive: outputStatus.Kind == AudioOutputDeviceKind.Asio
                && outputStatus.State == AudioOutputDeviceState.Started
                && (outputStatus.IsStreaming
                    || outputStatus.RenderCallbackCount > 0
                    || outputStatus.BackendCallbackCount > 0),
            HapticsRunning: _isRunning,
            EmergencyMute: _emergencyMuted,
            NormalMute: _normalMuted,
            OutputPeakLevel: Math.Max(_lastAudioSnapshot?.OutputPeakLevel ?? 0f, _lastManualAsioHardwareTestPeak),
            FramesSubmitted: outputStatus.SubmittedBufferCount * Math.Max(0, outputStatus.BufferSize),
            FramesRendered: renderedFrames,
            RenderCallbackCount: outputStatus.RenderCallbackCount,
            SubmittedFrameCount: Math.Max(
                outputStatus.SubmittedBufferCount * Math.Max(0, outputStatus.BufferSize),
                Interlocked.Read(ref _lastManualAsioHardwareSubmittedFrames)),
            DroppedFrameCount: Math.Max(
                outputStatus.DroppedBufferCount * Math.Max(0, outputStatus.BufferSize),
                Interlocked.Read(ref _lastManualAsioHardwareDroppedFrames)),
            BackendCallbackCount: outputStatus.BackendCallbackCount,
            LastPulseUsedAsio: _lastManualAsioHardwareTestUsedAsio,
            LastManualPulseUsedAsio: _lastManualBst1PulseUsedAsio,
            LastGearPulseUsedAsio: _lastGearBst1PulseUsedAsio,
            LastPulseBlocked: _lastManualAsioHardwareTestBlocked,
            LimiterApplied: _lastManualAsioHardwareTestLimiterApplied,
            PulseGenerationId: Interlocked.Read(ref _manualAsioHardwareTestPulseGeneration),
            StaleStopIgnoredCount: Interlocked.Read(ref _manualAsioHardwareTestStaleStopIgnoredCount),
            BlockedReason: blockedReason,
            LastTestSignal: lastRequest?.SignalName,
            LastTestDuration: lastRequest?.Duration,
            LastStrengthPercent: lastRequest?.StrengthPercent,
            LastOutputTrimPercent: lastRequest?.OutputTrimPercent,
            LastEffectivePreLimiterAmplitude: lastRequest?.EffectivePreLimiterAmplitude,
            LastEffectivePostLimiterAmplitude: _lastManualAsioHardwareTestPeak,
            LastFrequencyHz: lastRequest?.FrequencyHz,
            LastDurationMs: lastRequest?.DurationMilliseconds,
            LastSource: lastRequest?.Source,
            LastDurationMode: lastRequest?.DurationMode,
            ManualPulsePeak: _lastManualAsioHardwareTestPeak,
            FlightRecorderPath: _manualAsioHardwareFlightRecorder?.LogPath ?? "disabled",
            LastError: lastError ?? outputStatus.LastError,
            QueueCapacityBuffers: outputStatus.QueueCapacityBuffers,
            QueuedBufferCount: outputStatus.QueuedBufferCount);
    }

    public async ValueTask<HapticPipelinePacketResult> OfferLiveTelemetryPacketAsync(
        UdpTelemetryPacket packet,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);
        cancellationToken.ThrowIfCancellationRequested();

        var recordingResult = RecordingService.RecordPacket(packet);
        if (recordingResult.Status == TelemetryRecordingOperationStatus.Failure)
        {
            SetPipelineError(recordingResult.Message);
        }

        var parseResult = ProcessTelemetryPacket(packet, HapticPipelineInputSource.LiveUdp);
        var forwardingAttempted = false;

        try
        {
            forwardingAttempted = true;
            await TelemetryForwarder.ForwardAsync(packet, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetPipelineError($"UDP forwarding failed: {ex.Message}");
        }

        return new HapticPipelinePacketResult(
            HapticPipelineInputSource.LiveUdp,
            parseResult.ParseStatus,
            parseResult.VehicleStateUpdated,
            recordingResult.Status,
            forwardingAttempted,
            parseResult.Message);
    }

    public HapticPipelinePacketResult OfferReplayTelemetryPacket(UdpTelemetryPacket packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        var parseResult = ProcessTelemetryPacket(packet, HapticPipelineInputSource.Replay);
        return new HapticPipelinePacketResult(
            HapticPipelineInputSource.Replay,
            parseResult.ParseStatus,
            parseResult.VehicleStateUpdated,
            TelemetryRecordingOperationStatus.NotRecording,
            ForwardingAttempted: false,
            parseResult.Message);
    }

    public async ValueTask<HapticPipelineOperationResult> ReplayAsync(
        TelemetryRecording recording,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = await ReplayService.ReplayAsync(recording, options, cancellationToken).ConfigureAwait(false);
        return result.Succeeded || result.Status == TelemetryReplayStatus.Cancelled
            ? HapticPipelineOperationResult.Success(result.Message, replayResult: result)
            : HapticPipelineOperationResult.Failure(result.Message, replayResult: result);
    }

    public async ValueTask<HapticPipelineOperationResult> ReplayFileAsync(
        string path,
        TelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = await ReplayService.ReplayFileAsync(path, options, cancellationToken).ConfigureAwait(false);
        return result.Succeeded || result.Status == TelemetryReplayStatus.Cancelled
            ? HapticPipelineOperationResult.Success(result.Message, replayResult: result)
            : HapticPipelineOperationResult.Failure(result.Message, replayResult: result);
    }

    public async ValueTask<HapticPipelineOperationResult> RenderNextBufferAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isRunning)
        {
            return HapticPipelineOperationResult.Failure("Haptic pipeline is stopped; no output buffer was submitted.");
        }

        if (_options.UseOutputOwnedRendering)
        {
            return HapticPipelineOperationResult.Failure("Haptic pipeline is using output-owned rendering; manual render submission is disabled.");
        }

        await _renderGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_isRunning)
            {
                return HapticPipelineOperationResult.Failure("Haptic pipeline is stopped; no output buffer was submitted.");
            }

            AudioPipeline.MixerSettings = AudioPipeline.MixerSettings with
            {
                IsMuted = _normalMuted,
                EmergencyMute = _emergencyMuted
            };
            AudioPipeline.SafetyOptions = AudioPipeline.SafetyOptions with
            {
                EmergencyMute = _emergencyMuted
            };

            var renderResult = RenderIntoBuffer(_outputBuffer, DateTimeOffset.UtcNow);
            if (!renderResult.Succeeded)
            {
                SetPipelineError(renderResult.Message);
                return HapticPipelineOperationResult.Failure(renderResult.Message);
            }

            var outputResult = await OutputDevice.SubmitBufferAsync(_outputBuffer, cancellationToken).ConfigureAwait(false);

            if (outputResult.Succeeded)
            {
                SetPipelineError(null);
                return HapticPipelineOperationResult.Success(outputResult.Message, outputResult);
            }

            SetPipelineError(outputResult.Message);
            return HapticPipelineOperationResult.Failure(outputResult.Message, outputResult);
        }
        finally
        {
            _renderGate.Release();
        }
    }

    public HapticPipelineSnapshot GetSnapshot()
    {
        DateTimeOffset? lastPacketAtUtc;
        DateTimeOffset? lastVehicleStateUpdateAtUtc;
        string lastPacketMessage;
        string lastVehicleStateMessage;
        string? lastPipelineError;
        HapticPipelineInputSource inputSource;
        TimeSpan? telemetryAge;
        bool telemetryTimedOutMuted;

        lock (_diagnosticsGate)
        {
            lastPacketAtUtc = _lastPacketAtUtc;
            lastVehicleStateUpdateAtUtc = _lastVehicleStateUpdateAtUtc;
            lastPacketMessage = _lastPacketMessage;
            lastVehicleStateMessage = _lastVehicleStateMessage;
            lastPipelineError = _lastPipelineError;
            inputSource = _inputSource;
            telemetryAge = _lastRenderTelemetryAge;
            telemetryTimedOutMuted = _telemetryTimedOutMuted;
        }

        return new HapticPipelineSnapshot(
            _isRunning,
            inputSource,
            lastPacketAtUtc,
            lastVehicleStateUpdateAtUtc,
            Interlocked.Read(ref _packetsObserved),
            Interlocked.Read(ref _packetParseSuccessCount),
            Interlocked.Read(ref _packetParseIgnoredCount),
            Interlocked.Read(ref _packetParseFailureCount),
            Interlocked.Read(ref _vehicleStateUpdateCount),
            Interlocked.Read(ref _renderedBufferCount),
            telemetryAge,
            _options.TelemetryMuteTimeout,
            telemetryTimedOutMuted,
            _normalMuted,
            _emergencyMuted,
            lastPacketMessage,
            lastVehicleStateMessage,
            lastPipelineError,
            _telemetryGameAdapter.CurrentVehicleState,
            _lastEffectSnapshot,
            _lastAudioSnapshot,
            OutputDevice.GetStatus(),
            GetManualAsioHardwareTestSnapshot(),
            OutputDevice is NullAudioOutputDevice nullOutput ? nullOutput.GetSampleSinkSnapshot() : null,
            TelemetryForwarder.GetSnapshot(),
            CreatePacketDiagnosticsSnapshot(),
            RecordingService.GetSnapshot(),
            ReplayService.GetSnapshot());
    }

    private IReadOnlyList<HapticPipelinePacketDiagnostics> CreatePacketDiagnosticsSnapshot()
    {
        lock (_diagnosticsGate)
        {
            return _telemetryGameAdapter.PacketDescriptors
                .Select(definition => new HapticPipelinePacketDiagnostics(
                    definition.PacketId,
                    definition.Name,
                    definition.PacketId < _packetIdCounts.Length ? Interlocked.Read(ref _packetIdCounts[definition.PacketId]) : 0,
                    definition.PacketId < _packetIdLastObservedAtUtc.Length ? _packetIdLastObservedAtUtc[definition.PacketId] : null))
                .ToArray();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReplayService.PacketReplayed -= ReplayService_PacketReplayed;
        await ReplayService.StopAsync().ConfigureAwait(false);

        if (_ownsReplayService && ReplayService is IAsyncDisposable asyncReplayService)
        {
            await asyncReplayService.DisposeAsync().ConfigureAwait(false);
        }

        if (_ownsRecordingService)
        {
            await RecordingService.DisposeAsync().ConfigureAwait(false);
        }

        if (_ownsForwarder)
        {
            await TelemetryForwarder.DisposeAsync().ConfigureAwait(false);
        }

        await OutputDevice.DisposeAsync().ConfigureAwait(false);

        _lifecycleGate.Dispose();
        _renderGate.Dispose();
    }

    private void ReplayService_PacketReplayed(object? sender, TelemetryReplayPacketEventArgs e)
    {
        OfferReplayTelemetryPacket(e.Packet);
    }

    private PacketProcessingResult ProcessTelemetryPacket(
        UdpTelemetryPacket packet,
        HapticPipelineInputSource source)
    {
        Interlocked.Increment(ref _packetsObserved);

        lock (_diagnosticsGate)
        {
            _inputSource = source;
            _lastPacketAtUtc = packet.ReceivedAtUtc;
        }

        TelemetryPacketProcessResult parseResult;

        try
        {
            parseResult = _telemetryGameAdapter.Process(packet);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _packetParseFailureCount);

            lock (_diagnosticsGate)
            {
                _lastPacketMessage = $"Packet parser error: {ex.Message}";
            }

            SetPipelineError($"Packet parser error: {ex.Message}");
            return new PacketProcessingResult(
                TelemetryPacketParseStatus.Failure,
                VehicleStateUpdated: false,
                $"Packet parser error: {ex.Message}");
        }

        switch (parseResult.ParseStatus)
        {
            case TelemetryPacketParseStatus.Success:
                Interlocked.Increment(ref _packetParseSuccessCount);
                break;
            case TelemetryPacketParseStatus.Ignored:
                Interlocked.Increment(ref _packetParseIgnoredCount);
                break;
            case TelemetryPacketParseStatus.Failure:
                Interlocked.Increment(ref _packetParseFailureCount);
                break;
        }

        if (parseResult.PacketId is { } packetId
            && packetId >= 0
            && packetId < _packetIdCounts.Length)
        {
            Interlocked.Increment(ref _packetIdCounts[packetId]);
            lock (_diagnosticsGate)
            {
                _packetIdLastObservedAtUtc[packetId] = packet.ReceivedAtUtc;
            }
        }

        lock (_diagnosticsGate)
        {
            _lastPacketMessage = parseResult.PacketMessage;
        }

        var vehicleStateUpdate = parseResult.VehicleStateUpdate;
        var vehicleStateUpdated = vehicleStateUpdate.WasApplied;

        if (vehicleStateUpdated)
        {
            Interlocked.Increment(ref _vehicleStateUpdateCount);
            EffectEngine.Update(vehicleStateUpdate.State);

            lock (_diagnosticsGate)
            {
                _lastVehicleStateUpdateAtUtc = packet.ReceivedAtUtc;
                _lastVehicleStateWallClockAtUtc = DateTimeOffset.UtcNow;
                _telemetryTimedOutMuted = false;
            }
        }

        lock (_diagnosticsGate)
        {
            _lastVehicleStateMessage = vehicleStateUpdate.Message;
        }

        return new PacketProcessingResult(
            parseResult.ParseStatus,
            vehicleStateUpdated,
            parseResult.PacketMessage);
    }

    private void SetPipelineError(string? message)
    {
        lock (_diagnosticsGate)
        {
            _lastPipelineError = message;
        }
    }

    private AudioOutputRenderCallbackResult RenderOutputBuffer(
        AudioSampleBuffer destination,
        AudioOutputRenderContext context)
    {
        if (!_isRunning && !ShouldKeepLocalAsioPulseCallbackAlive())
        {
            destination.Clear();
            return AudioOutputRenderCallbackResult.Failure("Haptic pipeline is stopped; render callback produced silence.");
        }

        if (!Monitor.TryEnter(_renderCallbackGate))
        {
            destination.Clear();
            return AudioOutputRenderCallbackResult.Failure("Haptic render callback skipped because the previous render is still active.");
        }

        try
        {
            return RenderIntoBuffer(destination, context.CallbackStartedAtUtc);
        }
        finally
        {
            Monitor.Exit(_renderCallbackGate);
        }
    }

    private AudioOutputRenderCallbackResult RenderIntoBuffer(
        AudioSampleBuffer destination,
        DateTimeOffset renderStartedAtUtc)
    {
        AudioSampleBuffer.EnsureSameFormat(Format, destination.Format);

        var telemetryAge = CalculateTelemetryAge(renderStartedAtUtc);
        var hasVehicleState = Interlocked.Read(ref _vehicleStateUpdateCount) > 0;
        var telemetryTimedOut = hasVehicleState
            && telemetryAge is not null
            && telemetryAge.Value > _options.TelemetryMuteTimeout;
        var shouldRenderEffects = hasVehicleState
            && !telemetryTimedOut
            && !_normalMuted
            && !_emergencyMuted;
        var mixerInputs = new List<AudioMixerInput>();

        if (shouldRenderEffects)
        {
            var effectRender = EffectEngine.RenderNextBuffer();
            _lastEffectSnapshot = effectRender.Snapshot;
            mixerInputs.AddRange(effectRender.MixerInputs);
        }

        var manualAsioHardwareTestActive = TryAddManualAsioHardwareTestInput(
            mixerInputs,
            out var manualAsioHardwareTestRun);
        var telemetryMuteAppliesToMixer = telemetryTimedOut && !manualAsioHardwareTestActive;

        if (manualAsioHardwareTestActive)
        {
            _lastEffectSnapshot = EffectEngine.GetSnapshot();
        }

        AudioPipeline.MixerSettings = AudioPipeline.MixerSettings with
        {
            IsMuted = _normalMuted || telemetryMuteAppliesToMixer,
            EmergencyMute = _emergencyMuted
        };
        AudioPipeline.SafetyOptions = AudioPipeline.SafetyOptions with
        {
            EmergencyMute = _emergencyMuted
        };

        _lastAudioSnapshot = AudioPipeline.Process(mixerInputs, destination);
        if (manualAsioHardwareTestRun is not null)
        {
            var consumed = manualAsioHardwareTestRun.RecordPostLimiterOutput(destination);
            if (consumed > 0)
            {
                Interlocked.Add(ref _manualAsioHardwareTestRenderedFrameCount, consumed);
                _lastManualAsioHardwareTestPeak = Math.Max(
                    _lastManualAsioHardwareTestPeak,
                    manualAsioHardwareTestRun.PulseOwnedPeakPostLimiter);
                _lastManualAsioHardwareTestLimiterApplied =
                    _lastManualAsioHardwareTestLimiterApplied
                    || _lastAudioSnapshot.LimitedSampleCount > 0
                    || _lastAudioSnapshot.ClippedSampleCount > 0;
            }
        }
        Interlocked.Increment(ref _renderedBufferCount);
        lock (_diagnosticsGate)
        {
            _lastRenderTelemetryAge = telemetryAge;
            _telemetryTimedOutMuted = telemetryTimedOut;
            if (telemetryTimedOut)
            {
                _lastVehicleStateMessage = manualAsioHardwareTestActive
                    ? $"Telemetry stale for {telemetryAge!.Value.TotalMilliseconds:0} ms; normal effects muted while Manual ASIO Hardware Test remains local."
                    : $"Telemetry stale for {telemetryAge!.Value.TotalMilliseconds:0} ms; effects muted until fresh VehicleState arrives.";
            }
        }

        SetPipelineError(null);
        return AudioOutputRenderCallbackResult.Success(
            telemetryTimedOut ? "Telemetry stale; rendered safety silence." : "Rendered haptic buffer.",
            telemetryAge,
            telemetryTimedOut);
    }

    private bool ShouldKeepLocalAsioPulseCallbackAlive()
    {
        if (!_localAsioPulseStreaming)
        {
            return false;
        }

        var status = OutputDevice.GetStatus();
        return status.Kind == AudioOutputDeviceKind.Asio
            && status.State == AudioOutputDeviceState.Started;
    }

    private TimeSpan? CalculateTelemetryAge(DateTimeOffset nowUtc)
    {
        lock (_diagnosticsGate)
        {
            return _lastVehicleStateWallClockAtUtc is null
                ? null
                : nowUtc - _lastVehicleStateWallClockAtUtc.Value;
        }
    }

    private bool TryAddManualAsioHardwareTestInput(
        List<AudioMixerInput> mixerInputs,
        out ManualAsioHardwareTestRun? activeRun)
    {
        activeRun = null;
        lock (_manualAsioHardwareTestGate)
        {
            activeRun = _manualAsioHardwareTestRun;
            if (activeRun is null)
            {
                return false;
            }

            if (activeRun.FramesRemaining <= 0)
            {
                _manualAsioHardwareTestRun = null;
                activeRun = null;
                return false;
            }

            try
            {
                activeRun.Generator.Generate(activeRun.SignalBuffer);
                var framesThisBuffer = (int)Math.Min(activeRun.FramesRemaining, activeRun.SignalBuffer.FrameCount);
                if (framesThisBuffer < activeRun.SignalBuffer.FrameCount)
                {
                    ClearFrames(activeRun.SignalBuffer, framesThisBuffer);
                }

                activeRun.FramesRemaining -= framesThisBuffer;
                activeRun.RecordPreLimiterInput(framesThisBuffer);
                mixerInputs.Add(new AudioMixerInput(
                    activeRun.SignalBuffer,
                    name: $"BST-1 ASIO Pulse {activeRun.Request.SignalName}"));

                if (activeRun.FramesRemaining <= 0)
                {
                    if (ReferenceEquals(_manualAsioHardwareTestRun, activeRun))
                    {
                        _manualAsioHardwareTestRun = null;
                    }
                    else
                    {
                        Interlocked.Increment(ref _manualAsioHardwareTestStaleStopIgnoredCount);
                    }
                }

                return framesThisBuffer > 0;
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_manualAsioHardwareTestRun, activeRun))
                {
                    _manualAsioHardwareTestRun = null;
                }

                _lastManualAsioHardwareTestError = $"Manual ASIO Hardware Test failed safely: {ex.Message}";
                return false;
            }
        }
    }

    private async ValueTask RenderManualAsioHardwarePulseAsync(
        ManualAsioHardwareTestRun run,
        CancellationToken cancellationToken,
        int? maxBuffers = null,
        bool recordCompleted = true,
        bool paceSubmissions = false,
        bool respectQueueCapacity = false,
        long callbackCountBeforePulse = 0,
        DateTimeOffset? callbackActiveAtUtc = null)
    {
        await _renderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var buffersRemaining = (int)Math.Ceiling((double)run.TotalFrameCount / Configuration.BufferSize);
            if (maxBuffers is { } limit)
            {
                buffersRemaining = Math.Min(buffersRemaining, Math.Max(0, limit));
            }

            var buffersRequired = buffersRemaining;
            var buffersSubmitted = 0;
            var buffersAccepted = 0;
            var buffersDropped = 0;
            string? firstDropReason = null;
            DateTimeOffset? firstBufferConsumedAtUtc = null;
            DateTimeOffset? lastBufferConsumedAtUtc = null;
            var renderedFramesBeforePulse = 0L;

            while (buffersRemaining-- > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var queueStatusBeforeSubmit = respectQueueCapacity
                    ? await WaitForManualAsioQueueRoomAsync(
                        run.Request,
                        run.GenerationId,
                        run.StartedAtUtc,
                        cancellationToken).ConfigureAwait(false)
                    : OutputDevice.GetStatus();

                var renderResult = RenderIntoBuffer(_outputBuffer, DateTimeOffset.UtcNow);
                if (!renderResult.Succeeded)
                {
                    throw new InvalidOperationException(renderResult.Message);
                }

                var audioSnapshot = _lastAudioSnapshot;
                if (audioSnapshot is not null)
                {
                    _lastManualAsioHardwareTestPeak = Math.Max(
                        _lastManualAsioHardwareTestPeak,
                        audioSnapshot.OutputPeakLevel);
                    _lastManualAsioHardwareTestLimiterApplied =
                        _lastManualAsioHardwareTestLimiterApplied
                        || audioSnapshot.LimitedSampleCount > 0
                        || audioSnapshot.ClippedSampleCount > 0;
                }

                var submitResult = await OutputDevice.SubmitBufferAsync(_outputBuffer, cancellationToken).ConfigureAwait(false);
                buffersSubmitted++;
                if (!submitResult.Succeeded
                    && submitResult.Message.Contains("queue is full", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(GetOutputBufferDuration(), cancellationToken).ConfigureAwait(false);
                    submitResult = await OutputDevice.SubmitBufferAsync(_outputBuffer, cancellationToken).ConfigureAwait(false);
                }

                if (!submitResult.Succeeded)
                {
                    buffersDropped++;
                    firstDropReason ??= submitResult.Message;
                    Interlocked.Add(ref _lastManualAsioHardwareDroppedFrames, _outputBuffer.FrameCount);
                    throw new InvalidOperationException(submitResult.Message);
                }

                buffersAccepted++;
                Interlocked.Add(ref _lastManualAsioHardwareSubmittedFrames, _outputBuffer.FrameCount);
                firstBufferConsumedAtUtc ??= DateTimeOffset.UtcNow;
                lastBufferConsumedAtUtc = DateTimeOffset.UtcNow;
                RecordManualAsioHardwareFlight(
                    "buffer-submitted",
                    run.Request,
                    submitResult.Status,
                    run.GenerationId,
                    outputPeak: _lastManualAsioHardwareTestPeak,
                    limiterApplied: _lastManualAsioHardwareTestLimiterApplied,
                    streamStartRequested: true,
                    queueCountBeforeSubmit: queueStatusBeforeSubmit.QueuedBufferCount,
                    queueCountAfterSubmit: submitResult.Status.QueuedBufferCount,
                    buffersRequiredForPulse: buffersRequired,
                    buffersSubmitted: buffersSubmitted,
                    buffersAccepted: buffersAccepted,
                    buffersDropped: buffersDropped,
                    firstDropReason: firstDropReason,
                    callbackCountBeforePulse: callbackCountBeforePulse,
                    callbackCountAfterPulse: GetCombinedAsioCallbackCount(submitResult.Status),
                    renderedFrameCountBeforePulse: renderedFramesBeforePulse,
                    renderedFrameCountAfterPulse: Interlocked.Read(ref _manualAsioHardwareTestRenderedFrameCount),
                    startTimestamp: run.StartedAtUtc,
                    callbackActiveTimestamp: callbackActiveAtUtc,
                    firstBufferConsumedTimestamp: firstBufferConsumedAtUtc,
                    lastBufferConsumedTimestamp: lastBufferConsumedAtUtc);

                if (paceSubmissions)
                {
                    await Task.Delay(GetOutputBufferDuration(), cancellationToken).ConfigureAwait(false);
                }

                lock (_manualAsioHardwareTestGate)
                {
                    if (!ReferenceEquals(_manualAsioHardwareTestRun, run))
                    {
                        break;
                    }
                }
            }

            if (recordCompleted)
            {
                var renderedFramesAfterPulse = Interlocked.Read(ref _manualAsioHardwareTestRenderedFrameCount);
                var renderedFrameCount = Math.Max(0, renderedFramesAfterPulse - renderedFramesBeforePulse);
                var acceptedFrameCount = (long)buffersAccepted * Configuration.BufferSize;
                var completedFull = buffersDropped == 0
                    && renderedFrameCount >= run.TotalFrameCount
                    && acceptedFrameCount >= run.TotalFrameCount
                    && run.HasRequiredOutputEnergy;
                var completionReason = completedFull ? "completed-full" : "truncated";

                RecordManualAsioHardwareFlight(
                    completedFull ? "pulse-completed" : "pulse-truncated",
                    run.Request,
                    OutputDevice.GetStatus(),
                    run.GenerationId,
                    generatedSampleCount: run.PulseOwnedFramesGenerated,
                    outputPeak: run.PulseOwnedPeakPostLimiter,
                    limiterApplied: _lastManualAsioHardwareTestLimiterApplied,
                    streamStartRequested: true,
                    run: run,
                    buffersRequiredForPulse: buffersRequired,
                    buffersSubmitted: buffersSubmitted,
                    buffersAccepted: buffersAccepted,
                    buffersDropped: buffersDropped,
                    firstDropReason: firstDropReason,
                    callbackCountBeforePulse: callbackCountBeforePulse,
                    callbackCountAfterPulse: GetCombinedAsioCallbackCount(OutputDevice.GetStatus()),
                    renderedFrameCountBeforePulse: renderedFramesBeforePulse,
                    renderedFrameCountAfterPulse: renderedFramesAfterPulse,
                    startTimestamp: run.StartedAtUtc,
                    callbackActiveTimestamp: callbackActiveAtUtc,
                    firstBufferConsumedTimestamp: firstBufferConsumedAtUtc,
                    lastBufferConsumedTimestamp: lastBufferConsumedAtUtc,
                    stopDueTimestamp: run.StopDueAtUtc,
                    stopTimestamp: DateTimeOffset.UtcNow,
                    pulseCompleted: completedFull,
                    expectedFrameCount: run.TotalFrameCount,
                    acceptedFrameCount: acceptedFrameCount,
                    renderedFrameCount: run.PulseOwnedFramesConsumed,
                    completionReason: completionReason);

                if (!completedFull)
                {
                    throw new InvalidOperationException(
                        $"Manual BST-1 pulse truncated: rendered {renderedFrameCount:N0} of {run.TotalFrameCount:N0} expected frame(s); accepted {acceptedFrameCount:N0} frame(s).");
                }
            }
        }
        finally
        {
            _renderGate.Release();
        }
    }

    private async ValueTask WaitForManualAsioHardwarePulseRenderedByCallbackAsync(
        ManualAsioHardwareTestRun run,
        CancellationToken cancellationToken,
        long callbackCountBeforePulse)
    {
        await _renderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var renderedFramesBeforePulse = 0L;
            var deadline = DateTimeOffset.UtcNow
                + run.Request.Duration
                + TimeSpan.FromMilliseconds(500);
            var buffersRequired = (int)Math.Ceiling((double)run.TotalFrameCount / Configuration.BufferSize);

            while (DateTimeOffset.UtcNow <= deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var renderedFramesAfterPulse = Interlocked.Read(ref _manualAsioHardwareTestRenderedFrameCount);
                var renderedFrameCount = Math.Max(0, renderedFramesAfterPulse - renderedFramesBeforePulse);
                if (renderedFrameCount >= run.TotalFrameCount && run.HasRequiredOutputEnergy)
                {
                    RecordManualAsioHardwareFlight(
                        "pulse-completed",
                        run.Request,
                        OutputDevice.GetStatus(),
                        run.GenerationId,
                        generatedSampleCount: run.PulseOwnedFramesGenerated,
                        outputPeak: run.PulseOwnedPeakPostLimiter,
                        limiterApplied: _lastManualAsioHardwareTestLimiterApplied,
                        streamStartRequested: false,
                        run: run,
                        buffersRequiredForPulse: buffersRequired,
                        buffersSubmitted: 0,
                        buffersAccepted: 0,
                        buffersDropped: 0,
                        callbackCountBeforePulse: callbackCountBeforePulse,
                        callbackCountAfterPulse: GetCombinedAsioCallbackCount(OutputDevice.GetStatus()),
                        renderedFrameCountBeforePulse: renderedFramesBeforePulse,
                        renderedFrameCountAfterPulse: renderedFramesAfterPulse,
                        startTimestamp: run.StartedAtUtc,
                        firstBufferConsumedTimestamp: run.StartedAtUtc,
                        lastBufferConsumedTimestamp: DateTimeOffset.UtcNow,
                        stopDueTimestamp: run.StopDueAtUtc,
                        stopTimestamp: DateTimeOffset.UtcNow,
                        pulseCompleted: true,
                        expectedFrameCount: run.TotalFrameCount,
                        acceptedFrameCount: run.PulseOwnedFramesConsumed,
                        renderedFrameCount: run.PulseOwnedFramesConsumed,
                        completionReason: "completed-full");
                    return;
                }

                lock (_manualAsioHardwareTestGate)
                {
                    if (_manualAsioHardwareTestRun is not null
                        && !ReferenceEquals(_manualAsioHardwareTestRun, run)
                        && renderedFrameCount < run.TotalFrameCount)
                    {
                        break;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
            }

            var finalRenderedFramesAfterPulse = Interlocked.Read(ref _manualAsioHardwareTestRenderedFrameCount);
            var finalRenderedFrameCount = Math.Max(0, finalRenderedFramesAfterPulse - renderedFramesBeforePulse);
            lock (_manualAsioHardwareTestGate)
            {
                if (ReferenceEquals(_manualAsioHardwareTestRun, run))
                {
                    _manualAsioHardwareTestRun = null;
                }
            }

            RecordManualAsioHardwareFlight(
                "pulse-truncated",
                run.Request,
                OutputDevice.GetStatus(),
                run.GenerationId,
                generatedSampleCount: run.PulseOwnedFramesGenerated,
                outputPeak: run.PulseOwnedPeakPostLimiter,
                limiterApplied: _lastManualAsioHardwareTestLimiterApplied,
                streamStartRequested: false,
                run: run,
                buffersRequiredForPulse: buffersRequired,
                buffersSubmitted: 0,
                buffersAccepted: 0,
                buffersDropped: 0,
                callbackCountBeforePulse: callbackCountBeforePulse,
                callbackCountAfterPulse: GetCombinedAsioCallbackCount(OutputDevice.GetStatus()),
                renderedFrameCountBeforePulse: renderedFramesBeforePulse,
                renderedFrameCountAfterPulse: finalRenderedFramesAfterPulse,
                startTimestamp: run.StartedAtUtc,
                stopDueTimestamp: run.StopDueAtUtc,
                stopTimestamp: DateTimeOffset.UtcNow,
                pulseCompleted: false,
                expectedFrameCount: run.TotalFrameCount,
                acceptedFrameCount: run.PulseOwnedFramesConsumed,
                renderedFrameCount: run.PulseOwnedFramesConsumed,
                completionReason: finalRenderedFrameCount >= run.TotalFrameCount ? "zero-output-proof" : "truncated");

            throw new InvalidOperationException(
                finalRenderedFrameCount >= run.TotalFrameCount
                    ? "Manual BST-1 pulse did not produce non-zero pulse-owned post-limiter output proof."
                    : $"Manual BST-1 pulse truncated: rendered {finalRenderedFrameCount:N0} of {run.TotalFrameCount:N0} expected frame(s) through the running ASIO callback.");
        }
        finally
        {
            _renderGate.Release();
        }
    }

    private static void ClearFrames(AudioSampleBuffer buffer, int firstFrameToClear)
    {
        for (var frame = Math.Max(0, firstFrameToClear); frame < buffer.FrameCount; frame++)
        {
            for (var channel = 0; channel < buffer.ChannelCount; channel++)
            {
                buffer[frame, channel] = 0f;
            }
        }
    }

    private async ValueTask<DateTimeOffset?> WaitForStandaloneManualAsioCallbackActiveAsync(
        long callbackCountBeforePulse,
        ManualAsioHardwareTestRequest request,
        long generation,
        DateTimeOffset startTimestamp,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + StandaloneManualAsioCallbackActivationTimeout;
        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = OutputDevice.GetStatus();
            var callbackCount = GetCombinedAsioCallbackCount(status);
            if (status.Kind == AudioOutputDeviceKind.Asio
                && status.State == AudioOutputDeviceState.Started
                && callbackCount > callbackCountBeforePulse)
            {
                var activeAtUtc = DateTimeOffset.UtcNow;
                RecordManualAsioHardwareFlight(
                    "callback-active",
                    request,
                    status,
                    generation,
                    streamStartRequested: true,
                    callbackCountBeforePulse: callbackCountBeforePulse,
                    callbackCountAfterPulse: callbackCount,
                    startTimestamp: startTimestamp,
                    callbackActiveTimestamp: activeAtUtc);
                return activeAtUtc;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
        }

        RecordManualAsioHardwareFlight(
            "callback-active-timeout",
            request,
            OutputDevice.GetStatus(),
            generation,
            streamStartRequested: true,
            callbackCountBeforePulse: callbackCountBeforePulse,
            callbackCountAfterPulse: GetCombinedAsioCallbackCount(OutputDevice.GetStatus()),
            blockedReason: "ASIO callback did not become active before standalone pulse buffer submission.",
            startTimestamp: startTimestamp);
        return null;
    }

    private async ValueTask<AudioOutputStatus> WaitForManualAsioQueueRoomAsync(
        ManualAsioHardwareTestRequest request,
        long generation,
        DateTimeOffset startTimestamp,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + ManualAsioQueueRoomTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = OutputDevice.GetStatus();
            if (status.QueueCapacityBuffers <= 0 || status.QueuedBufferCount < status.QueueCapacityBuffers)
            {
                return status;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                var reason = $"Native ASIO backend queue is full before submit; capacity {status.QueueCapacityBuffers}, queued {status.QueuedBufferCount}.";
                RecordManualAsioHardwareFlight(
                    "queue-full-before-submit",
                    request,
                    status,
                    generation,
                    streamStartRequested: true,
                    blockedReason: reason,
                    firstDropReason: reason,
                    queueCountBeforeSubmit: status.QueuedBufferCount,
                    queueCountAfterSubmit: status.QueuedBufferCount,
                    startTimestamp: startTimestamp);
                throw new InvalidOperationException(reason);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    private static long GetCombinedAsioCallbackCount(AudioOutputStatus status)
    {
        return status.RenderCallbackCount + status.BackendCallbackCount;
    }

    private async ValueTask<AudioOutputDeviceResult> EnsureLocalAsioPulseStreamingAsync(
        ManualAsioHardwareTestRequest request,
        long generation,
        DateTimeOffset startTimestamp,
        CancellationToken cancellationToken)
    {
        var status = OutputDevice.GetStatus();
        if (status.State == AudioOutputDeviceState.Started && status.IsStreaming)
        {
            _localAsioPulseStreaming = true;
            return AudioOutputDeviceResult.Success("Local BST-1 ASIO pulse callback is already warm.", status);
        }

        RecordManualAsioHardwareFlight(
            "stream-start-requested",
            request,
            status,
            generation,
            streamStartRequested: true,
            callbackCountBeforePulse: GetCombinedAsioCallbackCount(status),
            startTimestamp: startTimestamp);

        var startResult = await OutputDevice.StartStreamingAsync(RenderOutputBuffer, cancellationToken).ConfigureAwait(false);
        if (startResult.Succeeded)
        {
            _localAsioPulseStreaming = true;
        }

        return startResult;
    }

    private ManualAsioHardwareTestResult StoreBlockedManualAsioHardwareTest(
        string reason,
        ManualAsioHardwareTestRequest? request = null,
        AudioOutputStatus? outputStatus = null)
    {
        var normalized = request?.Normalize();
        if (normalized is not null)
        {
            _lastManualAsioHardwareTestRequest = normalized;
        }

        lock (_manualAsioHardwareTestGate)
        {
            _manualAsioHardwareTestRun = null;
            _lastManualAsioHardwareTestBlockedReason = reason;
            _lastManualAsioHardwareTestError = null;
            _lastManualAsioHardwareTestUsedAsio = outputStatus?.Kind == AudioOutputDeviceKind.Asio;
            _lastManualAsioHardwareTestBlocked = true;
        }

        if (normalized is not null)
        {
            RecordManualAsioHardwareFlight(
                "pulse-blocked",
                normalized,
                outputStatus ?? OutputDevice.GetStatus(),
                Interlocked.Read(ref _manualAsioHardwareTestPulseGeneration),
                blockedReason: reason);
        }

        return ManualAsioHardwareTestResult.Blocked(
            $"Manual BST-1 pulse blocked: {reason}",
            GetManualAsioHardwareTestSnapshot());
    }

    private string? GetManualAsioHardwareTestBlockedReason(AudioOutputStatus outputStatus)
    {
        if (outputStatus.Kind != AudioOutputDeviceKind.Asio)
        {
            return "Output mode must be ASIO Output.";
        }

        var driverName = outputStatus.DeviceName ?? Configuration.RequestedDeviceName;
        if (!IsMTrackAsioDriver(driverName))
        {
            return $"M-Audio / M-Track ASIO driver must be selected; current driver is {driverName ?? "none"}.";
        }

        var isHardwareArmed = outputStatus.IsHardwareArmed || Configuration.IsHardwareArmed;
        if (!isHardwareArmed)
        {
            return "ASIO must be explicitly armed.";
        }

        if (_emergencyMuted)
        {
            return "Emergency mute is active.";
        }

        if (_normalMuted)
        {
            return "Normal mute is active.";
        }

        var selectedOutputChannel = outputStatus.SelectedOutputChannel ?? Configuration.SelectedOutputChannel;
        if (selectedOutputChannel is null)
        {
            return "Selected ASIO output channel is missing.";
        }

        if (selectedOutputChannel < 0)
        {
            return "Selected ASIO output channel must be zero or greater.";
        }

        if (outputStatus.DeviceOutputChannelCount is > 0 and var channelCount
            && selectedOutputChannel >= channelCount)
        {
            return $"Selected ASIO output channel {selectedOutputChannel} is outside the reported {channelCount} output channel(s).";
        }

        return null;
    }

    private void RecordManualAsioHardwareFlight(
        string eventName,
        ManualAsioHardwareTestRequest request,
        AudioOutputStatus outputStatus,
        long pulseGenerationId,
        ManualAsioHardwareTestRun? run = null,
        long generatedSampleCount = 0,
        float outputPeak = 0f,
        bool limiterApplied = false,
        string? blockedReason = null,
        Exception? exception = null,
        bool streamStartRequested = false,
        int? queueCountBeforeSubmit = null,
        int? queueCountAfterSubmit = null,
        int buffersRequiredForPulse = 0,
        int buffersSubmitted = 0,
        int buffersAccepted = 0,
        int buffersDropped = 0,
        string? firstDropReason = null,
        long callbackCountBeforePulse = 0,
        long callbackCountAfterPulse = 0,
        long renderedFrameCountBeforePulse = 0,
        long renderedFrameCountAfterPulse = 0,
        DateTimeOffset? startTimestamp = null,
        DateTimeOffset? callbackActiveTimestamp = null,
        DateTimeOffset? firstBufferConsumedTimestamp = null,
        DateTimeOffset? lastBufferConsumedTimestamp = null,
        DateTimeOffset? stopDueTimestamp = null,
        DateTimeOffset? stopTimestamp = null,
        bool pulseCompleted = false,
        long expectedFrameCount = 0,
        long acceptedFrameCount = 0,
        long renderedFrameCount = 0,
        string? completionReason = null,
        bool replacedByLatestPressWins = false)
    {
        var recorder = _manualAsioHardwareFlightRecorder;
        if (recorder is null)
        {
            return;
        }

        var record = ManualAsioHardwareTestFlightRecord.From(
            _manualAsioHardwareSessionId,
            eventName,
            request,
            outputStatus,
            pulseGenerationId) with
        {
            ElapsedMs = startTimestamp is null
                ? null
                : (DateTimeOffset.UtcNow - startTimestamp.Value).TotalMilliseconds,
            PulseSourceId = run?.PulseSourceId,
            RendererInstanceId = _localBst1PulseRendererInstanceId,
            TransportPath = _options.UseOutputOwnedRendering
                ? _isRunning ? "live-haptics-callback" : "local-persistent-callback"
                : "manual-standalone-if-still-used",
            HapticsRunningAtPulseStart = _isRunning,
            GeneratedSampleCount = generatedSampleCount == 0 && run is not null
                ? run.PulseOwnedFramesGenerated
                : generatedSampleCount,
            PulseOwnedFramesGenerated = run?.PulseOwnedFramesGenerated ?? generatedSampleCount,
            PulseOwnedFramesConsumed = run?.PulseOwnedFramesConsumed ?? renderedFrameCount,
            SubmittedFrameCount = Math.Max(
                outputStatus.SubmittedBufferCount * Math.Max(0, outputStatus.BufferSize),
                Interlocked.Read(ref _lastManualAsioHardwareSubmittedFrames)),
            DroppedFrameCount = Math.Max(
                outputStatus.DroppedBufferCount * Math.Max(0, outputStatus.BufferSize),
                Interlocked.Read(ref _lastManualAsioHardwareDroppedFrames)),
            AsioStreamStartRequested = streamStartRequested,
            QueueCountBeforeSubmit = queueCountBeforeSubmit ?? outputStatus.QueuedBufferCount,
            QueueCountAfterSubmit = queueCountAfterSubmit ?? outputStatus.QueuedBufferCount,
            BuffersRequiredForPulse = buffersRequiredForPulse == 0
                ? outputStatus.BufferSize <= 0
                    ? 0
                    : (int)Math.Ceiling(request.Duration.TotalSeconds * outputStatus.SampleRate / outputStatus.BufferSize)
                : buffersRequiredForPulse,
            BuffersSubmitted = buffersSubmitted,
            BuffersAccepted = buffersAccepted,
            BuffersDropped = buffersDropped,
            FirstDropReason = firstDropReason,
            CallbackCountBeforePulse = callbackCountBeforePulse,
            CallbackCountAfterPulse = callbackCountAfterPulse == 0
                ? GetCombinedAsioCallbackCount(outputStatus)
                : callbackCountAfterPulse,
            RenderedFrameCountBeforePulse = renderedFrameCountBeforePulse,
            RenderedFrameCountAfterPulse = renderedFrameCountAfterPulse == 0
                ? Interlocked.Read(ref _manualAsioHardwareTestRenderedFrameCount)
                : renderedFrameCountAfterPulse,
            OutputPeak = run?.PulseOwnedPeakPostLimiter ?? outputPeak,
            PulseOwnedPeakPreLimiter = run?.PulseOwnedPeakPreLimiter ?? 0f,
            PulseOwnedRmsPreLimiter = run?.PulseOwnedRmsPreLimiter ?? 0f,
            PulseOwnedPeakPostLimiter = run?.PulseOwnedPeakPostLimiter ?? outputPeak,
            PulseOwnedRmsPostLimiter = run?.PulseOwnedRmsPostLimiter ?? 0f,
            EffectivePostLimiterPeak = run?.PulseOwnedPeakPostLimiter ?? outputPeak,
            LimiterApplied = limiterApplied,
            BlockedReason = blockedReason,
            StartTimestamp = startTimestamp,
            PulseStartTimestamp = startTimestamp,
            CallbackActiveTimestamp = callbackActiveTimestamp,
            FirstBufferConsumedTimestamp = firstBufferConsumedTimestamp,
            LastBufferConsumedTimestamp = lastBufferConsumedTimestamp,
            StopDueTimestamp = stopDueTimestamp,
            StopTimestamp = stopTimestamp,
            PulseCompleted = pulseCompleted,
            ExpectedFrameCount = expectedFrameCount == 0
                ? (long)Math.Ceiling(request.Duration.TotalSeconds * outputStatus.SampleRate)
                : expectedFrameCount,
            AcceptedFrameCount = acceptedFrameCount == 0
                ? run?.PulseOwnedFramesConsumed ?? (long)buffersAccepted * Math.Max(0, outputStatus.BufferSize)
                : acceptedFrameCount,
            RenderedFrameCount = renderedFrameCount == 0
                ? run?.PulseOwnedFramesConsumed ?? Math.Max(0, renderedFrameCountAfterPulse - renderedFrameCountBeforePulse)
                : renderedFrameCount,
            GlobalCallbackFramesDelta = Math.Max(0, callbackCountAfterPulse - callbackCountBeforePulse),
            CompletedFromGlobalCallbackOnly = pulseCompleted
                && (run?.PulseOwnedFramesConsumed ?? renderedFrameCount) <= 0
                && Math.Max(0, callbackCountAfterPulse - callbackCountBeforePulse) > 0,
            DuplicateLocalGearSuppressed = false,
            LiveGearEffectSuppressed = IsManualAsioGearPulseSource(request.Source) && _isRunning,
            ActiveLocalPulseCount = _manualAsioHardwareTestRun is null ? 0 : 1,
            DroppedLocalPulseCount = Interlocked.Read(ref _manualAsioHardwareDroppedPulseCount),
            ReplacedByLatestPressWins = replacedByLatestPressWins || run?.WasSuperseded == true,
            CompletionReason = completionReason,
            StaleStopIgnored = Interlocked.Read(ref _manualAsioHardwareTestStaleStopIgnoredCount) > 0,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            ExceptionStackTrace = exception?.StackTrace,
            SanitizedErrorCategory = exception is null
                ? blockedReason is null ? null : "Blocked"
                : exception.GetType().Name
        };
        recorder.Record(record);
    }

    private async Task DelayForStandaloneManualAsioDrainAsync(
        ManualAsioHardwareTestRequest request,
        CancellationToken cancellationToken)
    {
        var bufferDuration = Configuration.SampleRate <= 0 || Configuration.BufferSize <= 0
            ? TimeSpan.FromMilliseconds(25)
            : GetOutputBufferDuration();
        var drainDelay = request.Duration + TimeSpan.FromTicks(bufferDuration.Ticks * 2);
        if (drainDelay > TimeSpan.FromMilliseconds(150))
        {
            drainDelay = TimeSpan.FromMilliseconds(150);
        }

        if (drainDelay > TimeSpan.Zero)
        {
            await Task.Delay(drainDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetOutputBufferDuration()
    {
        return Configuration.SampleRate <= 0 || Configuration.BufferSize <= 0
            ? TimeSpan.FromMilliseconds(25)
            : TimeSpan.FromSeconds((double)Configuration.BufferSize / Configuration.SampleRate);
    }

    private static bool IsManualAsioGearPulseSource(string? source)
    {
        return !string.IsNullOrWhiteSpace(source)
            && source.Contains("paddle gear", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMTrackAsioDriver(string? driverName)
    {
        return !string.IsNullOrWhiteSpace(driverName)
            && (driverName.Contains("M-Audio", StringComparison.OrdinalIgnoreCase)
                || driverName.Contains("M-Track", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PacketProcessingResult(
        TelemetryPacketParseStatus ParseStatus,
        bool VehicleStateUpdated,
        string Message);

    private sealed class ManualAsioHardwareTestRun
    {
        private readonly object _gate = new();
        private double _preLimiterSquareSum;
        private double _postLimiterSquareSum;
        private int _lastFramesPrepared;

        public ManualAsioHardwareTestRun(
            IAudioTestSignalGenerator generator,
            AudioSampleBuffer signalBuffer,
            ManualAsioHardwareTestRequest request,
            long framesRemaining,
            long generationId,
            DateTimeOffset startedAtUtc)
        {
            Generator = generator;
            SignalBuffer = signalBuffer;
            Request = request;
            FramesRemaining = framesRemaining;
            TotalFrameCount = framesRemaining;
            GenerationId = generationId;
            StartedAtUtc = startedAtUtc;
            StopDueAtUtc = startedAtUtc + request.Duration;
            Generator.Reset();
            PulseSourceId = $"{(IsManualAsioGearPulseSource(request.Source) ? "paddle" : "manual")}-{generationId:N0}";
        }

        public string PulseSourceId { get; }

        public IAudioTestSignalGenerator Generator { get; }

        public AudioSampleBuffer SignalBuffer { get; }

        public ManualAsioHardwareTestRequest Request { get; }

        public long FramesRemaining { get; set; }

        public long TotalFrameCount { get; }

        public long GenerationId { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public DateTimeOffset StopDueAtUtc { get; }

        public long PulseOwnedFramesGenerated { get; private set; }

        public long PulseOwnedFramesConsumed { get; private set; }

        public float PulseOwnedPeakPreLimiter { get; private set; }

        public float PulseOwnedPeakPostLimiter { get; private set; }

        public float PulseOwnedRmsPreLimiter
        {
            get
            {
                lock (_gate)
                {
                    return PulseOwnedFramesGenerated <= 0
                        ? 0f
                        : (float)Math.Sqrt(_preLimiterSquareSum / PulseOwnedFramesGenerated);
                }
            }
        }

        public float PulseOwnedRmsPostLimiter
        {
            get
            {
                lock (_gate)
                {
                    return PulseOwnedFramesConsumed <= 0
                        ? 0f
                        : (float)Math.Sqrt(_postLimiterSquareSum / PulseOwnedFramesConsumed);
                }
            }
        }

        public bool WasSuperseded { get; private set; }

        public bool HasRequiredOutputEnergy =>
            Request.EffectivePreLimiterAmplitude == 0f
            || (PulseOwnedPeakPostLimiter > 0f && PulseOwnedRmsPostLimiter > 0f);

        public void MarkSuperseded()
        {
            WasSuperseded = true;
        }

        public void RecordPreLimiterInput(int frameCount)
        {
            var frames = Math.Clamp(frameCount, 0, SignalBuffer.FrameCount);
            if (frames == 0)
            {
                _lastFramesPrepared = 0;
                return;
            }

            var peak = 0f;
            var squareSum = 0d;
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = SignalBuffer[frame, 0];
                if (!float.IsFinite(sample))
                {
                    sample = 0f;
                }

                peak = Math.Max(peak, Math.Abs(sample));
                squareSum += sample * sample;
            }

            lock (_gate)
            {
                _lastFramesPrepared = frames;
                PulseOwnedFramesGenerated += frames;
                PulseOwnedPeakPreLimiter = Math.Max(PulseOwnedPeakPreLimiter, peak);
                _preLimiterSquareSum += squareSum;
            }
        }

        public int RecordPostLimiterOutput(AudioSampleBuffer outputBuffer)
        {
            ArgumentNullException.ThrowIfNull(outputBuffer);
            var frames = Math.Clamp(_lastFramesPrepared, 0, outputBuffer.FrameCount);
            if (frames == 0)
            {
                return 0;
            }

            var peak = 0f;
            var squareSum = 0d;
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = outputBuffer[frame, 0];
                if (!float.IsFinite(sample))
                {
                    sample = 0f;
                }

                peak = Math.Max(peak, Math.Abs(sample));
                squareSum += sample * sample;
            }

            lock (_gate)
            {
                PulseOwnedFramesConsumed += frames;
                PulseOwnedPeakPostLimiter = Math.Max(PulseOwnedPeakPostLimiter, peak);
                _postLimiterSquareSum += squareSum;
                _lastFramesPrepared = 0;
            }

            return frames;
        }
    }
}
