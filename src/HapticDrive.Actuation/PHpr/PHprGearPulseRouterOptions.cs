namespace HapticDrive.Actuation.PHpr;

public sealed record PHprGearPulseRouterOptions
{
    public static PHprGearPulseRouterOptions Default { get; } = new();

    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Both;

    public PHprGearPulseProfile Profile { get; init; } = PHprGearPulseProfile.Default;

    public PHprGearPulseRouterOptions Normalize()
    {
        var target = Enum.IsDefined(TargetModule)
            ? TargetModule
            : PHprGearPulseTarget.Both;

        return this with
        {
            TargetModule = target,
            Profile = (Profile ?? PHprGearPulseProfile.Default).Normalize()
        };
    }
}
