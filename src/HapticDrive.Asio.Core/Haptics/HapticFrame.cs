using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticFrame(
    HapticFrameIdentity Identity,
    HapticTelemetrySignals Signals,
    HapticDrivingContext Context,
    IReadOnlyDictionary<string, VehicleSignalFreshness> Freshness);
