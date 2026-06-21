namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed record HapticSignalRequirement(
    string SignalName,
    bool RequiredForOutput);
