namespace HapticDrive.Asio.Core.Vehicle.Freshness;

public sealed record VehicleSignalFreshness(
    bool IsPresent,
    bool IsSameSession,
    bool IsNotFutureFrame,
    bool IsWithinFrameLag,
    bool IsWithinAge,
    TimeSpan? Age,
    uint? FrameLag)
{
    public bool IsSameSourceGeneration { get; init; } = true;

    public bool IsFresh => IsPresent && IsSameSession && IsNotFutureFrame && IsWithinFrameLag && IsWithinAge && IsSameSourceGeneration;
}
