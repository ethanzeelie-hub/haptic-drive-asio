using System.Net;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryForwardingDestination
{
    public UdpTelemetryForwardingDestination(string name, IPEndPoint endPoint, bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Destination name is required.", nameof(name));
        }

        Name = name;
        EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        Enabled = enabled;
    }

    public string Name { get; }

    public IPEndPoint EndPoint { get; }

    public bool Enabled { get; }
}
