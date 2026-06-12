using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class UnavailableAsioOutputBackend : IAsioOutputBackend
{
    private string? _driverName;
    private string? _lastError;
    private long _droppedBufferCount;

    public AsioOutputBackendSnapshot GetSnapshot()
    {
        return new AsioOutputBackendSnapshot(
            IsOpen: false,
            IsRunning: false,
            DriverName: _driverName,
            SampleRate: 0,
            BufferSize: 0,
            OutputChannelCount: 0,
            SubmittedBufferCount: 0,
            DroppedBufferCount: Interlocked.Read(ref _droppedBufferCount),
            CallbackCount: 0,
            UnderrunCount: 0,
            QueuedBufferCount: 0,
            LastCallbackJitter: null,
            MaximumCallbackJitter: null,
            LastError: _lastError,
            QueueCapacityBuffers: 0);
    }

    public ValueTask<AsioOutputBackendOpenResult> OpenAsync(
        string driverName,
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _driverName = driverName;
        _lastError = "Native ASIO streaming backend is not installed in this Stage 16 readiness build.";
        return ValueTask.FromResult(AsioOutputBackendOpenResult.Failure(_lastError));
    }

    public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure(
            _lastError ?? "ASIO backend is unavailable."));
    }

    public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("ASIO backend already stopped."));
    }

    public AsioOutputBackendOperationResult Submit(
        ReadOnlyMemory<float> interleavedSamples,
        int sampleRate,
        int frameCount,
        int outputChannelCount)
    {
        Interlocked.Increment(ref _droppedBufferCount);
        return AsioOutputBackendOperationResult.Failure(
            _lastError ?? "ASIO backend is unavailable.");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
