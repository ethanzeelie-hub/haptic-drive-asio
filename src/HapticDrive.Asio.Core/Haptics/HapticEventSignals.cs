namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticEventSignals(
    HapticEventKind Kind,
    bool InvolvesPlayer,
    byte? OtherVehicleIndex);
