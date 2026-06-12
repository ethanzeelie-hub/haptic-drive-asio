namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureSnapshot(
    RoadTextureSignal Signal,
    RoadTextureDiagnostics? Diagnostics = null);
