namespace HapticDrive.Actuation.PHpr;

public sealed record PaddleGearBenchTestOptions
{
    public static PaddleGearBenchTestOptions Disabled { get; } = new();

    public bool IsEnabled { get; init; }

    public bool IsArmed { get; init; }

    public PaddleGearBenchTestOutputMode OutputMode { get; init; } = PaddleGearBenchTestOutputMode.Mock;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Brake;

    public PHprGearPulseProfile Profile { get; init; } = PHprGearPulseProfile.Default with
    {
        Strength01 = 0.10d,
        FrequencyHz = 50d,
        DurationMs = 50
    };

    public PaddleGearBenchTestOptions Normalize()
    {
        return this with
        {
            IsArmed = IsEnabled && IsArmed,
            OutputMode = Enum.IsDefined(OutputMode) ? OutputMode : PaddleGearBenchTestOutputMode.Mock,
            TargetModule = Enum.IsDefined(TargetModule) ? TargetModule : PHprGearPulseTarget.Brake,
            Profile = (Profile ?? PHprGearPulseProfile.Default).Normalize()
        };
    }
}
