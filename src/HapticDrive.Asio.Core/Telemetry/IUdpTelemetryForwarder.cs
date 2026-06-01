namespace HapticDrive.Asio.Core.Telemetry;

public interface IUdpTelemetryForwarder : IAsyncDisposable
{
    IReadOnlyList<UdpTelemetryForwardingDestination> Destinations { get; }

    UdpTelemetryForwarderSnapshot GetSnapshot();

    ValueTask ForwardAsync(UdpTelemetryPacket packet, CancellationToken cancellationToken = default);
}
