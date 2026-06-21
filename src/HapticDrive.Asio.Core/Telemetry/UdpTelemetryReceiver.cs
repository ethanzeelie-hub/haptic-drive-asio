using System.Net;
using System.Net.Sockets;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed class UdpTelemetryReceiver : IUdpTelemetryReceiver
{
    private readonly object _gate = new();
    private readonly UdpTelemetryReceiverOptions _options;
    private CancellationTokenSource? _stopCts;
    private Task? _receiveTask;
    private UdpClient? _udpClient;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _lastPacketAtUtc;
    private long _packetCount;
    private long _errorCount;
    private long _sequenceNumber;
    private string? _lastErrorMessage;
    private int _boundPort;

    public UdpTelemetryReceiver(UdpTelemetryReceiverOptions? options = null)
    {
        _options = options ?? new UdpTelemetryReceiverOptions();
        _boundPort = _options.Port;
    }

    public event EventHandler<UdpTelemetryPacketReceivedEventArgs>? PacketReceived;

    public UdpTelemetryReceiverSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? startedAtUtc;
        DateTimeOffset? lastPacketAtUtc;
        string? lastErrorMessage;
        int boundPort;
        bool isRunning;

        lock (_gate)
        {
            startedAtUtc = _startedAtUtc;
            lastPacketAtUtc = _lastPacketAtUtc;
            lastErrorMessage = _lastErrorMessage;
            boundPort = _boundPort;
            isRunning = IsRunning;
        }

        var packetCount = Interlocked.Read(ref _packetCount);
        var elapsedSeconds = startedAtUtc is null
            ? 0
            : Math.Max(0.001, (now - startedAtUtc.Value).TotalSeconds);
        var timeSinceLastPacket = lastPacketAtUtc is null ? (TimeSpan?)null : now - lastPacketAtUtc.Value;
        var timeSinceStart = startedAtUtc is null ? TimeSpan.Zero : now - startedAtUtc.Value;
        var noPacketThreshold = _options.EffectiveNoPacketWarningThreshold;
        return new UdpTelemetryReceiverSnapshot(
            isRunning,
            _options.Port,
            boundPort,
            packetCount,
            isRunning ? packetCount / elapsedSeconds : 0,
            startedAtUtc,
            lastPacketAtUtc,
            timeSinceLastPacket,
            isRunning && (lastPacketAtUtc is null
                ? timeSinceStart >= noPacketThreshold
                : timeSinceLastPacket >= noPacketThreshold),
            Interlocked.Read(ref _errorCount),
            lastErrorMessage);
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsRunning)
            {
                return ValueTask.CompletedTask;
            }

            var stopCts = new CancellationTokenSource();
            var udpClient = new UdpClient(new IPEndPoint(_options.EffectiveBindAddress, _options.Port));
            _stopCts = stopCts;
            _udpClient = udpClient;
            _boundPort = ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
            _startedAtUtc = DateTimeOffset.UtcNow;
            _lastPacketAtUtc = null;
            _lastErrorMessage = null;
            Interlocked.Exchange(ref _packetCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
            Interlocked.Exchange(ref _sequenceNumber, 0);
            _receiveTask = Task.Run(() => ReceiveLoopAsync(udpClient, stopCts.Token), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? stopCts;
        Task? receiveTask;
        UdpClient? udpClient;

        lock (_gate)
        {
            stopCts = _stopCts;
            receiveTask = _receiveTask;
            udpClient = _udpClient;
            _stopCts = null;
            _receiveTask = null;
            _udpClient = null;
        }

        if (stopCts is null)
        {
            return;
        }

        await stopCts.CancelAsync().ConfigureAwait(false);
        udpClient?.Dispose();

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        stopCts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private bool IsRunning => _stopCts is { IsCancellationRequested: false };

    private async Task ReceiveLoopAsync(UdpClient udpClient, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (result.Buffer.Length > _options.MaxDatagramBytes)
                {
                    Interlocked.Increment(ref _errorCount);
                    lock (_gate)
                    {
                        _lastErrorMessage = $"Ignored oversized datagram ({result.Buffer.Length:N0} bytes).";
                    }

                    continue;
                }

                if (_options.AllowedRemoteAddresses is { Count: > 0 }
                    && !_options.AllowedRemoteAddresses.Contains(result.RemoteEndPoint.Address))
                {
                    continue;
                }

                var receivedAtUtc = _options.EffectiveTimeProvider.GetUtcNow();
                var receivedAtTimestamp = _options.EffectiveTimeProvider.GetTimestamp();
                lock (_gate)
                {
                    _lastPacketAtUtc = receivedAtUtc;
                }

                var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
                Interlocked.Increment(ref _packetCount);

                PacketReceived?.Invoke(
                    this,
                    new UdpTelemetryPacketReceivedEventArgs(
                        new UdpTelemetryPacket(
                            sequenceNumber,
                            result.Buffer.ToArray(),
                            result.RemoteEndPoint,
                            receivedAtUtc,
                            receivedAtTimestamp)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException ex) when (cancellationToken.IsCancellationRequested)
            {
                lock (_gate)
                {
                    _lastErrorMessage = ex.Message;
                }

                return;
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _lastErrorMessage = ex.Message;
                }

                Interlocked.Increment(ref _errorCount);
            }
        }
    }
}
