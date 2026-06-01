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
}
