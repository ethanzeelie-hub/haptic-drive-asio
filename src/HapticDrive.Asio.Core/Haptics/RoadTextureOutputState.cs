namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureOutputState(
    string Target,
    bool Enabled,
    bool Active,
    float StrengthScale,
    float Peak,
    float Rms,
    long CommandCount,
    long DroppedCount,
    string? LastDroppedReason);
