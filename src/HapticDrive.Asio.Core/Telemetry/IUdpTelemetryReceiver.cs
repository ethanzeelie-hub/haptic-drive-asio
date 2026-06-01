namespace HapticDrive.Asio.Core.Telemetry;

public interface IUdpTelemetryReceiver : IAsyncDisposable
{
    event EventHandler<UdpTelemetryPacketReceivedEventArgs>? PacketReceived;

    UdpTelemetryReceiverSnapshot GetSnapshot();

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
