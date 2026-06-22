using System.Net;
using HapticDrive.Asio.Core.Games;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed record TelemetrySourceIdentity(
    GameIntegrationId GameId,
    IPEndPoint RemoteEndPoint,
    ulong SessionUid,
    byte PlayerCarIndex,
    long Generation)
{
    public static TelemetrySourceIdentity Create(
        GameIntegrationId gameId,
        IPEndPoint remoteEndPoint,
        ulong sessionUid,
        byte playerCarIndex,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(gameId);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        return new TelemetrySourceIdentity(
            gameId,
            Normalize(remoteEndPoint),
            sessionUid,
            playerCarIndex,
            generation);
    }

    public static IPEndPoint Normalize(IPEndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);
        return new IPEndPoint(Normalize(remoteEndPoint.Address), remoteEndPoint.Port);
    }

    public static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
