using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed class HapticPipelineCoordinator : IAsyncDisposable
{
    private readonly object _diagnosticsGate = new();
    private readonly object _renderCallbackGate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly AudioSampleBuffer _outputBuffer;
    private readonly HapticPipelineOptions _options;
    private readonly F125VehicleStateAdapter _vehicleStateAdapter = new();
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
        HapticPipelineOptions? options = null)
    {
        Configuration = configuration ?? AudioOutputConfiguration.Default;
        _options = options ?? HapticPipelineOptions.Default;
        Format = AudioSampleFormat.FromConfiguration(Configuration);
        OutputDevice = outputDevice ?? new NullAudioOutputDevice();
        TelemetryForwarder = telemetryForwarder ?? new UdpTelemetryForwarder();
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
            OutputDevice is NullAudioOutputDevice nullOutput ? nullOutput.GetSampleSinkSnapshot() : null,
            TelemetryForwarder.GetSnapshot(),
            RecordingService.GetSnapshot(),
            ReplayService.GetSnapshot());
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
        IReadOnlyList<AudioMixerInput> mixerInputs = [];

        if (shouldRenderEffects)
        {
            var effectRender = EffectEngine.RenderNextBuffer();
            _lastEffectSnapshot = effectRender.Snapshot;
            mixerInputs = effectRender.MixerInputs;
        }

        AudioPipeline.MixerSettings = AudioPipeline.MixerSettings with
        {
            IsMuted = _normalMuted || telemetryTimedOut,
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
                _lastVehicleStateMessage = $"Telemetry stale for {telemetryAge!.Value.TotalMilliseconds:0} ms; effects muted until fresh VehicleState arrives.";
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

    private sealed record PacketProcessingResult(
        F125PacketParseStatus ParseStatus,
        bool VehicleStateUpdated,
        string Message);
}
