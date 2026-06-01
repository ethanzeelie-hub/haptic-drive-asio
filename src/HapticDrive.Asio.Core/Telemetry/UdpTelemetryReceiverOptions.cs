using System.Net;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryReceiverOptions(
    int Port = UdpTelemetryReceiverOptions.DefaultPort,
    IPAddress? BindAddress = null,
    TimeSpan? NoPacketWarningThreshold = null)
{
    public const int DefaultPort = 20_778;

    public IPAddress EffectiveBindAddress => BindAddress ?? IPAddress.Any;

    public TimeSpan EffectiveNoPacketWarningThreshold => NoPacketWarningThreshold ?? TimeSpan.FromSeconds(3);
}
