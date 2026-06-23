namespace HapticDrive.Asio.Core.Vehicle.Freshness;

public readonly record struct VehicleSignalFreshness(
    bool IsPresent,
    bool IsSameSession,
    bool IsNotFutureFrame,
    bool IsWithinFrameLag,
    bool IsWithinAge,
    TimeSpan? Age,
    uint? FrameLag)
{
    public static VehicleSignalFreshness Missing { get; } = new(false, false, false, false, false, null, null)
    {
        IsSameSourceGeneration = false
    };

    public bool IsSameSourceGeneration { get; init; } = true;

    public bool IsFresh => IsPresent && IsSameSession && IsNotFutureFrame && IsWithinFrameLag && IsWithinAge && IsSameSourceGeneration;
}
