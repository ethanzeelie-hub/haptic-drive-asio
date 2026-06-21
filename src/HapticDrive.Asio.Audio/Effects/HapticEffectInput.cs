using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public readonly record struct HapticEffectInput(
    HapticFrame Frame,
    VehicleState VehicleState);
