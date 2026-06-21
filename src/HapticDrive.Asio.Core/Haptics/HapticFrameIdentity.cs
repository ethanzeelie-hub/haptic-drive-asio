using HapticDrive.Asio.Core.Games;

namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticFrameIdentity(
    GameIntegrationId GameId,
    string Source,
    ulong? SessionUid,
    uint? OverallFrameIdentifier,
    byte? PlayerCarIndex,
    DateTimeOffset CreatedAtUtc,
    long CreatedAtTimestamp);
