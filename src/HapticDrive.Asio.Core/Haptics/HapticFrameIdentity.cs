using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticFrameIdentity(
    GameIntegrationId GameId,
    string Source,
    ulong? SessionUid,
    uint? OverallFrameIdentifier,
    byte? PlayerCarIndex,
    DateTimeOffset CreatedAtUtc,
    long CreatedAtTimestamp)
{
    public TelemetrySourceIdentity? SourceIdentity { get; init; }
}
