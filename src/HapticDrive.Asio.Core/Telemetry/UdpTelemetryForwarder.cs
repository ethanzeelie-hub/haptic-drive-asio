using System.Net.Sockets;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed class UdpTelemetryForwarder : IUdpTelemetryForwarder
{
    private readonly object _gate = new();
    private readonly UdpClient _udpClient = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly IReadOnlyList<UdpTelemetryForwardingDestination> _destinations;
    private readonly IReadOnlyList<UdpTelemetryForwardingDestination> _enabledDestinations;
    private DateTimeOffset? _lastForwardedAtUtc;
    private string? _lastErrorMessage;
    private long _inputPacketCount;
    private long _forwardedDatagramCount;
    private long _forwardedByteCount;
    private long _errorCount;
    private bool _disposed;

    public UdpTelemetryForwarder(IEnumerable<UdpTelemetryForwardingDestination>? destinations = null)
    {
        _destinations = (destinations ?? Array.Empty<UdpTelemetryForwardingDestination>()).ToArray();
        _enabledDestinations = _destinations.Where(destination => destination.Enabled).ToArray();
    }

    public IReadOnlyList<UdpTelemetryForwardingDestination> Destinations => _destinations;

    public UdpTelemetryForwarderSnapshot GetSnapshot()
    {
        DateTimeOffset? lastForwardedAtUtc;
        string? lastErrorMessage;

        lock (_gate)
        {
            lastForwardedAtUtc = _lastForwardedAtUtc;
            lastErrorMessage = _lastErrorMessage;
        }

        return new UdpTelemetryForwarderSnapshot(
            _enabledDestinations.Count > 0,
            _destinations.Count,
            _enabledDestinations.Count,
            Interlocked.Read(ref _inputPacketCount),
            Interlocked.Read(ref _forwardedDatagramCount),
            Interlocked.Read(ref _forwardedByteCount),
            Interlocked.Read(ref _errorCount),
            lastForwardedAtUtc,
            lastErrorMessage);
    }

    public async ValueTask ForwardAsync(UdpTelemetryPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _inputPacketCount);

        if (_enabledDestinations.Count == 0)
        {
            return;
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var destination in _enabledDestinations)
            {
                try
                {
                    await _udpClient.SendAsync(packet.Payload, packet.Payload.Length, destination.EndPoint).ConfigureAwait(false);
                    Interlocked.Increment(ref _forwardedDatagramCount);
                    Interlocked.Add(ref _forwardedByteCount, packet.Payload.Length);

                    lock (_gate)
                    {
                        _lastForwardedAtUtc = DateTimeOffset.UtcNow;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref _errorCount);

                    lock (_gate)
                    {
                        _lastErrorMessage = $"{destination.Name}: {ex.Message}";
                    }
                }
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _udpClient.Dispose();
        _sendGate.Dispose();

        return ValueTask.CompletedTask;
    }
}
