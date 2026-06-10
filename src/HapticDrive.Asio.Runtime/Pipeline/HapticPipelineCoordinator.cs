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
    private readonly object _diagnosticsGate = new();
    private readonly object _renderCallbackGate = new();
    private readonly object _manualAsioHardwareTestGate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly AudioSampleBuffer _outputBuffer;
    private readonly HapticPipelineOptions _options;
    private readonly F125VehicleStateAdapter _vehicleStateAdapter = new();
    private readonly long[] _packetIdCounts = new long[16];
    private readonly DateTimeOffset?[] _packetIdLastObservedAtUtc = new DateTimeOffset?[16];
    private readonly bool _ownsOutputDevice;
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
    private string _lastPacketMessage = "Waiting for F1 25 packets.";
    private string _lastVehicleStateMessage = "Waiting for parsed F1 25 packets.";
    private string? _lastPipelineError;
    private string? _lastManualAsioHardwareTestBlockedReason;
    private string? _lastManualAsioHardwareTestError;
    private ManualAsioHardwareTestRequest? _lastManualAsioHardwareTestRequest;
    private ManualAsioHardwareTestRun? _manualAsioHardwareTestRun;
    private long _manualAsioHardwareTestRenderedFrameCount;
    private bool _disposed;
    private bool _isRunning;
    private bool _outputOpened;
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
        IEnumerable<UdpTelemetryForwardingDestination>? forwardingDestinations = null)
    {
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
        _ownsOutputDevice = outputDevice is null;
        _ownsForwarder = telemetryForwarder is null;
        _ownsRecordingService = recordingService is null;
        _ownsReplayService = replayService is null;
        _currentProfile = profile ?? HapticDriveProfile.Default;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var normalized = request.Normalize();
        if (!normalized.IsSupportedFrequency)
        {
            return StoreBlockedManualAsioHardwareTest(
                $"Manual ASIO Hardware Test supports only 40 Hz or 50 Hz sine signals; requested {normalized.FrequencyHz:0.#} Hz.");
        }

        if (normalized.Duration <= TimeSpan.Zero || normalized.Duration > ManualAsioHardwareTestRequest.MaximumDuration)
        {
            return StoreBlockedManualAsioHardwareTest(
                "Manual ASIO Hardware Test duration must be greater than zero and no more than 1 second.");
        }

        var outputStatus = OutputDevice.GetStatus();
        var blockedReason = GetManualAsioHardwareTestBlockedReason(outputStatus);
        if (blockedReason is not null)
        {
            return StoreBlockedManualAsioHardwareTest(blockedReason);
        }

        var frameCount = Math.Max(1L, (long)Math.Ceiling(normalized.Duration.TotalSeconds * Configuration.SampleRate));
        var signal = new AudioTestSignalDefinition(
            AudioTestSignalKind.SineTone,
            normalized.Amplitude,
            normalized.FrequencyHz);
        var run = new ManualAsioHardwareTestRun(
            AudioTestSignalGeneratorFactory.Create(signal),
            AudioSampleBuffer.Allocate(Format),
            normalized,
            frameCount);

        lock (_manualAsioHardwareTestGate)
        {
            _manualAsioHardwareTestRun = run;
            _lastManualAsioHardwareTestRequest = normalized;
            _lastManualAsioHardwareTestBlockedReason = null;
            _lastManualAsioHardwareTestError = null;
            _manualAsioHardwareTestRenderedFrameCount = 0;
        }

        return ManualAsioHardwareTestResult.Success(
            $"Manual ASIO Hardware Test armed for {normalized.SignalName}, {normalized.Duration.TotalMilliseconds:0} ms.",
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
            TestMode: outputStatus.Kind == AudioOutputDeviceKind.Asio ? "ASIO Hardware" : "Null",
            SelectedAsioDriver: outputStatus.DeviceName ?? outputStatus.DisplayName,
            SelectedOutputChannel: outputStatus.SelectedOutputChannel,
            AsioRunning: outputStatus.Kind == AudioOutputDeviceKind.Asio
                && outputStatus.State == AudioOutputDeviceState.Started,
            AsioArmed: outputStatus.IsHardwareArmed,
            HapticsRunning: _isRunning,
            EmergencyMute: _emergencyMuted,
            NormalMute: _normalMuted,
            OutputPeakLevel: _lastAudioSnapshot?.OutputPeakLevel ?? 0f,
            FramesSubmitted: outputStatus.SubmittedBufferCount * Math.Max(0, outputStatus.BufferSize),
            FramesRendered: renderedFrames,
            RenderCallbackCount: outputStatus.RenderCallbackCount,
            BlockedReason: blockedReason,
            LastTestSignal: lastRequest?.SignalName,
            LastTestDuration: lastRequest?.Duration,
            LastError: lastError ?? outputStatus.LastError);
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
            _vehicleStateAdapter.Current,
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
            return F125PacketDefinitions.All
                .Select(definition => new HapticPipelinePacketDiagnostics(
                    definition.Id,
                    definition.Name,
                    definition.Id < _packetIdCounts.Length ? Interlocked.Read(ref _packetIdCounts[definition.Id]) : 0,
                    definition.Id < _packetIdLastObservedAtUtc.Length ? _packetIdLastObservedAtUtc[definition.Id] : null))
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

        if (_ownsOutputDevice)
        {
            await OutputDevice.DisposeAsync().ConfigureAwait(false);
        }

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

        F125PacketParseResult parseResult;

        try
        {
            parseResult = F125PacketParser.Parse(packet.Payload);
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
                F125PacketParseStatus.Failure,
                VehicleStateUpdated: false,
                $"Packet parser error: {ex.Message}");
        }

        switch (parseResult.Status)
        {
            case F125PacketParseStatus.Success:
                Interlocked.Increment(ref _packetParseSuccessCount);
                break;
            case F125PacketParseStatus.Ignored:
                Interlocked.Increment(ref _packetParseIgnoredCount);
                break;
            case F125PacketParseStatus.Failure:
                Interlocked.Increment(ref _packetParseFailureCount);
                break;
        }

        if (parseResult.Header is { } header
            && header.PacketId < _packetIdCounts.Length)
        {
            Interlocked.Increment(ref _packetIdCounts[header.PacketId]);
            lock (_diagnosticsGate)
            {
                _packetIdLastObservedAtUtc[header.PacketId] = packet.ReceivedAtUtc;
            }
        }

        lock (_diagnosticsGate)
        {
            _lastPacketMessage = parseResult.Succeeded && parseResult.Definition is not null
                ? $"{parseResult.Definition.Name} packet parsed."
                : parseResult.Message;
        }

        var vehicleStateUpdate = _vehicleStateAdapter.Apply(parseResult);
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
            parseResult.Status,
            vehicleStateUpdated,
            parseResult.Message);
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
        if (!_isRunning)
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

        var manualAsioHardwareTestActive = TryAddManualAsioHardwareTestInput(mixerInputs);
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

    private TimeSpan? CalculateTelemetryAge(DateTimeOffset nowUtc)
    {
        lock (_diagnosticsGate)
        {
            return _lastVehicleStateWallClockAtUtc is null
                ? null
                : nowUtc - _lastVehicleStateWallClockAtUtc.Value;
        }
    }

    private bool TryAddManualAsioHardwareTestInput(List<AudioMixerInput> mixerInputs)
    {
        ManualAsioHardwareTestRun? activeRun;
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
                _manualAsioHardwareTestRenderedFrameCount += framesThisBuffer;
                mixerInputs.Add(new AudioMixerInput(
                    activeRun.SignalBuffer,
                    name: $"Manual ASIO Hardware Test {activeRun.Request.SignalName}"));

                if (activeRun.FramesRemaining <= 0)
                {
                    _manualAsioHardwareTestRun = null;
                }

                return framesThisBuffer > 0;
            }
            catch (Exception ex)
            {
                _manualAsioHardwareTestRun = null;
                _lastManualAsioHardwareTestError = $"Manual ASIO Hardware Test failed safely: {ex.Message}";
                return false;
            }
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

    private ManualAsioHardwareTestResult StoreBlockedManualAsioHardwareTest(string reason)
    {
        lock (_manualAsioHardwareTestGate)
        {
            _manualAsioHardwareTestRun = null;
            _lastManualAsioHardwareTestBlockedReason = reason;
            _lastManualAsioHardwareTestError = null;
        }

        return ManualAsioHardwareTestResult.Blocked(
            $"Manual ASIO Hardware Test blocked: {reason}",
            GetManualAsioHardwareTestSnapshot());
    }

    private string? GetManualAsioHardwareTestBlockedReason(AudioOutputStatus outputStatus)
    {
        if (outputStatus.Kind != AudioOutputDeviceKind.Asio)
        {
            return "Output mode must be ASIO Output.";
        }

        if (!IsMTrackAsioDriver(outputStatus.DeviceName))
        {
            return $"M-Audio / M-Track ASIO driver must be selected; current driver is {outputStatus.DeviceName ?? "none"}.";
        }

        if (!outputStatus.IsHardwareArmed)
        {
            return "ASIO must be explicitly armed.";
        }

        if (!_isRunning || outputStatus.State != AudioOutputDeviceState.Started)
        {
            return "Haptics and ASIO output must be running.";
        }

        if (_emergencyMuted)
        {
            return "Emergency mute is active.";
        }

        if (_normalMuted)
        {
            return "Normal mute is active.";
        }

        if (outputStatus.SelectedOutputChannel is null)
        {
            return "Selected ASIO output channel is missing.";
        }

        if (outputStatus.SelectedOutputChannel < 0)
        {
            return "Selected ASIO output channel must be zero or greater.";
        }

        if (outputStatus.DeviceOutputChannelCount is { } channelCount
            && outputStatus.SelectedOutputChannel >= channelCount)
        {
            return $"Selected ASIO output channel {outputStatus.SelectedOutputChannel} is outside the reported {channelCount} output channel(s).";
        }

        return null;
    }

    private static bool IsMTrackAsioDriver(string? driverName)
    {
        return !string.IsNullOrWhiteSpace(driverName)
            && (driverName.Contains("M-Audio", StringComparison.OrdinalIgnoreCase)
                || driverName.Contains("M-Track", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PacketProcessingResult(
        F125PacketParseStatus ParseStatus,
        bool VehicleStateUpdated,
        string Message);

    private sealed class ManualAsioHardwareTestRun
    {
        public ManualAsioHardwareTestRun(
            IAudioTestSignalGenerator generator,
            AudioSampleBuffer signalBuffer,
            ManualAsioHardwareTestRequest request,
            long framesRemaining)
        {
            Generator = generator;
            SignalBuffer = signalBuffer;
            Request = request;
            FramesRemaining = framesRemaining;
            Generator.Reset();
        }

        public IAudioTestSignalGenerator Generator { get; }

        public AudioSampleBuffer SignalBuffer { get; }

        public ManualAsioHardwareTestRequest Request { get; }

        public long FramesRemaining { get; set; }
    }
}
