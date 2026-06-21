using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed record HapticEffectInput(
    HapticFrame Frame,
    VehicleState VehicleState);
