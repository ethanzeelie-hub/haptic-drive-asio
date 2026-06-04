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
        Host = endPoint.Address.ToString();
        Port = endPoint.Port;
        Enabled = enabled;
    }

    public UdpTelemetryForwardingDestination(string name, string host, int port, bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Destination name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Destination host is required.", nameof(host));
        }

        if (port is < 1 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Destination port must be between 1 and 65535.");
        }

        Name = name.Trim();
        Host = host.Trim();
        Port = port;
        EndPoint = IPAddress.TryParse(Host, out var address)
            ? new IPEndPoint(address, port)
            : null;
        Enabled = enabled;
    }

    public string Name { get; }

    public string Host { get; }

    public int Port { get; }

    public IPEndPoint? EndPoint { get; }

    public bool Enabled { get; }
}
