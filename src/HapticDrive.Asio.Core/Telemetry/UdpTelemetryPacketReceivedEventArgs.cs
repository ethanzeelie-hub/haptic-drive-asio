namespace HapticDrive.Asio.Core.Telemetry;

public sealed class UdpTelemetryPacketReceivedEventArgs : EventArgs
{
    public UdpTelemetryPacketReceivedEventArgs(UdpTelemetryPacket packet)
    {
        Packet = packet;
    }

    public UdpTelemetryPacket Packet { get; }
}
