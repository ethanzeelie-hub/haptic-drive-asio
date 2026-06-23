using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class AsioAudioOutputDevice : AudioOutputDeviceBase
{
    public const string PreferredDriverName = "M-Audio M-Track Solo and Duo ASIO";
    private const string BufferAcceptedStatusMessage = "ASIO output accepted a safety-processed buffer.";

    private readonly IAsioDriverCatalog _driverCatalog;
    private readonly IAsioOutputBackend _backend;
    private float[] _routedSamples = [];
    private int _driverOutputChannelCount;
    private int? _selectedOutputChannel;
    private long _submittedBufferCount;
    private long _droppedBufferCount;
    private string? _lastError;

    public AsioAudioOutputDevice()
        : this(new WindowsRegistryAsioDriverCatalog(), new NativeAsioOutputBackend())
    {
    }

    public AsioAudioOutputDevice(IAsioDriverCatalog driverCatalog)
        : this(driverCatalog, new NativeAsioOutputBackend())
    {
    }

    public AsioAudioOutputDevice(
        IAsioDriverCatalog driverCatalog,
        IAsioOutputBackend backend)
        : base(AudioOutputDeviceKind.Asio, "ASIO Output")
    {
        _driverCatalog = driverCatalog ?? throw new ArgumentNullException(nameof(driverCatalog));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public override bool RequiresPhysicalHardware => true;

    public override AudioOutputStatus GetStatus()
    {
        var baseStatus = base.GetStatus();
        var backendSnapshot = _backend.GetSnapshot();
        return baseStatus with
        {
            DeviceOutputChannelCount = _driverOutputChannelCount == 0 ? backendSnapshot.OutputChannelCount : _driverOutputChannelCount,
            DroppedBufferCount = baseStatus.DroppedBufferCount + Interlocked.Read(ref _droppedBufferCount) + backendSnapshot.DroppedBufferCount,
            IsHardwareArmed = Configuration.IsHardwareArmed,
            LastError = _lastError ?? backendSnapshot.LastError,
            SelectedOutputChannel = _selectedOutputChannel ?? Configuration.SelectedOutputChannel,
            SubmittedBufferCount = Interlocked.Read(ref _submittedBufferCount),
            BackendCallbackCount = backendSnapshot.CallbackCount,
            UnderrunCount = baseStatus.UnderrunCount + backendSnapshot.UnderrunCount,
            LastCallbackJitter = backendSnapshot.LastCallbackJitter ?? baseStatus.LastCallbackJitter,
            MaximumCallbackJitter = backendSnapshot.MaximumCallbackJitter ?? baseStatus.MaximumCallbackJitter,
            QueuedBufferCount = backendSnapshot.QueuedBufferCount,
            QueueCapacityBuffers = backendSnapshot.QueueCapacityBuffers
        };
    }

    public override async ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Configuration = configuration;
        _lastError = null;
        _selectedOutputChannel = configuration.SelectedOutputChannel;

        if (configuration.ChannelCount != 1)
        {
            State = AudioOutputDeviceState.Faulted;
            _lastError = "Stage 16 ASIO readiness supports mono source buffers only.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        if (configuration.SelectedOutputChannel is null)
        {
            State = AudioOutputDeviceState.Faulted;
            _lastError = "Select an ASIO output channel before opening ASIO output.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        if (configuration.SelectedOutputChannel.Value < 0)
        {
            State = AudioOutputDeviceState.Faulted;
            _lastError = "ASIO output channel must be zero or greater.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        var drivers = await _driverCatalog.GetDriverNamesAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(configuration.RequestedDeviceName))
        {
            State = AudioOutputDeviceState.Faulted;
            DeviceName = null;
            _lastError = "Select an ASIO driver before opening ASIO output.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        var requestedDriver = configuration.RequestedDeviceName.Trim();
        var selectedDriver = FindDriver(drivers, requestedDriver);

        if (selectedDriver is null)
        {
            State = AudioOutputDeviceState.Faulted;
            DeviceName = requestedDriver;
            _lastError = $"ASIO driver unavailable. Requested '{requestedDriver}', but no matching ASIO driver was found.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        DeviceName = selectedDriver;
        var openResult = await _backend.OpenAsync(selectedDriver, configuration, cancellationToken).ConfigureAwait(false);
        if (!openResult.Succeeded)
        {
            State = AudioOutputDeviceState.Faulted;
            _driverOutputChannelCount = 0;
            _lastError = openResult.ErrorMessage ?? openResult.Message;
            return await FailureAsync(
                $"ASIO driver '{selectedDriver}' was discovered, but the output backend could not open it: {openResult.Message}").ConfigureAwait(false);
        }

        if (openResult.OutputChannelCount <= 0)
        {
            State = AudioOutputDeviceState.Faulted;
            _driverOutputChannelCount = 0;
            _lastError = $"ASIO driver '{selectedDriver}' reported no output channels.";
            await _backend.StopAsync(cancellationToken).ConfigureAwait(false);
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        if (configuration.SelectedOutputChannel.Value >= openResult.OutputChannelCount)
        {
            State = AudioOutputDeviceState.Faulted;
            _driverOutputChannelCount = openResult.OutputChannelCount;
            _lastError = $"Selected ASIO output channel {configuration.SelectedOutputChannel.Value} is outside the reported {openResult.OutputChannelCount} output channel(s).";
            await _backend.StopAsync(cancellationToken).ConfigureAwait(false);
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        _driverOutputChannelCount = openResult.OutputChannelCount;
        _routedSamples = new float[checked(configuration.BufferSize * _driverOutputChannelCount)];
        State = AudioOutputDeviceState.Open;
        StatusMessage = $"ASIO driver '{selectedDriver}' opened for manual readiness. Channel {configuration.SelectedOutputChannel.Value} selected; armed {configuration.IsHardwareArmed}.";
        return await SuccessAsync(StatusMessage).ConfigureAwait(false);
    }

    public override async ValueTask<AudioOutputDeviceResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Configuration.IsHardwareArmed)
        {
            _lastError = "ASIO output must be explicitly armed before it can start.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        if (State is not (AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped))
        {
            _lastError = "ASIO output must be open before it can start.";
            return await FailureAsync(_lastError).ConfigureAwait(false);
        }

        var startResult = await _backend.StartAsync(cancellationToken).ConfigureAwait(false);
        if (!startResult.Succeeded)
        {
            State = AudioOutputDeviceState.Faulted;
            _lastError = startResult.ErrorMessage ?? startResult.Message;
            return await FailureAsync($"ASIO output could not start: {startResult.Message}").ConfigureAwait(false);
        }

        State = AudioOutputDeviceState.Started;
        StatusMessage = "ASIO output started after explicit selection and arming.";
        _lastError = null;
        return await SuccessAsync(StatusMessage).ConfigureAwait(false);
    }

    public override async ValueTask<AudioOutputDeviceResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopStreamingLoopAsync().ConfigureAwait(false);

        var stopResult = await _backend.StopAsync(cancellationToken).ConfigureAwait(false);
        if (!stopResult.Succeeded)
        {
            _lastError = stopResult.ErrorMessage ?? stopResult.Message;
            State = AudioOutputDeviceState.Stopped;
            return await FailureAsync($"ASIO output stopped with backend warning: {stopResult.Message}").ConfigureAwait(false);
        }

        if (State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped or AudioOutputDeviceState.Faulted)
        {
            State = AudioOutputDeviceState.Stopped;
            StatusMessage = "ASIO output stopped.";
            return await SuccessAsync(StatusMessage).ConfigureAwait(false);
        }

        return await FailureAsync("ASIO output is not open.").ConfigureAwait(false);
    }

    public override ValueTask<AudioOutputDeviceResult> SubmitBufferAsync(
        AudioSampleBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SubmitRoutedBuffer(buffer));
    }

    protected override AudioOutputDeviceResult SubmitStreamingBuffer(AudioSampleBuffer buffer)
    {
        return SubmitRoutedBuffer(buffer);
    }

    private AudioOutputDeviceResult SubmitRoutedBuffer(AudioSampleBuffer buffer)
    {
        if (State is not (AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped))
        {
            return AudioOutputDeviceResult.Failure(
                "ASIO output must be open before it can consume audio sample buffers.",
                GetStatus());
        }

        ValidateBufferMatchesConfiguration(buffer, Configuration);

        if (_selectedOutputChannel is null || _driverOutputChannelCount <= 0)
        {
            Interlocked.Increment(ref _droppedBufferCount);
            _lastError = "ASIO output channel routing is not configured.";
            return AudioOutputDeviceResult.Failure(_lastError, GetStatus());
        }

        RouteMonoBuffer(buffer, _selectedOutputChannel.Value, _driverOutputChannelCount, _routedSamples);
        var submitResult = _backend.Submit(
            _routedSamples,
            Configuration.SampleRate,
            Configuration.BufferSize,
            _driverOutputChannelCount);

        if (!submitResult.Succeeded)
        {
            _lastError = submitResult.ErrorMessage ?? submitResult.Message;
            return AudioOutputDeviceResult.Failure(submitResult.Message, GetStatus());
        }

        Interlocked.Increment(ref _submittedBufferCount);
        _lastError = null;
        StatusMessage = BufferAcceptedStatusMessage;
        return AudioOutputDeviceResult.Success(StatusMessage, GetStatus());
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await _backend.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await _backend.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string? FindDriver(IReadOnlyList<string> drivers, string? requestedDriver)
    {
        if (string.IsNullOrWhiteSpace(requestedDriver))
        {
            return null;
        }

        return drivers.FirstOrDefault(driver =>
            string.Equals(driver, requestedDriver, StringComparison.OrdinalIgnoreCase));
    }

    private static void RouteMonoBuffer(
        AudioSampleBuffer source,
        int selectedOutputChannel,
        int outputChannelCount,
        float[] destination)
    {
        if (source.ChannelCount != 1)
        {
            throw new ArgumentException("Stage 16 ASIO routing expects a mono source buffer.", nameof(source));
        }

        if ((uint)selectedOutputChannel >= (uint)outputChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedOutputChannel));
        }

        if (destination.Length != checked(source.FrameCount * outputChannelCount))
        {
            throw new ArgumentException("Routed ASIO buffer length does not match the selected output channel count.", nameof(destination));
        }

        Array.Clear(destination);
        for (var frame = 0; frame < source.FrameCount; frame++)
        {
            destination[(frame * outputChannelCount) + selectedOutputChannel] = source[frame, 0];
        }
    }
}
