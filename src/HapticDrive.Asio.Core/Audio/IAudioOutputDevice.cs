namespace HapticDrive.Asio.Core.Audio;

public interface IAudioOutputDevice : IAsyncDisposable
{
    AudioOutputDeviceKind Kind { get; }

    string DisplayName { get; }

    bool RequiresPhysicalHardware { get; }

    bool IsManualDebugOnly { get; }

    AudioOutputStatus GetStatus();

    ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default);

    ValueTask<AudioOutputDeviceResult> StartAsync(CancellationToken cancellationToken = default);

    ValueTask<AudioOutputDeviceResult> StopAsync(CancellationToken cancellationToken = default);
}
