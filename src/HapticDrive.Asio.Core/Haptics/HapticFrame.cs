namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticFrame(
    HapticFrameIdentity Identity,
    HapticTelemetrySignals Signals,
    HapticDrivingContext Context,
    HapticFrameSignalStamps SignalStamps);
