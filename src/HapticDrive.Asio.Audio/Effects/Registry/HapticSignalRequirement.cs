using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed record HapticSignalRequirement(
    HapticSignalKind Signal,
    bool RequiredForOutput);
