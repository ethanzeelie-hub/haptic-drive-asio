using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Haptics;

public readonly record struct HapticFrameFreshnessSnapshot(
    VehicleSignalFreshness Telemetry,
    VehicleSignalFreshness Motion,
    VehicleSignalFreshness Session,
    VehicleSignalFreshness Lap,
    VehicleSignalFreshness Participant,
    VehicleSignalFreshness CarStatus,
    VehicleSignalFreshness Damage,
    VehicleSignalFreshness MotionEx,
    VehicleSignalFreshness Event)
{
    public VehicleSignalFreshness Get(HapticSignalKind kind)
    {
        return kind switch
        {
            HapticSignalKind.Telemetry => Telemetry,
            HapticSignalKind.Motion => Motion,
            HapticSignalKind.Session => Session,
            HapticSignalKind.Lap => Lap,
            HapticSignalKind.CarStatus => CarStatus,
            HapticSignalKind.Damage => Damage,
            HapticSignalKind.MotionEx => MotionEx,
            HapticSignalKind.Event => Event,
            _ => VehicleSignalFreshness.Missing
        };
    }
}
