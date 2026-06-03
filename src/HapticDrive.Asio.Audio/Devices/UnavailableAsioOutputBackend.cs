using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class UnavailableAsioOutputBackend : IAsioOutputBackend
{
    private string? _driverName;
    private string? _lastError;

    public AsioOutputBackendSnapshot GetSnapshot()
    {
        return new AsioOutputBackendSnapshot(
            IsOpen: false,
            IsRunning: false,
            _driverName,
            SampleRate: 0,
            BufferSize: 0,
            OutputChannelCount: 0,
            SubmittedBufferCount: 0,
            DroppedBufferCount: 0,
            _lastError);
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

    public ValueTask<AsioOutputBackendOperationResult> SubmitAsync(
        ReadOnlyMemory<float> interleavedSamples,
        int sampleRate,
        int frameCount,
        int outputChannelCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure(
            _lastError ?? "ASIO backend is unavailable."));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
