namespace HapticDrive.Simagic.PHPR.Abstractions.Routing;

public sealed record PHprGearPulseProfile
{
    public static PHprGearPulseProfile Default { get; } = new();

    public double Strength01 { get; init; } = 0.05d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = 50;

    public int Priority { get; init; } = 100;

    public PHprGearPulseProfile Normalize()
    {
        return this with
        {
            Strength01 = double.IsFinite(Strength01) ? Math.Clamp(Strength01, 0d, 1d) : Default.Strength01,
            FrequencyHz = double.IsFinite(FrequencyHz) ? Math.Clamp(FrequencyHz, 1d, 50d) : Default.FrequencyHz,
            DurationMs = Math.Clamp(DurationMs, 10, 1_000),
            Priority = Math.Clamp(Priority, 0, 1_000)
        };
    }
}
