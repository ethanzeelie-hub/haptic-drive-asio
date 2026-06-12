namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureDiagnostics(
    RoadTextureSignal Signal,
    RoadTextureOutputState Bst1,
    RoadTextureOutputState BrakePHpr,
    RoadTextureOutputState ThrottlePHpr,
    long StaleTelemetrySuppressedCount);
