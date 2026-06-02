using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public abstract class AudioOutputDeviceBase : IAudioOutputDevice
{
    private AudioOutputConfiguration _configuration = AudioOutputConfiguration.Default;

    protected AudioOutputDeviceBase(AudioOutputDeviceKind kind, string displayName)
    {
        Kind = kind;
        DisplayName = displayName;
    }

    public AudioOutputDeviceKind Kind { get; }

    public string DisplayName { get; }

    public abstract bool RequiresPhysicalHardware { get; }

    public virtual bool IsManualDebugOnly => false;

    protected AudioOutputDeviceState State { get; set; } = AudioOutputDeviceState.Created;

    protected string StatusMessage { get; set; } = "Created";

    protected string? DeviceName { get; set; }

    protected AudioOutputConfiguration Configuration
    {
        get => _configuration;
        set
        {
            ValidateConfiguration(value);
            _configuration = value;
        }
    }

    public virtual AudioOutputStatus GetStatus()
    {
        return new AudioOutputStatus(
            Kind,
            State,
            DisplayName,
            StatusMessage,
            DeviceName,
            Configuration.SampleRate,
            Configuration.ChannelCount,
            Configuration.BufferSize,
            RequiresPhysicalHardware,
            IsManualDebugOnly,
            State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped);
    }

    public abstract ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default);

    public virtual ValueTask<AudioOutputDeviceResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
        {
            State = AudioOutputDeviceState.Started;
            StatusMessage = "Started";
            return SuccessAsync("Started");
        }

        return FailureAsync("Output device must be open before it can start.");
    }

    public virtual ValueTask<AudioOutputDeviceResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == AudioOutputDeviceState.Started)
        {
            State = AudioOutputDeviceState.Stopped;
            StatusMessage = "Stopped";
            return SuccessAsync("Stopped");
        }

        if (State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
        {
            State = AudioOutputDeviceState.Stopped;
            StatusMessage = "Stopped";
            return SuccessAsync("Stopped");
        }

        return FailureAsync("Output device is not running.");
    }

    public virtual ValueTask DisposeAsync()
    {
        State = AudioOutputDeviceState.Disposed;
        StatusMessage = "Disposed";
        return ValueTask.CompletedTask;
    }

    public virtual ValueTask<AudioOutputDeviceResult> SubmitBufferAsync(
        AudioSampleBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();

        if (State != AudioOutputDeviceState.Started)
        {
            return FailureAsync("Output device must be started before it can consume audio sample buffers.");
        }

        ValidateBufferMatchesConfiguration(buffer, Configuration);
        return FailureAsync("Sample buffer streaming is not implemented for this output device.");
    }

    protected ValueTask<AudioOutputDeviceResult> SuccessAsync(string message)
    {
        return ValueTask.FromResult(AudioOutputDeviceResult.Success(message, GetStatus()));
    }

    protected ValueTask<AudioOutputDeviceResult> FailureAsync(string message)
    {
        StatusMessage = message;
        return ValueTask.FromResult(AudioOutputDeviceResult.Failure(message, GetStatus()));
    }

    protected static void ValidateConfiguration(AudioOutputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Sample rate must be positive.");
        }

        if (configuration.ChannelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Channel count must be positive.");
        }

        if (configuration.BufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Buffer size must be positive.");
        }
    }

    protected static void ValidateBufferMatchesConfiguration(
        AudioSampleBuffer buffer,
        AudioOutputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidateConfiguration(configuration);

        if (buffer.SampleRate != configuration.SampleRate)
        {
            throw new ArgumentException(
                $"Audio buffer sample rate {buffer.SampleRate} does not match output sample rate {configuration.SampleRate}.",
                nameof(buffer));
        }

        if (buffer.ChannelCount != configuration.ChannelCount)
        {
            throw new ArgumentException(
                $"Audio buffer channel count {buffer.ChannelCount} does not match output channel count {configuration.ChannelCount}.",
                nameof(buffer));
        }

        if (buffer.FrameCount != configuration.BufferSize)
        {
            throw new ArgumentException(
                $"Audio buffer frame count {buffer.FrameCount} does not match output buffer size {configuration.BufferSize}.",
                nameof(buffer));
        }
    }
}
