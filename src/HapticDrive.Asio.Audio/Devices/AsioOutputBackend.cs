using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public interface IAsioOutputBackend : IAsyncDisposable
{
    AsioOutputBackendSnapshot GetSnapshot();

    ValueTask<AsioOutputBackendOpenResult> OpenAsync(
        string driverName,
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default);

    ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default);

    ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default);

    AsioOutputBackendOperationResult Submit(
        ReadOnlyMemory<float> interleavedSamples,
        int sampleRate,
        int frameCount,
        int outputChannelCount);
}

public sealed record AsioOutputBackendSnapshot(
    bool IsOpen,
    bool IsRunning,
    string? DriverName,
    int SampleRate,
    int BufferSize,
    int OutputChannelCount,
    long SubmittedBufferCount,
    long DroppedBufferCount,
    long CallbackCount,
    long UnderrunCount,
    int QueuedBufferCount,
    TimeSpan? LastCallbackJitter,
    TimeSpan? MaximumCallbackJitter,
    string? LastError,
    int QueueCapacityBuffers = 0);

public sealed record AsioOutputBackendOpenResult(
    bool Succeeded,
    string Message,
    int SampleRate,
    int BufferSize,
    int OutputChannelCount,
    string? ErrorMessage = null)
{
    public static AsioOutputBackendOpenResult Success(
        string message,
        int sampleRate,
        int bufferSize,
        int outputChannelCount)
    {
        return new(true, message, sampleRate, bufferSize, outputChannelCount);
    }

    public static AsioOutputBackendOpenResult Failure(string message, string? errorMessage = null)
    {
        return new(false, message, 0, 0, 0, errorMessage ?? message);
    }
}

public sealed record AsioOutputBackendOperationResult(
    bool Succeeded,
    string Message,
    string? ErrorMessage = null)
{
    public static AsioOutputBackendOperationResult Success(string message)
    {
        return new(true, message);
    }

    public static AsioOutputBackendOperationResult Failure(string message, string? errorMessage = null)
    {
        return new(false, message, errorMessage ?? message);
    }
}
