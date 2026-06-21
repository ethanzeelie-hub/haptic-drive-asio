using System.Net;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryReceiverOptions(
    int Port = UdpTelemetryReceiverOptions.DefaultPort,
    IPAddress? BindAddress = null,
    TimeSpan? NoPacketWarningThreshold = null,
    TimeProvider? TimeProvider = null,
    bool AllowLanTelemetry = false,
    IReadOnlySet<IPAddress>? AllowedRemoteAddresses = null,
    int MaxDatagramBytes = 4096)
{
    public const int DefaultPort = 20_778;

    public IPAddress EffectiveBindAddress => BindAddress
        ?? (AllowLanTelemetry ? IPAddress.Any : IPAddress.Loopback);

    public TimeSpan EffectiveNoPacketWarningThreshold => NoPacketWarningThreshold ?? TimeSpan.FromSeconds(3);

    public TimeProvider EffectiveTimeProvider => TimeProvider ?? TimeProvider.System;
}
