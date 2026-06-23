using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Haptics;

public readonly record struct HapticSignalStamp(
    string Source,
    TelemetryPacketKind PacketKind,
    ulong SessionUid,
    float SessionTime,
    uint FrameIdentifier,
    uint OverallFrameIdentifier,
    byte PlayerCarIndex,
    DateTimeOffset ReceivedAtUtc,
    long ReceivedAtTimestamp)
{
    public TelemetrySourceIdentity? SourceIdentity { get; init; }
}
